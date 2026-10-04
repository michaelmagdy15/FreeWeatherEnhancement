namespace SkyWeave.App.Models;

public class UserSettings
{
    public int RefreshIntervalSeconds { get; set; } = 20;
    public bool PassiveMode { get; set; } = false;
    public string ManualStationOverride { get; set; } = string.Empty;
    public int SmoothingDurationMinutes { get; set; } = 3;
    public int InjectionIntervalSeconds { get; set; } = 5;
    public bool AutoConnect { get; set; } = false;
    public bool IsDarkTheme { get; set; } = true;
    public int WindowWidth { get; set; } = 1400;
    public int WindowHeight { get; set; } = 900;
    public double TurbulenceIntensityPercent { get; set; } = 100;
    public bool WakeTurbulenceEnabled { get; set; } = true;
    public double WakeTurbulencePercent { get; set; } = 100;
    public double GustEnhancementPercent { get; set; } = 100;
    public double ThunderstormIntensityPercent { get; set; } = 100;
    public double PrecipitationPercent { get; set; } = 100;
    public double AerosolPercent { get; set; } = 100;
    public double GlassOpacityPercent { get; set; } = 85;
    public bool AllowLanEfbAccess { get; set; } = false;

    // Operational & Immersion Controls
    public bool FreezeWeather { get; set; } = false;
    public bool DepartureHoldEnabled { get; set; } = true;
    public bool ArrivalHoldEnabled { get; set; } = true;
    public bool AutoFreezeOnApproach { get; set; } = true;
    public string SimBriefPilotId { get; set; } = string.Empty;
    public bool AutoLoadSimBriefAtLaunch { get; set; } = false;
    public string PressureUnit { get; set; } = "inHg"; // "inHg" | "hPa"
    public string TemperatureUnit { get; set; } = "C"; // "C" | "F"
    public string WindSpeedUnit { get; set; } = "kt"; // "kt" | "m/s" | "km/h"
    public bool StreamerMode { get; set; } = false;
}