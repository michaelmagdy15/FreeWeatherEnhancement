namespace SkyWeave.Core.Models;

public class StormCell
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeFeet { get; set; }
    public double MotionDirectionDegrees { get; set; }
    public double MotionSpeedKnots { get; set; }
    public double Intensity { get; set; }
    public double RadiusNm { get; set; }
    public CellType Type { get; set; }
    public List<LightningStrike> NearbyStrikes { get; set; } = new();
    public List<CloudLayer> CloudLayers { get; set; } = new();
}

public enum CellType
{
    Core,
    Anvil,
    Embedded,
    MultiCell
}
