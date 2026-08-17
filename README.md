# SkyWeave

**Free, open-source real-weather injection engine for Microsoft Flight Simulator 2024**

SkyWeave injects real-world weather data into MSFS 2024 using SimConnect and Weather Preset (WPR) XML generation. Free forever under the MIT license.

## Features

- **Region-optimal multi-model engine** - HRRR 3 km (CONUS), ICON-EU (Europe), GFS 0.11°/0.25° + ECMWF IFS (global), altitude-mapped via geopotential heights
- **Physics-based wake turbulence** - traffic vortex model + airport corridor mode
- **CAPE-driven thunderstorms** - CAPE/lifted-index storm intensity, lightning clustering, SIGMET fusion
- **24 cloud layers** synthesized from METAR and pressure-level cloud cover
- **Icing & turbulence calculation** - thermal, convective, mountain wave, jetstream CAT, in-cloud detection
- **METAR-observed ground-truth fusion** - the sim matches the weather brief
- **Winds aloft** from 19 pressure levels (Open-Meteo)
- **Passive mode** - reads sim weather without injecting, for analysis
- **Smooth transitions** - 3-minute coast-then-ease blend, zero pop-in
- **REX Atmos CORE compatible** - we inject data, REX enhances visuals
- **Glassmorphic dashboard** with AS-style customization sliders/toggles (Avalonia UI)
- **Live data verification** - all fetchers validated against real endpoints; see tests/live-api-results.md

## Architecture

```
SkyWeave.Core        - Weather models, data fetchers, WPR generation, smoothing
SkyWeave.SimBridge   - MSFS 2024 SimConnect integration (requires MSFS SDK)
SkyWeave.App         - Avalonia desktop UI
```

### Weather Pipeline

```
METAR/TAF (aviationweather.gov) + Multi-model winds (HRRR / ICON-EU / GFS / ECMWF via Open-Meteo)
    → CloudLayerBuilder (24 layers, pressure-level cloud cover)
    → WindLayerBuilder (19 pressure levels, geopotential altitudes)
    → StormModeler (CAPE/lifted-index intensity) + WakeTurbulenceEngine
    → IcingCalculator + TurbulenceCalculator (CAT, mountain wave, in-cloud)
    → SmoothingPipeline (3-min coast-then-ease blend)
    → WprGenerator → MSFS via SimConnect
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

- **SkyWeave**: Injects weather DATA (clouds, wind, precipitation, turbulence) via SimConnect WPR XML
- **REX Atmos CORE**: Enhances visual RENDERING of weather (textures, shaders, atmospheric effects)

They work together: SkyWeave provides the weather engine, REX makes it look better. No conflicts.

## License

MIT License - Free forever. See [LICENSE](LICENSE) for details.
