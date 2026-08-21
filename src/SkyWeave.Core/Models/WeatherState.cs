using SkyWeave.Core.Decoders;

namespace SkyWeave.Core.Models;

public class WeatherState
{
    public DateTime ObservationTime { get; set; }
    public string StationId { get; set; } = string.Empty;
    public string RawMetar { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public double TemperatureCelsius { get; set; }
    public double DewpointCelsius { get; set; }
    public double PressureHpa { get; set; }
    public double AltimeterHpa { get; set; }
    public double VisibilityMeters { get; set; }
    public string FlightCategory { get; set; } = string.Empty;

    public double WindDirectionDegrees { get; set; }
    public double WindSpeedKnots { get; set; }
    public double? WindGustKnots { get; set; }
    public double? GustDirectionDegrees { get; set; }

    public List<CloudLayer> CloudLayers { get; set; } = new();
    public List<WindLayer> WindsAloft { get; set; } = new();
    public List<WeatherHazard> Hazards { get; set; } = new();
    public List<StormCell> StormCells { get; set; } = new();

    public PrecipitationType Precipitation { get; set; }
    public double PrecipitationRate { get; set; }

    public double HumidityPercent { get; set; }
    public double FreezingLevelFeet { get; set; }
    public double CeilingFeet { get; set; }

    public double IcingIndex { get; set; }
    public double TurbulenceIndex { get; set; }
    public List<IcingLayer> IcingLayers { get; set; } = new();
    public List<TurbulenceLayer> TurbulenceLayers { get; set; } = new();

    public double ThunderstormIntensity { get; set; }
    public double AerosolDensity { get; set; }

    public double? ConvectiveAvailablePotentialEnergy { get; set; }
    public double? LiftedIndex { get; set; }
    public string SourceModelName { get; set; } = "Unknown";
    public double DataAgeMinutes { get; set; }
    public TafData? Taf { get; set; }
}

public enum PrecipitationType
{
    None,
    Rain,
    Snow,
    Drizzle,
    Fog,
    Mist,
    IcePellets,
    FreezingRain
}
