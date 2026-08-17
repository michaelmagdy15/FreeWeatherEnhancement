namespace SkyWeave.Core.Models;

public class IcingLayer
{
    public double BaseFeet { get; set; }
    public double TopFeet { get; set; }
    public IcingSeverity Severity { get; set; }
    public double TemperatureCelsius { get; set; }
    public double CloudDensity { get; set; }
}

public enum IcingSeverity
{
    None,
    Light,
    Moderate,
    Severe,
    Extreme
}
