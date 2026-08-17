namespace SkyWeave.Core.Models;

public class WindsAloftData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime ForecastTime { get; set; }
    public string SourceModel { get; set; } = "Unknown";
    public double ModelResolutionKm { get; set; }
    public double DataAgeMinutes { get; set; }
    public double? ConvectiveAvailablePotentialEnergy { get; set; }
    public double? LiftedIndex { get; set; }
    public double? FreezingLevelHeightMeters { get; set; }
    public List<PressureLevelData> PressureLevels { get; set; } = new();
}

public class PressureLevelData
{
    public double PressureHpa { get; set; }
    public double AltitudeMeters { get; set; }
    public double AltitudeFeet { get; set; }
    public double TemperatureCelsius { get; set; }
    public double RelativeHumidity { get; set; }
    public double CloudCoverPercent { get; set; }
    public double WindSpeedKnots { get; set; }
    public double WindDirectionDegrees { get; set; }
    public double GeopotentialHeightMeters { get; set; }
}