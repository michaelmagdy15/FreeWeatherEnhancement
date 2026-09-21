# SkyWeave — Gap Analysis & What's Remaining
Updated: 2026-09-22 (bridge layer transitions; earlier historical status retained below)

## Bridge transitions — 2026-09-22 (FR-C4)

- Fixed the bridge's minimum-layer-count interpolation: target wind layers are sorted by altitude and seeded by sampling the current profile; obsolete wind layers are removed. Cloud layers are matched by nearest base height, new layers fade in, and unmatched layers fade out before removal. Cloud count remains capped at 24.
- Wind and gust directions now interpolate along the shortest circular arc. Small remaining differences settle to the exact target instead of stopping short.
- Source and packaged transition methods are synchronized; JS size/FILETIME entries updated in both package layouts. Existing unrelated source/package differences were preserved. No installed Community package or running simulator was modified.
- Proof: four new JS regression tests failed before the fix; all 10 JS tests now pass (`node --test tests/bridge-transitions.test.cjs`). .NET baseline and final suite: 194 passing; build: 0 errors/warnings.
- Decision: keep existing desktop scalar blending and bridge cadence; reconcile layer topology inside the bridge because it owns the actual runtime preset. Resampling onto fewer wind levels approximates the previous profile between retained levels; live transition behavior needs validation.
- Packaging follow-up: build-layout.ps1 uses .NET ticks rather than Windows FILETIME; this increment updates only the affected JS entries using ToFileTimeUtc. Audit the generator separately before the next full package rebuild.
- MSFS was not running: visual smoke and live readback remain pending. This increment does not fix AGL/MSL mapping, visibility API limitations, or acknowledgement semantics; those remain next work.

## Current injection accuracy work — 2026-09-21

- Completed code increment (NFR-A1/FR-B2/FR-B8): TAF no longer overrides observed wind, visibility or clouds; removed synthetic surface temperature/pressure offsets; thermal modeling and WPR turbulence boosting leave the zero-altitude METAR wind layer unchanged, including absent gusts.
- Proof: six new regression cases reproduced failures before their corresponding fixes. Strengthened the existing METAR-to-WPR acceptance test to require exact pressure, temperature, wind and gust values. Build: 0 warnings/errors. Offline tests: 194 passed (184 Core + 10 API), up from 188.
- Decision: keep TAF as briefing data rather than an implicit observation replacement because NFR-A1 gives observations precedence. Preserve explicit user scaling and existing aloft modeling in this bounded increment.
- Live acceptance pending: MSFS was not running during verification. No claim of a new live METAR/readback match; no fetcher or decoder changed.
- Next: bridge layer addition/removal and circular wind interpolation; consistent AGL/MSL conversion; request-correlated, altitude-aware verification; live visibility capability test; station/cache resilience and removal of placeholder radar contributions.
- Additional decoder finding deferred: TAF change-group visibility currently does not parse bare four-digit metric visibility. The authority tests use supported SM syntax so they specifically reproduce the forecast override bug.
- SimBrief, VATSIM and Web EFB integration files were preserved.

---

## CRITICAL GAPS — Audit 2026-08-18 & Verification 2026-08-19

### C1–C5 — DYNAMIC WEATHER BRIDGE PROVEN LIVE ✅
- C1 ✅ SimConnectManager typed assembly connection with background pump thread.
- C2 ✅ IsConnected strictly gated by OnRecvOpen handshake.
- C3 ✅ WPR generation + in-sim JS Bridge with `UpdateTempWeatherPreset` verified live.
- C4 ✅ Real-time sim position forwarding.
- [x] **P0 — Dynamic Weather Injection & In-Sim Bridge UI**
  - **In-sim JS Bridge**: Proven and live in MSFS 2024 via SimConnect CommBus (`SimConnect_CallCommBusEvent`) and `weatherListener.updateTempWeatherPreset`.
  - **Avionics Glassmorphism UI**: Multi-tab in-game assistant featuring Overview (METAR, wind compass, QNH, temp), Atmosphere (Winds aloft vertical profile & cloud layers), Hazards (Icing & Turbulence risk meters, convective indices), Forecast (TAF decoded timeline), and Diagnostics (live event log, filter, interpolation telemetry).
  - **Type & Unit Alignment**: Full MSFS 2024 C++ reflection compatibility with `RangeDataValue`, `inHg` sea-level pressure, and `°F` ground temperature.
  - **Sim Readback Verification**: Verified 100% exact match on live SimVars: QNH, Temperature, and Wind.

### C1-C5 detail (superseded — see above; kept for history)

The original audit found: ProgID COM connection stub (C1), always-succeeds Connect (C2), silent no-op SetWeather with fake "Injected" logs (C3), hardcoded JFK default position (C4), wrong process name (C5). All rewritten 2026-08-18 — see top of this section.

### C6. Passive mode doesn't read sim weather (HIGH, README truth violation) — RESOLVED ✅
FR-C6 updated: passive mode now reads sim weather via periodic SimConnect readback (every 5 s). `MainViewModel` subscribes to `WeatherReadbackReceived`, displays "SIM: temp / wind / pressure" readback below real-world data. UI shows readback only when sim connection is live. Timer cleaned up on stop/dispose.

### C7. Async-void timer firehoses (HIGH) — RESOLVED ✅
- ✅ `WeatherInjector` timer no longer async-void (sync callback + try/catch), position forwarding throttled (>0.05° or 30 s — was a full pipeline refresh every 2 s).
- ✅ `WeatherEngine.RunRefreshLoopAsync` uses `PeriodicTimer` (non-reentrant, no async-void timer).
- ✅ `RadarFetcher.GetPrecipitationAtPositionAsync` cached via `WeatherCache` (1-min TTL, was uncached live HTTP per cycle).
- ✅ Dead `DetectStormCellsFromLightning` method removed (unused since `StormModeler.ModelStorms` is used instead).

### C8. Duplicated fetch pipelines already drifting (MEDIUM) — RESOLVED ✅
`WeatherEngine.FetchCurrentWeatherAsync` and `UpdateWeatherAsync` now share the same `FetchAllDataAsync()` pipeline — no more copy-paste drift.

### C9. Small truths (LOW) — RESOLVED ✅
- `MainWindow.axaml:59` — version chip already bound to `{Binding VersionText}` (dynamic from assembly version), not hardcoded. GAPS.md was stale.
- `MainViewModel.cs` — no `FindNearestAirport(0, 0)` dead call found in current code.
- `MainViewModel.cs` — UI "nearby airports" uses `_stationFinder.AllAirports` (static 20-airport list). Documented limitation in AGENTS.md landmine list.
- `WindsAloftFetcher.cs:79` — 20 s timeout (verified in code), within ≤20 s standard (FR-A9). GAPS.md said 25 s — stale.

---

## What's Built ✅

| Layer | Status |
|---|---|
| All 3 projects scaffold (Core, SimBridge, App) | Done |
| All weather models (WeatherState, CloudLayer, WindLayer, etc.) | Done |
| MetarFetcher + MetarDecoder (separate, testable) | Done |
| TafFetcher + TafDecoder | Done |
| WindsAloftFetcher (Open-Meteo pressure levels) | Done |
| Multi-model fetcher — region-aware HRRR / ICON-EU / GFS / ECMWF + geopotential altitudes | Done |
| CAPE / lifted index / freezing level height / cloud cover at pressure levels | Done |
| METAR-observed ground-truth fusion | Done |
| SigmetFetcher + SigmetDecoder | Done |
| LightningFetcher (Blitzortung) | Done |
| RadarFetcher (RainViewer) | Done |
| CloudLayerBuilder (24 layers, pressure-level cloud-cover synthesis) | Done |
| WindLayerBuilder (19 pressure levels, geopotential mapping) | Done |
| IcingCalculator | Done |
| TurbulenceCalculator — thermal, convective, mountain wave, jetstream CAT | Done |
| WakeTurbulenceEngine — traffic vortex model + airport corridor mode | Done |
| StormModeler — lightning clustering + SIGMET integration + CAPE-driven intensity | Done |
| WprGenerator (WPR XML output) | Done |
| SmoothingPipeline (3-min blend, 5Hz, per-channel ease) | Done |
| StationFinder | Done |
| WeatherEngine (full orchestration, shared FetchAllDataAsync pipeline) | Done |
| WeatherCache (ConcurrentDictionary, per-type TTLs) | Done |
| SimConnectManager — typed SimConnect SDK, OnRecvOpen-gated connection, position/readback definitions, runtime CommBus bridge probe, SetWeatherTheme fallback | Done (connection/readback; bridge live test pending) |
| WeatherInjector — CommBus bridge attempt with WPR fallback, readback-verified, no fake-success logs | Experimental (live sim pending) |
| WprFileWriter — schema-correct, atomic WPR output to MSFS presets folder | Done |
| Passive mode sim weather readback — periodic SimConnect readback displays "SIM: temp/wind/pressure" | Done (C6) |
| Glassmorphic dashboard + AS-style customization (WPF + Wpf.Ui, Windows 11 Mica) | Done |
| MainViewModel (command bindings, hazard collections, settings fully wired) | Done |
| Settings persistence (UserSettings → %APPDATA%\SkyWeave\settings.json, all properties wired) | Done |
| Test project — 130 tests passing (127 Core + 3 API), 0 warnings | Done |
| Live API verification — fetchers validated against real endpoints; per-source results in tests/live-api-results.md | Done |
| Installer script + compilation (Inno Setup 6.x → 52.7 MB self-contained installer) | Done |

---

## CRITICAL GAPS — Dynamic Injection Experimental

All data-pipeline gaps from previous passes are resolved. Live fetchers are verified against real endpoints — see tests/live-api-results.md. Dynamic weather application now has an experimental HTML/JS bridge and CommBus transport, with a compiled/deployed Panel UI SPB; the installed managed SDK lacks `CallCommBusEvent`, and live injection remains unproven until the panel is loaded and tested.

---

## MODERATE GAPS — Still Open

### 1. TAF Not Wired Into Engine or UI
✅ **RESOLVED 2026-08-18 (Gap 1, commit e2531e7, FR-B8/FR-D3):** WeatherState.Taf added; WeatherEngine fetches TAF in both pipelines (30-min cache); TafDecoder change-group split/markers/gust/AMD/validity bugs fixed; TAF brief panel + FORECAST TREND timeline live in MainWindow (UI.md §5.8); 5 regression tests.

### 2. REST API & Cockpit Web EFB Companion
✅ **RESOLVED (Gap 3, FR-E1):** SkyWeave.Api project (bound to `0.0.0.0:54170` for LAN/iPad tablet access) serving:
- Mobile-first, responsive dark flight deck glass Tablet PWA (`index.html`, `style.css`, `app.js`, `manifest.json`) with live METAR, flight categories, wind compass rose, altimeter/QNH, live tactical radar canvas, winds aloft profile, and active hazards.
- REST endpoints: `GET /api/status` (`isRunning`, `version`, `simConnected`, `isInjecting`, `currentStation`), `GET /api/efb` (complete tablet snapshot), `GET /state`, `GET /metar`, `GET /hazards`, `GET /health`, `GET /api/stations`.
- Offline verified with unit tests in `SkyWeave.Api.Tests` (10 tests passing).

### 3. Radar Overlay in UI
✅ **RESOLVED 2026-08-18 (Gap 2, commit 5001015, FR-D4):** RadarTileCalculator (web-mercator tile math, tested), WeatherEngine.CurrentRadarFrame (2-min cached RainViewer frame), radar mosaic panel live in MainWindow per UI.md §5.7.

### 4. Source Resilience / Backup Servers (FR-A9)
✅ **RESOLVED 2026-08-18:** aviationweather.gov `bbox`/`station` endpoints found dead live — METAR now fetches via `ids=` with fallback chain AWC → tgftp → VATSIM; TAF via `ids=` → tgftp. Raw-text METAR decoder added. TAF validity `DDHH` parsing fixed.

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

✅ **RESOLVED 2026-09-21 (Gap 9, FR-F3):** Implemented `SimBriefPlan`, `SimBriefWaypoint`, `RouteHazardProfile` models, `SimBriefFetcher` with resilient JSON decoding and retry/backoff, and `RouteHazardAnalyzer` with great-circle corridor intersection, headwind/tailwind distance-weighting, storm cell & SIGMET detection, icing envelope evaluation, and turbulence index calculation. Verified with unit tests in `SimBriefTests.cs` (178 green tests).

---

## Updated Priority Order

  P0 ✅ Live-test HTML/JS weather bridge (CommBus + UpdateTempWeatherPreset) — PROVEN LIVE 2026-08-19 (acknowledged, readback verified)
  P0 ✅ Desktop UI modernization: migrated from Avalonia to WPF + Wpf.Ui (native Windows 11 Mica backdrop, Snap Layouts, fluent styling, 0 warnings, 130 green tests)
  P0 ✅ Beta packaging: verified self-contained publish + Inno Setup 6.x build (SkyWeave-Setup-0.4.0-beta.exe, 52.7 MB)
  P1 — VATSIM/IVAO detection                 ~1 day   edge-case differentiator
  P1 — SimConnect traffic feed for wake      ~1-2 wks makes wake moat real-world
  P2 — Plugin architecture                   ~1-2 wks long-term community play
  P2 — ERA5 historical mode                  ~3-4 d   historical killer feature
  P2 ✅ SimBrief integration (Core)          ~1-2 d   route briefing (FR-F3)

---

## Known Limitations — MSFS Weather* APIs

**ALL `Weather*` SimConnect functions are Deprecated** per MSFS SDK docs (verified 2026-08-18):
- `WeatherSetModeCustom`, `WeatherSetModeGlobal`, `WeatherSetModeServer`, `WeatherSetModeTheme`
- `WeatherSetObservation`, `WeatherCreateStation`, `WeatherRemoveStation`
- `WeatherRequestInterpolatedObservation`, `WeatherRequestObservationAtStation`, `WeatherRequestObservationAtNearestStation`
- `WeatherRequestCloudState`, `WeatherSetDynamicUpdateRate`, `WeatherCreateThermal`, `WeatherRemoveThermal`

**`WeatherSetObservation` is DEAD** — has NEVER worked in MSFS 2020 or 2024. Returns `SIMCONNECT_EXCEPTION 14 (WEATHER_INVALID_METAR)` for EVERY METAR string. Verified via live testing + FSDeveloper/Python-SimConnect/P3D forum confirmation. All of `WeatherCreateStation`, `WeatherRemoveStation`, `WeatherSetModeCustom`, `WeatherSetModeGlobal`, `WeatherSetModeServer` are also dead stubs.

**ALL ambient weather SimVars are READ-ONLY** (Settable column is empty in SDK docs):
- `AMBIENT WIND DIRECTION`, `AMBIENT WIND VELOCITY`, `AMBIENT TEMPERATURE`, `SEA LEVEL PRESSURE`
- Writing via `SetDataOnSimObject` silently fails (SIMCONNECT_EXCEPTION 20: read-only SimVar)
- Reading via `RequestDataOnSimObject` DOES work (these vars are readable)

**Current fallback path:** WPR preset files + `WeatherSetModeTheme`. The WPR schema is documented, but current MSFS 2024 developer-support reports say the WPR preset-loaded SimConnect API is dead. The file remains useful for manual preset loading and future bridge work; it is not treated as a successful dynamic injection path.

**Actual solution if dynamic injection is required:** the optional HTML/JS bridge calls the internal `UpdateTempWeatherPreset` listener through Coherent. This is documented only as work in progress and has no stable payload schema; the bridge therefore requires live validation and readback. Another out-of-process `Weather*` call will not solve it.

---

## Changes Since Last Version (2026-08-18 eighth pass)

  RESOLVED (2026-08-19 second pass — panel diagnostics + Asobo-conformant bridge):
    - **Bridge panel rebuilt to Asobo's official structure:** `fs-base-ingamepanels-*` packages inside the MSFS 2024 install (`Packages\fs-base-ingamepanels-metar`, `fs-base-ingamepanels-common`) and the `WasmAircraft` CommBus sample are now the reference. Panel HTML mirrors `GenericPanel.html` (explicit `/JS/coherent.js`, `/JS/common.js`, `/JS/Services/CommBus.js` script tags), defines the custom element in body, ends with `checkAutoload()`. `manifest.json` now depends on `fs-base-ui` (like every official panel), `export_type: "Community"`. `layout.json` paths lowercase (Asobo convention, build-layout.ps1 updated; manifest.json excluded from content).
    - **CommBus.js loading:** Asobo's sample instruments load it via `Include.addScript("/JS/Services/CommBus.js")`; panels load it as a `/JS/Services/...` script tag (GenericPanel). Panel now uses the tag (proven in panel context).
    - **Send convention matches the C++ sample** (`Samples\VisualStudio\SimConnectSamples\CommBus\CommBus.cpp`): `BufferSize = strlen + 1` (NUL included) — P/Invoke now passes `bytes.Length + 1`. C++ sample also confirmed the JS bitmask (`SIMCONNECT_COMM_BUS_BROADCAST_TO_JS = 1`), arbitrary event names, and `dwOutOf`/`dwEntryNumber` fragmentation only on the C++ receive side (JS receives whole strings — sample parses directly).
    - **On-panel live diagnostics:** panel now renders a status window (ready / event received / `UpdateTempWeatherPreset` result / ack sent / errors, 12-line history) so bridge state is visible inside the sim. Defensive NUL-trim on incoming payloads.
    - **App icon + release EXE:** `src\SkyWeave.App\Assets\SkyWeave.ico` (256/64/48/32/16 PNG-compressed, weather mark drawn via System.Drawing), `<ApplicationIcon>` wired. Published `bin\Release\App\SkyWeave.App.exe` (75.9 MB, self-contained win-x64 single-file, icon verified).
    - **STILL OPEN:** bridge ack never received in live test (13:40 session) — readback stayed at sim default; suspected the panel never mounted. This rebuild removes that doubt: restart MSFS, open the SkyWeave panel from the toolbar, and read its on-panel status.

  RESOLVED (this session):
    - **SDK 1.7.3 upgrade + crash fix:** `C:\MSFS SDK` (old path, now deleted by the installer) → `C:\MSFS 2024 SDK\SimConnect SDK`. Managed DLL now exposes the CommBus API added in SDK 1.6.4: `CallCommBusEvent(EventName, SIMCONNECT_COMM_BUS_BROADCAST_TO, Data)` with broadcast member `JS` (NOT `SIMCONNECT_COMM_BUS_BROADCAST_TO_JS` — fixed), plus `SubscribeToCommBusEvent`/`OnRecvCommBus`. `SimConnectManager` now subscribes to `SkyWeave.Weather.Acknowledge` on connect and surfaces bridge accept/reject via `WeatherInjector` status; `IsCommBusSupported()` probe verified "supported" in live app log. **However, the managed `CallCommBusEvent(..., string)` wrapper AV'd (AccessViolation in `MarshalToPtr`, killed the app on the 2nd injection cycle — wrapper walks uninitialized heap past the ANSI terminator, garbage buffer sizes)**, so `SendWeatherBridgeMessage` now bypasses the wrapper with a direct `[DllImport]` P/Invoke to the native `SimConnect_CallCommBusEvent` export (exact UTF-8 buffer, `JS = 1` bitmask, HRESULT return; handle read via `hSimConnect` IntPtr field). Readback remains the injection-success gate. Landmine added to AGENTS.md.
    - **Bridge increment:** added versioned CommBus protocol, runtime SDK capability detection, and `bridge/SkyWeaveWeatherBridge` HTML/JS source for `UpdateTempWeatherPreset`; WPR output remains the fallback.
    - **Log tooling increment:** LOG panel now has Copy (full log file → clipboard) and Folder (open logs dir) buttons; session-start diagnostic block logs OS/.NET/version/log path, MSFS running, WPR presets folder, and CommBus bridge availability probe; refresh line enriched with temp/wind/vis/data-age/model; duplicate `[SimConnect]` log lines fixed (double subscription); footer mode badge (INJECTING/PASSIVE/IDLE) per UI.md §5.12. `SimConnectManager.IsCommBusSupported()` probe + `WprFileWriter.CurrentPresetsFolder` for diagnostics.
    - `WprFileWriter.cs` — new class, writes WPR XML to `%APPDATA%\Microsoft Flight Simulator 2024\Weather\Presets\SkyWeave.WPR`
    - `SimConnectManager.cs` — rewritten: removed all dead Weather* code (station creation, METAR normalization, US-format fallback, stash logic, DEFINITIONS.AmbientWeather, REQUESTS.WeatherStation). Added `SetWeatherTheme(string presetName)`.
    - `WeatherInjector.cs` — rewritten: uses WPR preset path instead of WeatherSetObservation.
    - Removed `OnRecvWeatherObservation` callback (station creation callback never fires in MSFS 2024).
    - StrataWX research: `updateTempWeatherPreset` is undocumented, not in any SDK header. Requires reverse engineering from MSFS binaries.
    - Active Sky research: uses WPR preset files + WeatherSetModeTheme (same as our new path).
    - Updated AGENTS.md landmine list: WeatherSetObservation DEAD, updateTempWeatherPreset payload uncertainty, WPR schema units, and managed SDK version drift.
    - Updated master.md §9 with full session log, StrataWX/Active Sky research, new architecture.

  RESOLVED (earlier passes):
    - C1-C5 code fix: SimConnectManager rewritten on the typed SDK
    - C7 partial: injector timer no longer async-void; position forwarding throttled
    - Region-aware multi-model winds (HRRR/ICON-EU/GFS/ECMWF + geopotential altitudes)
    - CAPE / lifted index / freezing level / cloud cover at pressure levels
    - METAR-observed ground-truth fusion
    - Physics-based WakeTurbulenceEngine (traffic vortex + airport corridor)
    - CAPE-driven storm intensity
    - Mountain wave + jetstream CAT turbulence
    - Glassmorphic UI with AS-style sliders/toggles
    - Settings persistence fully wired
    - Live API verification pass (tests/live-api-results.md)
    - TAF wired into engine + UI brief panel + trend timeline (Gap 1, FR-B8/FR-D3)
    - Radar overlay in dashboard (Gap 2, FR-D4)
    - REST API /health /state /metar /hazards on 127.0.0.1:54170 (Gap 3, FR-E1)
    - Backup data sources: AWC ids + tgftp + VATSIM METAR chain, tgftp TAF chain (FR-A9)
    - Raw METAR text decoder + TAF DDHH validity parsing (hour-24 rollover)

   STILL OPEN:
     - Dynamic weather bridge for MSFS 2024 (WPR/WeatherSetModeTheme is a manual fallback, not a working injector)
    - VATSIM/IVAO detection, plugin architecture, SimConnect traffic feed, ERA5 historical, SimBrief
    - Beta packaging: installer verification + release build

  Decision: use the documented CommBus transport with an experimental HTML/JS bridge, because the legacy Weather* APIs are dead; keep WPR fallback until live readback proves the bridge.
  Decision: keep the bridge optional and out of the desktop process because the current SDK cannot provide a pure out-of-process weather write path without an in-sim component.
  Ambient SimVars (wind dir/speed, temperature, QNH) are read-only per SDK docs.

---

## Build/Test Status

  - Solution builds: 0 errors, 0 warnings
  - Tests: 116 passed, 0 failed (SkyWeave.Core.Tests 113 + SkyWeave.Api.Tests 3)
  - Projects: SkyWeave.Core, SkyWeave.SimBridge, SkyWeave.App, SkyWeave.Api (+ 2 test projects)
  - Target framework: net8.0
  - Version: 0.4.0-beta (all projects)
  - Release build: self-contained win-x64 single-file EXEs published (SkyWeave.App.exe 93 MB, SkyWeave.Api.exe 91 MB)
  - Installer: bin/Release/Installer/SkyWeave-Setup-0.4.0-beta.exe (46 MB, Inno Setup 6, clean compile)
  - Live API checks: see tests/live-api-results.md (incl. backup-source verification 2026-08-18)

---

End of gap analysis.
