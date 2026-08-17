# SkyWeave — Gap Analysis & What's Remaining
Updated: 2026-08-18 (sixth pass — C1–C5 typed SimConnect rewrite LANDED in code; live sim smoke test pending)

---

## 🚨 CRITICAL GAPS — Audit 2026-08-18

### C1–C5 — FIXED IN CODE (2026-08-18), LIVE SMOKE PENDING ⏳
- C1 ✅ SimConnectManager rewritten against the typed `Microsoft.FlightSimulator.SimConnect` assembly — real constructor, real `OnRecvOpen/OnRecvQuit/OnRecvException/OnRecvSimobjectData` events, background ReceiveMessage pump thread. COM ProgID hack gone.
- C2 ✅ `IsConnected` becomes true ONLY in `OnRecvOpen` (sim ack). `Connect()` returns false on every failure path; ViewModel sets IsConnected only from the `Connected` event. Status texts are honest ("Connecting…", "Connected (verified)").
- C3 ✅ Injection is now `WeatherSetModeCustom()` + `SetDataOnSimObject` of AMBIENT WIND DIR/VELOCITY, AMBIENT TEMPERATURE, SEA LEVEL PRESSURE, followed by an async readback request 2 s later. WeatherInjector logs "Injected ✓ verified" ONLY when sim readback matches within tolerances; mismatches are reported with sent-vs-read values. No more fake "Injected at" logs. WPR XML still generated (stashed for the future preset mode).
- C4 ✅ No default position anywhere. `GetAircraftPosition()` returns null until the first real sim fix; UI shows "awaiting sim position…"; Start/Passive refuse to fetch until a real fix or a manual airport selection (`ResolveStartCoordinates()`).
- C5 ✅ Process check covers both `FlightSimulator` and `FlightSimulator2024`.
- Bonus: native `SimConnect.dll` now actually deploys (old `None Update` on an absolute path never copied it — would have crashed at startup); injector position-forwarding throttled (>0.05° or 30 s — was a full pipeline refresh every 2 s firehose, part of C7).

**⏳ Still required to close:** live sim smoke test — connect with MSFS running, confirm OnRecvOpen fires, position arrives, injection readback VERIFIES (log line "Injected ✓ verified"). Cannot be executed without the sim running. Until then: build 0/0, tests 111 green, but sim-connectivity is code-verified only.

### C1-C5 detail (superseded — see above; kept for history)

The original audit found: ProgID COM connection stub (C1), always-succeeds Connect (C2), silent no-op SetWeather with fake "Injected" logs (C3), hardcoded JFK default position (C4), wrong process name (C5). All rewritten 2026-08-18 — see top of this section.

### C6. Passive mode doesn't read sim weather (HIGH, README truth violation) — OPEN
README claims "reads sim weather without injecting". Implementation (`WeatherEngine.cs:274-284`, passive start in `MainViewModel`) still fetches *real-world* data and displays it — `WeatherSimVars.WeatherVars` (the read-side vars) are defined but **used nowhere** (verified by grep). Now cheap to fix with the typed SimConnect: add a read definition over `WeatherVars` and surface it as "sim reports" alongside real-world data — or change README/FR-C6 wording to "monitor mode".
Effort: 0.5 day, or 10 min (docs fix) if deferred

### C7. Async-void timer firehoses (HIGH) — PARTIALLY FIXED
- ✅ `WeatherInjector` timer no longer async-void (sync callback + try/catch), position forwarding throttled (>0.05° or 30 s — was a full pipeline refresh every 2 s).
- ⏳ STILL OPEN: `WeatherEngine.cs:103,116` — `new Timer(async _ => await …)` unobserved exceptions/overlap; `RadarFetcher.GetPrecipitationAtPositionAsync` called uncached (`WeatherEngine.cs:182,248`) → live HTTP call per cycle → RainViewer rate-limit risk.
Fix: `PeriodicTimer` + non-reentrant loop; cache radar precip (60 s TTL).

### C8. Duplicated fetch pipelines already drifting (MEDIUM)
`WeatherEngine.FetchCurrentWeatherAsync` vs `UpdateWeatherAsync` are copy-paste; the former doesn't populate `RecentStrikes`/`DetectedStormCells`/`CurrentRadarFrame`. Consolidate into one private method both call.

### C9. Small truths (LOW)
- `MainWindow.axaml:59` — version chip hardcoded "v1.0" (we're v0.3).
- `MainViewModel.cs` — `FindNearestAirport(0, 0)` result discarded (dead call).
- `MainViewModel.cs` — UI "nearby airports" is a hardcoded 20-airport list, not StationFinder data.
- `WindsAloftFetcher.cs:79` — 25 s timeout exceeds the ≤20 s standard (FR-A9).

**Next in this track: live sim smoke test to close C1–C5 (requires MSFS running), then C7 remainder, C6, C8, C9.**

---

## What's Built ✅

| Layer | Status |
|---|---|
| All 3 projects scaffold (Core, SimBridge, App) | Done |
| All weather models (WeatherState, CloudLayer, WindLayer, etc.) | Done |
| MetarFetcher + MetarDecoder (separate, testable) | Done |
| TafFetcher + TafDecoder | Done (functional — NOT wired into engine/UI, see Gap 1) |
| WindsAloftFetcher (Open-Meteo pressure levels) | Done |
| Multi-model fetcher — region-aware HRRR / ICON-EU / GFS / ECMWF + geopotential altitudes | Done ← NEW |
| CAPE / lifted index / freezing level height / cloud cover at pressure levels | Done ← NEW |
| METAR-observed ground-truth fusion | Done ← NEW |
| SigmetFetcher + SigmetDecoder | Done |
| LightningFetcher (Blitzortung) | Done |
| RadarFetcher (RainViewer) | Done |
| CloudLayerBuilder (24 layers, pressure-level cloud-cover synthesis) | Done ← NEW |
| WindLayerBuilder (19 pressure levels, geopotential mapping) | Done ← NEW |
| IcingCalculator | Done |
| TurbulenceCalculator — thermal, convective, mountain wave, jetstream CAT | Done ← NEW |
| WakeTurbulenceEngine — traffic vortex model + airport corridor mode | Done ← NEW |
| StormModeler — lightning clustering + SIGMET integration + CAPE-driven intensity | Done ← NEW |
| WprGenerator (WPR XML output) | Done |
| SmoothingPipeline (3-min blend, 5Hz, per-channel ease) | Done |
| StationFinder | Done |
| WeatherEngine (full orchestration) | Done |
| WeatherCache (ConcurrentDictionary, per-type TTLs) | Done |
| SimConnectManager — typed SimConnect SDK, OnRecvOpen-gated connection, position/readback definitions | **Rewritten 2026-08-18 (C1–C5) — LIVE SMOKE PENDING** |
| WeatherInjector — readback-verified injection (WeatherSetModeCustom + ambient SimVars), no fake-success logs | **Rewritten 2026-08-18 (C3) — LIVE SMOKE PENDING** |
| Glassmorphic dashboard + AS-style customization sliders/toggles | Done ← NEW |
| MainViewModel (command bindings, hazard collections, settings fully wired) | Done |
| Settings persistence (UserSettings → %APPDATA%\SkyWeave\settings.json, all properties wired) | Done |
| Test project — 83 tests passing (multi-model fetcher, wake engine, CAPE storm intensity, cloud-cover synthesis, METAR epoch obsTime regression, null-safe winds parsing) | Done ← NEW |
| Live API verification — fetchers validated against real endpoints; per-source results in tests/live-api-results.md | Done ← NEW |
| Installer script (installer.iss) | Done |

---

## CRITICAL GAPS — None Remaining

All critical gaps from previous passes are resolved. Live fetchers are now verified against real endpoints — see tests/live-api-results.md (generated by the live-verification pass; every source checked: aviationweather.gov, Open-Meteo multi-model, RainViewer, Blitzortung). Baseline fix pass (2026-08-18): METAR `obsTime` epoch decoding + winds-aloft null-safe parsing fixed and re-verified live (tests/live-api-results.md "Follow-up verification").

---

## MODERATE GAPS — Still Open

### 1. TAF Not Wired Into Engine or UI (verified in code, 2026-08-18)
✅ **RESOLVED 2026-08-18 (Gap 1, commit e2531e7, FR-B8/FR-D3):** WeatherState.Taf added; WeatherEngine fetches TAF in both pipelines (30-min cache); TafDecoder change-group split/markers/gust/AMD/validity bugs fixed; TAF brief panel + FORECAST TREND timeline live in MainWindow (UI.md §5.8); 5 regression tests.

### 2. REST API
✅ **RESOLVED 2026-08-18 (Gap 3, FR-E1, commit pending):** SkyWeave.Api project (loopback :54170) with `/health`, `/state`, `/metar`, `/hazards`; IWeatherDataProvider abstraction; verified live (KJFK 26.1 C, HRRR model, TAF validity to 00:00Z). Extensibility moat from PLAN.md now concrete.

### 3. Radar Overlay in UI
✅ **RESOLVED 2026-08-18 (Gap 2, commit 5001015, FR-D4):** RadarTileCalculator (web-mercator tile math, tested), WeatherEngine.CurrentRadarFrame (2-min cached RainViewer frame), radar mosaic panel (3×3 tiles, range rings 25-250 nm, range selector, 250 ms fade) live in MainWindow per UI.md §5.7.

### 4. Source Resilience / Backup Servers (FR-A9)
✅ **RESOLVED 2026-08-18 (commit pending):** aviationweather.gov `bbox`/`station` endpoints found dead live (204/400/404) — METAR now fetches via `ids=` with fallback chain AWC → tgftp.nws.noaa.gov → metar.vatsim.net; TAF via `ids=` → tgftp. Raw-text METAR decoder added (DecodeRaw). TAF validity `DDHH` parsing fixed (was always "now"; hour-24 rollover). FetchCurrentWeatherAsync no longer swallows exceptions silently (LastError + ErrorOccurred). Winds/radar/sigmet: no key-free backup exists — graceful degrade only (recorded in live-api-results.md).

### 5. VATSIM / IVAO Detection
Detect if the user is flying on VATSIM/IVAO and optionally defer to their weather injection to avoid conflicts.

Effort: ~1 day (process detection, event, UI indicator)

### 6. Plugin Architecture
IWeatherDataSource interface + discovery so the community can add data sources. Design-heavy — deferred to v0.5 until the REST API proves the extension surface.

Effort: ~1-2 wks (design-heavy)

### 7. SimConnect Traffic Feed for Real Wake Encounters
WakeTurbulenceEngine currently works from a traffic model; feeding it live AI/multiplayer traffic via SimConnect makes wake encounters real — you feel the heavy that landed ahead of you.

Effort: ~1-2 wks

### 8. ERA5 Historical Mode
Open-Meteo historical API exposes ERA5 — the ASFS Advanced Historical / StrataWx Historical killer feature, free.

Effort: ~3-4 days (replay mode + UI scrubber)

### 9. SimBrief Integration
Fetch flight-plan routes to pre-brief hazards along the route.

Effort: ~1-2 days

---

## Updated Priority Order

  P0 — LIVE SIM SMOKE TEST to close C1–C5 (connect w/ MSFS running → OnRecvOpen → position fix → "Injected ✓ verified" readback)   requires sim
  P0 — C7 remainder: engine timer async-void + uncached radar precip call   ~2 h
  P0 — C6: True passive-mode readback OR truthful docs                                          ~0.5 day / 10 min
  P1 — C8: Consolidate duplicated fetch pipelines                                               ~2 h
  P1 — C9 small truths (version chip, dead call, hardcoded airport list, 25s timeout)           ~1 h
  P2 — VATSIM/IVAO detection                 ~1 day   edge-case differentiator
  P2 — SimConnect traffic feed for wake      ~1-2 wks makes wake moat real-world
  P2 — Plugin architecture                   ~1-2 wks long-term community play
  P2 — ERA5 historical mode                  ~3-4 d   historical killer feature
  P2 — SimBrief integration                  ~1-2 d   route briefing
  P2 — Beta packaging: installer verification + release build                                   ~1 d

---

## Changes Since Last Version (2026-08-18 audit pass)

  RESOLVED (this session):
    - C1-C5 code fix: SimConnectManager rewritten on the typed SDK (OnRecvOpen-gated connection,
      real events, pump thread, both process names checked); WeatherInjector now does
      WeatherSetModeCustom + ambient-SimVar injection with async READBACK VERIFICATION —
      "Injected ✓ verified" only on sim-confirmed match; fake-success logs eliminated;
      no default position anywhere ("awaiting sim position" until first real fix);
      native SimConnect.dll now actually deploys (None-Include fix)
    - C7 partial: injector timer no longer async-void; position forwarding throttled (0.05° / 30 s)

  RESOLVED (earlier passes):
    - Region-aware multi-model winds (HRRR/ICON-EU/GFS/ECMWF + geopotential altitudes)
    - CAPE / lifted index / freezing level / cloud cover at pressure levels
    - METAR-observed ground-truth fusion
    - Physics-based WakeTurbulenceEngine (traffic vortex + airport corridor)
    - CAPE-driven storm intensity
    - Mountain wave + jetstream CAT turbulence
    - Glassmorphic UI with AS-style sliders/toggles
    - Settings persistence fully wired (all properties now applied)
    - Live API verification pass (tests/live-api-results.md)
    - TAF wired into engine + UI brief panel + trend timeline (Gap 1, FR-B8/FR-D3)
    - Radar overlay in dashboard (Gap 2, FR-D4)
    - REST API /health /state /metar /hazards on 127.0.0.1:54170 (Gap 3, FR-E1)
    - Backup data sources: AWC ids + tgftp + VATSIM METAR chain, tgftp TAF chain (FR-A9)
    - Raw METAR text decoder + TAF DDHH validity parsing (hour-24 rollover)

  STILL OPEN:
    - LIVE SIM SMOKE TEST for C1-C5 (requires MSFS running) — until then sim connectivity is code-verified only
    - C7 remainder (engine timer async-void + uncached radar precip), passive readback (C6), pipeline consolidation (C8), small truths (C9)
    - VATSIM/IVAO detection, plugin architecture, SimConnect traffic feed, ERA5 historical, SimBrief
    - Beta packaging: installer verification + release build

  Decision: injection implemented as WeatherSetModeCustom + settable ambient SimVars (wind dir/speed,
  temperature, QNH) with readback verification, because it is SDK-documented and provable via
  SIMCONNECT readback; full WPR-preset injection (24 cloud layers) stays stashed and is the follow-up
  item once the base path is live-verified.

---

## Build/Test Status

  - Solution builds: 0 errors, 0 warnings
  - Tests: 111 passed, 0 failed (SkyWeave.Core.Tests 108 + SkyWeave.Api.Tests 3)
  - Projects: SkyWeave.Core, SkyWeave.SimBridge, SkyWeave.App, SkyWeave.Api (+ 2 test projects)
  - Target framework: net8.0
  - Live API checks: see tests/live-api-results.md (incl. backup-source verification 2026-08-18)

---

End of gap analysis.