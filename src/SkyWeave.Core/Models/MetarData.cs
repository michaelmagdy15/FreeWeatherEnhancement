namespace SkyWeave.Core.Models;

public class MetarData
{
    public string StationId { get; set; } = string.Empty;
    public DateTime ObservationTime { get; set; }
    public double WindDirectionDegrees { get; set; }
    public double WindSpeedKnots { get; set; }
    public double? WindGustKnots { get; set; }
    public double VisibilityMeters { get; set; }
    public double TemperatureCelsius { get; set; }
    public double DewpointCelsius { get; set; }
    public double AltimeterHpa { get; set; }
    public string FlightCategory { get; set; } = string.Empty;
    public List<MetarCloud> Clouds { get; set; } = new();
    public List<string> WeatherConditions { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
}

public class MetarCloud
{
    public string Coverage { get; set; } = string.Empty;
    public int BaseFeet { get; set; }
    public string Type { get; set; } = string.Empty;
}
