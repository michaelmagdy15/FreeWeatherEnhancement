# SkyWeave — Requirements Specification

**Product:** SkyWeave — Free, open-source real-weather injection engine for Microsoft Flight Simulator 2024
**License:** MIT — free forever, no account, no license server, no API keys
**Version:** v0.7.0 shipped (this document drives v0.7.0 → v1.0)
**Companions:** [PLAN.md](PLAN.md) (architecture & strategy) · [GAPS.md](GAPS.md) (live gap tracker) · [EVIDENCE_BASELINE.md](EVIDENCE_BASELINE.md) (ground-truth audit) · [SKYWEAVE_52_WEEK_ROADMAP.md](SKYWEAVE_52_WEEK_ROADMAP.md) · [agents.md](agents.md) (AI agent operating manual)

---

## 1. Vision & Positioning

SkyWeave is the **best free weather engine enhancement for MSFS 2024** — a credible free alternative to Active Sky FS (€24.99+VAT) and StrataWx ($29.99) that wins on:

1. **Accuracy** — region-optimal multi-model data (HRRR 3 km CONUS, ICON-EU Europe, GFS + ECMWF IFS global), METAR-observed ground-truth fusion, geopotential-height altitude mapping
2. **Physics** — wake turbulence, jetstream CAT, mountain wave, CAPE-driven convective storms
3. **Freedom** — MIT license, every data source free and keyless, passive mode, plugin/REST extensibility
4. **Trust** — weather in the sim must match the METAR on the pilot's brief. Always.

**Success metric:** A pilot compares the sim to the real METAR at their airport and they match; the flight feels alive (winds shift, turbulence lives where it should, storms tower where CAPE says they should); and it cost them nothing.

---

## 2. Users & Personas

| Persona | Needs | Priority |
|---|---|---|
| **GA VFR simmer** | METAR-match at departure/arrival, visibility, ceilings, smooth transitions | P0 |
| **Airliner IFR simmer** | Accurate winds aloft (fuel/perf planning), CAT, icing, storms en-route, TAF trends | P0 |
| **VATSIM/IVAO flyer** | No weather conflicts with network events, real-world-now conditions | P1 |
| **Companion-addon user (REX Atmos CORE)** | Weather DATA injection that never touches visual rendering hooks | P0 |
| **Community contributor / tinkerer** | Clean code, plugin surface, REST API, MIT license to fork and extend | P1 |

---

## 3. Current State (do not regress)

Built and green as of 2026-09-22: full pipeline (multi-model fetch → METAR fusion → cloud/wind/icing/turbulence/storm/wake modeling → WPR XML → SimConnect CommBus injection via in-sim bridge + WPR fallback → 3-min smoothed blending), glassmorphic WPF dashboard with Windows 11 Mica, settings persistence, 217 passing automated offline tests (194 Core + 10 Api in .NET; 13 in JS bridge), 0 build warnings, 0 build errors, Inno Setup installer script. Ground-truth evidence matrix recorded in [EVIDENCE_BASELINE.md](EVIDENCE_BASELINE.md); ongoing work prioritized in [GAPS.md](GAPS.md) and [SKYWEAVE_52_WEEK_ROADMAP.md](SKYWEAVE_52_WEEK_ROADMAP.md).

---

## 4. Functional Requirements

Each requirement has an ID used for traceability in commits, tests, and PRs.

### FR-A — Data Acquisition

| ID | Requirement | Status |
|---|---|---|
| FR-A1 | Fetch METAR by station and by position from aviationweather.gov (JSON, no auth). Decoder MUST handle the real API schema: short field names (`temp`, `dewp`, `wdir`, `wspd`, `wgst`, `visib`, `altim`, `fltCat`, `clouds[].cover/base`), and `obsTime` delivered as a **Unix epoch number** (not a string) — must be handled without exception | ✅ / regression-guard |
| FR-A2 | Fetch TAF (same API family); decode raw/JSON including BECMG/TEMPO groups | ✅ decoder & engine wired (feeds UI, bridge, and EFB) |
| FR-A3 | Fetch winds aloft + temperature + RH + cloud cover at 19 pressure levels via Open-Meteo, with `geopotential_height_*` used for all altitude mapping (never assume pressure = altitude) | ✅ |
| FR-A4 | Region-aware model selection: HRRR 3 km (CONUS), ICON-EU (Europe), GFS 0.11°/0.25° + ECMWF IFS (global fallback), auto-selected by aircraft position | ✅ |
| FR-A5 | Fetch CAPE, lifted index, freezing level height per position | ✅ |
| FR-A6 | Fetch SIGMET/convective SIGMET/AIRMET and decode | ✅ |
| FR-A7 | Fetch lightning strikes (Blitzortung; community registration, gated opt-in, degrade gracefully when absent) | ✅ |
| FR-A8 | Fetch radar precipitation (RainViewer global; MRMS CONUS when available) | ✅ |
| FR-A9 | All fetchers: retry with backoff, per-source TTL caching (ConcurrentDictionary), and **failure isolation** — one dead source must never block the pipeline or silently poison downstream data | ✅ / harden |
| FR-A10 | Historical mode: ERA5 via Open-Meteo historical API with time-scrub replay | ✅ ERA5 reanalysis fetcher across 8 pressure levels, 24h cache, synthetic METAR generator & time-scrub controls |

### FR-B — Weather Modeling

| ID | Requirement | Status |
|---|---|---|
| FR-B1 | Synthesize up to 24 cloud layers from METAR observations fused with pressure-level cloud cover; correct coverage→density and type→scattering mapping (FEW/SCT/BKN/OVC/CB/TCU). Surface fog deck synthesis (dense stratus deck at station elevation) for IMC visibility <= 1600m and FG/FZFG; aircraft anchor layer prioritization in primary ~3 slots | ✅ Surface fog deck synthesis + aircraft anchor prioritization wired and tested |
| FR-B2 | Build wind profile from 19 pressure levels with gusts where modeled; boundary-layer gust tapering (full <=3,000 ft, zero >=10,000 ft MSL to eliminate cruise yaw hunting; suppressed in calm air <5 kt); surface wind from METAR (observed wins over model — ground-truth fusion), anchored at reporting-station elevation in the MSL profile | ✅ offline station-height/gust protection & aloft boundary-layer tapering tests pass |
| FR-B3 | Icing bands: 0 °C to −40 °C envelope, peak at −15 °C, requires visible moisture; severity bands Light/Moderate/Severe/Extreme | ✅ |
| FR-B4 | Turbulence: thermal, convective, mechanical, mountain wave (wind-over-terrain + stability), jetstream CAT (vertical/shear wind-layer analysis), in-cloud detection via pressure-level cloud cover | ✅ |
| FR-B5 | Storm modeler: cell identification from radar + SIGMET, lightning clustering, motion advection, intensity driven by CAPE/lifted index (not lightning counts) | ✅ |
| FR-B6 | Wake turbulence: physics-based vortex model scaling with lead-aircraft weight class, wingtip separation, closure rate; airport corridor mode for approach/departure encounters | ✅ |
| FR-B7 | Precipitation type/rate mapping from METAR intensity codes (-RA/RA/+RA/SN/TS…) in mm/h | ✅ |
| FR-B8 | TAF forecast data feeds briefing: wind trends, flight-category evolution, BECMG/TEMPO awareness in state and UI. Forecast groups must not overwrite current METAR observations (NFR-A1). | ✅ briefing data retained; observation isolation regression-tested 2026-09-21 |
| FR-B9 | All model outputs bounded and physically plausible: every 0–1 index clamped, temperatures sane, altitudes non-negative, winds non-negative | ✅ / regression-guard |
| FR-B10 | Sandbox Mode & Manual Weather Studio: user-configured and preset weather scenarios (Cat III Fog, Crosswind, Supercell Storm, Mountain Wave/CAT, Severe Icing, CAVOK); custom sliders (wind, temp, dewpoint, QNH, visibility, turbulence, icing, convection); instant snap vs smooth transition; REST API endpoints (`/api/sandbox`) & Web EFB integration | ✅ |

### FR-C — Simulation Injection

| ID | Requirement | Status |
|---|---|---|
| FR-C1 | Generate valid Weather Preset (WPR) XML: ≤24 `<CloudLayer>`, `<WindLayer>` stack with boundary-layer tapered gusts, `AerosolDensity`, `Precipitations`, `ThunderstormIntensity` (scaled 10x for MSFS Coherent engine); feet→meters via ×0.3048 everywhere; `IsAltitudeAMGL` correct | ✅ |
| FR-C2 | Inject via SimConnect out-of-process (managed wrapper). **Never** as WASM/in-process — a SkyWeave crash must never crash the sim. Connection only counts after the sim acknowledges (OnRecvOpen); injection success only counts after readback verification | ✅ SimConnect CommBus P/Invoke + JS bridge UpdateTempWeatherPreset verified live in MSFS 2024; WPR preset file fallback retained |
| FR-C3 | Read aircraft position (lat/lon/alt) at 1 Hz to drive station selection and region-optimal model switching. No default position — hold "awaiting sim position" until the first real fix | ✅ 1 Hz position fix + auto-airport detection; deferred start on first fix |
| FR-C4 | Smoothing & Stability: per-channel coast-then-ease blend; physical wind slew rate clamping (max 5.0 kt/s speed, 7.5 deg/s angle across shortest arc) to prevent airliner autopilot roll disconnects ("plane-flip bug"); WindsAloft layer-by-layer interpolation; Sky Anchor Corridors (Climb-Out Hold <= 4000 ft AGL, Arrival Hold <= 30 NM, Final Freeze <= 5 NM & <= 1000 ft AGL, Manual Weather Freeze) | ✅ Slew rate clamping, WindsAloft interpolation, Sky Anchor Corridors, and freeze mode tested |
| FR-C5 | Dynamic cell illusion: since WPR is a global (not per-region) weather state, continuously refresh the preset as the aircraft moves (≤5 s cadence) so storm proximity modulates density/scattering/turbulence correctly | ✅ |
| FR-C6 | Passive mode: read sim weather via SimConnect readback and display alongside real-world data; no injection | ✅ |
| FR-C7 | Live traffic feed via SimConnect (AI/multiplayer objects) driving WakeTurbulenceEngine for real encounters | ✅ Live SimConnect AIRCRAFT scanner (15 NM, 2.5s cadence, crash-proof 64-bit float marshaling), wake vortex physics model, and UI/EFB/REST telemetry |
| FR-C8 | Never subscribe-write conflicting SimVars alongside companion visual addons (REX Atmos CORE reads `AMBIENT_*`; we write them — document and test) | ✅ / test each release |

### FR-D — UI / UX

| ID | Requirement | Status |
|---|---|---|
| FR-D1 | Glassmorphic WPF + Wpf.Ui dashboard with Windows 11 Mica backdrop: current conditions, decoded METAR, cloud/wind/icing/turbulence/storm state, hazards panel | ✅ |
| FR-D2 | AS-style customization: turbulence intensity scaling, cloud density, refresh cadence, theme — persisted to `%APPDATA%\SkyWeave\settings.json` | ✅ |
| FR-D3 | TAF panel: raw + decoded, trend groups, flight-category timeline | ✅ |
| FR-D4 | Radar mosaic overlay (RainViewer tiles) in dashboard | ✅ |
| FR-D5 | Sim connection status, injection state, data-source health indicators (per-source last-success age) | ✅ |
| FR-D6 | Structural ice readback (`STRUCTURAL ICE PCT`) display | partial → P1 |

### FR-E — Extensibility & API

| ID | Requirement | Status |
|---|---|---|
| FR-E1 | Local REST API & Cockpit Web EFB Companion (SkyWeave.Api on :54170): `GET /` serves responsive tablet PWA, `GET /api/status`, `GET /api/efb` (complete tablet snapshot), `GET /state`, `GET /metar`, `GET /hazards`, `GET /health` — localhost by default, explicit LAN tablet toggle, port conflict recovery | ✅ SkyWeave.Api on :54170 serving Web EFB tablet PWA + REST endpoints; Week 02 snapshot isolation & Week 03 lifecycle/hosting completed |
| FR-E2 | Plugin architecture: `IWeatherPlugin` interface, collectible `AssemblyLoadContext` dynamic loading, directory discovery (`%APPDATA%\SkyWeave\plugins`), thread-safe engine fusion, error containment, REST endpoints, and desktop UI | ✅ Completed: dynamic plugin discovery, hot toggling, safe pipeline data fusion, REST API, Web EFB, and Desktop UI |
| FR-E3 | Stable, documented C# surface of SkyWeave.Core reusable by third parties (MIT) | ✅ / keep public API deliberate |

### FR-F — Integrations & Killer Features

| ID | Requirement | Status |
|---|---|---|
| FR-F1 | TAF wired end-to-end (see FR-B8/FR-D3) | ✅ |
| FR-F2 | Online ATC & AI detection (VATSIM, IVAO, SayIntentions): process detection → UI indicator + ATIS decoding + METAR ground truth preservation + simulator baro calibration | ✅ Full integration: VATSIM/IVAO/SayIntentions detection, live ATIS fusion, QNH prioritization, and IVAO METAR fallback |
| FR-F3 | SimBrief & Navigraph integration & FMC Winds Aloft Exporter: fetch OFP route, track Navigraph AIRAC cycle, pre-brief corridor hazards; generate PMDG .wx, Fenix JSON, CSV; copy route & Navigraph Charts link | ✅ Full SimBrief OFP & Navigraph AIRAC tracking, FMC winds exporter, route clipboard & charts link |
| FR-F4 | ERA5 historical replay with UI scrubber: Open-Meteo archive API reanalysis, 8 pressure levels, date/hour scrubber, quick presets, and REST API | ✅ Complete ERA5 archive integration, time scrubber, quick presets, and /api/historical endpoints |
| FR-F5 | SimConnect traffic feed → wake engine (see FR-C7) | ✅ Completed: live SimConnect AI/multiplayer scanning, real wake vortex prioritization, hazard injection, and desktop/EFB alerts |
| FR-F6 | Dispatch Weather Briefing Package & Printable Navlog: operational briefing (origin/dest/alt METAR/TAF, runway crosswind analysis, en-route waypoints profile, corridor hazard detection), printable HTML with dark/light mode and PDF styling, and REST endpoints (/api/dispatch/briefing, /briefing) | ✅ Completed: airline-grade operational briefing generator, route hazards, runway wind analysis, responsive printable HTML, REST API, Web EFB, and Desktop UI |

### FR-G — Distribution & Release

| ID | Requirement | Status |
|---|---|---|
| FR-G1 | Inno Setup installer (`installer.iss`) producing a signed-or-unsigned clean installer; Start Menu entries; clean uninstall | ✅ / verify each release |
| FR-G2 | GitHub release with zip + installer, release notes listing verified data sources | 🔜 |
| FR-G3 | README kept truthful: features claimed = features shipped; data-source credits (NOAA AWC, Open-Meteo, Blitzortung, RainViewer, OurAirports) | ✅ / continuous |
| FR-G4 | `tests/live-api-results.md` regenerated on every release pass — every fetcher validated against its live endpoint with timestamped results | ✅ / continuous |

---

## 5. Non-Functional Requirements

### Performance
- **NFR-P1** — Weather refresh cycle ≤ 10 min (METAR cadence); HRRR region ≤ 1 h model refresh; storm-cell refresh ≤ 5 s near convection.
- **NFR-P2** — Injection loop at 5 Hz (200 ms) with negligible CPU; fetches async, never blocking UI thread.
- **NFR-P3** — UI stays responsive at all times (async/await throughout; no `.Result`/`.Wait()` on network paths).

### Reliability
- **NFR-R1** — Out-of-process always: SkyWeave crashing/stalled must leave the sim untouched.
- **NFR-R2** — Every external fetch has timeout (≤20 s), retry-with-backoff, and cached-fallback (stale data clearly labeled over no data).
- **NFR-R3** — **No swallowed exceptions on data paths.** A decode failure must surface (log + source-health flag), never silently return null downstream (this exact bug class broke METAR decode once — see `tests/live-api-results.md` §1; guard with tests).
- **NFR-R4** — One data source failing must degrade the product gracefully (Blitzortung absent → storms still modeled from CAPE/SIGMET/radar).

### Accuracy (the core promise)
- **NFR-A1** — At the aircraft's station, injected surface conditions must match the current METAR (wind dir/speed/gust ±, temp ±1 °C, visibility class, cloud coverage class, flight category). This is verified by test, not assertion.
- **NFR-A2** — Winds-aloft altitudes mapped via geopotential heights, never pressure-altitude assumption.
- **NFR-A3** — Model selection is region-optimal by construction (HRRR CONUS, ICON-EU Europe, GFS/ECMWF elsewhere).

### Compatibility
- **NFR-C1** — REX Atmos CORE coexistence: inject meteorological data only; never touch rendering hooks; documented + release-tested.
- **NFR-C2** — Do not run alongside another weather engine (ActiveSky FS) — detect, warn, refuse or defer.
- **NFR-C3** — Target MSFS 2024 (SimConnect SDK); MSFS 2020 support is out of scope until v1.0.

### Privacy & Freedom
- **NFR-S1** — No accounts, no license servers, no API keys, no telemetry, no paid data sources. Ever. This is a product moat — treat violations as release blockers.
- **NFR-S2** — All network endpoints enumerated in README; no undocumented calls.

### Quality & Testing
- **NFR-Q1** — `dotnet build` → 0 errors, 0 warnings; `dotnet test` → all green; no merge otherwise.
- **NFR-Q2** — New logic ships with xUnit tests; bug fixes ship with a regression test that fails before the fix.
- **NFR-Q3** — Touched fetcher ⇒ live endpoint verification recorded in `tests/live-api-results.md` (timestamped, per-source).
- **NFR-Q4** — Test suite runs offline (live checks are a separate, explicit pass — never inside `dotnet test`).

---

## 6. Release Plan & Exit Criteria

### v0.6.0 — "Avionics Glass & Dynamic Bridge" (Shipped Baseline)
- [x] TAF wired into engine + UI (FR-B8, FR-D3)
- [x] Radar overlay in dashboard (FR-D4)
- [x] REST API v1 & Web EFB Companion (FR-E1)
- [x] METAR-match acceptance tests automated (NFR-A1, 217 offline tests green)
- [x] Live verification pass recorded (FR-G4, tests/live-api-results.md)
- [x] Dynamic in-sim weather bridge with CommBus P/Invoke & UpdateTempWeatherPreset (FR-C2)
- [x] In-sim panel native window movement, detach/pop-out, and multi-tab glassmorphic UI (FR-C1/UI)
- [x] Ground-truth evidence baseline audited ([EVIDENCE_BASELINE.md](EVIDENCE_BASELINE.md))

### 52-Week Execution Roadmap (Active Development)
See [SKYWEAVE_52_WEEK_ROADMAP.md](SKYWEAVE_52_WEEK_ROADMAP.md) for weekly milestones:
- **Phase 0 (Weeks 01–13):** Trust & Live Acceptance (baseline audit, EFB snapshot isolation, auto-airport detection, altitude-aware readback).
- **Phase 1 (Weeks 14–26):** Flight Weather Director (SimBrief route briefing wire-up, hazard corridor, airport intelligence).
- **Phase 2 (Weeks 27–38):** Professional EFB & Weather Map (tactical map, layer toggles, tablet UX).
- **Phase 3 (Weeks 39–44):** Cloudscape & Visibility Realism (multi-deck stability, fog gradients, realistic convective scenes).
- **Phase 4 (Weeks 45–48):** Active Air & Training Realism (fine-tuned turbulence/icing, scenario presets).
- **Phase 5 (Weeks 49–50):** Historical Weather & Replay (ERA5 archive integration, time scrubber).
- **Phase 6 (Weeks 51–52):** Ecosystem, Release Pipeline & v1.0 Launch (VATSIM lock, installer/release automation).

---

## 7. Out of Scope (explicitly)

- ❌ Visual weather rendering (Rayleigh scattering, cloud textures, lighting) — REX Atmos CORE's domain
- ❌ WASM/in-process modules — stability rule (NFR-R1)
- ❌ Paid or key-gated data sources (NFR-S1)
- ❌ MSFS 2020 support before v1.0 (NFR-C3)
- ❌ Discrete per-lat/lon storm placement via WPR — MSFS exposes one global weather state; we model proximity effects instead (be honest in docs)

---

## 8. Traceability — Requirements → Moats

| Moat (PLAN.md) | Requirements |
|---|---|
| 1. Region-optimal multi-model accuracy | FR-A3, FR-A4, NFR-A2, NFR-A3 |
| 2. Physics-based wake turbulence | FR-B6, FR-C7 |
| 3. METAR ground-truth fusion | FR-A1, FR-B2, NFR-A1 |
| 4. CAPE/lifted-index storms | FR-A5, FR-B5 |
| 5. Free + MIT + passive mode | FR-C6, NFR-S1, NFR-S2 |
| 6. Glass UI + customization | FR-D1, FR-D2 |
| 7. Plugin + REST extensibility | FR-E1, FR-E2 |

---

*Maintained alongside PLAN.md (strategy), GAPS.md (live status), and agents.md (execution rules). When a requirement changes, update all four in the same commit.*
