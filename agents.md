# AGENTS.md — Operating Rules for AI Agents Working on SkyWeave

**You are an AI agent contributing to SkyWeave**, a free/MIT real-weather injection engine for MSFS 2024. Goal: best free weather addon. These rules are mandatory. Read [requirements.md](requirements.md) for *what* to build, this file for *how* to work. **You never need permission to proceed — §6 authorizes you to decide, act, and report.**

---

## 1. Ground Truth Documents (read order)

1. **requirements.md** — FR/NFR IDs, statuses, release criteria. Source of truth for scope.
2. **GAPS.md** — live status board + priority queue. Source of truth for *what's next*.
3. **PLAN.md** — architecture, data schemas, WPR format, competitive moats. Read before touching Core/SimBridge.
4. **workflow.md** — the daily loop. Follow it unless told otherwise.
5. **UI.md** — design system for SkyWeave.App (colors, layout, components, motion, acceptance bar). Read before any UI change; it is authoritative.
6. **tests/live-api-results.md** — verified real API schemas. Read before touching any fetcher/decoder.

**Conflict rule:** requirements.md wins on scope, GAPS.md wins on status, live-api-results.md wins on API reality, your assumptions lose always.

---

## 2. Non-Negotiables (violations = work rejected)

1. **Build clean:** `dotnet build` → 0 errors, **0 warnings**. **Test green:** `dotnet test` all passing. No exceptions.
2. **No swallowed exceptions, no silent nulls on data paths.** A fetch/decode failure must be logged and surfaced (source-health flag), never returned as null that poisons downstream. *This exact bug class broke METAR decode for every user once (see live-api-results.md §1 — `obsTime` epoch-number vs string).*
3. **Free forever (NFR-S1):** never add paid data sources, API keys, accounts, license servers, or telemetry.
4. **Out-of-process only (NFR-R1):** never WASM/in-process; never introduce code that can crash the sim.
5. **METAR match is the product (NFR-A1):** at the aircraft station, sim weather must match the live METAR. If your change breaks this, it is wrong.
6. **Truthful README (FR-G3):** claim only what's shipped and proven. Update README when user-visible behavior changes.
7. **One item per session** (workflow.md Phase 3). Bugs before features, always.
8. **Never commit:** `bin/`, `obj/`, secrets, or files unrelated to your item.

---

## 3. Engineering Standards

### C# / .NET 8
- Match existing project style; `Nullable` enabled — take it seriously.
- Async end-to-end on network paths; **never** `.Result`/`.Wait()` (NFR-P3).
- All public model outputs bounded: clamp every 0–1 index; validate temperature/wind/altitude plausibility (FR-B9).
- Feet→meters only via the shared constant (×0.3048). Never inline conversions.
- Every fetcher: timeout ≤20 s, retry-with-backoff, cache fallback, failure isolation (FR-A9).
- No new NuGet packages without checking they're MIT/free and justified.

### Testing (xUnit, tests/SkyWeave.Core.Tests)
- Bug fix ⇒ regression test that **fails before** the fix.
- New logic ⇒ tests shipped in the same change. Test count never decreases.
- Tests run offline; live-API checks are a separate explicit pass (NFR-Q4).

### Weather/meteorology correctness
- Altitude mapping via `geopotential_height` only — never pressure-altitude assumption (NFR-A2).
- Region-optimal model selection by aircraft position (FR-A4).
- Storm intensity from CAPE/lifted index, not lightning counts (FR-B5).
- Icing envelope 0…−40 °C, peak −15 °C, requires visible moisture (FR-B3).

### SimConnect / WPR
- WPR XML: ≤24 `<CloudLayer>`, meters for altitudes, valid units — schema in PLAN.md.
- Inject meteorological data ONLY; never touch rendering hooks (REX Atmos CORE coexistence, NFR-C1).
- Assume the WPR state is global — no per-region cell placement; model proximity effects (FR-C5).

---

## 4. Landmine List (known traps — never repeat)

| Trap | Detail |
|---|---|
| METAR authority after modeling | TAF must remain briefing data, not overwrite current observations. Do not add coordinate-based offsets to observed temperature/QNH or thermal/turbulence gust boosts to the METAR surface wind anchor (IsSurfaceLayer, at station elevation; legacy zero-altitude guard retained). Guard both AtmosphericModeler and WprGenerator; MetarAuthorityTests covers these paths. Elevated-airport altitude mapping remains separate work. |

| aviationweather.gov schema | Short field names (`temp`, `dewp`, `wdir`, `wspd`, `visib`, `altim`, `fltCat`); `obsTime` is a **Unix epoch number**, `wgst` absent when calm. Verify against live-api-results.md, not memory. |
| AWC bbox/station endpoints | **Dead as of 2026-08-18**: `metar?bbox=`/`taf?bbox=` → 204 No Content (always), `station=` → 400, `/station(s)` → 404. Only `ids=` works. Never reintroduce bbox fetches without a live check. |
| AWC nearest-station | With stations API dead, nearest-METAR resolution uses the bundled 20-major-airport list (StationFinder). Nearest-station accuracy is coarse outside those airports — documented limitation, revisit when AWC stations endpoint returns. |
| Raw METAR text fallbacks | tgftp (2-line: date + METAR) and VATSIM (`metar.vatsim.net/metar.php?id=`) return **plain text** — use `MetarDecoder.DecodeRaw`, never the JSON decoder. VATSIM `taf.php` returns the METAR, not a TAF. |
| European METAR visibility | `9999` = meters (≥10 km); 4-digit standalone number is meters, NOT SM (guard `(?<![QA])` so `Q1012`/`A2981` don't match). SM values are in statute miles (×1609.344). |
| Altimeter conversion | `A2981` = 29.81 inHg (divide the 4 digits by 100) × 33.8639 → hPa. Forgetting the /100 gives 100× the real pressure. |
| TAF validity `DDHH` | `1720/1824` = day 17 20:00Z to **day 18 24:00Z = day 19 00:00Z** (hour-24 rolls to next day). `TEMPO 1918/1922` is day 19, 18:00–22:00Z — NOT 19:18/19:22. Always day-aware; month-rollback when the day is > now+3 days. |
| TAF AMD prefix | `TAF AMD KJFK ...` — station regex must skip `AMD` or "AMD" is captured as the station. tgftp TAF bodies may start with a bare `TAF` line and wrap across lines — join trimmed lines with spaces before decoding. |
| Silent catch-alls | The old `FetchRetry.cs` catch swallowed `InvalidOperationException` → METAR returned null after 3 retries *while appearing healthy*. Never again. |
| Open-Meteo URL size | 19 pressure levels × params = very long URLs; the live pass reduced levels for sanity. Watch 414s/413s. |
| Open-Meteo nulls | `hourly` arrays contain `null` elements (e.g. below terrain); `GetDouble()` on them throws — always null-check before `.GetDouble()` (`TryGetArrayDouble` helper). |
| Open-Meteo metadata keys | Top-level `pressure_levels` and `model_run` are NOT returned by the API (verified live 2026-08-17) — never gate parsing on them; `DataAgeMinutes` stays 0 gracefully. |
| WPR is global | MSFS exposes one weather state; "placing" a storm at lat/lon is impossible — advect proximity instead. |
| Live tests in CI | Never put live-network calls inside `dotnet test`. Offline only. |
| SimConnect is not COM | Never create SimConnect via `GetTypeFromProgID` — it is a referenced managed assembly (`Microsoft.FlightSimulator.SimConnect.dll`), instantiate the typed `SimConnect` class. See audit C1 in GAPS.md (2026-08-18). |
| Fake-success logging | Never log "Injected"/"Connected" unless the sim actually acknowledged (OnRecvOpen / readback). WeatherInjector logged false "Injected at" while SetWeather no-oped — audit C2/C3. |
| Default position | Never seed a hardcoded "aircraft" position (JFK default poisoned all fetches) — display "awaiting sim position" until the first real fix. Audit C4. |
| MSFS Weather* APIs deprecated | ALL `Weather*` SimConnect functions (`WeatherSetModeCustom`, `WeatherSetModeGlobal`, `WeatherSetModeServer`, `WeatherSetModeTheme`, `WeatherSetObservation`, `WeatherCreateStation`, `WeatherRemoveStation`, `WeatherRequestInterpolatedObservation`, `WeatherRequestObservationAtStation`, `WeatherRequestObservationAtNearestStation`, `WeatherRequestCloudState`, `WeatherSetDynamicUpdateRate`, `WeatherCreateThermal`, `WeatherRemoveThermal`) are **Deprecated** per MSFS SDK docs. They may still work but are not recommended for new development. Verified 2026-08-18. |
| Ambient weather SimVars read-only | `AMBIENT WIND DIRECTION`, `AMBIENT WIND VELOCITY`, `AMBIENT TEMPERATURE`, `SEA LEVEL PRESSURE` — the "Settable" column is **empty** in the SDK docs. Writing via `SetDataOnSimObject` silently fails (SIMCONNECT_EXCEPTION 20: read-only SimVar). Readback verification via `RequestDataOnSimObject` DOES work (these vars are readable). Never attempt to write ambient weather SimVars. |
| WeatherSetObservation is DEAD | `WeatherSetObservation` has NEVER worked in MSFS 2020 or 2024. Returns `SIMCONNECT_EXCEPTION 14 (WEATHER_INVALID_METAR)` for EVERY METAR string, including the SDK's own examples. The legacy Weather* SimConnect functions were never connected to the MeteoBlue-based weather engine. All of `WeatherCreateStation`, `WeatherRemoveStation`, `WeatherSetModeCustom`, `WeatherSetModeGlobal`, `WeatherSetModeServer` are also dead stubs. Verified 2026-08-18 via live testing + FSDeveloper/Python-SimConnect/P3D forum confirmation. |
| updateTempWeatherPreset undocumented | The MSFS 2024 runtime exposes `UpdateTempWeatherPreset` through the registered `JS_LISTENER_WEATHER` service (`RegisterWeatherListener().updateTempWeatherPreset(preset, success, error)`). Global `Coherent.call(...)` returns `NoSuchMethod` because the runtime has no global BindCall, and raw `Coherent.trigger(...)` dispatches without applying the preset. Use the listener with a `WeatherPresetData`-shaped payload cloned from the current `UpdatePreset` snapshot. Reliable use still requires live payload/readback verification. Verified 2026-08-19. |
| WeatherSetModeTheme (WPR preset) | The documented (but deprecated) path for weather injection: write `.WPR` XML file to presets folder, call `WeatherSetModeTheme("presetName")` to load it. Active Sky uses this path. May also be dead in MSFS 2024 — testing required. Our current injection path as of 2026-08-18. |
| WPR schema units | `MSLPressure` is Pa (`hPa * 100`), `MSLTemperature` is Kelvin (`C + 273.15`), and wind gusts use `GustWave`; hPa/Celsius or `WindLayerGusts` can make the preset invalid. Always format numeric XML values with `InvariantCulture`. |
| WeatherSetModeTheme dead path | Current MSFS 2024 developer-support reports say the WPR preset-loaded SimConnect API is dead. A successful managed call is not acknowledgement; only a readback match proves application. Dynamic injection needs an in-sim bridge such as the undocumented `updateTempWeatherPreset` path. |
| SimConnect managed SDK version drift | The installed managed wrapper may compile successfully but expose no `CallCommBusEvent`/CommBus receive types even though current MSFS 2024 docs list them. Discover the method at runtime, log bridge unavailability, and retain the WPR fallback; never assume SDK docs match the local DLL. |
| AWC rawOb prefix | AWC JSON `rawOb` INCLUDES the `METAR ` prefix (`"METAR HECA 180700Z ..."`). Any station-ID extraction must skip `METAR`/`SPECI` tokens. |
| SmoothingPipeline field drops | `SmoothingPipeline.CloneState`/`Interpolate` build `new WeatherState` field-by-field — adding a WeatherState property (e.g. `RawMetar`, `Taf`) without updating BOTH methods silently nulls it downstream. This produced "Injection FAILED" with `RawMetar = ""` while all numeric fields were fine (2026-08-18). |
| SDK 1.7.3 managed `CallCommBusEvent` AV | The managed wrapper's `MarshalToPtr` string branch walks heap PAST the ANSI terminator and computes a garbage buffer size (probed: "hello" → sizes 15/31/23, not 6) — `AccessViolationException` in `coreclr.dll`, killed the app on the 2nd injection cycle (2026-08-19). NEVER call `SimConnect.CallCommBusEvent(..., object Data)` with a string. Use the direct P/Invoke `SimConnect_CallCommBusEvent` (native export, exact UTF-8 buffer, `JS = 1` bitmask, HRESULT return). `SubscribeToCommBusEvent` uses the safe `ToHGlobal` path and is fine. |
| MSFS custom toolbar panels require SPB | HTML/CSS/JS under `html_ui/InGamePanels` alone does not create a toolbar entry. Add a `SimBase.Document Type="InGamePanels"` source XML, compile it with an `SPB` asset group, copy HTML with a separate `Copy` group, and provide the referenced toolbar icon. The Project Editor must report `Compiling SPB file ...`; a successful copy-only build is insufficient. Verified with GSX Pro and a working MSFS 2024 panel package (2026-08-19). |
| Single-file publish breaks managed SimConnect | The self-contained single-file release crashed at startup with `BadImageFormatException` loading `Microsoft.FlightSimulator.SimConnect.dll`, while a self-contained folder publish with managed and native SimConnect DLLs beside the EXE launched correctly. Keep the SimConnect assemblies external in release output. Verified 2026-08-19. |

When you discover a new landmine, add it to this table (workflow.md Phase 6).

---

## 5. Session Protocol

Follow **workflow.md** phases: TRIAGE → VERIFY BASELINE → PICK ONE ITEM → EXECUTE → PROVE → RECORD → SHIP.

- **Baseline broken?** Your item is now "fix the baseline." Nothing ships on red.
- **Touched a fetcher/decoder?** Run live verification against the real endpoint; append timestamped results to tests/live-api-results.md (FR-G4). If the live schema drifts, fixing the decoder is part of your item.
- **Done means proven:** build 0/0, tests green, (UI change ⇒ manual smoke; modeling change ⇒ METAR spot-check), docs updated (GAPS.md always; requirements.md + README.md when status/claims change).
- **Commit:** one logical change, message references FR-id or Gap# (e.g. `FR-D3: TAF panel (tests: 47)`). Never amend a failed commit; fix forward.

---

## 6. Autonomy Rules — decide and proceed, never block

You are fully authorized to complete your item without asking permission. Do not stop, do not ask, do not wait for human input mid-session. When a situation is not covered by the docs, **pick the safe default from the table below, keep working, and record the decision** in the commit body (one line: `Decision: <what> because <why>`).

| Situation | Default decision (do not ask) |
|---|---|
| Station altitude datum | Cloud BaseMeters/TopMeters and wind heights are MSL; cloud *FeetAgl fields are station-relative briefing heights. Pass reporting-station elevation to builders, retain IsSurfaceLayer when copying winds, and never use briefing AGL heights for JS MSL presets or icing/turbulence altitude comparisons. Old builder overloads assume sea level. |
| Live API changed shape (renamed field, new type) | Fix the decoder to match reality, update live-api-results.md, add regression test. Continue. |
| Live API endpoint dead | Switch to the documented fallback source; if none exists, degrade that source gracefully (failure isolation, NFR-R4), log it, continue. |
| Ambiguity between documents | Apply the §1 Conflict rule; if still ambiguous, choose the option that keeps the build green and breaks no FR/NFR. Record decision. |
| Implementation choice undocumented | The documented way wins. If undocumented everywhere, choose the conservative option (no new dependencies, no schema changes, no public API breaks). Record decision. |
| SimConnect/WPR behavior contradicts PLAN.md | Trust observed runtime behavior over the doc; adapt the code, note the contradiction in the commit, update PLAN.md in the same change if the doc is now wrong. |
| Schema/naming/UI choices | Match existing project conventions. Any reasonable choice beats stopping. |
| Tests failing at baseline | Your item becomes "fix the baseline" (workflow.md Phase 2). Fix, continue. |
| Would break an FR/NFR to proceed | Never break NFR-S1 (free forever) or NFR-R1 (out-of-process) — redesign instead. If genuinely impossible both ways, ship the smaller safe increment and record the remainder as a new gap in GAPS.md. |
| Licensing/attribution uncertainty (new data source or package) | Default to sources already in PLAN.md/README.md. If a new one is required, use only MIT/CC0/public-domain equivalents, add attribution in the same commit. If unclear, skip the dependency and record the gap. |

**Hard limits (the only things you may never decide alone — but still don't block; skip and record):** deleting user data outside the repo, publishing releases publicly, or legal/licensing commitments beyond MIT/public-domain. If an item hits one, complete everything else, mark the remainder in GAPS.md, and list it in the end-of-session summary.

**End-of-session summary (replaces asking):** when done, report (1) item completed + proof, (2) any `Decision:` lines you logged, (3) anything skipped under hard limits. The human reads this *after* the work, not during.
