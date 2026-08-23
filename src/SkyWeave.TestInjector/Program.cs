using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using SkyWeave.SimBridge;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("===================================================================");
Console.WriteLine("  SkyWeave Live In-Flight Weather & Multi-Cloud Test Injector CLI  ");
Console.WriteLine("===================================================================");
Console.ResetColor();

var isLive = args.Contains("--live") || args.Contains("-live");
var customStation = args.FirstOrDefault(a => a.StartsWith("--station=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1].ToUpperInvariant();
var presetType = args.FirstOrDefault(a => a.StartsWith("--preset=", StringComparison.OrdinalIgnoreCase))?.Split('=')[1].ToLowerInvariant()
                 ?? (args.Contains("--fog") ? "fog" : args.Contains("--clear") ? "clear" : args.Contains("--highwinds") ? "highwinds" : (isLive ? "live" : "storm"));
var isLoop = args.Contains("--loop") || args.Contains("-l");

using var simConnect = new SimConnectManager();
var connectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

simConnect.Connected += (s, e) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("[SimConnect] Handshake OK — Connected to Microsoft Flight Simulator!");
    Console.ResetColor();
    connectedTcs.TrySetResult(true);
};

simConnect.ErrorOccurred += (s, msg) =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[SimConnect Error] {msg}");
    Console.ResetColor();
};

simConnect.LogMessage += (s, msg) =>
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[SimConnect Log] {msg}");
    Console.ResetColor();
};

simConnect.BridgeAckReceived += (s, ack) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[Bridge ACK] EventID: {ack.EventID} | Reply: {ack.Data}");
    Console.ResetColor();
};

simConnect.WeatherReadbackReceived += (s, rb) =>
{
    var inHg = rb.SeaLevelPressureHpa * 0.029529983;
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write($"[Sim Readback] Wind: {rb.WindDirectionDegrees:F0}° @ {rb.WindSpeedKnots:F0} kt | Temp: {rb.TemperatureCelsius:F1}°C | ");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"QNH: {inHg:F2} inHg ({rb.SeaLevelPressureHpa:F0} hPa)");
    Console.ResetColor();
};

Console.WriteLine("Connecting to MSFS SimConnect (polling until sim is detected)...");
var attempts = 0;
while (!simConnect.Connect())
{
    attempts++;
    if (attempts >= 10 && !isLoop)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("MSFS is not running yet. Start MSFS 2024, load a flight, and re-run this tool.");
        Console.ResetColor();
        return;
    }
    Console.Write(".");
    await Task.Delay(1000);
}

Console.WriteLine("\nAwaiting SimConnect OnRecvOpen handshake...");
var completed = await Task.WhenAny(connectedTcs.Task, Task.Delay(10000));
if (completed != connectedTcs.Task)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Timed out waiting for SimConnect handshake. Please ensure you are spawned inside the cockpit.");
    Console.ResetColor();
    return;
}

// Position acquisition
var stationFinder = new StationFinder();
double lat = 40.6399;
double lon = -73.7787;
double alt = 50.0;

if (!string.IsNullOrEmpty(customStation))
{
    var matchingAirport = stationFinder.AllAirports.FirstOrDefault(a => a.IcaoId.Equals(customStation, StringComparison.OrdinalIgnoreCase));
    if (matchingAirport != null)
    {
        lat = matchingAirport.Latitude;
        lon = matchingAirport.Longitude;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Target Station] {customStation} resolved to {lat:F4}°, {lon:F4}°");
        Console.ResetColor();
    }
    else if (customStation.Equals("EGLL", StringComparison.OrdinalIgnoreCase))
    {
        lat = 51.4770;
        lon = -0.4610;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Target Station] EGLL resolved to {lat:F4}°, {lon:F4}°");
        Console.ResetColor();
    }
}
else
{
    for (int p = 0; p < 10; p++)
    {
        var pos = simConnect.GetAircraftPosition();
        if (pos.HasValue && (Math.Abs(pos.Value.Latitude) > 0.001 || Math.Abs(pos.Value.Longitude) > 0.001))
        {
            lat = pos.Value.Latitude;
            lon = pos.Value.Longitude;
            alt = pos.Value.AltitudeFeet;
            break;
        }
        await Task.Delay(200);
    }

    var nearest = stationFinder.FindNearestStation(lat, lon);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[GPS Position] Acquired from sim: {lat:F4}°, {lon:F4}° | Alt: {alt:F0} ft | Nearest: {nearest}");
    Console.ResetColor();
}

WeatherEngine? liveEngine = null;
if (isLive || presetType == "live")
{
    Console.WriteLine("Initializing SkyWeave Live Multi-Model Weather Engine...");
    liveEngine = new WeatherEngine();
    await liveEngine.StartAsync(lat, lon);
}

var wprWriter = new WprFileWriter(msg => Console.WriteLine($"[WPR Writer] {msg}"));

do
{
    WeatherState state;
    if (liveEngine != null)
    {
        Console.WriteLine($"Fetching real-world live METAR, Winds Aloft, and synthesizing cloud stratification...");
        var liveState = await liveEngine.FetchCurrentWeatherAsync();
        state = liveState ?? CreateStormState();
    }
    else
    {
        state = presetType switch
        {
            "fog" => CreateFogState(),
            "clear" => CreateClearState(),
            "highwinds" => CreateHighWindsState(),
            _ => CreateStormState()
        };
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n--- [{DateTime.UtcNow:HH:mm:ss}Z] Injecting Weather: {state.StationId} ({state.FlightCategory}) ---");
    Console.ResetColor();
    Console.WriteLine($" METAR: {state.RawMetar}");
    Console.WriteLine($" Target QNH: {state.AltimeterHpa:F1} hPa ({(state.AltimeterHpa * 0.029529983):F2} inHg)");
    Console.WriteLine($" Target Temp: {state.TemperatureCelsius:F1}°C ({(state.TemperatureCelsius * 9 / 5 + 32):F1}°F)");
    Console.WriteLine($" Target Wind: {state.WindDirectionDegrees:F0}° @ {state.WindSpeedKnots:F0} kt (Gust {state.WindGustKnots:F0} kt)");
    Console.WriteLine($" Cloud Layers: {state.CloudLayers.Count} total synthesized layers (Surface to FL360)");
    foreach (var (c, idx) in state.CloudLayers.Select((layer, i) => (layer, i + 1)))
    {
        Console.WriteLine($"   Layer #{idx}: {c.BaseFeetAgl:F0} ft -> {c.TopFeetAgl:F0} ft | Cov: {c.CoveragePercent:P0} | Dens: {c.Density:P0} | Type: {c.Type}");
    }
    Console.WriteLine($" Winds Aloft: {state.WindsAloft.Count} atmospheric levels");

    // 1. Write WPR preset file with ALL cloud layers (up to 24)
    var wprPath = wprWriter.WritePreset(state, turbulenceBoostKnots: 20);
    Console.WriteLine($"[WPR] Preset written to: {wprPath}");

    // 2. Send CommBus Apply message to in-sim bridge
    var requestId = Guid.NewGuid().ToString("N");
    var applyJson = WeatherBridgeProtocol.CreateApplyMessage(requestId, state, wprWriter.LastPresetXml ?? "");
    var sent = simConnect.SendWeatherBridgeMessage(applyJson);
    Console.WriteLine($"[CommBus] Sent apply event (Request ID: {requestId}) -> {(sent ? "SUCCESS (QUEUED)" : "FAILED")}");

    // 3. Request SimVar Ambient Readback
    for (int i = 1; i <= 3; i++)
    {
        await Task.Delay(1500);
        simConnect.RequestWeatherReadback();
    }

    if (isLoop)
    {
        Console.WriteLine("\nLoop mode active — next injection in 10 seconds. Press Ctrl+C to stop.");
        await Task.Delay(8500);
    }
} while (isLoop);

liveEngine?.Dispose();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\nInjection routine complete. Verify your in-cockpit altimeter & in-game panel.");
Console.ResetColor();

// Scenario Generators
static WeatherState CreateStormState() => new()
{
    StationId = "KDFW",
    RawMetar = "KDFW 231700Z 27025G38KT 3000 +TSRA OVC008CB 18/17 A2965",
    ObservationTime = DateTime.UtcNow,
    Latitude = 32.8998,
    Longitude = -97.0403,
    TemperatureCelsius = 18.0,
    DewpointCelsius = 17.0,
    PressureHpa = 1004.0,
    AltimeterHpa = 1004.0,
    VisibilityMeters = 3000,
    FlightCategory = "IFR",
    WindDirectionDegrees = 270,
    WindSpeedKnots = 25,
    WindGustKnots = 38,
    GustDirectionDegrees = 270,
    Precipitation = PrecipitationType.Rain,
    PrecipitationRate = 35.0,
    ThunderstormIntensity = 0.90,
    HumidityPercent = 96,
    AerosolDensity = 0.35,
    CloudLayers = new List<CloudLayer>
    {
        new() { BaseFeetAgl = 800, TopFeetAgl = 4500, CoveragePercent = 100, Density = 1.0, Scattering = 0.8, Type = CloudType.OVC },
        new() { BaseFeetAgl = 5500, TopFeetAgl = 12000, CoveragePercent = 85, Density = 0.85, Scattering = 0.6, Type = CloudType.BKN },
        new() { BaseFeetAgl = 14000, TopFeetAgl = 38000, CoveragePercent = 95, Density = 0.95, Scattering = 0.9, Type = CloudType.CB },
        new() { BaseFeetAgl = 28000, TopFeetAgl = 42000, CoveragePercent = 60, Density = 0.5, Scattering = 0.9, Type = CloudType.FEW }
    },
    WindsAloft = new List<WindLayer>
    {
        new() { AltitudeFeet = 0, DirectionDegrees = 270, SpeedKnots = 25, TemperatureCelsius = 18, GustSpeedKnots = 38 },
        new() { AltitudeFeet = 3000, DirectionDegrees = 280, SpeedKnots = 40, TemperatureCelsius = 11 },
        new() { AltitudeFeet = 10000, DirectionDegrees = 300, SpeedKnots = 65, TemperatureCelsius = -5 },
        new() { AltitudeFeet = 30000, DirectionDegrees = 290, SpeedKnots = 110, TemperatureCelsius = -45 }
    }
};

static WeatherState CreateFogState() => new()
{
    StationId = "EGLL",
    RawMetar = "EGLL 231700Z 08004KT 0300 FG VV001 04/04 Q1022",
    ObservationTime = DateTime.UtcNow,
    Latitude = 51.4700,
    Longitude = -0.4543,
    TemperatureCelsius = 4.0,
    DewpointCelsius = 4.0,
    PressureHpa = 1022.0,
    AltimeterHpa = 1022.0,
    VisibilityMeters = 300,
    FlightCategory = "LIFR",
    WindDirectionDegrees = 80,
    WindSpeedKnots = 4,
    Precipitation = PrecipitationType.None,
    PrecipitationRate = 0,
    ThunderstormIntensity = 0,
    HumidityPercent = 100,
    AerosolDensity = 0.8,
    CloudLayers = new List<CloudLayer>
    {
        new() { BaseFeetAgl = 100, TopFeetAgl = 800, CoveragePercent = 100, Density = 1.0, Scattering = 0.1, Type = CloudType.OVC },
        new() { BaseFeetAgl = 1200, TopFeetAgl = 3500, CoveragePercent = 60, Density = 0.6, Scattering = 0.4, Type = CloudType.BKN },
        new() { BaseFeetAgl = 24000, TopFeetAgl = 32000, CoveragePercent = 30, Density = 0.3, Scattering = 0.9, Type = CloudType.FEW }
    },
    WindsAloft = new List<WindLayer>
    {
        new() { AltitudeFeet = 0, DirectionDegrees = 80, SpeedKnots = 4, TemperatureCelsius = 4 },
        new() { AltitudeFeet = 3000, DirectionDegrees = 110, SpeedKnots = 12, TemperatureCelsius = 2 }
    }
};

static WeatherState CreateClearState() => new()
{
    StationId = "KSFO",
    RawMetar = "KSFO 231700Z 29012KT 10SM CLR 22/11 A3002",
    ObservationTime = DateTime.UtcNow,
    Latitude = 37.6188,
    Longitude = -122.3750,
    TemperatureCelsius = 22.0,
    DewpointCelsius = 11.0,
    PressureHpa = 1016.6,
    AltimeterHpa = 1016.6,
    VisibilityMeters = 16093.44,
    FlightCategory = "VFR",
    WindDirectionDegrees = 290,
    WindSpeedKnots = 12,
    Precipitation = PrecipitationType.None,
    PrecipitationRate = 0,
    ThunderstormIntensity = 0,
    HumidityPercent = 50,
    AerosolDensity = 0.05,
    CloudLayers = new List<CloudLayer>(),
    WindsAloft = new List<WindLayer>
    {
        new() { AltitudeFeet = 0, DirectionDegrees = 290, SpeedKnots = 12, TemperatureCelsius = 22 },
        new() { AltitudeFeet = 5000, DirectionDegrees = 310, SpeedKnots = 20, TemperatureCelsius = 14 },
        new() { AltitudeFeet = 18000, DirectionDegrees = 330, SpeedKnots = 45, TemperatureCelsius = -18 }
    }
};

static WeatherState CreateHighWindsState() => new()
{
    StationId = "LOWI",
    RawMetar = "LOWI 231700Z 18032G55KT 9999 FEW040 12/02 Q0998",
    ObservationTime = DateTime.UtcNow,
    Latitude = 47.2602,
    Longitude = 11.3440,
    TemperatureCelsius = 12.0,
    DewpointCelsius = 2.0,
    PressureHpa = 998.0,
    AltimeterHpa = 998.0,
    VisibilityMeters = 10000,
    FlightCategory = "VFR",
    WindDirectionDegrees = 180,
    WindSpeedKnots = 32,
    WindGustKnots = 55,
    GustDirectionDegrees = 185,
    Precipitation = PrecipitationType.None,
    PrecipitationRate = 0,
    ThunderstormIntensity = 0,
    TurbulenceIndex = 0.85,
    HumidityPercent = 45,
    AerosolDensity = 0.1,
    CloudLayers = new List<CloudLayer>
    {
        new() { BaseFeetAgl = 4000, TopFeetAgl = 7000, CoveragePercent = 30, Density = 0.6, Scattering = 0.8, Type = CloudType.FEW },
        new() { BaseFeetAgl = 12000, TopFeetAgl = 16000, CoveragePercent = 40, Density = 0.5, Scattering = 0.8, Type = CloudType.SCT },
        new() { BaseFeetAgl = 26000, TopFeetAgl = 34000, CoveragePercent = 70, Density = 0.7, Scattering = 0.9, Type = CloudType.FEW }
    },
    WindsAloft = new List<WindLayer>
    {
        new() { AltitudeFeet = 0, DirectionDegrees = 180, SpeedKnots = 32, TemperatureCelsius = 12, GustSpeedKnots = 55 },
        new() { AltitudeFeet = 5000, DirectionDegrees = 180, SpeedKnots = 55, TemperatureCelsius = 5, GustSpeedKnots = 70 },
        new() { AltitudeFeet = 12000, DirectionDegrees = 190, SpeedKnots = 80, TemperatureCelsius = -8 },
        new() { AltitudeFeet = 34000, DirectionDegrees = 200, SpeedKnots = 125, TemperatureCelsius = -52 }
    }
};
