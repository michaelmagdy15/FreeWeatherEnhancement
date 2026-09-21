# FreeWeatherEnhancement — Build Plan

## Project Name
**SkyWeave** — An open-source real-weather injection engine for MSFS 2024

## Overview
A free, community-built alternative to commercial weather addons for Microsoft Flight Simulator 2024. Reads real-world weather and builds a full 3-D atmosphere around the aircraft with best-in-class convective/thunderstorm modeling.

---

## Architecture Decision: Out-of-Process SimConnect (Not WASM)

**Decision**: Build as an out-of-process C#/.NET desktop application using SimConnect managed SDK.

**Rationale** (from MSFS 2024 SDK docs):
- MSFS 2024 recommends out-of-process apps over WASM for stability — if the app crashes, it won't crash the sim
- Out-of-process supports managed code (.NET) with rich object support and UI capabilities
- SimConnect managed wrapper is available in `$(MSFS SDK)\SimConnect SDK\lib\managed`
- WASM modules are more limited (no Windows API, no C++ exceptions, no threads)

### Weather Data vs. Visual Rendering

MSFS 2024 weather has two layers:
1. **Meteorological data** (our responsibility): wind, temperature, pressure, clouds, precipitation
2. **Visual rendering** (REX Atmos CORE's responsibility): Rayleigh scattering, cloud colors, lighting, haze

We inject #1. REX Atmos CORE enhances #2. Both can coexist because they touch different SimVars and engine hooks.

---

## Solution Structure

```
FreeWeatherEnhancement/
├── FreeWeatherEnhancement.sln
├── src/
│   ├── SkyWeave.Core/              # Weather modeling library
│   │   ├── SkyWeave.Core.csproj
│   │   ├── Models/
│   │   │   ├── WeatherState.cs     # Full atmospheric state
│   │   │   ├── CloudLayer.cs       # Cloud deck definition
│   │   │   ├── WindLayer.cs        # Wind at altitude
│   │   │   ├── IcingLayer.cs       # Icing conditions
│   │   │   ├── TurbulenceLayer.cs  # Turbulence conditions
│   │   │   ├── WeatherHazard.cs    # SIGMET/AIRMET/PIREP
│   │   │   ├── StormCell.cs        # Thunderstorm cell model
│   │   │   └── LightningStrike.cs  # Blitzortung strike
│   │   ├── Fetchers/
│   │   │   ├── MetarFetcher.cs     # NOAA Aviation Weather API
│   │   │   ├── TafFetcher.cs       # Terminal Aerodrome Forecast
│   │   │   ├── WindsAloftFetcher.cs # Open-Meteo pressure levels
│   │   │   ├── SigmetFetcher.cs    # SIGMET/AIRMET data
│   │   │   ├── RadarFetcher.cs     # MRMS/RainViewer radar data
│   │   │   └── LightningFetcher.cs # Blitzortung strike data
│   │   ├── Decoders/
│   │   │   ├── MetarDecoder.cs     # Decode raw/JSON METAR
│   │   │   ├── TafDecoder.cs       # Decode raw/JSON TAF
│   │   │   └── SigmetDecoder.cs    # Decode SIGMET/AIRMET
│   │   ├── Builders/
│   │   │   ├── CloudLayerBuilder.cs # Build 24 cloud layers
│   │   │   ├── WindLayerBuilder.cs  # Build wind profile
│   │   │   ├── IcingCalculator.cs   # Calculate icing bands
│   │   │   ├── TurbulenceCalculator.cs # Calculate turbulence
│   │   │   └── StormModeler.cs      # Thunderstorm cell logic
│   │   ├── Injectors/
│   │   │   ├── WprGenerator.cs     # Generate WPR XML
│   │   │   └── SimVarInjector.cs   # Inject SimVars
│   │   ├── Services/
│   │   │   ├── WeatherEngine.cs    # Main orchestration
│   │   │   ├── SmoothingPipeline.cs # Transition blending
│   │   │   └── StationFinder.cs    # Nearest station lookup
│   │   └── Caching/
│   │       └── WeatherCache.cs     # In-memory + file cache
│   │
│   ├── SkyWeave.SimBridge/         # SimConnect integration
│   │   ├── SkyWeave.SimBridge.csproj
│   │   ├── SimConnectManager.cs    # Connection lifecycle
│   │   ├── AircraftPosition.cs     # Read lat/lon/alt
│   │   ├── WeatherInjector.cs      # Push WeatherState to sim
│   │   └── WeatherSimVars.cs       # SimVar definitions
│   │
│   └── SkyWeave.App/               # WPF + Wpf.Ui desktop UI (Windows 11 Fluent & Mica)
│       ├── SkyWeave.App.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── Converters.cs
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── WeatherDisplayViewModel.cs
│       │   ├── SettingsViewModel.cs
│       │   └── SharedViewModels.cs
│       └── Views/
│           ├── MainWindow.xaml
│           └── MainWindow.xaml.cs
│
├── tests/
│   └── SkyWeave.Core.Tests/
│       ├── MetarDecoderTests.cs
│       └── SmoothingPipelineTests.cs
│
└── README.md
```

---

## Data Sources & APIs

### 1. METAR/TAF (Aviation Weather Center)
- **Endpoint**: `https://aviationweather.gov/api/data/metar?ids={ICAO}&format=json`
- **Endpoint**: `https://aviationweather.gov/api/data/taf?ids={ICAO}&format=json`
- **Auth**: None required
- **Rate limit**: 100 requests/minute
- **Key fields**: icaoId, temperature, dewpoint, windDir, windSpeed, windGust, visibility, clouds[], flightCategory

### 2. Winds Aloft (Open-Meteo)
- **Endpoint**: `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&hourly=temperature_1000hPa,temperature_975hPa,...,wind_speed_1000hPa,...,wind_direction_1000hPa,...&pressure_level=1000,975,950,925,900,850,800,700,600,500,400,300,250,200,150,100`
- **Auth**: None required
- **Rate limit**: Generous (10,000/day)
- **Key fields**: temperature at pressure levels, wind_speed at pressure levels, wind_direction at pressure levels, geopotential_height

### 3. SIGMET/AIRMET (Aviation Weather Center)
- **Endpoint**: `https://aviationweather.gov/api/data/sigmet?format=json`
- **Endpoint**: `https://aviationweather.gov/api/data/airsigmet?format=json`
- **Auth**: None required

### 4. Lightning (Blitzortung)
- **WebSocket**: `ws://ws1.blitzortung.org`
- **HTTP**: `https://data.blitzortung.org/Data/Protected/last_strikes.php`
- **Auth**: Community registration required for full access
- **Note**: Commercial use prohibited; attribution required

### 5. Airport Database (OurAirports)
- **Source**: https://ourairports.com/data/airports.csv (public domain)
- **Usage**: Offline lookup for nearest station to aircraft position

---

## WeatherState Model

```csharp
public class WeatherState
{
    public DateTime ObservationTime { get; set; }
    public string StationId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    // Surface conditions
    public double TemperatureCelsius { get; set; }
    public double DewpointCelsius { get; set; }
    public double PressureHpa { get; set; }
    public double AltimeterHpa { get; set; }
    public double VisibilityMeters { get; set; }
    public string FlightCategory { get; set; } // VFR, MVFR, IFR, LIFR
    
    // Surface wind
    public double WindDirectionDegrees { get; set; }
    public double WindSpeedKnots { get; set; }
    public double? WindGustKnots { get; set; }
    public double? GustDirectionDegrees { get; set; }
    
    // Cloud layers (up to 24)
    public List<CloudLayer> CloudLayers { get; set; } = new();
    
    // Winds aloft profile (keyed by altitude in feet)
    public List<WindLayer> WindsAloft { get; set; } = new();
    
    // Hazards
    public List<WeatherHazard> Hazards { get; set; } = new();
    
    // Thunderstorm cells
    public List<StormCell> StormCells { get; set; } = new();
    
    // Precipitation
    public PrecipitationType Precipitation { get; set; }
    public double PrecipitationRate { get; set; } // mm/hr
    
    // Atmospheric conditions
    public double HumidityPercent { get; set; }
    public double FreezingLevelFeet { get; set; }
    public double CeilingFeet { get; set; }
    
    // Icing/Turbulence indices (0-1 scale)
    public double IcingIndex { get; set; }
    public double TurbulenceIndex { get; set; }
    public List<IcingLayer> IcingLayers { get; set; } = new();
    public List<TurbulenceLayer> TurbulenceLayers { get; set; } = new();
    
    // Thunderstorm intensity (0-1)
    public double ThunderstormIntensity { get; set; }
    
    // Aerosol/haze
    public double AerosolDensity { get; set; } // 0-1
}

public class CloudLayer
{
    public int Id { get; set; } // 1-24
    public double BaseMeters { get; set; }
    public double TopMeters { get; set; }
    public double BaseFeetAgl { get; set; }
    public double TopFeetAgl { get; set; }
    public CloudType Type { get; set; } // FEW, SCT, BKN, OVC, CB, TCU
    public double Density { get; set; } // 0.0 - 1.0 (opacity)
    public double Scattering { get; set; } // 0.0 (stratiform) - 1.0 (convective)
    public double CoveragePercent { get; set; } // 0.0 - 1.0
    
    // For thunderstorm layers
    public bool IsConvective { get; set; }
    public double? ThunderstormIntensity { get; set; }
}

public class WindLayer
{
    public int Id { get; set; }
    public double AltitudeMeters { get; set; }
    public double AltitudeFeet { get; set; }
    public double DirectionDegrees { get; set; }
    public double SpeedKnots { get; set; }
    public double? GustSpeedKnots { get; set; }
    public double? GustDirectionDegrees { get; set; }
    public double TemperatureCelsius { get; set; }
    public double? TurbulenceIntensity { get; set; } // 0-1
}

public class IcingLayer
{
    public double BaseFeet { get; set; }
    public double TopFeet { get; set; }
    public IcingSeverity Severity { get; set; } // None, Light, Moderate, Severe, Extreme
    public double TemperatureCelsius { get; set; }
    public double CloudDensity { get; set; }
}

public class TurbulenceLayer
{
    public double BaseFeet { get; set; }
    public double TopFeet { get; set; }
    public TurbulenceIntensity Intensity { get; set; } // None, Light, Moderate, Severe, Extreme
    public TurbulenceType Type { get; set; } // Thermal, Convective, Mechanical, MountainWave
}

public class StormCell
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeFeet { get; set; }
    public double MotionDirectionDegrees { get; set; }
    public double MotionSpeedKnots { get; set; }
    public double Intensity { get; set; } // 0-1
    public double RadiusNm { get; set; }
    public CellType Type { get; set; } // Core, Anvil, Embedded
    public List<LightningStrike> NearbyStrikes { get; set; } = new();
}

public class LightningStrike
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public double DistanceNm { get; set; } // from aircraft
}

public enum CloudType { FEW, SCT, BKN, OVC, CB, TCU, ST, NS }
public enum PrecipitationType { None, Rain, Snow, Drizzle, Fog, Mist, IcePellets, FreezingRain }
public enum IcingSeverity { None, Light, Moderate, Severe, Extreme }
public enum TurbulenceIntensity { None, Light, Moderate, Severe, Extreme }
public enum TurbulenceType { Thermal, Convective, Mechanical, MountainWave, Wake }
public enum CellType { Core, Anvil, Embedded, MultiCell }
```

---

## SimConnect Integration

### Reading Aircraft Position
```csharp
// Define data structure
simconnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
    "Plane Latitude", "degrees",
    SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
simconnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
    "Plane Longitude", "degrees",
    SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
simconnect.AddToDataDefinition(DEFINITIONS.AircraftPosition,
    "Plane Altitude", "feet",
    SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);

// Request periodic updates
simconnect.RequestDataOnSimObject(
    REQUESTS.AircraftPosition,
    DEFINITIONS.AircraftPosition,
    SIMCONNECT_OBJECT_ID_USER,
    SIMCONNECT_PERIOD.SECOND);
```

### Weather Injection Method

MSFS 2024 weather injection uses **two approaches**:

#### 1. SimConnect SimVars (Readback Only)
```csharp
// Read surface weather for verification/passive mode. These SimVars are read-only.
simconnect.AddToDataDefinition(DEFINITIONS.WeatherState,
    "AMBIENT WIND DIRECTION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
simconnect.AddToDataDefinition(DEFINITIONS.WeatherState,
    "AMBIENT WIND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
simconnect.AddToDataDefinition(DEFINITIONS.WeatherState,
    "AMBIENT TEMPERATURE", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
simconnect.AddToDataDefinition(DEFINITIONS.WeatherState,
    "SEA LEVEL PRESSURE", "millibars", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
```

#### 2. Weather Preset XML (Cloud Layers + Wind Layers + Thunderstorms)
Generate WPR XML for the fallback and send a versioned payload over the documented SimConnect CommBus to the optional HTML/JS bridge. The bridge registers `JS_LISTENER_WEATHER` and calls its work-in-progress `UpdateTempWeatherPreset` method inside Coherent:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<SimBase.Document Type="WeatherPreset" version="1,3">
    <Descr>AceXML Document</Descr>
    <WeatherPreset.Preset>
        <Name>SkyWeave</Name>
        <IsAltitudeAMGL>False</IsAltitudeAMGL>
        <MSLPressure Value="101325" Unit="pa" />
        <MSLTemperature Value="293.15" Unit="k" />
        <SnowCover Value="0" Unit="m" />
        
        <!-- Surface conditions -->
        <AerosolDensity Value="0.200" Unit="density factor" />
        <Precipitations Value="5.000" Unit="mm/h" />
        <ThunderstormIntensity Value="0.300" Unit="(0 - 1)" />
        
        <!-- Cloud Layer 1: Low stratus -->
        <CloudLayer>
            <CloudLayerDensity Value="0.800" Unit="(0 - 1)" />
            <CloudLayerAltitudeBot Value="500" Unit="m" />
            <CloudLayerAltitudeTop Value="1500" Unit="m" />
            <CloudLayerScattering Value="0.100" Unit="(0 - 1)" />
        </CloudLayer>
        
        <!-- Cloud Layer 2: Mid-level altostratus -->
        <CloudLayer>
            <CloudLayerDensity Value="0.600" Unit="(0 - 1)" />
            <CloudLayerAltitudeBot Value="3000" Unit="m" />
            <CloudLayerAltitudeTop Value="5000" Unit="m" />
            <CloudLayerScattering Value="0.200" Unit="(0 - 1)" />
        </CloudLayer>
        
        <!-- Cloud Layer 3: Cumulonimbus (thunderstorm) -->
        <CloudLayer>
            <CloudLayerDensity Value="0.950" Unit="(0 - 1)" />
            <CloudLayerAltitudeBot Value="1000" Unit="m" />
            <CloudLayerAltitudeTop Value="12000" Unit="m" />
            <CloudLayerScattering Value="0.900" Unit="(0 - 1)" />
        </CloudLayer>
        
        <!-- ... up to 24 layers ... -->
        
        <!-- Wind Layer 1: Surface -->
        <WindLayer>
            <WindLayerAltitude Value="0" Unit="m" />
            <WindLayerAngle Value="270" Unit="degrees" />
            <WindLayerSpeed Value="15" Unit="knts" />
            <GustWave>
                <GustWaveDuration Value="2" Unit="sec" />
                <GustWaveInterval Value="10" Unit="sec" />
                <GustWaveSpeed Value="25" Unit="knts" />
                <GustAngle Value="280" Unit="degrees" />
            </GustWave>
        </WindLayer>
        
        <!-- Wind Layer 2: 3000ft -->
        <WindLayer>
            <WindLayerAltitude Value="914" Unit="m" />
            <WindLayerAngle Value="250" Unit="degrees" />
            <WindLayerSpeed Value="35" Unit="knts" />
        </WindLayer>
        
        <!-- ... more wind layers ... -->
        
    </WeatherPreset.Preset>
</SimBase.Document>
```

### WPR Generation Pipeline

```
WeatherState
    ↓
WprGenerator
    ├─ Convert cloud layers to <CloudLayer> elements
    │   ├─ BaseFeet → meters (× 0.3048)
    │   ├─ TopFeet → meters
    │   ├─ Density from cloud type/coverage
    │   └─ Scattering from cloud type
    ├─ Convert wind layers to <WindLayer> elements
    │   ├─ AltitudeFeet → meters
    │   ├─ Direction, Speed, Gusts
    │   └─ Turbulence intensity (if available)
    ├─ Set thunderstorm intensity
    ├─ Set precipitation rate
    └─ Set aerosol density (haze/fog)
    ↓
WPR XML string
     ↓
CommBus TO_JS: SkyWeave.Weather.Apply
     ↓
HTML/JS RegisterWeatherListener().updateTempWeatherPreset(weatherPreset)
     ↓
SimVar readback verification
```

### WPR Generator Class

```csharp
public class WprGenerator
{
    private const double FEET_TO_METERS = 0.3048;
    
    public string GenerateWprXml(WeatherState state)
    {
        var xml = new XDocument(
            new XElement("SimBase.Document",
                new XAttribute("Type", "WeatherPreset"),
                new XAttribute("version", "1,3"),
                new XElement("Descr", "SkyWeave Live Weather"),
                new XElement("WeatherPreset.Preset",
                    new XElement("Name", "SkyWeave Live"),
                    new XElement("IsAltitudeAMGL", "False"),
                    
                    // Surface conditions
                    new XElement("AerosolDensity",
                        new XAttribute("Value", state.AerosolDensity.ToString("F3")),
                        new XAttribute("Unit", "density factor")),
                    new XElement("Precipitations",
                        new XAttribute("Value", state.PrecipitationRate.ToString("F3")),
                        new XAttribute("Unit", "mm/h")),
                    new XElement("ThunderstormIntensity",
                        new XAttribute("Value", state.ThunderstormIntensity.ToString("F3")),
                        new XAttribute("Unit", "(0 - 1)")),
                    
                    // Cloud layers
                    state.CloudLayers.Select(cl => GenerateCloudLayer(cl)),
                    
                    // Wind layers
                    state.WindsAloft.Select(wl => GenerateWindLayer(wl))
                )
            )
        );
        
        return xml.ToString();
    }
    
    private XElement GenerateCloudLayer(CloudLayer layer)
    {
        return new XElement("CloudLayer",
            new XElement("CloudLayerDensity",
                new XAttribute("Value", layer.Density.ToString("F3")),
                new XAttribute("Unit", "(0 - 1)")),
            new XElement("CloudLayerAltitudeBot",
                new XAttribute("Value", layer.BaseMeters.ToString("F0")),
                new XAttribute("Unit", "m")),
            new XElement("CloudLayerAltitudeTop",
                new XAttribute("Value", layer.TopMeters.ToString("F0")),
                new XAttribute("Unit", "m")),
            new XElement("CloudLayerScattering",
                new XAttribute("Value", layer.Scattering.ToString("F3")),
                new XAttribute("Unit", "(0 - 1)"))
        );
    }
    
    private XElement GenerateWindLayer(WindLayer layer)
    {
        var windLayer = new XElement("WindLayer",
            new XElement("WindLayerAltitude",
                new XAttribute("Value", layer.AltitudeMeters.ToString("F0")),
                new XAttribute("Unit", "m")),
            new XElement("WindLayerAngle",
                new XAttribute("Value", layer.DirectionDegrees.ToString("F0")),
                new XAttribute("Unit", "degrees")),
            new XElement("WindLayerSpeed",
                new XAttribute("Value", layer.SpeedKnots.ToString("F0")),
                new XAttribute("Unit", "knts"))
        );
        
        // Add gusts if present
        if (layer.GustSpeedKnots.HasValue)
        {
            windLayer.Add(new XElement("WindLayerGusts",
                new XElement("WindLayerGustSpeed",
                    new XAttribute("Value", layer.GustSpeedKnots.Value.ToString("F0")),
                    new XAttribute("Unit", "knts")),
                new XElement("WindLayerGustAngle",
                    new XAttribute("Value", layer.GustDirectionDegrees?.ToString("F0") ?? layer.DirectionDegrees.ToString("F0")),
                    new XAttribute("Unit", "degrees"))
            ));
        }
        
        return windLayer;
    }
}
```

---

## Smoothing Pipeline

When a new WeatherState is fetched, don't snap — ease values over configurable duration:

```csharp
public class SmoothingPipeline
{
    private WeatherState _current;
    private WeatherState _target;
    private DateTime _transitionStart;
    private TimeSpan _transitionDuration = TimeSpan.FromMinutes(3);
    
    public WeatherState GetInterpolatedState()
    {
        var elapsed = DateTime.UtcNow - _transitionStart;
        var t = Math.Min(1.0, elapsed.TotalSeconds / _transitionDuration.TotalSeconds);
        
        // Ease function (smooth step)
        t = t * t * (3 - 2 * t);
        
        return Interpolate(_current, _target, t);
    }
}
```

Update at ~5Hz (200ms interval) and apply to sim.

---

## MVP Scope (v0.1)

1. ✅ Solution scaffold with 3 projects
2. ✅ WeatherState model (full cloud/wind/icing layers)
3. ✅ METAR fetch + decode from aviationweather.gov
4. ✅ SimConnect connection + aircraft position read
5. ✅ Basic weather injection (wind, temperature, pressure)
6. ✅ Simple Avalonia dashboard showing decoded METAR
7. ✅ Smoothing pipeline (3-min blend)
8. ✅ README with build instructions

### v0.2 — Full Cloud & Wind Layers
- ✅ Winds aloft from Open-Meteo (pressure levels)
- ✅ Multi-layer cloud injection (up to 24 layers)
- ✅ WPR XML generation and preset-file output; dynamic injection bridge blocked by MSFS 2024 API behavior
- ✅ Icing layer calculation and display
- ✅ Turbulence layer calculation
- ✅ Region-aware multi-model winds — HRRR (CONUS, 3 km), ICON-EU (Europe), GFS 0.11°/0.25° + ECMWF IFS (global), altitude-mapped via geopotential heights
- ✅ Pressure-level cloud cover synthesis, CAPE, lifted index, and freezing level height
- ✅ METAR-observed ground-truth fusion (observed conditions weighted over model output)

### v0.3 — Thunderstorm Engine
- ✅ Convective SIGMET parsing
- ✅ Radar reflectivity integration (MRMS/RainViewer)
- ✅ Storm cell modeling (core, anvil, embedded)
- ✅ Cell motion advection
- ✅ Lightning strike display (Blitzortung)
- ✅ Thunderstorm intensity modulation
- ✅ CAPE / lifted-index-driven storm intensity scaling
- ✅ Physics-based wake turbulence engine (traffic vortex model + airport corridor mode)
- ✅ Mountain wave + jetstream CAT turbulence

### v0.4 — Hazards & Advisories
- SIGMET/AIRMET/PIREP panel
- Icing severity bands
- Turbulence intensity display
- Freezing level tracking
- Convective risk assessment

### v0.5+ — Advanced Features
- Historical/ERA5 mode
- Sandbox mode
- Hybrid mode
- SimBrief integration
- Navigraph integration

---

## Build Steps

### Step 1: Install prerequisites
- .NET 8+ SDK (already have .NET 9 installed)
- Avalonia templates: `dotnet new install Avalonia.Templates`
- MSFS 2024 SDK (download from https://docs.flightsimulator.com/msfs2024/html/5_Getting_Started/SDK.htm)
  - Install path: `C:\MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`
  - Also need: `C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll` (native, for runtime)

### Step 2: Create solution
```powershell
cd C:\Users\Mi5a\FreeWeatherEnhancement
dotnet new sln -n SkyWeave
```

### Step 3: Create projects
```powershell
# Core library
dotnet new classlib -n SkyWeave.Core -o src/SkyWeave.Core
dotnet sln add src/SkyWeave.Core/SkyWeave.Core.csproj

# SimBridge (SimConnect)
dotnet new classlib -n SkyWeave.SimBridge -o src/SkyWeave.SimBridge
dotnet sln add src/SkyWeave.SimBridge/SkyWeave.SimBridge.csproj

# App (Avalonia UI)
dotnet new avalonia.mvvm -n SkyWeave.App -o src/SkyWeave.App
dotnet sln add src/SkyWeave.App/SkyWeave.App.csproj
```

### Step 4: Add NuGet packages
```powershell
# Core
dotnet add src/SkyWeave.Core/SkyWeave.Core.csproj package System.Text.Json
dotnet add src/SkyWeave.Core/SkyWeave.Core.csproj package System.Net.Http.Json

# SimBridge (SimConnect managed wrapper - reference local DLL)
# Reference: $(MSFS2024_SDK)\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll

# App
dotnet add src/SkyWeave.App/SkyWeave.App.csproj package CommunityToolkit.Mvvm
```

### Step 5: Implement Core
1. WeatherState models
2. MetarFetcher + MetarDecoder
3. StationFinder (nearest ICAO from lat/lon)
4. WeatherCache

### Step 6: Implement SimBridge
1. SimConnectManager (connection lifecycle)
2. AircraftPosition (read lat/lon/alt)
3. WeatherInjector (push WeatherState)

### Step 7: Implement App
1. MainWindow with dashboard
2. DashboardViewModel (refresh loop)
3. Connect to SimBridge + Core

### Step 8: Smoothing
1. SmoothingPipeline with configurable blend duration
2. 5Hz update loop

### Step 9: README
- Project description (weather DATA engine, not visual renderer)
- Build instructions
- Data source credits (NOAA, Open-Meteo)
- Compatible addons section (REX Atmos CORE, ActiveSky FS)
- License (MIT)

---

## WIN STRATEGY — Beat Active Sky & StrataWx

Two paid products define the bar: **Active Sky FS** (HiFi, €24.99+VAT, closed source, online account required) and **StrataWx** (Strata Designs, $29.99 one-time, v0.1.x). StrataWx is the closer threat — same platform, same free data sources. ASFS is the feature benchmark — its Active Air Effects list is our north star. We win on accuracy where StrataWx is weak, features where StrataWx has none, and price everywhere.

### Competitive Table

| Feature | Active Sky FS (€24.99+VAT) | StrataWx ($29.99) | SkyWeave (free, MIT) |
|---|---|---|---|
| **Price** | €24.99 + VAT, online account required | $29.99 one-time | **Free forever — MIT, no account, no license server** |
| **Wake turbulence** | ✅ Active Air Effects (vortex/wake) | ❌ None — they admit ASFS has it | ✅ Physics-based WakeTurbulenceEngine — traffic vortex model + airport corridor mode |
| **Turbulence — CAT / jetstream** | ✅ Active Air Effects | ✅ 3-D turbulence/shear/mountain-wave model | ✅ Jetstream CAT + shear from wind-layer analysis |
| **Turbulence — thermal / convective** | ✅ Active Air Effects (thermals, drafts, updrafts/downdrafts, microbursts) | ✅ | ✅ TurbulenceCalculator — thermal + convective from storm cells |
| **Turbulence — mountain wave** | ✅ | ✅ | ✅ Mountain wave from wind-over-terrain + stability |
| **Turbulence — cloud / in-cloud** | ✅ Cloud turbulence with in-cloud detection | ⚠️ Exaggerated/ongoing turbulence outside hazard areas (user reports) | ✅ In-cloud detection via pressure-level cloud-cover layers |
| **Storm / thunderstorm fidelity** | ✅ Enhanced cloudscape + thunderstorm depiction | ⚠️ Weak cells, no lightning | ✅ StormModeler — lightning clustering + SIGMET fusion + CAPE/lifted-index-driven intensity |
| **Data models** | Proprietary + HiFi backend | GFS 0.25° + Open-Meteo | ✅ Region-optimal: HRRR 3 km (CONUS, hourly), ICON-EU (~13 km, Europe), GFS 0.11°/0.25° + ECMWF IFS 9 km (global) |
| **CAPE / lifted index / freezing level** | ✅ | Freezing levels only | ✅ CAPE, lifted index, freezing level height, cloud cover at levels — all free |
| **METAR accuracy & fusion** | ✅ | ❌ Weather sometimes doesn't match METAR (user reports) | ✅ METAR-observed ground-truth fusion — observed conditions weighted over model output |
| **Global coverage fidelity** | ✅ | ⚠️ Wrong upper winds in Europe/North Africa (early reports) | ✅ Region-optimal model per region — fixes the "wrong winds" gap by construction |
| **Historical weather** | ✅ Advanced Historical with playback | ✅ ERA5 historical | 🔜 ERA5 via Open-Meteo historical API (v0.5) |
| **Sandbox mode** | Preset + Passive modes | ✅ Sandbox | 🔜 Sandbox (v0.5) |
| **Passive mode** | ✅ Passive depiction | ❌ None | ✅ Passive mode — Asobo clouds + our winds/turbulence |
| **Transition smoothness** | ✅ | ⚠️ Coast-then-ease good, but tile-boundary cloud pop-in + transition artifacts remain | ✅ SmoothingPipeline — per-channel 3-min coast-then-ease, zero pop-in |
| **UI** | Feature-rich, premium | EFIS-style glass, dark/light | ✅ Glassmorphic dashboard + AS-style customization sliders/toggles |
| **Plugin / API extensibility** | ✅ Public API + web companion app | ❌ | 🔜 Plugin architecture + local REST API (v0.5) |
| **Open source** | ❌ Closed | ❌ Closed | ✅ MIT — community can audit, extend, contribute |
| **Sim coverage** | MSFS 2020 + 2024 | MSFS 2024 | MSFS 2024 (SimConnect, out-of-process) |

### The 7 Moats

#### Moat 1 — Region-optimal multi-model accuracy
StrataWx's worst-reviewed failure is **wrong upper winds in Europe/North Africa**. We fix it by construction: region-aware model selection — HRRR 3 km hourly over CONUS, ICON-EU over Europe, GFS 0.11°/0.25° + ECMWF IFS elsewhere — with all altitudes mapped via **geopotential heights** (never assumed pressure-altitude). Every region gets the best free model that exists there.

#### Moat 2 — Physics-based wake turbulence (unmatched in open source)
No open-source engine has wake turbulence. ASFS charges for it; StrataWx admits it's missing. Ours is physics-based: a **traffic-based vortex model** (strength scales with lead-aircraft weight class, wingtip separation, and closure rate) plus **airport corridor mode** (wake encounters on approach/departure even without live traffic). MSFS 2024's native wake physics does the rest — we create the conditions that trigger it.

#### Moat 3 — METAR-observed ground-truth fusion
Pilots compare everything against the METAR at their station. When the sim disagrees with the METAR, the product loses credibility — this is a top StrataWx complaint. We weight **observed conditions (METAR/TAF) over model output** at the aircraft station and blend smoothly outward, so the sim matches what the pilot sees on the weather brief.

#### Moat 4 — CAPE / lifted-index-driven storms
Storm intensity and convection depth come from **CAPE and lifted index** fetched per location — not lightning-count heuristics. High CAPE produces a towering CB with matching turbulence and precipitation; a capped atmosphere stays suppressed. Physically grounded, and it runs on free data.

#### Moat 5 — Free + MIT + passive mode
No price, no account, no license server — the anti-ASFS. MIT means the sim community can audit, fork, and extend. Passive mode (inject winds/turbulence, keep Asobo clouds) covers users who want fidelity without a full takeover — a mode StrataWx lacks.

#### Moat 6 — Glass UI + customization
Glassmorphic dashboard with **AS-style customization sliders/toggles** — turbulence intensity scaling, cloud density, refresh cadence, theme — the commercial-tier experience without the commercial-tier price.

#### Moat 7 — Plugin architecture + REST API roadmap
ASFS's public API is a paid moat; we neutralize it by being open. An **IWeatherDataSource plugin interface** lets the community add data sources; a **local REST API** lets EFBs, SimBrief, and community tools query SkyWeave's live state without SimConnect.

### What Would Make Us Win — Ranked Roadmap

Ordered by impact-to-effort. Ranks 1–7 kill the specific complaints users raise against StrataWx; ranks 8+ widen the gap and complete the product.

| Rank | Item | Effort | Milestone |
|---|---|---|---|
| 1 | METAR-observed ground-truth fusion | ~20 h | v0.4 |
| 2 | Region-optimal multi-model winds (HRRR / ICON-EU / GFS / ECMWF + geopotential heights) | in flight | v0.4 |
| 3 | Physics-based wake turbulence + airport corridor mode | in flight | v0.4 |
| 4 | CAPE / lifted-index-driven storm intensity | in flight | v0.4 |
| 5 | Mountain wave + jetstream CAT turbulence | in flight | v0.4 |
| 6 | Glass UI + AS-style customization sliders/toggles | in flight | v0.4 |
| 7 | Live API verification vs. METAR ground truth (tests/live-api-results.md) | ongoing | v0.4 |
| 8 | TAF wiring into engine + UI | 20 min | v0.4 |
| 9 | Radar overlay in UI | ~1 day | v0.5 |
| 10 | REST API (local, for EFBs/community tools) | ~2-3 days | v0.5 |
| 11 | ERA5 historical mode (Open-Meteo historical API) | ~3-4 days | v0.5 |
| 12 | SimBrief integration | ~1-2 days | v0.5 |
| 13 | VATSIM/IVAO detection | ~1 day | v0.5 |
| 14 | SimConnect traffic feed for real wake encounters | ~1-2 wks | v0.5 |
| 15 | Plugin architecture (IWeatherDataSource discovery) | ~1-2 wks | v0.5 |

Rule of thumb: every row where ASFS is ✅ and StrataWx is ❌ is a sale we take from StrataWx; every row where ASFS is ✅ and we are ❌/🔜 is a roadmap item.

---

## REX Atmos CORE Compatibility

**REX Atmos CORE is a visual atmospheric renderer, NOT a weather data engine.** It enhances how weather *looks* — Rayleigh scattering, cloud colors, lighting, haze, particulate simulation — but does not provide the underlying meteorological data.

**Our project (SkyWeave) and REX Atmos CORE are complementary:**

| Layer | Our Project (SkyWeave) | REX Atmos CORE |
|-------|------------------------|----------------|
| Purpose | Weather DATA engine | Visual ATMOSPHERE renderer |
| Provides | Winds, temperature, pressure, clouds, precip, hazards | Rayleigh scattering, cloud colors, lighting, haze |
| Data source | Real-world METAR/TAF/Open-Meteo | Inherits whatever weather source is active |
| SimConnect role | Inject meteorological SimVars | Enhance rendering engine hooks |

### How They Work Together

1. SkyWeave injects real weather data → sim shows correct wind/temp/clouds
2. REX Atmos CORE enhances visual rendering → sky looks more realistic
3. REX Atmos CORE works with any weather source (Asobo Live Weather, ActiveSky FS, or us)

### Compatibility Requirements

- **Do NOT override REX Atmos CORE's rendering hooks** — we inject weather DATA, not visual effects
- **Document compatibility** — list REX Atmos CORE as a recommended companion addon
- **No conflicting SimConnect subscriptions** — REX Atmos CORE uses `AMBIENT_*` vars for reading, we use them for writing; this is safe as long as we don't subscribe to the same data definitions they write
- **Weather mode detection** — if user has REX Atmos CORE active, our visual enhancements (if any) should defer to it

### What We Should NOT Do

- ❌ Do not implement Rayleigh scattering, cloud color tuning, or atmospheric rendering — REX Atmos CORE does this better
- ❌ Do not claim to replace REX Atmos CORE — we are complementary
- ❌ Do not inject into the rendering pipeline — we inject meteorological data only

### What We Should Do

- ✅ Position SkyWeave as a weather DATA engine (like ActiveSky FS, not like REX Atmos)
- ✅ Document REX Atmos CORE as a recommended companion in README
- ✅ Test with REX Atmos CORE installed to verify no conflicts
- ✅ Support the same weather mode switching REX Atmos CORE expects (Live Weather, Preset, Custom)

---

## Cloud Layer Injection (Up to 24+ Layers)

### Weather Preset XML (WPR) Format

MSFS 2024 supports **multiple `<CloudLayer>` elements** in a single weather preset. Each layer defines:

```xml
<CloudLayer>
    <CloudLayerDensity Value="0.700" Unit="(0 - 1)" />
    <CloudLayerAltitudeBot Value="1500" Unit="m" />
    <CloudLayerAltitudeTop Value="3000" Unit="m" />
    <CloudLayerScattering Value="0.300" Unit="(0 - 1)" />
</CloudLayer>
```

| Parameter | Range | Meaning |
|-----------|-------|---------|
| `Density` | 0.0 - 1.0 | Opacity/scattering (1.0 = fully opaque) |
| `AltitudeBot` | meters | Base of cloud layer |
| `AltitudeTop` | meters | Top of cloud layer |
| `Scattering` | 0.0 - 1.0 | 0.0 = stratiform, 1.0 = convective |

### Cloud Layer Synthesis Pipeline

```
METAR/TAF (surface clouds)
    ↓
Open-Meteo pressure levels (humidity at altitude)
    ↓
CloudLayerBuilder
    ├─ Identify cloud decks from humidity > 70%
    ├─ Determine base from lifting condensation level
    ├─ Determine top from moisture depth
    ├─ Set density from relative humidity
    └─ Set scattering from cloud type (stratus vs cumulus)
    ↓
24+ CloudLayer objects
    ↓
WPR XML generation
    ↓
WPR preset file output (manual-loading fallback)
```

### Cloud Type Mapping

| METAR Code | MSFS CloudType | Scattering | Density |
|------------|----------------|------------|---------|
| FEW | FEW | 0.3 | 0.2 |
| SCT | SCT | 0.5 | 0.4 |
| BKN | BKN | 0.2 | 0.7 |
| OVC | OVC | 0.0 | 1.0 |
| CB | CB | 0.9 | 0.9 |
| TCU | TCU | 0.8 | 0.8 |

### Cloud Layer Builder (Core Algorithm)

```csharp
public class CloudLayerBuilder
{
    // Build cloud layers from METAR + Open-Meteo data
    public List<CloudLayer> BuildCloudLayers(MetarData metar, WindsAloftData windsAloft)
    {
        var layers = new List<CloudLayer>();
        
        // 1. Add METAR-reported cloud layers
        foreach (var metarCloud in metar.Clouds)
        {
            layers.Add(new CloudLayer
            {
                BaseMeters = FeetToMeters(metarCloud.BaseFeet),
                TopMeters = FeetToMeters(metarCloud.BaseFeet + EstimateLayerThickness(metarCloud.Type)),
                Density = MapCoverageToDensity(metarCloud.Coverage),
                Scattering = MapCloudTypeToScattering(metarCloud.Type),
                Type = metarCloud.Type
            });
        }
        
        // 2. Add layers from humidity data (Open-Meteo pressure levels)
        foreach (var pressureLevel in windsAloft.PressureLevels)
        {
            if (pressureLevel.RelativeHumidity > 70) // Cloud threshold
            {
                var existingLayer = FindOverlappingLayer(layers, pressureLevel.AltitudeMeters);
                if (existingLayer == null)
                {
                    layers.Add(new CloudLayer
                    {
                        BaseMeters = pressureLevel.AltitudeMeters - 500,
                        TopMeters = pressureLevel.AltitudeMeters + 500,
                        Density = MapHumidityToDensity(pressureLevel.RelativeHumidity),
                        Scattering = EstimateScattering(pressureLevel),
                        Type = EstimateCloudType(pressureLevel)
                    });
                }
            }
        }
        
        // 3. Merge overlapping layers
        layers = MergeOverlappingLayers(layers);
        
        // 4. Limit to 24 layers (MSFS maximum)
        return layers.Take(24).ToList();
    }
    
    private double MapCoverageToDensity(string coverage)
    {
        return coverage switch
        {
            "FEW" => 0.2,
            "SCT" => 0.4,
            "BKN" => 0.7,
            "OVC" => 1.0,
            _ => 0.5
        };
    }
    
    private double MapCloudTypeToScattering(string type)
    {
        return type switch
        {
            "FEW" => 0.3,
            "SCT" => 0.5,
            "BKN" => 0.2,
            "OVC" => 0.0,
            "CB" => 0.9,
            "TCU" => 0.8,
            _ => 0.5
        };
    }
}
```

---

## Thunderstorm Injection Engine

### The Challenge
MSFS 2024 weather presets are **global** — they apply everywhere, not per-region. You cannot place a discrete storm cell at a specific lat/lon via WPR XML.

### Our Solution: Dynamic Cell Advecting

Instead of placing static cells, we **continuously update the weather preset as the aircraft moves**, creating the illusion of discrete cells:

```
Real-world data:
├─ Convective SIGMETs (cell locations, motion vectors)
├─ Radar reflectivity (NOAA MRMS for CONUS, RainViewer global)
└─ Lightning strikes (Blitzortung)
    ↓
StormCellBuilder
    ├─ Identify active cells from radar/SIGMETs
    ├─ Model cell structure: core, anvil, embedded cells
    ├─ Calculate motion vector from SIGMET motion data
    └─ Scale intensity by distance to aircraft
    ↓
DynamicWeatherPreset
    ├─ Base layer: ambient weather from METAR/winds aloft
    ├─ Storm modulation: adjust density/scattering based on cell proximity
    ├─ Update every 5 seconds as aircraft moves
    └─ Smooth transitions between updates
```

### Thunderstorm Cell Structure

```
        ┌─────────────────┐
        │   ANVIL TOP     │  FL400-FL600
        │   (thin cirrus) │  density: 0.3, scattering: 0.1
        ├─────────────────┤
        │                 │
        │   MAIN CB CORE  │  FL100-FL400
        │   (dense cumulus)│  density: 0.9, scattering: 0.9
        │   ⚡ LIGHTNING   │
        │                 │
        ├─────────────────┤
        │   EMBEDDED CELL │  FL050-FL200
        │   (rain shaft)  │  density: 0.8, scattering: 0.7
        └─────────────────┘
```

### Thunderstorm WPR Parameters

```xml
<ThunderstormIntensity Value="0.8" Unit="(0 - 1)" />
<Precipitations Value="25.0" Unit="mm/h" />
```

- `ThunderstormIntensity`: 0.0 (none) to 1.0 (severe)
- `Precipitations`: rain/snow rate in mm/h

### Lightning Injection

We cannot inject individual lightning strikes via SimConnect (MSFS doesn't expose this). However, we can:

1. **Trigger visual lightning** by modulating `ThunderstormIntensity` near strike locations
2. **Display strikes on our UI** for pilot awareness
3. **Scale turbulence/icing** based on proximity to real strikes

---

## Icing Conditions Engine

### How MSFS Icing Works

MSFS 2024 determines icing based on:
- **Temperature**: 0°C to -20°C range (most severe at -10°C to -15°C)
- **Visible moisture**: Clouds, precipitation
- **Aircraft altitude**: Within cloud layers

The sim calculates `STRUCTURAL ICE PCT` automatically — we don't inject ice directly, but we control the **conditions that cause icing** by placing cloud layers correctly.

### Icing Band Calculation

```csharp
public class IcingCalculator
{
    // Icing intensity based on temperature and cloud presence
    public double CalculateIcingIndex(double tempC, bool inCloud, double cloudDensity)
    {
        if (!inCloud || tempC > 0 || tempC < -40)
            return 0.0; // No icing outside these bounds
        
        // Peak icing at -15°C
        double tempFactor = 1.0 - Math.Abs(tempC + 15.0) / 15.0;
        tempFactor = Math.Max(0, tempFactor);
        
        // Icing requires visible moisture
        double moistureFactor = cloudDensity;
        
        return tempFactor * moistureFactor;
    }
}
```

### Icing Layer Injection

For each altitude where icing conditions exist:
1. Calculate temperature at that altitude (from winds aloft data)
2. Check if aircraft is in a cloud layer at that altitude
3. If temp is between 0°C and -20°C AND in cloud → icing conditions
4. Adjust cloud density to ensure proper moisture depiction

### Icing Severity Bands

| Band | Temp Range | Severity | Cloud Adjustment |
|------|------------|----------|------------------|
| Rime | -10°C to -20°C | Moderate | density: 0.6-0.8 |
| Mixed | -5°C to -10°C | Severe | density: 0.8-1.0 |
| Clear | 0°C to -5°C | Light | density: 0.4-0.6 |

### Structural Ice Feedback

The sim provides `STRUCTURAL ICE PCT` — we can read this and display it in our UI, but the actual accretion is handled by MSFS's built-in icing model.

---

## Precipitation Injection

### WPR Precipitation Parameter

```xml
<Precipitations Value="12.5" Unit="mm/h" />
```

### Precipitation Rate from METAR

| METAR Code | Rate (mm/h) | Description |
|------------|-------------|-------------|
| -RA | 0.5-2.5 | Light rain |
| RA | 2.5-8.0 | Moderate rain |
| +RA | 8.0-50.0 | Heavy rain |
| -SN | 0.5-2.0 | Light snow |
| SN | 2.0-5.0 | Moderate snow |
| +SN | 5.0-30.0 | Heavy snow |
| TS | 10.0-50.0 | Thunderstorm |

### Precipitation Data Sources

1. **METAR**: Current precipitation type and intensity
2. **NASA GPM IMERG**: Global precipitation rate (via GIBS)
3. **RainViewer API**: Real-time radar-based precipitation
4. **NOAA MRMS**: High-resolution CONUS precipitation

---

## Key Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| SimConnect weather injection API changed in MSFS 2024 | Research current SDK docs; use Weather Preset XML as fallback |
| Open-Meteo pressure level data may not map cleanly to flight levels | Use geopotential_height for accurate altitude mapping |
| Blitzortung requires community registration | Gate behind user opt-in; degrade gracefully without it |
| MSFS exposes one global weather state (can't paint discrete cells) | Be honest in docs; simulate cell proximity effects on turbulence/icing |
| Conflict with REX Atmos CORE rendering hooks | We inject weather DATA only, not visual effects; defer rendering to REX |

---

## License
MIT License — maximum community reuse

---

## Compatible Addons (Tested)

| Addon | Type | Compatibility |
|-------|------|---------------|
| REX Atmos CORE | Atmospheric renderer | ✅ Fully compatible — we provide weather data, REX enhances visuals |
| ActiveSky FS | Weather engine | ⚠️ Do not run both simultaneously — both inject weather data |
| Asobo Live Weather | Built-in weather | ✅ Our engine replaces this when active |
| Navigraph Charts | Navigation | 🔜 Phase 2 integration |
| SimBrief | Flight planning | 🔜 Phase 2 integration |

## Credits
- NOAA Aviation Weather Center — METAR/TAF/SIGMET data
- Open-Meteo — Winds aloft forecast data
- Blitzortung.org — Community lightning strike data
- OurAirports — Airport database
- Avalonia UI — Cross-platform .NET UI framework
- REX Simulations — Atmospheric rendering (complementary addon)
