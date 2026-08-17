# UI.md — SkyWeave Design System & UX Specification

**Authority:** this file is the single source of truth for every visual and interaction decision in SkyWeave.App (Avalonia, `src/SkyWeave.App/Views/MainWindow.axaml`). Any UI change must comply with it. If reality and this doc conflict, fix whichever is wrong in the same commit.

**Goal:** a **premium, modern MSFS 2024-era app** — glassmorphic, dark, calm, avionics-inspired. The user should feel they opened a $30 product that happens to be free. Aviation credible, never gamer-busy.

---

## 1. Design Language — "Dark Flight Deck Glass"

One sentence: **a night-flight instrument panel made of glass.**

| Pillar | Meaning |
|---|---|
| **Glass over dark** | Frosted acrylic panels (AcrylicBlur) floating on a near-black backdrop — like instruments glowing in a dark cockpit |
| **Avionics accuracy** | Data rendered like an EFIS/MFD: uppercase labels, tabular numbers, severity colors pilots already know (green/amber/red) |
| **Calm motion** | Weather changes ease in (3-min blend) — so must the UI. Nothing blinks, jumps, or pops. Ever. |
| **Depth via light** | Hierarchy comes from luminance and blur, not borders and boxes-on-boxes |
| **One accent does the talking** | SkyWeave red (#E94560) is reserved for brand + active state. Data colors are reserved for data. Never mix. |

### Anti-language (instant rejection)
- ❌ Skeuomorphic bezels, screws, brushed metal, fake LCD screens
- ❌ Rainbow gradients, neon glows on everything, "RGB gamer" aesthetics
- ❌ Dense toolbars, menu bars, status bars stacked like a 2005 desktop app
- ❌ Modal dialogs for anything recoverable (use inline banners/toasts)
- ❌ White/light theme as default (dark is the identity; light theme is v1.x optional)

---

## 2. Color System

### Base palette (dark layers)
| Token | Hex | Use |
|---|---|---|
| `Bg.Deep` | `#0A0E14` | Window base under the acrylic |
| `Bg.Panel` | `#141922` @ 72% opacity + blur | Glass panel fill |
| `Bg.Panel.Hover` | `#1B2230` @ 80% | Hover state fill |
| `Bg.Inset` | `#0E131B` | Inset wells: code blocks, radar frame, read-only fields |
| `Stroke.Subtle` | `#FFFFFF` @ 8% | 1px panel edges, dividers |
| `Stroke.Focus` | `#00D2D3` @ 60% | Keyboard focus ring |

### Brand accents
| Token | Hex | Use |
|---|---|---|
| `Accent.Brand` | `#E94560` | Logo, active tab underline, Start/Injecting state, panel headers |
| `Accent.Brand.Glow` | `#E94560` @ 40% | Soft box-shadow behind brand marks only — never data |
| `Accent.Teal` | `#00D2D3` | Secondary accent: links, focus, Passive mode, fresh-data timestamps |

Rule: **red = SkyWeave acting, teal = SkyWeave observing.** Brand red never collides with hazard red (below) — hazard red only ever appears inside data components.

### Data & severity colors (EFIS convention — sacred)
| Token | Hex | Meaning |
|---|---|---|
| `Data.Good` | `#2ECC71` | VFR, healthy source, nominal |
| `Data.Caution` | `#F39C12` | MVFR, stale data, moderate hazard |
| `Data.Warning` | `#E67E22` | IFR, degraded source |
| `Data.Critical` | `#FF4D4D` | LIFR, source failed, severe hazard |
| `Data.Info` | `#3498DB` | Informational (wind arrow, annotations) |
| `Data.Ice` | `#5DADE2` | Icing bands (cold blue, distinguishable from Info at a glance by context) |
| `Text.Primary` | `#EAEAEA` | Main values |
| `Text.Secondary` | `#9AA3B2` | Labels, units |
| `Text.Disabled` | `#555E6B` | Offline/disabled controls |

Flight category chips: VFR=`Data.Good`, MVFR=`Data.Caution`, IFR=`Data.Warning`, LIFR=`Data.Critical` — filled @ 20% opacity background, full-color text, 4px corner radius.

### Usage laws
1. Severity colors appear **only** on data (values, chips, gauges, health dots) — never on chrome (buttons, tabs, headers).
2. Brand accent appears **only** on chrome — never as a data color.
3. Max **one** saturated color per glass panel beyond severity chips; everything else stays neutral.
4. Contrast: body text ≥ 4.5:1 against its actual composited background; values ≥ 7:1.

---

## 3. Typography

| Role | Font | Size | Weight | Case |
|---|---|---|---|---|
| Brand mark | Inter/Segoe UI | 24 | Bold | "SkyWeave" exact case |
| Panel header | Inter/Segoe UI | 12 | SemiBold | UPPER, letter-spacing 0.5px, `Accent.Brand` |
| Primary value | Inter/Segoe UI | 26–34 | SemiBold | Tabular numerals (`FontFeature="tnum"`) |
| Secondary value | Inter/Segoe UI | 15 | Regular | Tabular numerals |
| Label / unit | Inter/Segoe UI | 11 | Regular | `Text.Secondary`; units after values, smaller (`°C`, `kt`, `ft`, `hPa`) |
| METAR/TAF raw | Cascadia Code / Consolas | 13 | Regular | `Bg.Inset` well, wrap, monochrome `Text.Primary` |
| Button | Inter/Segoe UI | 13 | SemiBold | Title case |

Laws: system font stack only (no bundled display fonts); tabular numerals everywhere numbers update live (no width jitter); never more than 3 sizes visible in one panel; no italics anywhere.

---

## 4. Layout Architecture

Window: default **1400×900**, min **1200×700**, `WindowStartupLocation="CenterScreen"`, `TransparencyLevelHint="AcrylicBlur"`.

```
┌──────────────────────────────────────────────────────────────────┐
│ HEADER (glass): brand · connection pulse · status · mode buttons │  64px
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  CONTENT GRID                                                    │
│  ┌──────────┬──────────┬──────────┬──────────────┐               │
│  │ Current  │ Winds    │ Clouds/  │ RIGHT RAIL   │               │
│  │ Cond.    │ Aloft    │ Storm    │ 300px fixed  │               │
│  ├──────────┴──────────┤ profile  │ Hazards      │               │
│  │ METAR/TAF brief     │          │ Source health│               │
│  │ + radar overlay     │          │ Settings     │               │
│  └─────────────────────┴──────────┴──────────────┘               │
├──────────────────────────────────────────────────────────────────┤
│ STATUS BAR (glass, 32px): mode · station · refresh countdown ·   │
│ sim connection · version                                         │
└──────────────────────────────────────────────────────────────────┘
```

- **Grid discipline:** 12px gutters, 12px outer margin, 8px corner radius on all glass panels. Panels are a uniform glass material — hierarchy by content order, not by different fills.
- **Right rail is fixed 300px** — it must never reflow; it's the pilot's "at-a-glance column."
- **Left/mid region is a 3×2 responsive grid**; panels stretch, they never wrap or stack on window resize down to min size.
- Panels fill their cells edge-to-edge — no nested card-in-card-in-card.

---

## 5. Components

### 5.1 Glass panel (`Classes="glassPanel"`)
Fill `Bg.Panel`, blur, 1px `Stroke.Subtle` edge, 8px radius, 12px inner padding, panel header per §3. On hover: fill animates to `Bg.Panel.Hover` over 150ms. That is the entire hover behavior — no scale, no glow.

### 5.2 Header
Left: `SkyWeave` wordmark with the soft brand glow + version chip. Center-right: connection **pulse dot** (breathing ellipse, 1.5s opacity cycle — green connected / amber connecting / red failed) + status text + last-update timestamp (teal when < 2 min old, secondary when older). Right: control cluster — `Connect`, `Start` (toggle, brand-filled when injecting), `Passive` (toggle, teal-filled when active), `Refresh`, `Stop`. Start and Passive are **mutually exclusive** — enabling one visually disables the other (50% opacity, not hidden).

### 5.3 Data rows (the workhorse)
Label left (`Text.Secondary`, 11px), value right (`Text.Primary`, tabular, right-aligned), severity tint when the value IS a severity (flight category, source health). Dense: 24–28px row height. Dividers: 1px `Stroke.Subtle` between rows only, never boxes.

### 5.4 Gauges & indices (icing, turbulence, storm intensity)
Horizontal **segmented bar**: 10 segments, 3px gap, rounded 2px. Filled segments use the severity ramp green→amber→red mapped to the 0–1 value; unfilled are `Stroke.Subtle`. Value label sits right of the bar, tabular. No circular gauges, no needles — a weather engine updates continuously and segmented bars read at a glance without motion noise.

### 5.5 Wind/altitude profile (winds aloft)
Vertical profile, altitude axis (ft, left, tabular), one wind row per level: **arrow glyph rotated to wind direction** (SVG path, `Data.Info`), speed + temp tabular right. The **aircraft's current altitude is a brand-red 1px horizontal line** with a tiny chevron at the axis — the single most important glance target in the app. Levels the aircraft is inside or about to cross get `Bg.Panel.Hover` row fill.

### 5.6 Cloud layer stack
Vertical column of layer bands positioned by true relative altitude (base→top to scale). Band fill = density (opacity 20→90%), texture by scattering: stratiform = flat fill, convective = subtle inner gradient. CB/TCU bands get a 1px `Data.Critical` top edge. Ceiling annotation (dashed line + "CEILING 2,500 ft") when BKN/OVC exists.

### 5.7 Radar overlay (FR-D4)
Full-width tile mosaic inside an `Bg.Inset` well, RainViewer reflectivity palette (transparent→green→yellow→red→magenta). Aircraft at center, 25/50/100 nm range rings (`Stroke.Subtle`), heading-up orientation, range selector chips (25/50/100/250 nm). Smooth crossfade on tile refresh — no flicker, no pop-in (tile-blend the same way weather blends).

### 5.8 METAR/TAF brief panel (FR-D3)
Raw text in the monospace inset well (colorless). Below: decoded chips — flight category chip, wind, vis, temp/dewp, cloud summary rows, and for TAF a **timeline strip**: time axis, colored blocks per forecast group (FM/BECMG/TEMPO), flight-category color per block. TEMPO blocks render at 60% opacity with a dashed top edge. "Observed vs forecast" divergence (e.g., METAR says IFR, TAF says VFR) gets a small amber dot on the timeline — no alarm, just a flag.

### 5.9 Hazards panel (SIGMET/AIRMET)
One row per hazard: severity dot (`Data.Caution`/`Data.Critical`), type tag (SIGMT/CNVT/AIRMET, 10px, uppercase, outlined), plain-language description, distance/bearing to affected area (tabular). Rows sort by proximity to route/aircraft. Empty state: single line "No active hazards within 500 nm" in `Text.Secondary` — never an empty panel, never a placeholder graphic.

### 5.10 Source health cluster
Per-source row: source name, green/amber/red dot, last-success age ("12 s", "4 m", "—"). Amber > 2× expected TTL, red = failed (NFR — surfaced failures, never silent). Dead sources stay visible and red — hiding a failure is a §agents.md violation in UI form.

### 5.11 Sliders & toggles (AS-style customization, FR-D2)
Slider: 4px track `Stroke.Subtle`, filled portion `Accent.Brand`, 14px circular thumb with `Stroke.Focus` ring on keyboard focus, value bubble (tabular) right of label — live-updating, no drag-end delay. Range always shown (e.g., "0.0–2.0×"). Toggle: 36×20px pill, off=`Stroke.Subtle` fill, on=`Accent.Brand`, 150ms thumb slide. Every control's effect must be **immediately visible** in the app — no Apply button, ever.

### 5.12 Status bar
Single 32px glass strip: mode badge (INJECTING brand-red pill / PASSIVE teal pill / IDLE neutral), station ICAO + name, next-refresh countdown ("refresh in 2:41"), sim connection state, version. It is the app's heartbeat — glanceable from across the room.

---

## 6. Interaction & Motion

| Rule | Spec |
|---|---|
| Response budget | Every click/toggle feels acknowledged in **< 100ms** (even if work is async — optimistic state + spinner) |
| Transitions | 150ms fills/hovers; 250ms panel content crossfades; easing `0.25, 0.1, 0.25, 1` |
| Data updates | Values **crossfade** (old fades down, new fades up, 250ms) — numbers never snap mid-glance |
| Live values | Update cadence ≤ 1 Hz in UI regardless of 5 Hz engine loop — readable, not strobing |
| No blocking | UI thread never awaits network (NFR-P3); busy panels dim 20% + top progress shimmer |
| Errors | Inline amber/red banner inside the affected panel with action link ("Retry", "Open log") — never a modal, never a silent swallow |
| Empty states | One line of `Text.Secondary` explaining what will appear here and when |
| First run | Preflight checklist card: connect sim → pick mode → start. Auto-dismisses on first successful injection |
| Window chrome | Standard OS chrome (min/max/close), Fluent. No custom titlebar in v1.0 |

---

## 7. UX Flows

**Primary loop (the product):** open SkyWeave → (auto)connect → glance: status bar mode, current conditions, profile altitude line → fly. The UI must answer in 3 seconds: *Is it live? What's it doing? Does it match the METAR?*

**Mode switching:** Start ⇄ Passive ⇄ Stop are one-click, take effect on next injection tick (≤5 s), and the header + status bar change color identity instantly so mode is **never ambiguous**.

**Trust flow:** the pilot verifies SkyWeave against the METAR. So the METAR panel is always exactly one glance from everywhere: raw text visible, decoded chips aligned with what the sim should show, and the observed-vs-forecast dot when they diverge. Never bury the ground truth.

**Failure flow:** any source fails → its health dot goes red, the affected panel shows the amber "degraded" banner naming the source and the fallback in use (NFR-R4). The rest of the app carries on, visually unbothered — graceful degradation must *look* graceful.

---

## 8. Accessibility & Quality Bar

- Keyboard: Tab order = visual order; every control reachable; visible `Stroke.Focus` ring; Enter activates.
- Screen-reader: every panel header and value is a named AutomationProperties element; status changes raise live-region announcements.
- Color is never the *only* signal — severity pairs with glyphs/labels (color-blind safe by construction).
- Text scaling to 125% without clipping (test on min window size).
- DPI: crisp on 150%/200% Windows scaling (SVG icons only, no stretched bitmaps).
- Performance: 60°C UI at idle, < 1% CPU — glass effects drop to solid fills automatically if compositor can't blur.

---

## 9. Do / Don't Cheat Sheet

| ✅ Do | ❌ Don't |
|---|---|
| Dark glass, one brand accent, EFIS severity colors | Rainbow accents, glow-everything |
| Tabular numbers, right-aligned values | Proportional fonts on live numbers |
| Crossfade data changes | Snap/blink value updates |
| Inline banners for errors | Modal error popups |
| Fixed right rail, disciplined grid | Reconfigurable/dockable panel chaos |
| 150ms micro-transitions everywhere | 1s cinematic animations |
| Empty states that teach | Blank panels, lorem placeholders |
| Red brand for action, red data for danger | Using brand red to display hazard values |

---

## 10. Acceptance — "does it feel premium?" checklist

A UI change ships only if all hold:
- [ ] New components use §2 tokens and §3 type scale — zero ad-hoc hex values in markup (tokens live in App.axaml resources)
- [ ] Glanceable: mode + METAR-match answerable in 3 s from any screen state
- [ ] All transitions within §6 budgets; nothing blinks or snaps
- [ ] Keyboard-complete; focus always visible
- [ ] Failure states designed, not defaulted (source-dead looks intentional)
- [ ] Manual smoke on min-size (1200×700) and 150% DPI — no clipping, no blur artifacts
- [ ] Screenshots before/after attached to the commit or session report

*When "premium" and "clear" ever conflict, **clear wins**. A pilot in IMC doesn't need pretty — they need true.*
