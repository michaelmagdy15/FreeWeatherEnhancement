# MASTER.md — SkyWeave Autonomous Agent Brief

**This is the only file you need to be handed.** It tells you everything about using the other documents to work fully autonomously on SkyWeave until it is production-ready for flight simmers. You never ask for permission. You start now, you decide, you ship, you report.

---

## 1. What You Are Building

**SkyWeave** — a free, MIT-licensed, real-weather injection engine for MSFS 2024 (C#/.NET 8, out-of-process SimConnect). Mission: become the best free weather addon — beating Active Sky FS (€24.99) and StrataWx ($29.99) on accuracy, physics, and freedom.

**Current state:** v0.3 shipped and green — full pipeline (multi-model fetch → METAR fusion → cloud/wind/icing/turbulence/storm/wake modeling → WPR XML → SimConnect injection → smoothed blending), glass UI, 40+ passing tests, 0 build warnings. The road to v1.0 is queued and waiting for you.

---

## 2. The Document Map — what each file owns

Read this file first. Then consult the others **only when needed**:

| File | Owns | When you consult it |
|---|---|---|
| [requirements.md](requirements.md) | **WHAT to build** — every FR/NFR ID, status, release criteria | Start of session (pick item), whenever you touch a feature, before claiming anything is "done" |
| [GAPS.md](GAPS.md) | **WHAT'S NEXT** — live status board + priority queue | Start of session — this is your work queue |
| [agents.md](agents.md) | **HOW to work** — non-negotiables, engineering standards, landmine list, autonomy rules | Before writing any code — its rules are mandatory |
| [workflow.md](workflow.md) | **THE DAILY LOOP** — TRIAGE → VERIFY → PICK → EXECUTE → PROVE → RECORD → SHIP | Your session protocol — follow it every session |
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

## 4. The Fast-Track to Production — priority queue

Work top-to-bottom. Each line = roughly one session. Current queue (from GAPS.md — always re-check it, it's live):

**P0 — Complete the Brief (v0.4)**
1. TAF wired into engine + UI (FR-B8, FR-D3) — ~20 min, forecast data is currently dead code
2. Radar overlay in dashboard (FR-D4) — ~1 day, visual wow
3. Local REST API (FR-E1) — ~2-3 days, slice it: scaffold + /health → /state → /metar → /hazards
4. METAR-match acceptance test automated (NFR-A1) — the core product promise, proven by test
5. Full live-API re-verification pass (FR-G4) — regenerate tests/live-api-results.md

**P1 — Feel Everything (v0.5)**
6. VATSIM/IVAO detection (FR-F2) · 7. SimConnect traffic feed → real wake encounters (FR-C7) · 8. ERA5 historical replay (FR-F4) · 9. SimBrief route briefing (FR-F3) · 10. Plugin architecture (FR-E2)

**P2 — Ship It (v1.0)**
11. Installer + release pipeline verified (FR-G1/G2) · 12. Companion-addon compatibility matrix (REX Atmos CORE) · 13. Community docs (contributing, plugin author, REST API reference) · 14. Final truthfulness audit of README vs shipped behavior

Interleave a **QUALITY day** (live re-verification, coverage, docs audit) after every 2–3 feature sessions — workflow.md's weekly cadence shows the pattern.

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
