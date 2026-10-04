namespace SkyWeave.Core.Models;

public class SoundingLevel
{
    public double AltitudeFeet { get; set; }
    public double Y { get; set; }
    public double TemperatureCelsius { get; set; }
    public double DewpointCelsius { get; set; }
    public double TemperatureX { get; set; }
    public double DewpointX { get; set; }
    public double WindDirectionDegrees { get; set; }
    public double WindSpeedKnots { get; set; }
    public string FlightLevelName { get; set; } = string.Empty;
    public string WindText { get; set; } = string.Empty;
    public string TempText { get; set; } = string.Empty;
}

public class SoundingCloudBlock
{
    public double BaseAltitudeFeet { get; set; }
    public double TopAltitudeFeet { get; set; }
    public double Y { get; set; }
    public double Height { get; set; }
    public double CoveragePercent { get; set; }
    public double Opacity { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class SoundingHazardBand
{
    public string Type { get; set; } = "ICING"; // "ICING" or "TURBULENCE"
    public double BaseAltitudeFeet { get; set; }
    public double TopAltitudeFeet { get; set; }
    public double Y { get; set; }
    public double Height { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#38BDF8";
    public string Label { get; set; } = string.Empty;
}

public class SoundingProfileData
{
    public double CanvasWidth { get; set; } = 380;
    public double CanvasHeight { get; set; } = 280;
    public string TemperaturePath { get; set; } = string.Empty;
    public string DewpointPath { get; set; } = string.Empty;
    public double FreezingLevelFeet { get; set; }
    public double FreezingLevelY { get; set; }
    public string FreezingLevelLabel { get; set; } = "0°C FREEZING LEVEL";
    public double? AircraftAltitudeFeet { get; set; }
    public double? AircraftY { get; set; }
    public List<SoundingCloudBlock> CloudBlocks { get; set; } = new();
    public List<SoundingHazardBand> HazardBands { get; set; } = new();
    public List<SoundingLevel> StandardLevels { get; set; } = new();
}
