namespace SkyWeave.Core.Models;

public enum AircraftWeightClass
{
    Unknown,
    Light,
    Medium,
    Heavy,
    Super
}

public class AircraftTraffic
{
    public string Callsign { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeFeet { get; set; }
    public double HeadingDegrees { get; set; }
    public double SpeedKnots { get; set; }
    public double GroundSpeedKnots { get; set; }
    public AircraftWeightClass WeightClass { get; set; } = AircraftWeightClass.Medium;
    public bool OnGround { get; set; }
}