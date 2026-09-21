namespace SkyWeave.Core.Models;

public class WindLayer
{
    public int Id { get; set; }
    /// <summary>Observed station wind anchor; excluded from synthetic gust boosts.</summary>
    public bool IsSurfaceLayer { get; set; }
    public double AltitudeMeters { get; set; }
    public double AltitudeFeet { get; set; }
    public double DirectionDegrees { get; set; }
    public double SpeedKnots { get; set; }
    public double? GustSpeedKnots { get; set; }
    public double? GustDirectionDegrees { get; set; }
    public double TemperatureCelsius { get; set; }
    public double? TurbulenceIntensity { get; set; }
}
