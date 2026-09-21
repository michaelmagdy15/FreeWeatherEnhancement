# SkyWeave

**Free, open-source real-weather injection engine for Microsoft Flight Simulator 2024**

SkyWeave generates real-world weather data and valid MSFS 2024 Weather Preset (WPR) XML. An experimental HTML/JS in-sim bridge is included, while the installed SDK's missing CommBus method means dynamic injection remains unproven on this machine. Free forever under the MIT license.

## Features

- **Region-optimal multi-model engine** - HRRR 3 km (CONUS), ICON-EU (Europe), GFS 0.11°/0.25° + ECMWF IFS (global), altitude-mapped via geopotential heights
- **Physics-based wake turbulence** - traffic vortex model + airport corridor mode
- **CAPE-driven thunderstorms** - CAPE/lifted-index storm intensity, lightning clustering, SIGMET fusion
- **24 cloud layers** synthesized from METAR and pressure-level cloud cover
- **Icing & turbulence calculation** - thermal, convective, mountain wave, jetstream CAT, in-cloud detection
- **METAR-observed ground-truth fusion** - current targets preserve observed temperature, pressure and surface wind at default settings; TAF remains briefing data and does not overwrite observations. Offline regression tests cover this path; full simulator agreement still requires live readback validation.
- **Winds aloft** from 19 pressure levels (Open-Meteo)
- **Monitor mode** - observes and displays real-world weather and sim-weather readback at the aircraft position without injecting
- **Weather transitions** - desktop scalar blending plus bridge cloud fades, wind-profile layer reconciliation, and shortest-arc wind/gust direction interpolation. Offline regression-tested; visual smoothness requires live MSFS validation.
- **REX Atmos CORE compatible** - we inject data, REX enhances visuals
- **Glassmorphic dashboard** with AS-style customization sliders/toggles, live radar mosaic, and TAF trend timeline (WPF + Wpf.Ui with native Windows 11 Mica backdrop)
- **Live data verification** - all fetchers validated against real endpoints; see tests/live-api-results.md
- **Backup data sources** - METAR/TAF fall back across AWC, NOAA tgftp, and VATSIM METAR proxies automatically
- **Cockpit Web EFB Companion & Local REST API** - mobile-first dark flight deck tablet PWA (`http://<ip>:54170` or `http://127.0.0.1:54170`) featuring live METAR & flight categories, wind compass rose, altimeter/QNH, live tactical radar canvas, winds aloft table, and active hazard alerts; plus REST endpoints (`/api/status`, `/api/efb`, `/health`, `/state`, `/metar`, `/hazards`)
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
