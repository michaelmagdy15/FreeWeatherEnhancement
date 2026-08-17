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
`SimConnectManager.cs:51` creates the connection via `Type.GetTypeFromProgID("Microsoft.FlightSimulator.SimConnect.SimConnect")`. SimConnect is **not a COM server** — that ProgID does not exist on any user machine → instance is always null → "simulation mode". The csproj references the real typed `Microsoft.FlightSimulator.SimConnect.dll`, but **zero code uses it** (verified by grep). Fix: rewrite SimConnectManager against the typed API (`new SimConnect(name, hwnd, WM_USER_SIMCONNECT, ...)`, real `OnRecvOpen/OnRecvQuit/OnRecvSimobjectData/OnRecvException` signatures).
Effort: ~1 day + live sim test

### C2. Connect() always claims success (BLOCKER, truthful-UI violation)
`SimConnectManager.cs:43,54-64,105-111` — `_isConnected = true` and `Connected` event fire even when the ProgID is missing, instance creation fails, or Connect() catches an exception (line 108 literally sets `_isConnected = true` in the failure catch). UI shows "Connected" while nothing is connected.
Fix: `_isConnected` may only become true in `OnRecvOpen` (real sim ack). Failure paths must return false + surface error.

### C3. SetWeather() silently injects nothing (BLOCKER)
`SimConnectManager.cs:153-154` — guard returns immediately when `_simConnectInstance == null` (always, per C1). Worse, `WeatherInjector.cs:97` still logs "Injected at HH:mm:ss" — **false success logs**. Even with an instance, `SetDataOnSimObject(…byte[] xml)` (line 162-166) is not a real SimConnect facility and the reflection signature lookup returns null → silent skip. WPR/preset injection needs the documented MSFS 2024 mechanism (WeatherSetModeCustom + settable ambient SimVars via a properly-defined definition, and/or the weather-preset path) — verify against SDK docs before rewriting (agents.md autonomy: trust observed runtime behavior; needs live sim).
Fix: with C1 rewrite; define injection as a method that provably changes sim weather (manual smoke: set wind 0 vs 30 kt, read back `AMBIENT WIND VELOCITY`).

### C4. Aircraft position is hardcoded JFK (BLOCKER for NFR-A1)
`SimConnectManager.cs:44,109` seeds `_lastPosition = (40.6413, -73.7781, 0)`; `MainViewModel.cs:345-353` uses it whenever "connected". The engine therefore fetches JFK weather forever, wherever the user flies. METAR-match-at-aircraft is impossible as shipped. Also `MainViewModel.cs:380` (passive start) hardcodes JFK directly. Position events never fire in practice (poll timer only created in `OnSimConnectOpen`, which needs a real connection).
Fix: with C1; no default position may ever be treated as aircraft position — display "awaiting sim position" until first real fix.

### C5. MSFS 2024 process name likely wrong (HIGH)
`SimConnectManager.cs:8` — `FlightSimulator2024`. MSFS's executable process is `FlightSimulator` (2020 and 2024 share it; verify on a live system). If wrong, `IsSimRunning()` is always false and Connect refuses to start.
Fix: check both names; verify against task manager on real sim. 5 min once verified.

### C6. Passive mode doesn't read sim weather (HIGH, README truth violation)
README claims "reads sim weather without injecting". Implementation (`WeatherEngine.cs:274-284`, `MainViewModel.cs:374-390`) still fetches *real-world* data and displays it — `WeatherSimVars.WeatherVars` (the read-side vars) are defined but **used nowhere** (verified by grep). Either implement true sim-weather readback via SimConnect data definitions or change README/FR-C6 wording to "monitor mode".
Effort: 0.5 day with C1, or 10 min (docs fix) if deferred

### C7. Async-void timer firehoses (HIGH)
- `WeatherInjector.cs:44` and `WeatherEngine.cs:103,116` — `new Timer(async _ => await …)`: unobserved exceptions, overlapping runs.
- `WeatherInjector.cs:62-68` — every `PositionUpdated` (2 s) calls `UpdatePositionAsync` → **full fetch+model pipeline every 2 s**; `RadarFetcher.GetPrecipitationAtPositionAsync` is called uncached (`WeatherEngine.cs:182,248`) → live HTTP call every cycle → RainViewer rate-limit risk.
Fix: `PeriodicTimer` + non-reentrant loop; cache radar precip (60 s TTL); throttle full rebuilds (position changes > 0.25° or 30 s).

### C8. Duplicated fetch pipelines already drifting (MEDIUM)
`WeatherEngine.FetchCurrentWeatherAsync` (151-201) vs `UpdateWeatherAsync` (212-297) are copy-paste; the former doesn't populate `RecentStrikes`/`DetectedStormCells`/`CurrentRadarFrame`. Consolidate into one private method both call.

### C9. Small truths (LOW)
- `MainWindow.axaml:59` — version chip hardcoded "v1.0" (we're v0.3).
- `MainViewModel.cs:434` — `FindNearestAirport(0, 0)` result discarded (dead call).
- `MainViewModel.cs:486-511` — UI "nearby airports" is a hardcoded 20-airport list, not StationFinder data.
- `WindsAloftFetcher.cs:79` — 25 s timeout exceeds the ≤20 s standard (FR-A9).

**Fix order: C1+C2+C3+C4 are ONE item** (typed SimConnect rewrite with real events, real injection, real position, honest connection state — provable by live sim smoke test: connect, read position, inject wind change, read back). C5 included in it. Then C7, C6, C8, C9.

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
| SimConnectManager — real SimConnect.dll, real events | **REVERTED to open — audit C1-C5: reflection-via-ProgID stub, never uses typed SDK, injection no-ops silently** |
| WeatherInjector | Done |
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

  P0 — C1-C5: Typed SimConnect rewrite (connect/connect-honesty/inject/position/process name)   ~1-2 days   without this NOTHING reaches the sim
  P0 — C7: Async-void timer + radar-fetch firehose fix                                          ~0.5 day
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

## Changes Since Last Version (2026-08-14 v2)

  RESOLVED:
    - Region-aware multi-model winds (HRRR/ICON-EU/GFS/ECMWF + geopotential altitudes)
    - CAPE / lifted index / freezing level / cloud cover at pressure levels
    - METAR-observed ground-truth fusion
    - Physics-based WakeTurbulenceEngine (traffic vortex + airport corridor)
    - CAPE-driven storm intensity
    - Mountain wave + jetstream CAT turbulence
    - Glassmorphic UI with AS-style sliders/toggles
    - Settings persistence fully wired (all properties now applied)
    - Live API verification pass (tests/live-api-results.md)
    - Test coverage grown past 40 tests (multi-model, wake, CAPE, cloud-cover)
    - TAF wired into engine + UI brief panel + trend timeline (Gap 1, FR-B8/FR-D3)
    - Radar overlay in dashboard (Gap 2, FR-D4)
    - REST API /health /state /metar /hazards on 127.0.0.1:54170 (Gap 3, FR-E1)
    - Backup data sources: AWC ids + tgftp + VATSIM METAR chain, tgftp TAF chain (FR-A9)
    - Raw METAR text decoder + TAF DDHH validity parsing (hour-24 rollover)
    - Tests: 111 passing (was 83)

  STILL OPEN:
    - SimConnect typed rewrite (C1-C5), timer firehose (C7), passive readback (C6), pipeline consolidation (C8)
    - VATSIM/IVAO detection, plugin architecture, SimConnect traffic feed, ERA5 historical, SimBrief
    - Beta packaging: installer verification + release build

---

## Build/Test Status

  - Solution builds: 0 errors, 0 warnings
  - Tests: 111 passed, 0 failed (SkyWeave.Core.Tests 108 + SkyWeave.Api.Tests 3)
  - Projects: SkyWeave.Core, SkyWeave.SimBridge, SkyWeave.App, SkyWeave.Api (+ 2 test projects)
  - Target framework: net8.0
  - Live API checks: see tests/live-api-results.md (incl. backup-source verification 2026-08-18)

---

End of gap analysis.