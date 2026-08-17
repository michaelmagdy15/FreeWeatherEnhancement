# SkyWeave — Daily Development Workflow

**Purpose:** A repeatable daily operating procedure for driving SkyWeave toward v1.0 ("best free weather engine for MSFS 2024") using AI agents. Run this every day. Each pass must end with the repo greener than it started.

**Companions:** [requirements.md](requirements.md) (what to build, FR/NFR IDs) · [agents.md](agents.md) (agent rules) · [PLAN.md](PLAN.md) (architecture & moats) · [GAPS.md](GAPS.md) (live status board)

**Core loop, every day:**

```
TRIAGE → VERIFY BASELINE → PICK ONE WORK ITEM → EXECUTE → PROVE → RECORD → SHIP
   15 min      5 min            5 min           ~2-4 h      ~30 min    ~15 min   ~10 min
```

---

## Day 0 — One-Time Setup (skip if already done)

- [ ] Initialize git (repo is currently NOT under version control):
  ```powershell
  git init; git add -A; git commit -m "SkyWeave v0.3 baseline"
  ```
- [ ] Push to a GitHub remote (private or public).
- [ ] Add `.gitignore` for `bin/`, `obj/` (they're currently committed artifacts).
- [ ] Confirm baseline: `dotnet build` → 0 errors 0 warnings; `dotnet test` → 40+ passed.
- [ ] Read [GAPS.md](GAPS.md) priority list and [requirements.md](requirements.md) §6 release plan once.

---

## Phase 1 — Triage (15 min)

1. **Check the boards:**
   - [GAPS.md](GAPS.md) "Updated Priority Order" — current P0/P1/P2 queue
   - GitHub issues (if open to users): bugs first, always
   - `git log --oneline -10` — what landed recently (avoid re-doing / conflicting)
2. **Pick today's track:**

| Track | Trigger | Focus |
|---|---|---|
| **BUG DAY** | Any open bug, user report, or failing test | Fixes + regression tests. Bugs always outrank features. |
| **FEATURE DAY** | Zero open bugs | Next P0/P1 from GAPS.md (current: TAF wiring → radar overlay → REST API) |
| **QUALITY DAY** | After every 2–3 feature days | Test coverage, live-API re-verification, docs truthfulness, refactor debt |
| **RELEASE DAY** | Milestone exit criteria met (requirements.md §6) | Tag, notes, installer, live verification pass |

3. **Write today's mission as one sentence** in the task/commit, e.g.:
   > "Wire TAF into WeatherEngine + add TAF panel (FR-B8, FR-D3, Gap 1)."

---

## Phase 2 — Verify Baseline (5 min, never skip)

```powershell
dotnet build SkyWeave.sln            # MUST be 0 errors, 0 warnings
dotnet test                          # MUST be all green
```

- If baseline is broken, today's track becomes **BUG DAY** automatically. Fix first; add nothing.
- Record the passing test count — it must be strictly higher by end of day (except on pure-refactor days, where it must not drop).

---

## Phase 3 — Pick One Work Item (5 min)

Rules:
- **One item per day.** Half-finished features are worse than missing features.
- Order within a track: bugs > P0 gaps > P1 > P2. Never invent new P0s; they come from GAPS.md/requirements.md only.
- Big items (REST API, plugin architecture, traffic feed) get **sliced**: day 1 = project scaffold + `GET /health`, day 2 = `GET /state`, etc. Each slice ships green.
- If the item needs a decision the docs don't answer, apply the agents.md §6 default-decision table, log `Decision: <what> because <why>` in the commit, and keep moving. Never block on a human.

---

## Phase 4 — Execute (~2–4 h)

Standard build sequence for any item:

1. **Read before writing** — the target file(s), its tests, and the matching FR/NFR in requirements.md.
2. **Test first where possible** — write the failing test that defines "done" (bug fix ⇒ regression test that fails before the fix; feature ⇒ tests for the new behavior).
3. **Implement** — follow agents.md rules (no swallowed exceptions, bounded outputs, async all the way, README stays truthful).
4. **Touching a fetcher?** The item is not done until live verification runs (Phase 5, step 3).

Feature-day slicing guide for current queue:
- **TAF wiring (Gap 1):** add `TafData? CurrentTaf` to WeatherState → call TafFetcher in `WeatherEngine.UpdateWeatherAsync()` → TAF panel in MainWindow → tests.
- **Radar overlay (Gap 2):** tile fetch service → Canvas overlay control → cache → UI toggle.
- **REST API (Gap 3):** new `SkyWeave.Api` project → minimal API, localhost-only → `GET /state` → `GET /metar` → `GET /hazards` → `GET /health` → tests.

---

## Phase 5 — Prove (~30 min)

The item is NOT done until all of these pass:

1. **Build & test:**
   ```powershell
   dotnet build SkyWeave.sln    # 0 errors, 0 warnings
   dotnet test                  # all green, count ≥ baseline
   ```
2. **Manual smoke** (if UI/sim-facing): launch `dotnet run --project src/SkyWeave.App`, connect to MSFS if available, confirm the feature behaves and nothing else regressed (METAR still matches, injection still smooth).
3. **Live API verification (only if fetchers touched):** hit each touched endpoint for real, append timestamped per-source results to [tests/live-api-results.md](tests/live-api-results.md). Watch for the bug class that broke METAR once: schema/type drift (e.g. `obsTime` epoch number vs string). If the live API changed shape, fixing the decoder is part of today's item.
4. **Accuracy spot-check (if modeling touched):** compare injected surface conditions vs the live METAR at a known station (NFR-A1). Must match.

---

## Phase 6 — Record (~15 min)

Update, in the same session:

- [ ] **GAPS.md** — move the item to "What's Built" (or shrink its gap entry), bump the date header, note test count change.
- [ ] **requirements.md** — flip the FR/NFR status if it changed (🔜 → ✅). Only claim what's proven.
- [ ] **README.md** — new user-visible feature ⇒ new feature line. Claims must equal shipped behavior (FR-G3).
- [ ] **agents.md** — only if a new landmine was discovered (add it to the pitfalls list so agents never repeat it).

---

## Phase 7 — Ship (~10 min)

```powershell
git status; git diff        # review: only intended files, no bin/obj, no secrets
git add <intended files>
git commit -m "<FR-id or Gap#> <what changed> (tests: <new count>)"
```

Commit message pattern: `FR-B8/D1: wire TAF into engine + dashboard panel (tests: 47)` · `Gap 3: REST API scaffold + GET /health (tests: 45)` · `Fix: MetarDecoder obsTime epoch handling + regression test (tests: 44)`.

- One logical change per commit. Never mix features and unrelated refactors.
- Push to remote. On RELEASE DAY: tag `v0.x.0`, attach installer + zip, release notes list verified data sources with timestamps.

---

## Weekly Cadence (optional but recommended)

| Day | Default track |
|---|---|
| Mon | Triage + FEATURE (start the week's P0) |
| Tue | FEATURE (continue/slice) |
| Wed | FEATURE or BUG |
| Thu | QUALITY: full live-API re-verification, coverage, docs audit |
| Fri | Bugs found during the week + GAPS.md/requirements.md full re-sync |
| End of milestone | RELEASE DAY |

---

## Autonomy Rules — never block, always ship something

You never need permission to proceed. When a situation isn't covered by the docs, take the safe default, log it, keep working:

| Situation | Default (do not ask) |
|---|---|
| Live API changed shape / endpoint dead | Fix the decoder to match reality (or switch to documented fallback / degrade gracefully); update live-api-results.md; add regression test. |
| Implementation choice undocumented | Conservative option: no new dependencies, no schema changes, no public API breaks. Match existing conventions. |
| SimConnect/WPR contradicts PLAN.md | Trust observed runtime behavior; adapt code, update PLAN.md in the same change. |
| Would break an FR/NFR | Redesign; never break NFR-S1 (free) or NFR-R1 (out-of-process). Otherwise ship the smaller safe increment, log remainder in GAPS.md. |
| Baseline broken | Today becomes BUG DAY automatically (Phase 2). |
| Licensing/attribution unclear | Use only sources already documented; MIT/CC0/public-domain only; otherwise skip and record the gap. |

**Hard limits (never decide alone — but don't block: skip, record, move on):** deleting user data outside the repo, publishing public releases, legal commitments beyond MIT. Record any hits in GAPS.md + end-of-day summary.

**End-of-day summary (replaces asking):** (1) what shipped + proof, (2) `Decision:` lines logged, (3) anything skipped. The human reads this after the session, not during.

---

## Anti-Goals (daily reminders)

- ❌ No new features while a bug is open or tests are red.
- ❌ No swallowed exceptions, no silent null-return on data paths (NFR-R3).
- ❌ No claiming features in README that aren't proven by test or manual smoke (FR-G3).
- ❌ No WASM/in-process code, no paid/keyed data sources, no telemetry (NFR-S1, NFR-R1).
- ❌ No half-finished merges — every day ends green or gets reverted.

---

*Run it tomorrow. Then the day after. v1.0 is ~40 good days away.*
