# SkyWeave: 52-week implementation roadmap

**Planning window:** 22 September 2026–20 September 2027 (52 consecutive seven-day weeks). The annual review falls on 21 September 2027. This is a planning sequence, not a claim that listed capabilities already work. Re-plan every week from [requirements.md](requirements.md), [GAPS.md](GAPS.md), real simulator evidence, and source availability. Use [SKYWEAVE_MASTER_AGENT_PROMPT.md](SKYWEAVE_MASTER_AGENT_PROMPT.md) to execute each bounded increment.

## What success means

A pilot can start SkyWeave with MSFS 2024, see the active reporting station and source age, use desktop, toolbar, and EFB as one coherent product, and distinguish requested weather from bridge receipt, simulator application, and measured verification. Surface weather honors METAR observations. Route briefings, map layers, cloud improvements, training, and replay grow around that trusted core.

The order deliberately puts trust and installation before broad features. Quarter gates are weeks 13, 26, 39, and 52. Calendar weeks are targets; a failed gate moves unfinished work forward and displaces optional work. Do not mark a planned week complete based solely on a green build or a sent command.

## Capacity and execution rules

- Assume one primary implementation stream with about 20–30 focused engineering hours weekly, plus occasional review/testing help. If actual capacity differs, preserve order and gates rather than silently declaring parallel work complete.
- Within each week follow the repository's one-bounded-item-per-session loop. A week can contain multiple separately tested, documented increments; it is not one giant commit.
- Budget roughly 60% for implementation, 25% for tests and live validation, 15% for documentation, packaging, and unexpected repairs. Gate and buffer weeks absorb slipped dependencies.
- For every code increment: capture baseline, add meaningful offline regressions, build at zero errors/warnings, run all relevant suites, validate any touched fetcher against its real endpoint separately, and document user-visible behavior accurately.
- Simulator proof requires the running version and installed compiled bridge version to match the tested source. When MSFS is unavailable, report the exact unverified claim and do not advance it to accepted.
- Report capabilities separately as **implemented**, **bridge accepted**, **MSFS reported applied**, **partially readback verified**, **fully verified where supported**, **advisory only**, or **unavailable**. Unsupported cloud-cover readback must not be represented as a measured match.
- No mandatory accounts, paid data, keys, telemetry, in-sim executable/WASM weather engine, rendering hooks, or writes to read-only ambient SimVars. Existing HTML/JS toolbar integration remains a small simulator adapter; the weather engine remains out of process.
- Historical sources, terrain, radar, map tiles, live traffic, and aircraft effect actuators each have an explicit free/license/coverage/capability gate. If a gate fails, use the listed honest fallback and update the backlog.
- This plan prepares local release candidates. Public publication, licensing commitments beyond existing MIT terms, and deletion outside the repository remain separate hard limits under [AGENTS.md](AGENTS.md).

## Evidence that must exist at each quarterly gate

| Gate | Pilot-visible outcome | Minimum proof |
| --- | --- | --- |
| Week 13 | One reliable live aircraft weather state, trusted status, and a useful route briefing | Published desktop/EFB smoke, matching source/package versions, sea-level and elevated-station checks, no false Applied labels, clean suites |
| Week 26 | Coherent route map and improved clouds within known MSFS controls | Map/source provenance, airport and route examples, fixed-scene cloud checks, no METAR regression, clean suites |
| Week 39 | Optional alerts, scenarios, recording, and replay with honest effect levels | Deterministic replay, no weather commands in passive mode, scenario/manual smoke, clean suites |
| Week 52 | Stable milestone or explicitly labeled beta | Install/upgrade evidence, long-flight acceptance when available, release criteria audit, docs and UI aligned with actual capabilities |

## Weekly plan

### Week 01 · 2026-09-22 to 2026-09-28 · Establish the evidence baseline

**Focus:** Trust. **Implement:** Audit the actual solution, desktop, Core, API, bridge source, compiled package, installer, and tests. Reconcile stale requirement statuses and contradictory test counts against fresh output. Inventory existing SimBrief, VATSIM, EFB, modeling, and replay code before planning replacements.

**Deliverable:** Produce a feature/evidence matrix, dependency diagram, prioritized defect list, and reproducible build commands. Assign every selected item an existing FR/NFR or a clearly proposed requirement.

**Depends on:** None; read governing documents and preserve unrelated working-tree changes.

**Acceptance:** Clean build with zero warnings/errors; all discovered offline suites pass; record per-project and total counts. Every claimed feature has a code location and evidence level.

**Fallback:** If the baseline fails, spend this week repairing it; move subsequent work rather than adding features on red.

### Week 02 · 2026-09-29 to 2026-10-05 · Make the EFB a safe view of the aircraft (COMPLETED 2026-09-22)

**Focus:** Trust. **Implement:** Audit every state, METAR, hazard, and manual-station API path. Separate shared aircraft snapshots from browsing requests so tablet activity cannot change injection position, target, or mode. Remove arbitrary location fallbacks.

**Deliverable:** An immutable/versioned snapshot contract and an independent briefing-query path, integrated into the current EFB.

**Depends on:** Week 1; inspect existing WeatherApiServer and shared WeatherEngine ownership.

**Acceptance:** Concurrent tablet station browsing leaves aircraft state and injection commands unchanged; no-position requests return explicit unavailable/loading responses; latitude or longitude zero remains valid.

**Fallback:** Deliver aircraft-following read-only endpoints first; defer manual browsing until its isolation is proven.

### Week 03 · 2026-10-06 to 2026-10-12 · Finish automatic EFB hosting and shutdown (COMPLETED 2026-09-23)

**Focus:** Trust. **Implement:** Make desktop startup, hosted-server readiness, cancellation, stop, and disposal ordered and observable. Handle port 54170 conflicts, missing assets, repeated starts, and closing during startup. Keep localhost default; make tablet LAN access an explicit setting with a documented access boundary.

**Deliverable:** One desktop launch serves the bundled EFB; a connection helper reports the actual address and server health.

**Depends on:** Week 2; existing desktop host and release asset pipeline.

**Acceptance:** Launch the published folder, open the EFB, close the desktop, and verify port release; test port conflict and startup cancellation. LAN browsing cannot control injection by default.

**Fallback:** Keep reliable localhost hosting if LAN access cannot be completed safely; never silently start a second engine.

### Week 04 · 2026-10-13 to 2026-10-19 · Detect the aircraft airport correctly

**Focus:** Trust. **Implement:** Gate automatic weather acquisition and pending Start on a valid loaded-flight position. Separate nearest airport from suitable METAR reporting station. Add station-switch hysteresis and visible source distance; clear stale session state on disconnect.

**Deliverable:** Automatic location workflow that handles taxi, takeoff, reconnect, relocation, and manual briefing without an unrelated default airport.

**Depends on:** Weeks 1–2; audit station catalog coverage and current finder behavior.

**Acceptance:** Offline traces cover menu/no-fix, real zero coordinates, nearby airports, missing reports, and reconnect. Live smoke at a sea-level field and KDEN when available.

**Fallback:** If reporting-station coverage is coarse, disclose distance/coverage and retain the last valid station only with a stale flag.

### Week 05 · 2026-10-20 to 2026-10-26 · Correlate bridge application status

**Focus:** Trust. **Implement:** Define session, request, and target-generation identifiers across transport and JS callbacks. Distinguish queued, received, application-reported, partially verified, failed, and timed out. Ignore callbacks belonging to superseded targets.

**Deliverable:** One explicit injection state machine shared by desktop, bridge, and EFB.

**Depends on:** Weeks 1–4; preserve the existing weather-listener adapter.

**Acceptance:** Tests cover out-of-order success/error callbacks, duplicate acknowledgements, missing callbacks, disconnect, and a new session reusing a counter. No timer or receive event alone displays Applied.

**Fallback:** Report transport receipt only when application confirmation is unavailable; keep live application acceptance open.

### Week 06 · 2026-10-27 to 2026-11-02 · Prevent redundant injection and recover reliably

**Focus:** Trust. **Implement:** Fingerprint the complete effective bridge payload rather than only WPR XML. Separate data refresh, heartbeat, readback, interpolation, and target submission. Add bounded retries/backoff and reset dedup state on relevant reconnect/restart failures.

**Deliverable:** Change-driven target submission with recovery and observable counters.

**Depends on:** Week 5; enumerate all fields the actual bridge consumes.

**Acceptance:** Unchanged targets generate no repeated preset submissions; visibility-only and other consumed-field changes are delivered. Rejected or lost requests retry within limits; a recovered session receives its target.

**Fallback:** Retain a conservative configurable cadence if necessary, but never suppress recovery indefinitely or infer success from sending.

### Week 07 · 2026-11-03 to 2026-11-09 · Verify weather using the correct altitude and datum

**Focus:** Trust. **Implement:** Compare aircraft readback with an altitude-appropriate expected profile; retain a separate station-surface authority check. Review QNH versus pressure, temperature reference, calm-wind direction, circular angle differences, units, and sample timestamps.

**Deliverable:** Per-field verification with tolerance, expected value, observed value, provenance, and unsupported/mismatch/stale states.

**Depends on:** Weeks 4–6; supported five-double readback contract.

**Acceptance:** Offline sea-level/elevated/cruise cases and circular wind tests pass. Live supported-field comparisons at two elevations document actual tolerances and remaining uncertainty.

**Fallback:** Keep partial verification when cloud coverage or other fields are unreadable; never convert unknown to zero or fully verified.

### Week 08 · 2026-11-10 to 2026-11-16 · Prove bridge capabilities and package integrity

**Focus:** Trust. **Implement:** Audit visibility/cloud parameter application and passive-mode boundaries. Validate toolbar icon references, compiled SPB, Copy assets, manifest/layout values, cache versions, and desktop/bridge protocol compatibility. Investigate the missing icon from evidence.

**Deliverable:** A capability matrix plus a reproducible, internally installable bridge package and toolbar smoke checklist.

**Depends on:** Weeks 5–7; installed SDK availability.

**Acceptance:** Inspect produced assets and source/package parity; verify icon, panel opening, drag/pop-out, reconnect, and callback state in MSFS. Record unsupported controls separately from failed controls.

**Fallback:** If the SDK or sim is unavailable, deliver package validation and mark visual/runtime acceptance pending; do not claim the icon is fixed.

### Week 09 · 2026-11-17 to 2026-11-23 · Make data freshness and free-source feasibility explicit

**Focus:** Data. **Implement:** Track observation time, forecast valid time, retrieval time, and model run time only when supplied. Expose source health, cache age, nulls, rate limits, and fallback provenance. Investigate free archive, terrain, map, and radar availability early.

**Deliverable:** Source capability/license/coverage register and health contract; decisions for later conditional features.

**Depends on:** Weeks 1–8; actual API schema evidence.

**Acceptance:** Fixtures cover unknown run time, stale cache, null pressure levels, timeout, and malformed responses. Separately verify touched live endpoints and log UTC results.

**Fallback:** Unknown age stays unknown; an unavailable source becomes degraded rather than fabricated fresh data. Reject dependencies with incompatible terms.

### Week 10 · 2026-11-24 to 2026-11-30 · Unify route imports and validation

**Focus:** Director. **Implement:** Reuse existing SimBrief models/fetcher/analyzer. Normalize airports, waypoint coordinates, cruise/step levels, ETAs, alternates, and route provenance. Support manual/local import without an account-dependent core workflow.

**Deliverable:** A validated route contract consumed by desktop and EFB with import warnings.

**Depends on:** Weeks 2 and 9; audit optional service integration terms.

**Acceptance:** Offline fixtures cover missing alternate, malformed coordinates, duplicates, unavailable waypoints, and partial OFPs. Importing a route cannot relocate injection.

**Fallback:** Ship manual route entry/local import if optional remote import is unavailable; retain explicit missing fields.

### Week 11 · 2026-12-01 to 2026-12-07 · Sample route weather in space, height, and time

**Focus:** Director. **Implement:** Implement a bounded sampler at departure, climb, cruise, descent, arrival, and selected hazard points. Cache by source/location/valid time, cancel superseded routes, and prevent request storms.

**Deliverable:** A route sampling service with documented budgets and uncertainty for estimated timing.

**Depends on:** Weeks 9–10.

**Acceptance:** Test time zones/UTC rollover, date-line crossing, missing ETAs, forecast limits, cancellation, and cache reuse. Large routes stay within configured network/concurrency limits.

**Fallback:** Use coarser sampling with visible resolution; never extrapolate precise forecasts beyond source coverage.

### Week 12 · 2026-12-08 to 2026-12-14 · Deliver the first complete Flight Weather Director

**Focus:** Director. **Implement:** Combine route samples into departure, en-route, arrival, and alternate briefings. Show head/tailwind components, forecast hazards, altitude bands, timing, and source confidence. Require pilot/aircraft inputs for any performance estimate.

**Deliverable:** One useful IFR briefing flow in the integrated EFB and a concise desktop summary.

**Depends on:** Weeks 10–11.

**Acceptance:** A sample multi-leg route produces coherent time/height-aware risks; missing data is visible. Briefing work does not alter current observed surface weather.

**Fallback:** Omit fuel-impact estimates until inputs support them; winds and elapsed-time sensitivity are useful without invented aircraft performance.

### Week 13 · 2026-12-15 to 2026-12-21 · Quarter 1 stabilization and trust gate

**Focus:** Gate. **Implement:** Fix regressions, complete pending live acceptance, improve onboarding for the current workflow, and reconcile documentation and release artifacts.

**Deliverable:** An internal reliability milestone with a supportable end-to-end flight and evidence report.

**Depends on:** Weeks 1–12; unmet trust criteria take priority.

**Acceptance:** Clean builds/all offline suites; published-app EFB smoke; sea-level and elevated-airport injection checks; reconnect/stop/passive tests; at least a 60-minute logged flight without false success.

**Fallback:** Do not advance broad weather effects while injection state is untrustworthy. Carry unresolved blockers into the next weeks explicitly.

### Week 14 · 2026-12-22 to 2026-12-28 · Build the route map foundation

**Focus:** EFB. **Implement:** Use a license-compatible map approach with aircraft, route, airports, range, legend, units, and sensible pan/follow controls. Keep map selection separate from aircraft weather authority.

**Deliverable:** A responsive, usable map that does not depend on a paid key.

**Depends on:** Weeks 9–13; map source terms and hosting limits approved internally.

**Acceptance:** Tablet portrait/landscape and desktop smoke; dateline route display; keyboard controls; attribution and unavailable-map behavior.

**Fallback:** Use a local schematic route map if a suitable free basemap is unavailable; do not ship unlicensed tiles.

### Week 15 · 2026-12-29 to 2027-01-04 · Add truthful radar playback

**Focus:** EFB. **Implement:** Integrate supported radar coverage with geographic alignment, observation time, animation controls, unavailable areas, and stale-tile handling. Distinguish observed radar from forecasts.

**Deliverable:** A radar layer with clear age, coverage, attribution, and bounded caching.

**Depends on:** Weeks 9 and 14.

**Acceptance:** Fixture/manual checks validate time ordering, coordinates, transparency, missing tiles, and coverage edges; live endpoint verification remains separate from tests.

**Fallback:** Disable uncovered regions visibly; no invented global radar or recycled old frames labeled live.

### Week 16 · 2027-01-05 to 2027-01-11 · Add time- and altitude-filtered hazards

**Focus:** EFB. **Implement:** Display available SIGMET/AIRMET and modeled icing/turbulence layers with explicit source/type legends, validity windows, altitude filters, and route intersections.

**Deliverable:** A map hazard inspector connected to the briefing timeline.

**Depends on:** Weeks 11–15.

**Acceptance:** Tests cover polygon intersections, expiration, dateline geometry, unit conversion, unknown altitude limits, and absence of observations.

**Fallback:** Show point/corridor samples where spatial coverage is sparse; do not interpolate a detailed-looking continuous field without support.

### Week 17 · 2027-01-12 to 2027-01-18 · Create the vertical route weather view

**Focus:** EFB. **Implement:** Build a route cross-section for terrain where available, cloud bases/tops, freezing bands, winds, and modeled risks. Separate station AGL briefing heights from MSL route heights.

**Deliverable:** An interactive cross-section synchronized with map and route position.

**Depends on:** Weeks 11 and 16; terrain feasibility from Week 9.

**Acceptance:** Verify axis units, gaps, elevated airports, climb/descent profiles, and touch readability with deterministic fixtures.

**Fallback:** Omit terrain shading if no suitable dataset exists; keep MSL labels and do not suggest terrain clearance guidance.

### Week 18 · 2027-01-19 to 2027-01-25 · Improve arrival and alternate intelligence

**Focus:** Director. **Implement:** Combine current METAR with separately labeled TAF periods at estimated arrival. Add wind components for reliable runway headings and alternate comparison. Explain TEMPO/BECMG uncertainty.

**Deliverable:** Arrival/alternate cards answering what may change before landing.

**Depends on:** Weeks 10–12 and 17; validated runway data.

**Acceptance:** Test true versus magnetic heading handling, gusts, variable wind, multiple TAF groups, midnight rollover, missing runway data, and revised arrival times.

**Fallback:** Show airport wind only if runway geometry or heading reference is uncertain; runway suggestions remain weather comparisons.

### Week 19 · 2027-01-26 to 2027-02-01 · Finish VATSIM detection and ATIS behavior

**Focus:** Network. **Implement:** Audit existing network client detector and ATIS fetcher. Handle multiple clients, stale controllers, reconnect, empty ATIS, and station changes. Define exactly what ATIS lock controls; separate network reports from official observations.

**Deliverable:** Reliable optional network awareness and visible weather conflicts without hidden authority changes.

**Depends on:** Weeks 4, 9, and 18.

**Acceptance:** Fixture tests for client lifecycle and controller ambiguity; optional live verification documents actual service behavior. ATIS selection cannot silently replace METAR surface authority.

**Fallback:** Keep ATIS as briefing/advisory when authority or availability is uncertain; core weather works without network accounts.

### Week 20 · 2027-02-02 to 2027-02-08 · Make transitions flight-phase aware

**Focus:** Injection. **Implement:** Use aircraft movement, station changes, source freshness, and flight phase to choose bounded transition behavior. Preserve surface observations and avoid cloud resets during ordinary climb/cruise.

**Deliverable:** A configurable transition policy with explainable target changes.

**Depends on:** Weeks 6–7 and 18–19.

**Acceptance:** Replay traces cover taxi near station boundaries, cruise, destination descent, abrupt relocation, stale source recovery, and stop. No endless transitions or target starvation.

**Fallback:** Keep the existing stable smoothing path if new policies fail the trust gate; limit the change to proven improvements.

### Week 21 · 2027-02-09 to 2027-02-15 · Derive cloud thickness from available profiles

**Focus:** Clouds. **Implement:** Improve modeled cloud tops and thickness using supported humidity/temperature/cloud fields and geopotential heights. Preserve observed bases and show modeled-top provenance.

**Deliverable:** Profile-based cloud depth synthesis with bounded fallbacks.

**Depends on:** Weeks 7, 9, and 20.

**Acceptance:** Fixtures cover thin layers, deep moist layers, dry gaps, inversions, below-terrain nulls, elevated stations, and unavailable profiles. METAR-authority regressions remain green.

**Fallback:** Use conservative documented thickness defaults when evidence is absent, explicitly labeled estimated.

### Week 22 · 2027-02-16 to 2027-02-22 · Preserve cloud decks and improve type selection

**Focus:** Clouds. **Implement:** Stabilize layer identities through interpolation, prevent unnecessary deck merging, enforce the 24-layer limit, and map supported cloud parameters from evidence. Do not promise new renderer-native cloud species.

**Deliverable:** Multi-deck scenes that remain coherent across transitions.

**Depends on:** Weeks 8 and 21.

**Acceptance:** Offline layer birth/decay/crossing/cap tests plus repeatable live screenshots at fixed location/time/settings. Assess observed bases and transition popping separately.

**Fallback:** Where the runtime lacks type control, improve coverage/depth and report the limitation rather than exposing nonfunctional selectors.

### Week 23 · 2027-02-23 to 2027-03-01 · Improve convective weather synthesis

**Focus:** Clouds. **Implement:** Use CAPE/lifted index and available moisture/lapse information for bounded convective severity and tops. Make proximity influence explicit under a global weather state; lightning is supporting information only.

**Deliverable:** Convective synthesis with transparent evidence and no fictional geographic storm placement.

**Depends on:** Weeks 16 and 21–22.

**Acceptance:** Test high CAPE without moisture, lightning without instability, missing data, cap limits, and station METAR conflicts; record live global-scene behavior.

**Fallback:** Keep storm location on the briefing map while limiting injection to supported global effects.

### Week 24 · 2027-03-02 to 2027-03-08 · Calibrate fog and visibility behavior

**Focus:** Clouds. **Implement:** Audit actual visibility controls and readback semantics before adding humidity/dewpoint/precipitation-based evolution. Preserve observed station visibility and distinguish reporting thresholds from exact values.

**Deliverable:** A validated visibility policy and improved low-visibility scene transitions.

**Depends on:** Weeks 7–8 and 20–23.

**Acceptance:** Test 9999 versus statute miles, CAVOK/threshold displays, elevated-airport cases, and stale observations. Use fixed live scenes to document runtime response.

**Fallback:** Expose visibility targets and capability limits if exact runtime control remains unproven; never claim a match from the requested value.

### Week 25 · 2027-03-09 to 2027-03-15 · Improve precipitation and freezing-level coherence

**Focus:** Clouds. **Implement:** Align available precipitation type/intensity estimates with thermal/moisture profiles, cloud layers, and observations. Evaluate snow/freezing-rain controls only where the bridge supports them.

**Deliverable:** Coherent precipitation briefing and supported weather payloads.

**Depends on:** Weeks 9 and 21–24.

**Acceptance:** Fixtures cover warm rain, cold profiles, melting layers, absent precipitation data, and contradictions with current METAR. Live-check supported controls.

**Fallback:** Keep unsupported precipitation types advisory; do not turn missing data into zero precipitation.

### Week 26 · 2027-03-16 to 2027-03-22 · Quarter 2 realism and briefing gate

**Focus:** Gate. **Implement:** Stabilize map, route, network, and cloud work; assess free-source reliability and remaining rendering limits. Refresh documentation and internal package.

**Deliverable:** An integrated route-and-cloud milestone backed by representative flights.

**Depends on:** Weeks 14–25 and Quarter 1 trust gate.

**Acceptance:** Repeat sea-level/elevated METAR checks; fly GA low-cloud, IFR cruise/descent, and convective scenarios. Test tablet use during injection and source outages.

**Fallback:** Move failed realism features behind a disabled experimental setting; carry defects ahead of additional scope.

### Week 27 · 2027-03-23 to 2027-03-29 · Establish the air-effects capability boundary

**Focus:** Effects. **Implement:** Inventory existing turbulence, thermal, mountain-wave, wake, and icing code. Separate detection/risk, requested weather parameters, and demonstrated flight-model response. Evaluate only permitted documented/runtime-verified controls.

**Deliverable:** An effects capability table and architecture decision for every proposed effect.

**Depends on:** Weeks 8 and 26.

**Acceptance:** For each effect list input, output, actuator if any, evidence, and fallback. Passive mode produces no weather or force commands.

**Fallback:** If a safe control is unavailable, deliver useful risk/advisory behavior; no WASM, memory hooks, or read-only SimVar writes.

### Week 28 · 2027-03-30 to 2027-04-05 · Improve clear-air turbulence risk

**Focus:** Effects. **Implement:** Refine wind shear/stability-based CAT using available profile data and valid geopotential heights. Calibrate thresholds against documented scenarios without pretending to observe actual turbulence.

**Deliverable:** Altitude- and route-aware CAT risk with confidence and explanations.

**Depends on:** Weeks 11, 17, and 27.

**Acceptance:** Deterministic stable/unstable/sheared profile tests; bounded outputs and sensitivity checks; assess data resolution limits.

**Fallback:** Ship briefing-only CAT if no demonstrated compatible actuator exists.

### Week 29 · 2027-04-06 to 2027-04-12 · Improve thermals, ridge lift, and mountain-wave risk

**Focus:** Effects. **Implement:** Use available terrain, solar/time, wind, and stability inputs; separate mechanisms and avoid terrain-derived claims where elevation data is missing.

**Deliverable:** Terrain-aware risk profiles for supported regions and clear coverage limits.

**Depends on:** Weeks 9, 17, and 27–28.

**Acceptance:** Fixtures cover windward/leeward relationships, time-of-day transitions, stable layers, missing terrain, and unrealistic wind inputs.

**Fallback:** Prioritize one proven mechanism, such as thermal risk, if terrain sourcing blocks the others; record the remainder.

### Week 30 · 2027-04-13 to 2027-04-19 · Improve icing envelopes and pilot explanations

**Focus:** Effects. **Implement:** Use visible moisture and temperature profiles with the required 0 to −40 C envelope and peak near −15 C. Separate cloud icing risk, aircraft reports if readable, and any supported effect.

**Deliverable:** An icing altitude band view with severity rationale and missing-data confidence.

**Depends on:** Weeks 21, 27–29.

**Acceptance:** Test dry subzero air, cloud intersections, boundaries, warm layers, altitude datums, and severity bounds. Never infer ice accumulation solely from an advisory.

**Fallback:** Keep risk-only output when aircraft icing cannot be controlled or read reliably.

### Week 31 · 2027-04-20 to 2027-04-26 · Make wake-risk modeling traffic aware

**Focus:** Effects. **Implement:** Audit free traffic inputs and optional network traffic. Use timestamped position, altitude, aircraft category where known, wind advection, and bounded lifetimes. Label feed coverage and uncertainty.

**Deliverable:** Traffic-supported wake advisories without inventing nearby aircraft.

**Depends on:** Weeks 9 and 27; existing traffic/wake requirements.

**Acceptance:** Offline encounters test stale tracks, unknown weight/category, crossing paths, altitude separation, expiration, and absent traffic.

**Fallback:** Use offline scenario traffic for training if live sources are unavailable; no fake live wake effects.

### Week 32 · 2027-04-27 to 2027-05-03 · Add conservative wind-shear and gust-front advisories

**Focus:** Effects. **Implement:** Combine supported temporal wind changes, profile shear, and convective context. Distinguish observed changes from modeled possibilities; apply confidence and alert cooldowns.

**Deliverable:** Pilot-facing advisories integrated with route and local weather.

**Depends on:** Weeks 23 and 28–31.

**Acceptance:** Test benign variability, station switching, gust reports, missing profiles, and repeated updates to avoid false alerts. Model outputs stay bounded.

**Fallback:** Microburst awareness may remain a scenario/briefing feature; do not promise spatial microburst injection.

### Week 33 · 2027-05-04 to 2027-05-10 · Unify alerts across desktop, bridge, and EFB

**Focus:** UX. **Implement:** Define severity, confidence, trigger, persistence, acknowledgement, expiration, and deduplication for all alerts. Share one alert identity across surfaces.

**Deliverable:** A quiet, useful alert center with configurable categories.

**Depends on:** Weeks 12, 19, and 28–32.

**Acceptance:** Tests cover duplicate clients, disappearing hazards, stale data, route changes, reconnect, and acknowledgement. UI shows why an alert exists.

**Fallback:** Ship visual alerts first; do not add sound until alert timing and false-positive behavior are acceptable.

### Week 34 · 2027-05-11 to 2027-05-17 · Add optional local voice briefings

**Focus:** UX. **Implement:** Use an available local speech mechanism for concise ATIS-style summaries and selected alerts. Add mute, repeat, priority, cancellation, and rate controls; no cloud account.

**Deliverable:** Opt-in voice that complements the visual briefing.

**Depends on:** Week 33; confirm local speech dependency/license.

**Acceptance:** Test muted startup, unavailable voice engine, language/number formatting, alert interruption, and repeat suppression; manual listening pass.

**Fallback:** Retain text-only briefings if local speech is unavailable; no paid speech fallback.

### Week 35 · 2027-05-18 to 2027-05-24 · Design the offline scenario format and editor

**Focus:** Training. **Implement:** Define a versioned declarative format for initial conditions, time steps, transitions, provenance, units, and deterministic seed where needed. Validate bounded payloads and separate scenario mode from live mode.

**Deliverable:** A local save/load/editor flow with safe validation and migration rules.

**Depends on:** Weeks 20, 27, and 33.

**Acceptance:** Round-trip and malformed-file tests, unknown version handling, input limits, mode-switch behavior, and no executable content.

**Fallback:** Start with a simple form editor and a small schema; defer scripting and complex sharing features.

### Week 36 · 2027-05-25 to 2027-05-31 · Ship a useful training scenario library

**Focus:** Training. **Implement:** Author low-ceiling approach, icing climb, frontal passage, mountain-wave awareness, and convective-avoidance examples using supported controls. Include goals, conditions, expected observations, and limitations.

**Deliverable:** A curated offline library with preview and reset.

**Depends on:** Weeks 28–35.

**Acceptance:** Each scenario loads offline, produces deterministic targets, exits cleanly to live mode, and is manually exercised where simulator control is claimed.

**Fallback:** Label unsupported phenomena as briefing exercises; omit impressive-sounding templates that cannot demonstrate useful behavior.

### Week 37 · 2027-06-01 to 2027-06-07 · Record reproducible local weather sessions

**Focus:** Replay. **Implement:** Persist versioned snapshots, source provenance, targets, bridge/application events, and relevant aircraft context with bounded retention. Exclude secrets and unnecessary pilot identifiers.

**Deliverable:** User-controlled local recording with storage estimates and export.

**Depends on:** Weeks 5, 9, and 35.

**Acceptance:** Round-trip fixtures, corrupt/incomplete recordings, retention limits, opt-out, and disk-write failure tests. Recording never blocks injection.

**Fallback:** Record lower-frequency snapshots if necessary; preserve event ordering and explain reduced resolution.

### Week 38 · 2027-06-08 to 2027-06-14 · Implement deterministic session replay

**Focus:** Replay. **Implement:** Introduce explicit replay time for pause, seek, speed, and reset. Keep retrieval time, source-valid time, simulator time, and replay elapsed time distinct. Prevent live fetches from contaminating replay.

**Deliverable:** Offline replay of recorded sessions with visible mode and provenance.

**Depends on:** Week 37.

**Acceptance:** Identical recording/seed yields identical targets; seek/pause/resume and end-of-file tests; switching modes clears obsolete queued requests.

**Fallback:** Support briefing replay before simulator injection replay if time synchronization or application acceptance is unresolved.

### Week 39 · 2027-06-15 to 2027-06-21 · Quarter 3 effects, alerts, and replay gate

**Focus:** Gate. **Implement:** Stabilize optional effects, voice, training, and recording. Review every claim against its capability level and remove unsupported success labels.

**Deliverable:** A training/replay milestone with clear advisory-versus-simulated distinctions.

**Depends on:** Weeks 27–38.

**Acceptance:** Offline scenario/replay suites; voice/alert manual smoke; no-control passive test; interruption/reconnect tests; at least a two-hour representative soak.

**Fallback:** Defer optional effects rather than weakening surface weather accuracy, app stability, or core startup simplicity.

### Week 40 · 2027-06-22 to 2027-06-28 · Implement bounded historical surface weather

**Focus:** History. **Implement:** Use the free-source feasibility results to support historical METAR retrieval for verified stations/date ranges. Show archive availability before loading and preserve original observation times.

**Deliverable:** Historical surface-weather browsing and, where validated, injection.

**Depends on:** Weeks 9 and 38–39.

**Acceptance:** Separate live endpoint checks plus offline archived fixtures; missing dates, sparse stations, midnight boundaries, and source-outage tests. No current METAR fallback in historical mode.

**Fallback:** Use imported or locally recorded historical observations when no compliant online archive is available.

### Week 41 · 2027-06-29 to 2027-07-05 · Add historical upper-air data where supported

**Focus:** History. **Implement:** Evaluate archived forecast/reanalysis profiles with actual parameter, time, region, resolution, and license coverage. Use returned geopotential heights and distinguish reanalysis from original observations.

**Deliverable:** Historical profile integration for explicitly supported coverage.

**Depends on:** Weeks 9 and 40.

**Acceptance:** Verify real endpoint shapes; test profile times, below-terrain nulls, absent heights, and mismatch with surface observation time.

**Fallback:** Offer surface-only history with explicit missing aloft data; do not invent pressure-level archive support.

### Week 42 · 2027-07-06 to 2027-07-12 · Add archive-aware hazards and radar

**Focus:** History. **Implement:** Integrate only verified archived radar/hazard products; describe independent availability windows. Keep absent layers visibly absent and retain original valid intervals.

**Deliverable:** A source-by-source historical coverage view and optional archived overlays.

**Depends on:** Weeks 15–16 and 40–41.

**Acceptance:** Tests ensure no live tile or current hazard appears as historical; verify archive gaps, expired links, and mixed source resolutions.

**Fallback:** Recorded-session overlays are an acceptable complete fallback; universal historical radar is not a requirement to claim.

### Week 43 · 2027-07-13 to 2027-07-19 · Unify historical route briefing and time controls

**Focus:** History. **Implement:** Connect selected historical time, route ETAs, archive availability, replay controls, and injection mode. Explain simulator-clock mismatch and avoid changing simulator time without a deliberate setting.

**Deliverable:** One coherent historical flight workflow across desktop and EFB.

**Depends on:** Weeks 38 and 40–42.

**Acceptance:** Test timezone/daylight-saving presentation, UTC storage, date-range boundaries, mode switches, and route timing. All panels show the same selected mode/time.

**Fallback:** Disable unsupported historical layers instead of filling them with present-day weather.

### Week 44 · 2027-07-20 to 2027-07-26 · Harden regional and long-haul behavior

**Focus:** Quality. **Implement:** Exercise oceanic routes, polar regions, dateline crossing, sparse station coverage, different model domains, long flights, and cache pressure. Bound memory and request counts.

**Deliverable:** A regional coverage report and fixes for the most consequential gaps.

**Depends on:** Weeks 11, 26, and 43.

**Acceptance:** Deterministic route fixtures and recorded performance runs document CPU, memory, request volume, cancellation, and freshness behavior.

**Fallback:** Reduce sampling density with visible confidence where needed; do not claim worldwide uniform accuracy.

### Week 45 · 2027-07-27 to 2027-08-02 · Polish onboarding, accessibility, and tablet usability

**Focus:** UX. **Implement:** Make first-run connection, bridge version, weather mode, EFB access, active station, and data health understandable. Follow UI.md; address keyboard, contrast, readable units, touch targets, and reduced motion.

**Deliverable:** A complete first-flight setup flow with accessible core screens.

**Depends on:** Weeks 3, 8, 33, and 43–44.

**Acceptance:** Manual desktop/tablet accessibility smoke plus relevant UI tests; first-flight walkthrough from a clean local configuration.

**Fallback:** Prioritize functional accessibility and truthful status over visual redesign or decorative animations.

### Week 46 · 2027-08-03 to 2027-08-09 · Add safe support bundles and settings portability

**Focus:** Support. **Implement:** Export/import versioned settings, route/scenario references, source health, and diagnostic excerpts with preview/redaction. Keep sharing deliberate and local; never upload automatically.

**Deliverable:** A self-service diagnostic bundle and validated settings migration.

**Depends on:** Weeks 9, 35, 37, and 45.

**Acceptance:** Tests cover redaction, malformed imports, unsupported versions, rollback, file-size limits, and preservation of existing settings on failure.

**Fallback:** Allow a minimal text diagnostic report if richer packaging would compromise privacy or reliability.

### Week 47 · 2027-08-10 to 2027-08-16 · Define restrained extension contracts

**Focus:** Ecosystem. **Implement:** Audit FR-E extension requirements and current interfaces. Version provider and briefing contracts; isolate failures and document allowed data ownership. Avoid arbitrary untrusted in-process code loading.

**Deliverable:** A small documented extension surface and one local example, subject to capacity.

**Depends on:** Weeks 9, 27, and 46.

**Acceptance:** Contract tests cover provider timeout, invalid data, version mismatch, and failure isolation; core weather remains usable without extensions.

**Fallback:** Publish interface documentation and defer dynamic loading if safe isolation is not justified within the year.

### Week 48 · 2027-08-17 to 2027-08-23 · Stabilize the local API and contributor experience

**Focus:** Ecosystem. **Implement:** Document snapshot, health, briefing, and recording contracts with examples and compatibility rules. Provide reproducible developer setup and explain that external reads do not own aircraft state.

**Deliverable:** A versioned local API guide and working read-only companion example.

**Depends on:** Weeks 2–3, 46–47.

**Acceptance:** Examples run against the bundled host; schema/contract tests pass; documented error, stale, unavailable, and mode states match actual responses.

**Fallback:** Support a narrow stable read API instead of prematurely exposing remote injection commands.

### Week 49 · 2027-08-24 to 2027-08-30 · Finish installation and release-candidate packaging

**Focus:** Release. **Implement:** Validate desktop runtime/SimConnect DLL layout, EFB assets, compiled bridge/SPB/icon assets, version metadata, upgrade paths, and local rollback. Build distributable artifacts and draft release notes.

**Deliverable:** A reproducible local release candidate with checksums and installation evidence.

**Depends on:** Weeks 8, 45–48.

**Acceptance:** Clean-machine or documented equivalent install smoke; offline startup behavior; upgrade from supported prior output; uninstall preserves user-owned data. No single-file SimConnect regression.

**Fallback:** Prepare artifacts locally; public publication requires separate authorization and is not implied by this roadmap.

### Week 50 · 2027-08-31 to 2027-09-06 · Perform independent review and long-flight acceptance

**Focus:** Release. **Implement:** Review concurrency, cancellation, unsafe interop, HTML output handling, network exposure, input limits, and all success claims. Exercise failure recovery and extended flights.

**Deliverable:** A findings report, resolved critical defects, and a release acceptance dossier.

**Depends on:** Week 49; reviewer may be another contributor or a separate review pass.

**Acceptance:** Run an eight-hour soak when facilities permit, including EFB clients, stale source recovery, bridge reconnect, and memory/request monitoring. Repeat core METAR acceptance.

**Fallback:** Any critical finding displaces optional polish; if live facilities are unavailable, mark release acceptance incomplete.

### Week 51 · 2027-09-07 to 2027-09-13 · Use the release buffer for defects and documentation

**Focus:** Release. **Implement:** Freeze feature scope. Fix acceptance failures, close installation/documentation gaps, and retest only affected paths plus the final regression suite. Reconcile requirement status and known limitations.

**Deliverable:** A candidate whose README, UI, tests, and actual capabilities agree.

**Depends on:** Week 50 and all mandatory requirement gates.

**Acceptance:** Zero warnings/errors and all offline suites green; no unresolved release-blocking defect; install smoke repeated after packaging changes.

**Fallback:** Remain a clearly labeled beta when mandatory requirements or live proof are missing; do not relabel gaps as completed.

### Week 52 · 2027-09-14 to 2027-09-20 · Close the year with a proven milestone and next-year backlog

**Focus:** Gate. **Implement:** Evaluate all mandatory release criteria, free-source sustainability, user journeys, coverage, and remaining simulator limitations. Prepare an internal milestone and the next prioritized backlog.

**Deliverable:** A v1.0 candidate only if the governing release criteria are met; otherwise an honest stable beta plus a gap-based Year 2 plan.

**Depends on:** Weeks 13, 26, 39, and 49–51.

**Acceptance:** Publish the local evidence index, supported-capability matrix, known limitations, migration notes, and acceptance outcomes. Compare results with Week 1 baseline.

**Fallback:** Public release remains a separate authorized action. Move unsupported ambitions to a documented research backlog rather than claiming parity.

## Replanning protocol

At the end of each week, record shipped behavior, evidence links or commands, actual source and simulator limitations, remaining defects, and the next bounded item in GAPS.md. Compare planned versus actual capacity. Shift the next dependent week when its prerequisite is unproven. Fix critical regressions before adding features. Preserve the quarter-gate acceptance criteria even if the calendar changes.

The year ends with a reviewable milestone, not an automatic v1.0 label. Apply the release criteria in requirements.md to decide the version and claim only what the installed build and simulator have demonstrated.

