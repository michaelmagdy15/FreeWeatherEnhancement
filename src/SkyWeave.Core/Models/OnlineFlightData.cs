using System;

namespace SkyWeave.Core.Models;

public enum OnlineNetwork
{
    Vatsim,
    Ivao
}

public class OnlineFlightData
{
    public string Callsign { get; set; } = string.Empty;
    public OnlineNetwork Network { get; set; } = OnlineNetwork.Vatsim;
    public string PilotName { get; set; } = string.Empty;
    public string AircraftType { get; set; } = string.Empty;
    public string Departure { get; set; } = string.Empty;
    public string Arrival { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeFeet { get; set; }
    public double HeadingDegrees { get; set; }
    public double GroundSpeedKnots { get; set; }
    public double DistanceNm { get; set; }
    public bool IsOnGround { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public string AltitudeText => AltitudeFeet >= 10000 ? $"FL{AltitudeFeet / 100:000}" : $"{AltitudeFeet:F0} ft";
    public string SpeedText => $"{GroundSpeedKnots:F0} kt";
    public string RouteText => !string.IsNullOrEmpty(Departure) && !string.IsNullOrEmpty(Arrival)
        ? $"{Departure} ➔ {Arrival}"
        : "En-route";
}
