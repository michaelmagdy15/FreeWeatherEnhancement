# SkyWeave

**Free, open-source real-weather injection engine for Microsoft Flight Simulator 2024**

SkyWeave generates real-world weather data and valid MSFS 2024 Weather Preset (WPR) XML. An experimental HTML/JS in-sim bridge is included, while the installed SDK's missing CommBus method means dynamic injection remains unproven on this machine. Free forever under the MIT license.

## Features

- **Region-optimal multi-model engine** - HRRR 3 km (CONUS), ICON-EU (Europe), GFS 0.11°/0.25° + ECMWF IFS (global), altitude-mapped via geopotential heights
- **Physics-based wake turbulence** - traffic vortex model + airport corridor mode
- **CAPE-driven thunderstorms** - CAPE/lifted-index storm intensity, lightning clustering, SIGMET fusion
- **24 cloud layers** synthesized from METAR and pressure-level cloud cover
- **Station-aware layer heights** - observed cloud bases and surface wind are anchored to reporting-station elevation; MSL heights stay consistent through WPR and bridge payloads. Offline tests cover elevated airports; in-sim validation is pending.
- **Surface fog deck synthesis** - automatically synthesizes a dense ground-level stratus deck at station elevation when METAR reports FG/FZFG or visibility <= 1600m (1 SM), giving MSFS 2024 genuine volumetric IMC runway fog
- **Aircraft cloud anchor prioritization** - prioritizes the cloud deck enclosing or closest to the aircraft's altitude into MSFS's primary ~3 volumetric rendering slots so clouds never disappear while flying through them
- **Icing & turbulence calculation** - thermal, convective, mountain wave, jetstream CAT, in-cloud detection
- **METAR-observed ground-truth fusion** - current targets preserve observed temperature, pressure and surface wind at default settings; TAF remains briefing data and does not overwrite observations. Offline regression tests cover this path; full simulator agreement still requires live readback validation.
- **Winds aloft** from 19 pressure levels (Open-Meteo) with full vertical and temporal layer interpolation
- **Wind slew rate limiting** - clamps wind speed changes to max 5.0 kt/s and direction shifts to max 7.5 deg/s across the shortest circular arc, eliminating the airliner autopilot disconnect ("plane-flip bug") during sudden cruise wind shifts
- **Boundary-layer gust tapering** - tapers gusts between 3,000 ft and 10,000 ft MSL and suppresses them in cruise and calm air (<5 kt) to stop erratic aircraft yaw hunting, while strictly preserving the station surface wind anchor
- **Synoptic Weather Map (Isobars & Wind Barbs)** - dynamic mean sea level pressure (MSLP) isobar contours at standard 4-hPa intervals (e.g., 996, 1004, 1016, 1024 hPa), labeled High ("H") and Low ("L") pressure system badges, and SVG vector wind barbs (calm rings, 5kt half-barbs, 10kt barbs, 50kt pennants) rendered across the WPF radar mosaic and Web EFB
- **Vertical Atmospheric Sounding & Skew-T Profile** - high-fidelity atmospheric cross-section (Surface to FL450) rendering temperature lapse rate curves, dewpoint curves, 0°C freezing level line, aircraft altitude indicator, volumetric cloud decks with opacity/coverage, and icing/turbulence hazard bands in both graphical Skew-T and tabular flight-level formats
- **SimBrief Flight Plan Corridor & Route Briefing (FR-F3)** - interactive SimBrief OFP flight plan import, route corridor summary, en-route waypoint weather aloft, and one-click FMC wind uplink exports (PMDG `.wx`, Fenix A320 JSON, standard CSV)
- **Pilot Units & Customization** - full pilot customization for altimeter (inHg / hPa), temperature (°C / °F), wind speed (kt / m/s), live UTC/Zulu clock (`HH:mm:ss Z`), and Streamer Mode
- **Atmospheric Freeze & Sky Anchor Corridors** - intelligent flight phase stability:
  - *Climb-Out Hold*: locks departure airport METAR surface parameters up through 4,000 ft AGL
  - *Arrival Hold*: smoothly transitions to destination airport METAR within 30 NM of destination
  - *Final Freeze*: auto-freezes weather within 5 NM and <= 1,000 ft AGL on short final to guarantee zero wind jumps during flare and touchdown
  - *Manual Weather Freeze*: top-bar one-click toggle in desktop app, in-sim glass panel, and Web EFB to hold live atmosphere constant on demand
- **FMC Winds Aloft Exporter** - generates PMDG 737/777 FMC wind uplink text files (`<ORIGIN><DEST>01.wx`), Fenix A320 AOC/ACARS JSON, and navigation CSV files from SimBrief flight plans
- **Monitor mode** - observes and displays real-world weather and sim-weather readback at the aircraft position without injecting. Readback supports wind, temperature, pressure and visibility; sky cloud coverage is unavailable through SimConnect, so matching these fields is only partial verification.
- **Weather transitions** - desktop scalar blending plus bridge cloud fades, wind-profile layer reconciliation, and shortest-arc wind/gust direction interpolation. Offline regression-tested; visual smoothness requires live MSFS validation.
- **REX Atmos CORE compatible** - we inject data, REX enhances visuals
- **Glassmorphic dashboard** with AS-style customization sliders/toggles, live radar mosaic with synoptic layer toggles, vertical sounding drawer, and TAF trend timeline (WPF + Wpf.Ui with native Windows 11 Mica backdrop)
- **Live data verification** - all fetchers validated against real endpoints; see tests/live-api-results.md
- **Backup data sources** - METAR/TAF fall back across AWC, NOAA tgftp, and VATSIM METAR proxies automatically
- **Cockpit Web EFB Companion & Local REST API** - starts automatically with SkyWeave.App and shares its live aircraft weather; mobile-first dark flight deck tablet PWA (`http://<ip>:54170` or `http://127.0.0.1:54170`) featuring live METAR & flight categories, wind compass rose, altimeter/QNH, live tactical radar canvas with synoptic isobar/wind overlays, vertical Skew-T sounding profile canvas, SimBrief OFP briefing & FMC downloads, and active hazard alerts; plus REST endpoints (`/api/status`, `/api/efb`, `/api/sounding`, `/api/synoptic`, `/api/simbrief`, `/api/fmc/export`, `/health`, `/state`, `/metar`, `/hazards`)
- **Experimental in-sim bridge** - sends versioned weather commands over SimConnect CommBus to the MSFS `JS_LISTENER_WEATHER` `UpdateTempWeatherPreset` method; requires a compatible MSFS SDK and loaded HTML/JS panel

## Architecture

```
SkyWeave.Core        - Weather models, data fetchers, WPR generation, smoothing
SkyWeave.SimBridge   - MSFS 2024 SimConnect integration (requires MSFS SDK)
SkyWeave.App         - WPF desktop UI with Wpf.Ui (native Windows 11 Mica & Fluent Design)
SkyWeave.Api         - Cockpit Web EFB Companion PWA & Local REST API (:54170)
```

### Weather Pipeline

```
METAR/TAF (aviationweather.gov → NOAA tgftp → VATSIM) + Multi-model winds (HRRR / ICON-EU / GFS / ECMWF via Open-Meteo)
    → CloudLayerBuilder (24 layers, pressure-level cloud cover)
    → WindLayerBuilder (19 pressure levels, geopotential altitudes)
    → StormModeler (CAPE/lifted-index intensity) + WakeTurbulenceEngine
    → IcingCalculator + TurbulenceCalculator (CAT, mountain wave, in-cloud)
    → SmoothingPipeline (3-min coast-then-ease blend)
     → WprGenerator → CommBus HTML/JS bridge (experimental) → WPR preset file fallback
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [MSFS 2024 SDK](https://www.flightsimulator.com/developers/) (for SimConnect integration)
- MSFS 2024 running

## Build

```bash
git clone https://github.com/yourusername/skyweave.git
cd skyweave
dotnet build
```

## Run

```bash
dotnet run --project src/SkyWeave.App
```

## Data Sources

| Source | Data | Auth |
|--------|------|------|
| [aviationweather.gov](https://aviationweather.gov/api/data) | METAR, TAF, SIGMETs | None |
| [NOAA tgftp](https://tgftp.nws.noaa.gov) | METAR/TAF text backup (global) | None |
| [VATSIM METAR](https://metar.vatsim.net) | METAR text backup (community proxy) | None |
| [Open-Meteo](https://open-meteo.com) — GFS 0.11°/0.25° + ECMWF IFS | Global winds aloft, temperature, pressure levels | None |
| Open-Meteo — HRRR (3 km) | CONUS high-resolution winds, hourly refresh | None |
| Open-Meteo — ICON-EU | European winds (~13 km) | None |
| Open-Meteo | CAPE, lifted index, freezing level height, cloud cover at levels | None |
| [Blitzortung](https://www.blitzortung.org) | Lightning strikes | Community registration |
| [RainViewer](https://www.rainviewer.com) | Weather radar | None |

Every data source is free and requires no API key - no accounts, no license servers.

## Live Data Verification

Every fetcher is validated against its live endpoint on each release pass - per-source results and timestamps are in [tests/live-api-results.md](tests/live-api-results.md).

## REX Atmos CORE Compatibility

SkyWeave is **complementary** to REX Atmos CORE:

- **SkyWeave**: Generates weather DATA and WPR XML; experimental dynamic injection uses an in-sim HTML/JS bridge, with readback verification
- **REX Atmos CORE**: Enhances visual RENDERING of weather (textures, shaders, atmospheric effects)

They work together: SkyWeave provides the weather engine, REX makes it look better. No conflicts.

## License

MIT License - Free forever. See [LICENSE](LICENSE) for details.
