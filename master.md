# MASTER.md — SkyWeave Autonomous Agent Brief

**This is the only file you need to be handed.** It tells you everything about using the other documents to work fully autonomously on SkyWeave until it is production-ready for flight simmers. You never ask for permission. You start now, you decide, you ship, you report.

---

## 1. What You Are Building

**SkyWeave** — a free, MIT-licensed, real-weather injection engine for MSFS 2024 (C#/.NET 8, out-of-process SimConnect). Mission: become the best free weather addon — beating Active Sky FS (€24.99) and StrataWx ($29.99) on accuracy, physics, and freedom.

**Current state:** v0.7.0 Beta — Full production architecture operational with **336 tests green** (280 Core + 56 Api) and 0 build errors / 0 warnings.
- **Core Engine & Smoothing**: 2-Tier continuous smoothing engine (cubic Hermite easing, 1Hz in-memory preset mutation via MSFS `JS_LISTENER_WEATHER.updateTempWeatherPreset`), completely eliminating cloud popping, 1 FPS lighting stutter, and autopilot roll flips.
- **In-Sim Toolbar Panel (`SkyWeaveWeatherBridge`)**: Built and compiled with binary `.spb` registration, `<ingamepanel-skyweave>` extending `TemplateElement` for native MSFS window dragging and multi-monitor popout, and a 5-tab glassmorphic avionics UI.
- **Desktop MFD Presentation Layer**: WPF + Wpf.Ui native Windows 11 Mica backdrop, Snap Layouts, 6-tab airline MFD workspace (Flight Deck, Synoptic Radar with CartoDB Dark Matter basemap and 4-hPa isobars, SimBrief OFP & FMC wind exports for PMDG/Fenix, Vertical Skew-T Sounding FL0-FL450, Weather Studio 6-preset sandbox, and Settings).
- **Aeronautical Operations & Integrations**: Free worldwide AIP charts via Airmate and ChartFox with credential storage; 1-click official MSFS 2024 Web Flight Planner; FSDreamTeam GSX Pro ground ops (weather-aware deicing HOT countdown and ramp safety limits); live SimConnect AI traffic scanner & physics-based wake vortex model; online VATSIM / IVAO traffic overlay.
- **Cockpit Web EFB Companion**: Mobile/tablet PWA companion hosted at `http://localhost:54170` with read-only flight deck briefing.
- **Release Packaging**: Inno Setup 6 installer (`SkyWeave-Setup-0.7.0.exe`, 4.4 MB) with automated detection and deployment to MSFS 2024 Community folder (Store/Xbox App and Steam). Automated release pipeline script (`release.ps1`). Automated bridge regression tests in `MsfsCommunityBridgeManagerTests.cs`. All live on GitHub Releases.

---

## 9. Live Session Log — MSFS 2024 Injection Debug (2026-08-18)

**Goal:** make `WeatherSetObservation` actually inject METAR weather into a live MSFS 2024 sim (NFR-A1). Debugging live via `%APPDATA%\SkyWeave\logs\skyweave-YYYY-MM-DD.log`.

### Fixed this session (all verified via live log)
1. **SmoothingPipeline dropped `RawMetar`/`Taf`** — `CloneState`/`Interpolate` built new WeatherState field-by-field and nulled `RawMetar` ⇒ "Injection FAILED" with empty METAR while numerics were fine. Fixed both methods + 2 regression tests (tests now 116).
2. **AWC `rawOb` starts with `"METAR "`** — station-ID extraction got "METAR" (5 chars) instead of "HECA" ⇒ injection rejected. Extraction now skips `METAR`/`SPECI` tokens (SimConnectManager + MetarDecoder paths).
3. **Station ready-callback never fires in MSFS 2024** — `OnRecvWeatherObservation` after `WeatherCreateStation` never arrives ⇒ injector stashed forever. Now force-applies after 2 stash cycles.
4. **File logging** — every UI/engine/injector/SimConnect line appends to `%APPDATA%\SkyWeave\logs\skyweave-<date>.log` (ms timestamps, daily rotation, session header). This is the primary live-debug channel.
5. **`WeatherSetModeCustom()` moved to OnRecvOpen** — sent at connect, before any station/observation calls.
6. **Exception table corrected** — real `SIMCONNECT_EXCEPTION` names from the managed assembly (14 = WEATHER_INVALID_METAR, 16 = WEATHER_UNABLE_TO_CREATE_STATION, …); old table had wrong numbers.
7. **METAR normalizer + US-format fallback** — strips `METAR/SPECI` prefix and trailing groups (`NOSIG`, `RMK…`) after QNH/A; on exception 14 retries with US format (`9999`→`10SM`, `Q1011`→`A2985`).

### Root cause (confirmed 2026-08-18 via live research)

**`WeatherSetObservation` has NEVER worked in MSFS 2020 or 2024.** This is a dead ESP/FSX-era API that returns `SIMCONNECT_EXCEPTION 14 (WEATHER_INVALID_METAR)` for **every** METAR string — including the SDK's own examples. Multiple developers since 2020 have confirmed this exact issue (FSDeveloper #449033, Python-SimConnect #83, P3D forums). Microsoft never connected the legacy Weather* SimConnect functions to the MeteoBlue-based weather engine. `WeatherCreateStation`, `WeatherRemoveStation`, `WeatherSetModeCustom`, `WeatherSetModeGlobal`, `WeatherSetModeServer` — all dead stubs.

**How Active Sky / REX actually inject weather:** They write `.WPR` (Weather Preset) XML files to the user's weather presets folder and load them via `WeatherSetModeTheme` (or have the user select them in the weather UI). The community folder "plugin" ActiveSky installs is just an `.exe` for SimConnect communication — it does NOT inject weather via SimConnect Weather* APIs. Weather injection is file-based, not API-based.

### StrataWX / Active Sky research (confirmed 2026-08-18)

**StrataWX** uses `updateTempWeatherPreset` — an **undocumented** internal MSFS function NOT in any SDK header. It runs from an in-game toolbar panel (WASM gauge). The desktop app sends weather data to the WASM gauge via `SimConnect_CallCommBusEvent`, and the gauge calls `updateTempWeatherPreset` every frame with per-channel smoothing.

**Active Sky** uses WPR preset files + `WeatherSetModeTheme` — the documented (but deprecated) path. It writes `.WPR` files and loads them as weather presets.

**`updateTempWeatherPreset`** is NOT documented in `MSFS_Weather.h`, `SimConnect.h`, `gauges.h`, or any other SDK file. StrataWX reverse-engineered it from MSFS binaries. To use it would require IDA/Ghidra reverse engineering — multi-day effort with uncertain outcome.

### New architecture (WPR preset injection) — IMPLEMENTED 2026-08-18

We retained the **WPR preset + WeatherSetModeTheme** path as a fallback and added the documented CommBus transport to an experimental HTML/JS bridge. The bridge triggers the work-in-progress `UpdateTempWeatherPreset` event; live readback is still required before claiming success.

**Flow:**
1. `WprGenerator` generates WPR XML from `WeatherState` (existing)
2. `WprFileWriter` writes `SkyWeave.WPR` to `%APPDATA%\Microsoft Flight Simulator 2024\Weather\Presets\` (new)
3. `SimConnectManager.SetWeatherTheme("SkyWeave")` calls `WeatherSetModeTheme` (new)
4. Readback verification confirms whether the sim accepted it (existing)

**Files changed:**
- `WprFileWriter.cs` — new, writes WPR XML to MSFS presets folder
- `SimConnectManager.cs` — rewritten: removed all dead Weather* code (station creation, METAR normalization, US-format fallback, stash logic), added `SetWeatherTheme()`, removed `DEFINITIONS.AmbientWeather` and `REQUESTS.WeatherStation`
- `WeatherInjector.cs` — rewritten: uses WPR preset path instead of `WeatherSetObservation`

### What was killed (all confirmed dead in MSFS 2024)
- `WeatherCreateStation` — dead (callback never fires)
- `WeatherSetObservation` — dead (always exception 14)
- `WeatherSetModeCustom` — dead (no effect)
- Station-ready-callback / stash / force-apply logic — dead
- METAR normalizer / US-format fallback — dead (was trying to fix an unfixable API)
- `DEFINITIONS.AmbientWeather` (write definition) — removed (ambient SimVars are read-only)

### What's next
- **Test in live sim** — does `WeatherSetModeTheme` work from out-of-process?
- **If yes:** done, weather injection works via WPR presets
- **If no:** retain WPR fallback and diagnose the installed SDK/panel; do not add another out-of-process Weather* call
- UI indicator for WPR preset status

**Every finding above is also recorded in agents.md §4 Landmine List — keep that table in sync as new traps are found.**

### 2026-08-19 — HTML bridge panel registration and live package build

The first bridge package contained valid HTML/JS/CSS but did not appear in the MSFS toolbar. A fully restarted simulator confirmed that copying an unloaded `html_ui/InGamePanels` directory is insufficient: the toolbar requires a compiled `InGamePanels/*.spb` registration.

**Investigation and verified references:**
- Official MSFS 2024 packages were compared: `fs-base-ingamepanels-metar` contains `InGamePanels/InGamePanel_Metar.spb` plus its HTML panel; `fs-base-ingamepanels-common` provides shared HTML only and has no registration SPB.
- The installed GSX Pro package at `Community\fsdreamteam-gsx-pro` was inspected. It contains `InGamePanels/fsdreamteam-ingamepanels-gsx.spb`, HTML panel files, and a toolbar icon.
- The SDK confirms SPBs are generated from base XML, and lists Panel UI as an SPB-producing asset type, although the public Panel UI documentation is still marked `TO DO`.
- A working public MSFS 2024 panel project (AeroACARS) supplied the exact `SimBase.Document Type="InGamePanels"` XML pattern and the `html_ui/Textures/Menu/toolbar/` icon convention.

**Package/build corrections:**
- The original `content_type: "UI"` was invalid; installed MSFS 2024 packages use `MISC`/`CORE`/other supported types. SkyWeave uses `MISC`.
- Package name was changed to kebab-case `skyweave-weather-bridge-package`; the Project Editor initially rejected `skyweaveweatherbridgepackage`.
- The package definition was split into two asset groups: `SPB` for `SkyWeaveWeatherBridge\\InGamePanels\\` and `Copy` for `SkyWeaveWeatherBridge\\html_ui\\`.
- Added `bridge/SkyWeaveWeatherBridge/InGamePanels/skyweave-weather-bridge.xml` with `PANEL_SKYWEAVE_WEATHER_BRIDGE`, toolbar visibility, HTML URL, resize defaults, and icon reference.
- Added `html_ui/Textures/Menu/toolbar/ICON_TOOLBAR_SKYWEAVE_WEATHER_BRIDGE.svg`.
- `bridge/build-layout.ps1` was rerun; layout now includes the source XML and toolbar icon.

**Build proof:**
- Project Editor registered 2 asset groups and 3 build commands.
- Log: `Compiling SPB file skyweave-weather-bridge.xml...`
- Result: `1 skipped, 2 done and 0 failed`.
- Generated artifact: `bridge\\Packages\\skyweave-weather-bridge-package\\InGamePanels\\skyweave-weather-bridge.spb` (733 bytes).
- Generated package was deployed to the Community folder as `SkyWeaveWeatherBridge`.

**Current status:** the SPB build and deployment are complete, and the panel has appeared and registered its listener. CommBus delivery is proven. The first live apply exposed that global `Coherent.call("UpdateTempWeatherPreset", state)` is not bound; a raw `Coherent.trigger(...)` dispatches but does not apply weather. The panel now loads `/JS/Services/Weather.js`, registers `JS_LISTENER_WEATHER`, snapshots the current `WeatherPresetData`, maps SkyWeave surface/cloud/wind values into that shape, and calls `weatherListener.updateTempWeatherPreset(...)`. The rebuilt package is deployed; a fresh sim session must verify readback. No injection success may be claimed before ambient weather readback matches.

**Live panel proof:** after the successful rebuild/restart, the panel displayed `CommBus listener registered by the sim` and `Bridge ready - listening for SkyWeave.Weather.Apply` (14:31:39). The bridge is now loaded and ready for a desktop-to-sim injection test.

**Baseline proof:** the installed Avalonia 12.1.1 packages do not expose the stale `WithDeveloperTools()` extension in `SkyWeave.App/Program.cs`; removing that optional DEBUG-only call restored the required baseline. Sequential verification completed with `dotnet build SkyWeave.sln` at 0 errors/0 warnings and `dotnet test SkyWeave.sln` at 124 passed (121 Core + 3 API). Parallel build/test execution is unsafe because both processes write the same intermediate DLLs.

**UI increment:** the Avalonia dashboard received a token-based visual pass: darker flight-deck canvas, stronger text contrast, consistent glass-panel depth, improved button/toggle/input states, and clearer header/footer hierarchy. The data layout and bindings were preserved; no weather values or injection state were cosmetically promoted to success.

**Live apply result:** CommBus delivered multiple weather requests to the loaded panel, proving desktop-to-panel transport. The first-generation global call returned `NoSuchMethod`; raw trigger dispatches returned acknowledgements but left readback mismatched. The panel now uses the registered MSFS weather listener and a native `WeatherPresetData`-shaped payload. The toolbar tooltip showed `SkyWeave Weather`; the icon source was corrected to the working GSX `HIGHLIGHT` convention and invalid duplicate SVG closure was removed. The generated release app was republished to `bin\\Release\\App` after the UI pass.

**Release packaging fix:** the single-file self-contained EXE crashed at startup with `System.BadImageFormatException` while loading the managed `Microsoft.FlightSimulator.SimConnect.dll` from the bundle. A self-contained folder publish with the managed and native SimConnect DLLs beside `SkyWeave.App.exe` launches successfully and is now the release output at `bin\\Release\\App`.

---

## 10. Live Session Log — Core Engine Hardening & WPR Bridge Polish (2026-08-23)

**Commit:** `8d97832` — `feat: implement WPR weather preset file system and simulation bridge infrastructure`  
**Baseline:** Build 0 errors / 0 warnings · **127 tests passed** (124 Core + 3 API)

### What was done

#### MetarDecoder — visibility parsing hardened
- Added **fallback path**: if the AWC JSON field `visibility` is zero or missing, `DecodeRaw()` is called on the raw METAR string and the parsed value back-fills the field.
- **CAVOK / 9999 / P6SM** special-case: any METAR containing these tokens forces `VisibilityMeters` to ≥ 10,000 m (previously could come back as 0).
- **`wxString` parsing**: the AWC `wxString` field (e.g. `"-RA BR"`) is now split by spaces and each token added to `WeatherConditions`; previously wx codes from this field were silently dropped.
- **Observation-time parsing**: `DDHHmmZ` group is parsed with a month-rollover guard (day > today → subtract one month) and clamped to valid calendar days; exceptions fall back to `DateTime.UtcNow`.
- **US statute-mile visibility regex** tightened: correctly handles integer (`10SM`), fractional (`1/4SM`), and mixed (`1 1/2SM`) forms with an optional `P` prefix.

#### WakeTurbulenceEngine — geometry fix
- Fixed the **trailing-distance sign convention**: separation is now computed as the *negative* dot product of the displacement vector with the leader's track direction (`behindNm = -(offsetEast·trackEast + offsetNorth·trackNorth)`), correctly returning a positive value when you are behind the leader. Previously, aircraft ahead could be misclassified as behind, yielding no wake encounter when one was present.
- **Longitude scale correction**: east/west offset now multiplied by `cos(latitude)` before converting to nautical miles; at mid-latitudes (~30–60°) the old code over-estimated lateral separation by 15–40%.

#### WakeTurbulenceEngine — new tests (6 added, total +18 lines)
| Test | Verifies |
|---|---|
| `CalculateWakeLayers_HeavyAircraftTwoNmAheadSameAltitude_ReturnsWake` | Happy path — heavy 2 nm ahead returns a wake layer |
| `CalculateWakeLayers_AircraftBehind_ReturnsNoWake` | Aircraft behind us produces no wake encounter |
| `CalculateWakeLayers_AircraftTenNmAhead_ReturnsNoWake` | Aircraft > 5 nm ahead is out of wake range |
| `CalculateWakeLayers_AircraftHighAbove_ReturnsNoWake` | Altitude separation (3000 ft) suppresses wake |
| `CalculateWakeLayers_AircraftToSide_ReturnsNoWake` | Lateral separation (3 nm) suppresses wake |

#### SmoothingPipeline — `RawMetar` / `Taf` clone fix (+1 line)
- Added the missing `RawMetar` / `Taf` field copy in `CloneState` / `Interpolate`; regression test added to `SmoothingPipelineTests.cs`.

#### HTML/JS Bridge (CommBus panel) — polished
- `SkyWeaveWeatherBridge.js` and `.css` overhauled: better `WeatherPresetData`-shaped payload mapping, improved panel UI, more robust `Coherent` listener registration.
- Built package redeployed; `layout.json` and `manifest.json` bumped.

#### Tooling
- Added `.vscode/launch.json` and `.vscode/tasks.json` for one-click debug from VS Code.
- Added `run-debug.ps1` — convenience script to attach the test injector and tail the log in one step.

#### WeatherEngine / WeatherInjector / WprFileWriter minor fixes
- `WeatherEngine.cs`: state-propagation tweaks to ensure `RawMetar` survives the full pipeline.
- `WeatherInjector.cs`: minor null-guard and retry-path cleanup.
- `WprFileWriter.cs`: added missing edge-case for empty wind data.

### Open items / next up

- **Live verify:** the hardened METAR decoder and wake geometry need a live METAR spot-check against a known station.
- **TAF wired into engine + UI** (FR-B8 / FR-D3) — top of queue; forecast data is still dead code.
- **Radar overlay** (FR-D4) — next visual milestone.

---

## 11. Live Session Log — v0.7.0 Release & Major System Milestones (2026-10-06)

**Commit:** `acbe9c3` / `736ce18` — `feat(bridge): add automatic MSFS 2024 Community folder detection and in-app sync` / `chore(release): bump version to 0.7.0 and add automated release pipeline`  
**Baseline:** Build 0 errors / 0 warnings · **333 tests passed** (277 Core + 56 Api)

### What was completed

#### Automated MSFS 2024 Community Folder Deployment (`installer.iss` + `MsfsCommunityBridgeManager.cs`)
- Added deep auto-detection of MSFS 2024 Community folder across Microsoft Store / Xbox Game Pass (`Packages/Microsoft.Limitless_8wekyb3d8bbwe/LocalCache/UserCfg.opt`) and Steam (`AppData/Microsoft Flight Simulator 2024/UserCfg.opt`), parsing `InstalledPackagesPath` with fallbacks.
- Integrated automated in-app bridge synchronization and status reporting in `SettingsViewModel` and `MainWindow.xaml`.
- Packaged bridge folder auto-installs during setup with zero manual file copying.

#### Free Aeronautical Charts (Airmate & ChartFox) & MSFS 2024 Web Flight Planner
- Seamless 1-click departure, destination, and alternate aerodrome charts via Airmate (`fly.airmate.aero`) and ChartFox (`chartfox.org`), supporting user login persistence in `UserSettings`.
- 1-click access to the official MSFS 2024 Web Flight Planner (`planner.flightsimulator.com`).

#### FSDreamTeam GSX Pro Ground Servicing & Telemetry Integration
- Automated deicing decision support based on OAT $\le 3^\circ\text{C}$ and moisture; FAA/EASA Holdover Time (HOT) countdown timers for Type I / Type IV fluids.
- Ramp weather safety limit monitoring (caution $\ge 35\text{ kt}$, high wind stop $\ge 45\text{ kt}$, lightning $\le 5\text{ NM}$ ramp closure).
- Out-of-process SimConnect event dispatching (`FSDT_GSX_*_REQUEST`) and in-sim bridge telemetry polling (`POST /api/gsx/telemetry`).

#### CartoDB Dark Matter Basemap & Online VATSIM / IVAO Traffic Radar
- High-resolution synoptic radar overlay on CartoDB Dark Matter tile basemap with 4-hPa isobars, High/Low pressure centers, and range rings.
- Live online traffic positions fetched from VATSIM and IVAO data feeds with aircraft heading icons and callsign labels.

#### Release Pipeline Automation (`release.ps1`)
- Built single-command PowerShell script: cleans running processes, regenerates bridge layout, synchronizes local Community folder, executes full solution build in Release configuration, runs automated test suite, publishes self-contained app, and compiles Inno Setup installer (`SkyWeave-Setup-0.7.0.exe`).
- Published Beta Quick-Start Guide (`BETA_TESTING.md`).

---

## 12. Live Session Log — In-Sim Bridge SPB Packaging & Toolbar Registration Hotfix (2026-10-07)

**Commit:** `bd18644` — `fix(bridge): package compiled SPB and layout registration for MSFS toolbar (tests: 336)`  
**Baseline:** Build 0 errors / 0 warnings · **336 tests passed** (280 Core + 56 Api) · **13 Node.js bridge tests passing**

### What was identified & resolved

#### Root Cause: MSFS 2024 Toolbar Registration Failure
- Users installing via `SkyWeave-Setup-0.7.0.exe` observed that the SkyWeave bridge did not appear on the MSFS 2024 top toolbar.
- **Investigation:** MSFS 2024 (and 2020) Toolbar Manager registers in-game panels **strictly** from compiled binary `.spb` (SimBase Document) files located in `InGamePanels/` and declared in `layout.json`.
- The source distribution directory `bridge/SkyWeaveWeatherBridge/InGamePanels/` had only `skyweave-weather-bridge.xml` (uncompiled source XML) because `.spb` was previously only generated into `bridge/Packages/skyweave-weather-bridge-package/InGamePanels/`.
- `layout.json` contained `"ingamepanels/skyweave-weather-bridge.xml"`. MSFS scanned `Community/SkyWeaveWeatherBridge`, found zero valid `.spb` definitions, and ignored the package for toolbar icon creation.

#### Fix & Verification
1. Copied compiled `skyweave-weather-bridge.spb` (733 bytes) into `bridge/SkyWeaveWeatherBridge/InGamePanels/`.
2. Updated `bridge/build-layout.ps1` to exclude source `.xml` files so `layout.json` accurately registers `"ingamepanels/skyweave-weather-bridge.spb"` with exact size and Windows FILETIME timestamp.
3. Updated `bridge/SkyWeaveWeatherBridge/manifest.json` with `"package_order_hint": "PANEL_PATCH"` and `"minimum_game_version": "1.8.16"`.
4. Synchronized the corrected package to the local Community folder (`%LOCALAPPDATA%\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community\SkyWeaveWeatherBridge`).
5. Added automated regression assertions to `MsfsCommunityBridgeManagerTests.cs` verifying `skyweave-weather-bridge.spb` existence and layout registration upon deployment.
6. Re-ran `release.ps1`: 336 tests passed, clean build, and generated updated `SkyWeave-Setup-0.7.0.exe` (4.4 MB).
7. Force-updated git tag `v0.7.0` and updated the release asset on GitHub Releases.

## 2. The Document Map — what each file owns

Read this file first. Then consult the others **only when needed**:

| File | Owns | When you consult it |
|---|---|---|
| [requirements.md](requirements.md) | **WHAT to build** — every FR/NFR ID, status, release criteria | Start of session (pick item), whenever you touch a feature, before claiming anything is "done" |
| [GAPS.md](GAPS.md) | **WHAT'S NEXT** — live status board + priority queue | Start of session — this is your work queue |
| [agents.md](agents.md) | **HOW to work** — non-negotiables, engineering standards, landmine list, autonomy rules | Before writing any code — its rules are mandatory |
| [workflow.md](workflow.md) | **THE DAILY LOOP** — TRIAGE → VERIFY → PICK → EXECUTE → PROVE → RECORD → SHIP | Your session protocol — follow it every session |
| [UI.md](UI.md) | **Design system** — colors, typography, layout, components, motion, premium-feel acceptance bar | Before ANY SkyWeave.App/UI change — it is authoritative |
| [PLAN.md](PLAN.md) | Architecture, data schemas, WPR format, competitive moats | Before touching SkyWeave.Core or SkyWeave.SimBridge |
| [tests/live-api-results.md](tests/live-api-results.md) | Verified real API schemas | Before touching ANY fetcher/decoder — its schema findings beat your memory |
| [README.md](README.md) | Public claims | Update when user-visible behavior changes — claims must equal shipped truth |

**Conflict rule:** requirements.md wins on scope → GAPS.md wins on status → live-api-results.md wins on API reality → your assumptions always lose.

---

## 3. Session Bootstrap — do this immediately, in order

1. **Read this file fully** (you are doing that).
2. **Verify baseline** (workflow.md Phase 2 — never skip):
   ```powershell
   dotnet build SkyWeave.sln   # must be 0 errors, 0 warnings
   dotnet test                 # must be all green
   ```
   Red baseline? Your session is now "fix the baseline" — nothing else ships.
3. **Open GAPS.md** → take the top item from "Updated Priority Order" (bugs always first, then P0 → P1 → P2).
4. **Read the matching FR/NFR** in requirements.md and the relevant landmines in agents.md §4.
5. **Execute one item** using workflow.md Phases 4–7. One item per session. Ship it green.
6. **Report** (see §7 below).

If git is not yet initialized (Day 0 in workflow.md), do that first: `git init`, add a `.gitignore` for `bin/`/`obj/`, commit the baseline.

---

## 4. The Fast-Track to Production — Status & Priority Queue

### Completed Milestones ✅
- [x] **P0 — Dynamic Weather Bridge & In-Sim Panel**: `updateTempWeatherPreset` 1Hz in-memory preset mutation, SimConnect CommBus transport, compiled `.spb` binary registration, native simulator window dragging (`<ingamepanel-skyweave>` extending `TemplateElement`), and glassmorphism multi-tab avionics UI.
- [x] **P0 — Desktop MFD Avionics Redesign**: WPF + Wpf.Ui Windows 11 Mica backdrop, Snap Layouts, 6-tab airline MFD workspace (Flight Deck, Synoptic Radar, SimBrief & FMC exports, Vertical Skew-T Sounding, Weather Studio 6-preset sandbox, and Settings).
- [x] **P0 — Real-Time Synoptic Radar & Live Online Traffic**: RainViewer radar overlay with CartoDB Dark Matter basemap, 4-hPa isobars, High/Low pressure centers, and live VATSIM/IVAO traffic overlays.
- [x] **P0 — Local REST API & Cockpit Web EFB Companion**: Kestrel web service on `http://localhost:54170` serving dark flight deck glass PWA and REST endpoints (`/api/status`, `/api/efb`, `/api/snapshot`, `/api/traffic`, etc.).
- [x] **P1 — SimBrief & FMC Exports**: SimBrief OFP decode, great-circle route hazard profiling, one-click FMC winds aloft export (PMDG `.wx`, Fenix JSON, CSV).
- [x] **P1 — Free Aeronautical Charts & MSFS Planner**: 1-click official AIP charts via Airmate and ChartFox with credential storage; official MSFS 2024 Web Flight Planner.
- [x] **P1 — FSDreamTeam GSX Pro Integration**: Automated deicing decision support based on OAT $\le 3^\circ\text{C}$ and moisture; FAA/EASA Holdover Time (HOT) countdown; ramp safety limits.
- [x] **P1 — Real Traffic Wake Vortex Physics**: SimConnect 15 NM live AI/multiplayer aircraft scanner with physics-based wake vortex decay and cockpit hazard alerts.
- [x] **P1 — ERA5 Historical Replay**: Open-Meteo ERA5 archive (1940-present) with desktop date/hour scrubber and presets.
- [x] **P1 — Community Plugin Architecture**: Isolated `AssemblyLoadContext` plugin engine discovering `%APPDATA%\SkyWeave\plugins`.
- [x] **P1 — Dispatch Weather Briefing Package**: Printable FAA/ICAO executive flight dispatch navlog with `@media print` dark/light modes.
- [x] **P2 — Installer & Release Automation**: Inno Setup 6 installer (`SkyWeave-Setup-0.7.0.exe`) with auto-detection and installation to MSFS 2024 Community folder; single-command release script (`release.ps1`); GitHub release deployment.

### Current Priority Queue (v0.7.0 Beta & Path to v1.0)
1. **Live Community Beta Pilot Feedback**: Monitor user bug reports and telemetry from beta testers on real flight operations.
2. **Companion Addon Testing**: Verify coexistence with REX Atmos CORE and scenery packages.
3. **Documentation Polish**: Plugin author API guide and contributing documentation.

---

## 5. "Production Ready" — the exact definition of done

SkyWeave is production ready when ALL of these are true (from requirements.md §6, v1.0 exit criteria):

- [ ] Every FR and NFR in requirements.md is ✅ (or explicitly descoped in GAPS.md)
- [ ] **METAR match proven:** at the aircraft station, sim weather matches the live METAR — verified by automated test, not assertion (NFR-A1)
- [ ] Build 0 errors / 0 warnings; entire test suite green offline (NFR-Q1, Q4)
- [ ] Every fetcher re-verified against its live endpoint with timestamped results (FR-G4)
- [ ] Installer produces a clean install/uninstall (FR-G1); release assets build (FR-G2)
- [ ] REX Atmos CORE coexistence tested; no other-weather-engine conflicts unhandled (NFR-C1/C2)
- [ ] README claims = shipped features, nothing more (FR-G3)
- [ ] No crash path to the sim — out-of-process always (NFR-R1)

Do not soften these criteria. When all boxes tick, v1.0 is done and you stop.

---

## 6. Rules of Engagement (summary — agents.md is authoritative)

**Never break:**
- Free forever — no paid sources, API keys, accounts, telemetry (NFR-S1)
- Out-of-process only — nothing that can crash the sim (NFR-R1)
- No swallowed exceptions / silent nulls on data paths (NFR-R3)
- METAR match is the product (NFR-A1)
- Tests never decrease; bug fixes ship with failing-first regression tests (NFR-Q2)

**Always do:**
- One item per session; bugs before features
- Async end-to-end; bounded/clamped outputs; geopotential heights for altitude mapping
- Touch a fetcher ⇒ live-verify and record in tests/live-api-results.md
- Update GAPS.md every session; requirements.md + README.md when status/claims change
- Commit as `FR-xx/Gap# description (tests: N)` — one logical change, no bin/obj, no secrets

**Autonomy (agents.md §6):** you never need permission. Unknown situation → take the safe default from the agents.md decision table → log `Decision: <what> because <why>` in the commit → keep working. The only hard limits (never done alone, but never blocking — skip and record): deleting user data outside the repo, publishing public releases, legal commitments beyond MIT. Park those in GAPS.md and list them in your report.

---

## 7. End-of-Session Report (this replaces asking — always produce it)

When your session ends, output exactly this:

```
SESSION REPORT — <date>
1. Item completed: <FR-id/Gap# — one line what> 
   Proof: build 0/0, tests <N> passed (baseline <M>), <manual smoke / live verification / METAR spot-check result>
2. Decisions logged: <each "Decision:" line from commits, or "none">
3. Skipped under hard limits: <items parked in GAPS.md, or "none">
4. Next up: <top of GAPS.md queue for the next session>
```

The human reads this after you finish — never during. Do not wait for a reply to it; if sessions continue, start the next one immediately using §3.

---

## 8. Kickoff Prompt (copy-paste to start any agent)

> You are working on SkyWeave, a free MIT weather engine for MSFS 2024. Read MASTER.md in the repo root and follow it exactly: verify the baseline, take the top item from GAPS.md's priority queue, execute one item per session per workflow.md, obey agents.md non-negotiables, and finish with the SESSION REPORT from MASTER.md §7. You are fully authorized to make decisions without asking permission — log them as Decision: lines in your commits. Work until SkyWeave is production ready per MASTER.md §5, one green session at a time.

---

**Everything is explained. The queue is real. The baseline is green. Start at §3 — right now.**
