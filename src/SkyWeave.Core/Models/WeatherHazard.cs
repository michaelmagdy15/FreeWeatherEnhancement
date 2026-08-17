namespace SkyWeave.Core.Models;

public class WeatherHazard
{
    public string Id { get; set; } = string.Empty;
    public HazardType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Severity { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AltitudeMinFeet { get; set; }
    public double? AltitudeMaxFeet { get; set; }
    public string RawText { get; set; } = string.Empty;
}

public enum HazardType
{
    Sigmet,
    Airmet,
    Pirep,
    ConvectiveSigmet,
    TurbulenceSigmet,
    IcingSigmet,
    VolcanicAshSigmet
}
