namespace SkyWeave.Core.Models;

public class TurbulenceLayer
{
    public double BaseFeet { get; set; }
    public double TopFeet { get; set; }
    public TurbulenceIntensity Intensity { get; set; }
    public TurbulenceType Type { get; set; }
}

public enum TurbulenceIntensity
{
    None,
    Light,
    Moderate,
    Severe,
    Extreme
}

public enum TurbulenceType
{
    Thermal,
    Convective,
    Mechanical,
    MountainWave,
    Wake
}
