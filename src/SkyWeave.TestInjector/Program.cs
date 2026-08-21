using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.SimBridge;

Console.WriteLine("====================================================");
Console.WriteLine(" SkyWeave CLI Test Rain & Storm Weather Injector");
Console.WriteLine("====================================================");

var rainState = new WeatherState
{
    StationId = "TEST",
    RawMetar = "TEST 191600Z 27025G38KT 3000 +RA TS OVC008 18/17 Q1004",
    ObservationTime = DateTime.UtcNow,
    Latitude = 30.0,
    Longitude = 31.0,
    TemperatureCelsius = 18.0,
    DewpointCelsius = 17.5,
    PressureHpa = 1004.0,
    AltimeterHpa = 1004.0,
    VisibilityMeters = 3000,
    FlightCategory = "IFR",
    WindDirectionDegrees = 270,
    WindSpeedKnots = 25,
    WindGustKnots = 38,
    GustDirectionDegrees = 270,
    Precipitation = PrecipitationType.Rain,
    PrecipitationRate = 25.0, // Heavy rain
    ThunderstormIntensity = 0.85,
    HumidityPercent = 95,
    AerosolDensity = 0.3,
    CloudLayers = new List<CloudLayer>
    {
        new CloudLayer
        {
            BaseFeetAgl = 800,
            TopFeetAgl = 12000,
            CoveragePercent = 100,
            Density = 1.0,
            Scattering = 0.8,
            Type = CloudType.OVC
        },
        new CloudLayer
        {
            BaseFeetAgl = 12000,
            TopFeetAgl = 28000,
            CoveragePercent = 90,
            Density = 0.9,
            Scattering = 0.8,
            Type = CloudType.CB
        }
    },
    WindsAloft = new List<WindLayer>
    {
        new WindLayer { AltitudeFeet = 0, DirectionDegrees = 270, SpeedKnots = 25, TemperatureCelsius = 18, GustSpeedKnots = 38 },
        new WindLayer { AltitudeFeet = 3000, DirectionDegrees = 280, SpeedKnots = 35, TemperatureCelsius = 12 },
        new WindLayer { AltitudeFeet = 10000, DirectionDegrees = 300, SpeedKnots = 50, TemperatureCelsius = -2 }
    }
};

using var simConnect = new SimConnectManager();
var connectedTcs = new TaskCompletionSource<bool>();
var ackTcs = new TaskCompletionSource<BridgeAckData>();
var readbackTcs = new TaskCompletionSource<AmbientWeatherData>();

simConnect.Connected += (s, e) =>
{
    Console.WriteLine("[SimConnect] Connected to MSFS!");
    connectedTcs.TrySetResult(true);
};

simConnect.ErrorOccurred += (s, msg) => Console.WriteLine($"[SimConnect Error] {msg}");
simConnect.LogMessage += (s, msg) => Console.WriteLine($"[SimConnect Log] {msg}");

simConnect.BridgeAckReceived += (s, ack) =>
{
    Console.WriteLine($"[Bridge ACK] EventID: {ack.EventID}, Data: {ack.Data}");
    ackTcs.TrySetResult(ack);
};

simConnect.WeatherReadbackReceived += (s, rb) =>
{
    Console.WriteLine($"[Sim Readback] Wind: {rb.WindDirectionDegrees:F0}° @ {rb.WindSpeedKnots:F0} kt | Temp: {rb.TemperatureCelsius:F1}°C | QNH: {rb.SeaLevelPressureHpa:F0} hPa");
    readbackTcs.TrySetResult(rb);
};

Console.WriteLine("Connecting to MSFS SimConnect...");
if (!simConnect.Connect())
{
    Console.WriteLine("Failed to initiate SimConnect connection. Is MSFS running?");
    return;
}

Console.WriteLine("Waiting for SimConnect OnRecvOpen handshake (up to 10s)...");
var completed = await Task.WhenAny(connectedTcs.Task, Task.Delay(10000));
if (completed != connectedTcs.Task)
{
    Console.WriteLine("Timed out waiting for SimConnect connection.");
    return;
}

// 1. Write WPR preset file
var wprWriter = new WprFileWriter(msg => Console.WriteLine($"[WPR Writer] {msg}"));
var wprPath = wprWriter.WritePreset(rainState, turbulenceBoostKnots: 20);
Console.WriteLine($"[WPR] Preset written to: {wprPath}");

// 2. Send CommBus Apply message to in-sim bridge
var requestId = Guid.NewGuid().ToString("N");
var applyJson = WeatherBridgeProtocol.CreateApplyMessage(requestId, rainState, wprWriter.LastPresetXml ?? "");
Console.WriteLine($"[CommBus] Sending Rain & Storm weather injection (Request {requestId})...");
var sent = simConnect.SendWeatherBridgeMessage(applyJson);
Console.WriteLine($"[CommBus] Message dispatch status: {(sent ? "QUEUED" : "FAILED")}");

// 3. Wait for Bridge ACK
Console.WriteLine("Waiting for in-sim panel ACK (up to 8s)...");
var ackTask = await Task.WhenAny(ackTcs.Task, Task.Delay(8000));
if (ackTask == ackTcs.Task)
{
    var ack = await ackTcs.Task;
    Console.WriteLine($"[In-Sim Bridge] Received reply: {ack.Data}");
}
else
{
    Console.WriteLine("[In-Sim Bridge] No ACK received in 8s. Is the SkyWeave toolbar panel open?");
}

// 4. Request Readback
Console.WriteLine("Requesting ambient weather readback from sim...");
for (int i = 0; i < 4; i++)
{
    await Task.Delay(2000);
    simConnect.RequestWeatherReadback();
}

await Task.Delay(2000);
Console.WriteLine("Test injection script finished.");
