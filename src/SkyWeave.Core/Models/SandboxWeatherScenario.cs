using System;
using System.Collections.Generic;

namespace SkyWeave.Core.Models;

/// <summary>
/// Defines a custom user-configured or preset weather scenario for Sandbox mode.
/// Allows pilots to test specific instrument approaches, severe crosswinds, CAT, or convective conditions.
/// </summary>
public class SandboxWeatherScenario
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Custom Scenario";
    public string Description { get; set; } = "User-defined manual atmospheric scenario";

    // Surface Parameters
    public double SurfaceWindDirection { get; set; } = 270.0; // 0..360 deg
    public double SurfaceWindSpeedKnots { get; set; } = 10.0;  // 0..100 kt
    public double? SurfaceWindGustKnots { get; set; } = null;  // optional gust
    public double TemperatureCelsius { get; set; } = 15.0;    // -50..+50 °C
    public double DewpointCelsius { get; set; } = 10.0;       // -50..+50 °C
    public double PressureHpa { get; set; } = 1013.25;        // 900..1080 hPa
    public double VisibilityMeters { get; set; } = 10000.0;   // 50..50000 m
    public PrecipitationType Precipitation { get; set; } = PrecipitationType.None;
    public double PrecipitationRateMmHr { get; set; } = 0.0;  // 0..100 mm/hr
    public bool Thunderstorm { get; set; } = false;
    public double SnowCoverMeters { get; set; } = 0.0;

    // Atmospheric & Hazard Parameters
    public double TurbulenceIntensity { get; set; } = 0.0;    // 0..1 scale
    public double IcingSeverity { get; set; } = 0.0;          // 0..1 scale
    public double ConvectiveAvailablePotentialEnergy { get; set; } = 0.0; // CAPE (J/kg)
    public double LiftedIndex { get; set; } = 0.0;

    // Volumetric Cloud Decks
    public List<SandboxCloudDeck> CloudDecks { get; set; } = new();

    // Winds Aloft Profiles
    public List<SandboxWindLayer> WindsAloft { get; set; } = new();

    // Application Mode: true = instant snap (0s), false = smooth blend via SmoothingPipeline
    public bool InstantTransition { get; set; } = false;

    // ==========================================
    // Pre-Configured Extreme Presets
    // ==========================================

    public static SandboxWeatherScenario CreateCat3Fog()
    {
        return new SandboxWeatherScenario
        {
            Id = "cat3_fog",
            Name = "CAT III ILS 0/0 Fog",
            Description = "Zero-ceiling dense radiation fog down to runway surface (RVR 150m / 1/16 SM). Cat III autoland certification.",
            SurfaceWindDirection = 0,
            SurfaceWindSpeedKnots = 2,
            SurfaceWindGustKnots = null,
            TemperatureCelsius = 3.0,
            DewpointCelsius = 3.0,
            PressureHpa = 1020.0,
            VisibilityMeters = 150.0,
            Precipitation = PrecipitationType.Mist,
            PrecipitationRateMmHr = 0,
            TurbulenceIntensity = 0.05,
            IcingSeverity = 0.2,
            CloudDecks = new List<SandboxCloudDeck>
            {
                new()
                {
                    BaseFeetAgl = 0,
                    TopFeetAgl = 600,
                    CoveragePercent = 100,
                    Type = CloudType.ST,
                    Density = 0.95,
                    Scattering = 0.15
                }
            },
            WindsAloft = new List<SandboxWindLayer>
            {
                new() { AltitudeFeet = 1000, DirectionDegrees = 350, SpeedKnots = 4, TemperatureCelsius = 2 },
                new() { AltitudeFeet = 3000, DirectionDegrees = 340, SpeedKnots = 8, TemperatureCelsius = 1 },
                new() { AltitudeFeet = 6000, DirectionDegrees = 320, SpeedKnots = 12, TemperatureCelsius = -2 }
            }
        };
    }

    public static SandboxWeatherScenario CreateCrosswindLanding()
    {
        return new SandboxWeatherScenario
        {
            Id = "crosswind",
            Name = "Severe Crosswind Landing",
            Description = "35 kt sustained crosswind gusting 50 kt with intense boundary layer mechanical turbulence.",
            SurfaceWindDirection = 90.0,
            SurfaceWindSpeedKnots = 35.0,
            SurfaceWindGustKnots = 50.0,
            TemperatureCelsius = 18.0,
            DewpointCelsius = 8.0,
            PressureHpa = 1005.0,
            VisibilityMeters = 15000.0,
            TurbulenceIntensity = 0.80,
            IcingSeverity = 0.0,
            CloudDecks = new List<SandboxCloudDeck>
            {
                new()
                {
                    BaseFeetAgl = 3500,
                    TopFeetAgl = 6000,
                    CoveragePercent = 40,
                    Type = CloudType.SCT,
                    Density = 0.5,
                    Scattering = 0.5
                }
            },
            WindsAloft = new List<SandboxWindLayer>
            {
                new() { AltitudeFeet = 1000, DirectionDegrees = 95, SpeedKnots = 42, TemperatureCelsius = 16 },
                new() { AltitudeFeet = 3000, DirectionDegrees = 105, SpeedKnots = 48, TemperatureCelsius = 12 },
                new() { AltitudeFeet = 6000, DirectionDegrees = 115, SpeedKnots = 56, TemperatureCelsius = 6 }
            }
        };
    }

    public static SandboxWeatherScenario CreateSupercellThunderstorm()
    {
        return new SandboxWeatherScenario
        {
            Id = "supercell",
            Name = "Severe Supercell Thunderstorm",
            Description = "Extreme convective cell with severe updrafts/downdrafts, CAPE > 3800 J/kg, heavy rain, and lightning.",
            SurfaceWindDirection = 270.0,
            SurfaceWindSpeedKnots = 26.0,
            SurfaceWindGustKnots = 52.0,
            TemperatureCelsius = 25.0,
            DewpointCelsius = 22.0,
            PressureHpa = 992.0,
            VisibilityMeters = 1600.0,
            Precipitation = PrecipitationType.Rain,
            PrecipitationRateMmHr = 55.0,
            Thunderstorm = true,
            ConvectiveAvailablePotentialEnergy = 3850.0,
            LiftedIndex = -7.8,
            TurbulenceIntensity = 0.95,
            IcingSeverity = 0.85,
            CloudDecks = new List<SandboxCloudDeck>
            {
                new()
                {
                    BaseFeetAgl = 1200,
                    TopFeetAgl = 42000,
                    CoveragePercent = 100,
                    Type = CloudType.CB,
                    Density = 0.95,
                    Scattering = 0.85
                }
            },
            WindsAloft = new List<SandboxWindLayer>
            {
                new() { AltitudeFeet = 3000, DirectionDegrees = 260, SpeedKnots = 38, TemperatureCelsius = 18 },
                new() { AltitudeFeet = 6000, DirectionDegrees = 250, SpeedKnots = 48, TemperatureCelsius = 11 },
                new() { AltitudeFeet = 12000, DirectionDegrees = 240, SpeedKnots = 65, TemperatureCelsius = -3 },
                new() { AltitudeFeet = 24000, DirectionDegrees = 245, SpeedKnots = 85, TemperatureCelsius = -28 },
                new() { AltitudeFeet = 34000, DirectionDegrees = 250, SpeedKnots = 110, TemperatureCelsius = -49 }
            }
        };
    }

    public static SandboxWeatherScenario CreateMountainWaveCat()
    {
        return new SandboxWeatherScenario
        {
            Id = "mountain_wave",
            Name = "Mountain Wave & Clear Air Turb",
            Description = "Severe Clear Air Turbulence (CAT) and mountain wave oscillations near high ridges, with 120kt jet core.",
            SurfaceWindDirection = 280.0,
            SurfaceWindSpeedKnots = 20.0,
            SurfaceWindGustKnots = 34.0,
            TemperatureCelsius = 10.0,
            DewpointCelsius = -6.0,
            PressureHpa = 1012.0,
            VisibilityMeters = 35000.0,
            TurbulenceIntensity = 0.90,
            IcingSeverity = 0.15,
            CloudDecks = new List<SandboxCloudDeck>
            {
                new()
                {
                    BaseFeetAgl = 14000,
                    TopFeetAgl = 17000,
                    CoveragePercent = 45,
                    Type = CloudType.BKN,
                    Density = 0.6,
                    Scattering = 0.6
                }
            },
            WindsAloft = new List<SandboxWindLayer>
            {
                new() { AltitudeFeet = 6000, DirectionDegrees = 275, SpeedKnots = 35, TemperatureCelsius = 0 },
                new() { AltitudeFeet = 12000, DirectionDegrees = 280, SpeedKnots = 60, TemperatureCelsius = -12 },
                new() { AltitudeFeet = 18000, DirectionDegrees = 285, SpeedKnots = 82, TemperatureCelsius = -24 },
                new() { AltitudeFeet = 24000, DirectionDegrees = 285, SpeedKnots = 105, TemperatureCelsius = -36 },
                new() { AltitudeFeet = 34000, DirectionDegrees = 290, SpeedKnots = 135, TemperatureCelsius = -52 }
            }
        };
    }

    public static SandboxWeatherScenario CreateSevereIcing()
    {
        return new SandboxWeatherScenario
        {
            Id = "severe_icing",
            Name = "Severe Structural Icing",
            Description = "Freezing cloud layer (-4°C to -15°C) with dense supercooled water droplets causing rapid airframe ice accretion.",
            SurfaceWindDirection = 40.0,
            SurfaceWindSpeedKnots = 14.0,
            SurfaceWindGustKnots = 22.0,
            TemperatureCelsius = -2.0,
            DewpointCelsius = -3.0,
            PressureHpa = 1016.0,
            VisibilityMeters = 3200.0,
            Precipitation = PrecipitationType.FreezingRain,
            PrecipitationRateMmHr = 4.0,
            TurbulenceIntensity = 0.35,
            IcingSeverity = 0.95,
            CloudDecks = new List<SandboxCloudDeck>
            {
                new()
                {
                    BaseFeetAgl = 1200,
                    TopFeetAgl = 9500,
                    CoveragePercent = 100,
                    Type = CloudType.ST,
                    Density = 0.85,
                    Scattering = 0.3
                }
            },
            WindsAloft = new List<SandboxWindLayer>
            {
                new() { AltitudeFeet = 2000, DirectionDegrees = 45, SpeedKnots = 18, TemperatureCelsius = -5 },
                new() { AltitudeFeet = 5000, DirectionDegrees = 50, SpeedKnots = 26, TemperatureCelsius = -9 },
                new() { AltitudeFeet = 9000, DirectionDegrees = 65, SpeedKnots = 32, TemperatureCelsius = -16 }
            }
        };
    }

    public static SandboxWeatherScenario CreateClearAndCalm()
    {
        return new SandboxWeatherScenario
        {
            Id = "clear_calm",
            Name = "CAVOK / Fair Weather",
            Description = "Ceiling and Visibility OK. Gentle 4-knot breeze, mild temperatures, crystal clear skies.",
            SurfaceWindDirection = 220.0,
            SurfaceWindSpeedKnots = 4.0,
            SurfaceWindGustKnots = null,
            TemperatureCelsius = 22.0,
            DewpointCelsius = 11.0,
            PressureHpa = 1013.25,
            VisibilityMeters = 50000.0,
            Precipitation = PrecipitationType.None,
            PrecipitationRateMmHr = 0,
            TurbulenceIntensity = 0.0,
            IcingSeverity = 0.0,
            CloudDecks = new List<SandboxCloudDeck>(),
            WindsAloft = new List<SandboxWindLayer>
            {
                new() { AltitudeFeet = 3000, DirectionDegrees = 230, SpeedKnots = 6, TemperatureCelsius = 16 },
                new() { AltitudeFeet = 6000, DirectionDegrees = 240, SpeedKnots = 10, TemperatureCelsius = 10 },
                new() { AltitudeFeet = 12000, DirectionDegrees = 250, SpeedKnots = 16, TemperatureCelsius = -2 }
            }
        };
    }

    public static IReadOnlyList<SandboxWeatherScenario> GetDefaultPresets()
    {
        return new List<SandboxWeatherScenario>
        {
            CreateCat3Fog(),
            CreateCrosswindLanding(),
            CreateSupercellThunderstorm(),
            CreateMountainWaveCat(),
            CreateSevereIcing(),
            CreateClearAndCalm()
        };
    }
}

public class SandboxCloudDeck
{
    public double BaseFeetAgl { get; set; } = 2500;
    public double TopFeetAgl { get; set; } = 5000;
    public double CoveragePercent { get; set; } = 75; // 0..100%
    public CloudType Type { get; set; } = CloudType.BKN;
    public double Density { get; set; } = 0.5; // 0..1
    public double Scattering { get; set; } = 0.5; // 0..1
}

public class SandboxWindLayer
{
    public double AltitudeFeet { get; set; }
    public double DirectionDegrees { get; set; }
    public double SpeedKnots { get; set; }
    public double TemperatureCelsius { get; set; }
}
