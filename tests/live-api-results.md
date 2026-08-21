# SkyWeave Live API Verification Report

- **Agent:** LIVE-DATA VERIFICATION (research only — no source edits)
- **Test date/time (UTC):** 2026-08-17T22:14:50Z (METAR/SIGMET follow-up at ~22:17Z)
- **Method:** PowerShell `Invoke-RestMethod` / `HttpWebRequest`, 20s timeout per request
- **Reference code:** `src/SkyWeave.Core/Fetchers/WindsAloftFetcher.cs`, `src/SkyWeave.Core/Fetchers/MetarFetcher.cs`, `src/SkyWeave.Core/Decoders/MetarDecoder.cs`
- **Open-Meteo test config:** `pressure_level=1000,850,500` (reduced from the fetcher's 19 levels for URL sanity); hourly params `temperature_/wind_speed_/wind_direction_/relative_humidity_/cloud_cover_/geopotential_height_ {1000,850,500}hPa` + `cape,lifted_index,freezing_level_height`; `wind_speed_unit=kn&forecast_days=1`

---

## 1. METAR — aviationweather.gov

| Field | Value |
|---|---|
| URL | `https://aviationweather.gov/api/data/metar?ids=KJFK&format=json` (also `ids=KORD`) |
| HTTP status | 200 |
| Response size | KJFK 545 B, KORD 509 B |
| Elapsed | 569 ms / 99 ms |

**Verified fields (both KJFK and KORD):** array of 1 object; `icaoId`, `temp`, `dewp`, `wdir`, `wspd`, `visib`, `altim`, `fltCat`, `obsTime`, `rawOb`, `clouds` (array with `cover`/`base`), plus `receiptTime`, `reportTime`, `lat`, `lon`, `elev`, `name`, `cover`, `qcField`.

**Numeric sanity (KJFK / KORD):** temp 27.2 / 25.6 °C; dewp 21.1 / 16.1; wdir 40 / 340°; wspd 9 / 5 kt; visib "10+"; fltCat VFR/VFR; clouds `[{cover:FEW,base:2000},{cover:FEW,base:16000},{cover:BKN,base:25000}]` / `[{cover:SCT,base:4500},{cover:BKN,base:6500}]`.

**Absences/failures:**
- `wgst` key is **absent** when no gusts (decoder handles via `TryGetProperty` + null check — OK).
- **CRITICAL — `obsTime` type mismatch:** API returns `obsTime` as a Unix-epoch **number** (e.g. `1787003460`). `MetarDecoder.cs:54` calls `obsTime.GetString()`, which throws `InvalidOperationException` on a numeric `JsonElement`. The exception is swallowed by the catch-all in `FetchRetry.cs:28`, so `FetchMetarAsync` returns **null after 3 retries on every METAR**. Decoding is effectively broken despite the API working.
- Note: the long field names in the verification brief (`temperature`, `dewpoint`, `windDir`…) do **not** exist; the real API uses the short names above, which is what the decoder reads (correct).

## 2. GFS 0.25° (fallback model) — ncep_gfs025 @ KDEN (39.86, -104.67)

| Field | Value |
|---|---|
| URL | `https://api.open-meteo.com/v1/forecast?latitude=39.8600&longitude=-104.6700&hourly=<22 pressure arrays>,cape,lifted_index,freezing_level_height&pressure_level=1000,850,500&models=ncep_gfs025&wind_speed_unit=kn&forecast_days=1` |
| HTTP status | 200 |
| Response size | 4110 B, 387 ms |

**Verified present:** `hourly.time` (array, `2026-08-17T00:00` start), all 22 `hourly` arrays incl. `cape`, `lifted_index`, `freezing_level_height`, `cloud_cover_1000/850/500hPa`, `geopotential_height_1000/850/500hPa`, `temperature_*hPa`, `wind_speed_*hPa`, `wind_direction_*hPa`, `relative_humidity_*hPa`.

**Numeric sanity (index 0):** cape = 200.0; lifted_index = -1.60; freezing_level_height = 4900 m; geopotential_height_850hPa = **1542 m** (≈1400 expected — plausible for warm conditions); temperature_850hPa = 24.6 °C (within -40..40 ✓); cloud_cover_850hPa = 0.

**Absences/failures:**
- **CRITICAL — top-level `pressure_levels` missing:** fetcher gate at `WindsAloftFetcher.cs:107-108` requires `response.pressure_levels`, but the API returns only `latitude, longitude, generationtime_ms, utc_offset_seconds, timezone, timezone_abbreviation, elevation, hourly_units, hourly` — **no `pressure_levels` anywhere**. The early-return triggers and `data.PressureLevels` is always empty; winds aloft data is discarded for **all** models.
- **`model_run` missing:** fetcher `WindsAloftFetcher.cs:123` expects `response.model_run` (ISO string). Not returned by the API → `DataAgeMinutes` stays 0 and `ForecastTime` = `DateTime.UtcNow` (graceful degradation, but data-age tracking is dead).

## 3. HRRR CONUS — ncep_hrrr_conus @ KORD (41.98, -87.90)

| Field | Value |
|---|---|
| URL | same shape as #2, `models=ncep_hrrr_conus` |
| HTTP status | 200 |
| Response size | 4093 B, 66 ms |

**Verified present:** `hourly.time`; **`lifted_index` PRESENT (index 0 = -4.00)** — HRRR *does* return lifted_index; `cape` = 910.0; `freezing_level_height` = 4750 m; `cloud_cover_1000hPa` = 0, `cloud_cover_850hPa` = 0 (present); `geopotential_height_850hPa` = 1524 m; `temperature_850hPa` = 17.7 °C (sane); wind/direction/humidity arrays present.

**Absences/failures:** same two as GFS — no top-level `pressure_levels`, no `model_run`.

## 4. ICON-EU — dwd_icon_eu @ EGLL (51.47, -0.45)

| Field | Value |
|---|---|
| URL | same shape as #2, `models=dwd_icon_eu` |
| HTTP status | 200 |
| Response size | 4126 B, 74 ms |

**Verified present:** `hourly.time`; **`cloud_cover_1000hPa` = 0 and `cloud_cover_850hPa` = 0 — PRESENT** (ICON-EU *does* return cloud_cover at pressure levels); `cape` = 0.0 (present); `freezing_level_height` = 3800 m (present); `geopotential_height_850hPa` = 1511 m; `temperature_850hPa` = 12.2 °C (sane).

**Absences/failures:**
- **`lifted_index` ABSENT** for ICON-EU (hourly key missing entirely) — confirmed. Fetcher handles this defensively (`TryGetDouble` → `LiftedIndex = null`) — no crash.
- Same two as GFS: no top-level `pressure_levels`, no `model_run`.

## 5. TAF — aviationweather.gov

| Field | Value |
|---|---|
| URL | `https://aviationweather.gov/api/data/taf?ids=KJFK&format=json` |
| HTTP status | 200 |
| Response size | 3035 B, 298 ms |

**Verified:** array of 1 object; `icaoId` = KJFK, `issueTime` = `2026-08-17T19:33:00.000Z`, `rawTAF` present, plus `bulletinTime`, `validTimeFrom`, `validTimeTo`, `remarks`, `fcsts` (forecast array — note the API field is named **`fcsts`**, not `forecast`), `lat/lon/elev`, `mostRecent`.

**Absences/failures:** none.

## 6. SIGMET — aviationweather.gov

| Field | Value |
|---|---|
| URL | `https://aviationweather.gov/api/data/sigmet?format=json` |
| HTTP status | 200 |
| Response size | 26893 B (JSON array; sample: `{"icaoId":"KKCI","airSigmetType":"SIGMET","hazard":"CONVECTIVE",...}`) |

**Absences/failures:** none. (First attempt via `Invoke-WebRequest` hit a PowerShell 5.1 NonInteractive-mode prompt quirk; re-tested with raw `HttpWebRequest` → clean 200.)

---

## Follow-up verification — fixes re-checked against live endpoints

- **Test date/time (UTC):** 2026-08-17T22:31:22Z
- **Method:** one-off console harness referencing `SkyWeave.Core` (temp project, since deleted) — real `MetarDecoder` and `WindsAloftFetcher.ParseWindsAloftResponse` against live URLs; full 19-level HRRR URL (no 414).

| Check | Result |
|---|---|
| METAR KJFK decode | **PASS** — temp 27.2 °C, dewp 21.1, wind 40/9 kt, `obsTime` 2026-08-17T21:51:00Z (epoch 1787003460 decoded as UTC), fltCat VFR, 3 cloud layers. `Decode_HandlesUnixEpochObservationTime` regression test added (fails before fix). |
| HRRR full 19-level URL | **PASS** — HTTP 200, no 414; 19 `PressureLevelData` entries parsed (top-level `pressure_levels` absent → all configured levels used). 850 hPa: gph 1496 m, temp 17.5 °C, wind 320/13.3 kt, cloud 2 %. cape 60, lifted_index −1.2, freezing_level 4780 m. `DataAgeMinutes` 0 (model_run still absent from API — expected, graceful). |
| Null array elements | **PASS** — nulls in `hourly` arrays no longer throw (new landmine, see agents.md §4; regression test `ParseWindsAloftResponse_NullArrayElements_DoNotThrow`). |

**Verdict updates:** #1 METAR → **PASS** (fixed + regression-guarded); #2/#3/#4 winds → **PASS** (gate removed, levels derive from response or full list; null-safe); remaining known: `model_run` absent (age=0, graceful), ICON-EU lacks `lifted_index` (defensive, as designed).

---

## Verdict Summary

| # | Endpoint | Verdict | Notable findings |
|---|---|---|---|
| 1 | METAR (KJFK/KORD) | **PARTIAL** | API works and shape matches the decoder's short field names, but `obsTime` is a Unix-epoch **number**; `MetarDecoder.cs:54` `GetString()` throws → caught by FetchRetry catch-all → METAR **always null**. |
| 2 | GFS 0.25 (KDEN) | **PARTIAL** | All data present & sane (gph850=1542 m, t850=24.6 °C, cape=200). But no top-level `pressure_levels` (fetcher gate → PressureLevels always empty) and no `model_run`. |
| 3 | HRRR CONUS (KORD) | **PARTIAL** | **`lifted_index` IS returned** (-4.00); cape 910, cloud_cover at all levels present. Same `pressure_levels`/`model_run` caveats. |
| 4 | ICON-EU (EGLL) | **PARTIAL** | **`cloud_cover_{p}hPa` IS returned** (0/0); cape 0.0, freezing_level 3800 m. **`lifted_index` ABSENT** (fetcher handles gracefully). Same `pressure_levels`/`model_run` caveats. |
| 5 | TAF (KJFK) | **PASS** | Full forecast payload incl. `rawTAF`, `issueTime`, `fcsts`. |
| 6 | SIGMET | **PASS** | HTTP 200, valid JSON array, ~27 KB. |

---

## Discrepancies between fetcher expectations and live API behavior (priority order)

1. **`obsTime` numeric vs decoder `.GetString()` (MetarDecoder.cs:52-55)** — every METAR decode throws; METAR feature returns null permanently. Needs: `GetInt64()`/`GetDouble()` + Unix-epoch conversion (or `TryGetInt64` fallback).
2. **Top-level `pressure_levels` does not exist in open-meteo responses (WindsAloftFetcher.cs:107-108)** — the gate `!response.TryGetProperty("pressure_levels", ...)` always short-circuits, so `WindsAloftData.PressureLevels` is always empty for GFS, HRRR, and ICON-EU alike. The pressure level list is only echoed inside `hourly` arrays; the gate should be removed or keyed on `hourly.time` alone.
3. **`model_run` is never returned by open-meteo (WindsAloftFetcher.cs:123-129)** — data-age tracking (`DataAgeMinutes`) is always 0; forecast time falls back to `UtcNow`. Graceful, but the feature is dead. (open-meteo exposes model run via the `model_run` request parameter or response headers, not this JSON key.)
4. **ICON-EU lacks `lifted_index`** — handled defensively (null), as designed. Note for future work: a fallback index (e.g. computed from temp lapse rate) could fill the gap.
5. Minor: TAF forecast array is `fcsts`, not `forecast`; METAR `wgst` absent when calm (decoder already defensive).
---

## Backup-source verification (2026-08-18)

Aviationweather.gov API surface change: METAR/TAF `bbox` queries now return **204 No Content** (was working 2026-08-17). Verdicts:

| # | Endpoint | Status | Notes |
|---|---|---|---|
| 1 | AWC `/api/data/metar?bbox=...` | **DEAD (204)** | Empty for every tested box (KJFK, KLAX areas). Fetcher now uses `ids=` instead. |
| 2 | AWC `/api/data/taf?bbox=...` | **DEAD (204)** | Same; `ids=` path used now. |
| 3 | AWC `/api/data/station` / `stations` | **DEAD (404)** | Nearest-station-by-bbox gone; fetchers fall back to bundled 20-major-airport list. |
| 4 | AWC `metar?station=` | **400 Bad Request** | `ids=` is the only working selector. |
| 5 | AWC `metar?ids=` / `taf?ids=` | **PASS** | Confirmed 2026-08-18 (KJFK: temp 26.1 C, wdir 50, wspd 9). |
| 6 | `tgftp.nws.noaa.gov/data/observations/metar/stations/{ICAO}.TXT` | **PASS** | 2-line body: `YYYY/MM/DD HH:MM` + raw METAR. Global coverage. |
| 7 | `tgftp.nws.noaa.gov/data/forecasts/taf/stations/{ICAO}.TXT` | **PASS** | Line 1 = date, line 2+ = TAF body (may wrap, may start with bare `TAF`, supports `AMD`). |
| 8 | `metar.vatsim.net/metar.php?id=` | **PASS** | Raw METAR text; clean `ICAO DDHHMMZ` format. Community-run; used as third fallback. |
| 9 | `metar.vatsim.net/taf.php?id=` | **NOT USABLE** | Returns the METAR, not a TAF. |
| 10 | `www.aviationweather.gov/data/metar/?format=raw` (old ADDS) | **DEAD (308)** | Permanent redirect loop. |

Fallback chain implemented in MetarFetcher (AWC ids -> tgftp -> VATSIM) and TafFetcher (AWC ids -> tgftp). Winds aloft: Open-Meteo remains single-source (no key-free backup exists; degrade gracefully). Radar: RainViewer single-source. Lightning: Blitzortung GEOjson with existing built-in fallback URL.

---

## Radar (RainViewer) verification � 2026-08-19

- **URL:** https://api.rainviewer.com/public/weather-maps.json
- **HTTP status:** 200
- **Top-level keys:** ersion, generated, host, adar, satellite
- **adar.past:** 12 frames; latest 	ime = 1787139600 (2026-08-19 11:40Z), path = /v2/radar/56492af8f43d
- **host:** https://tilecache.rainviewer.com � API returns host and relative path SEPARATELY.
- **BUG FIXED:** RadarFetcher stored only the relative path in RadarFrame.TileUrl, so the app's Image control got a hostless URL and the radar rendered nothing. Decoder now combines host + path into an absolute TileUrl (fallback constant https://tilecache.rainviewer.com if host absent). Regression tests added (tests: 124).
- **Tile URL pattern verified live:** {host}{path}/256/256/9/256/256/2/1_1.png ? HTTP 200, image/png (1370 B).
