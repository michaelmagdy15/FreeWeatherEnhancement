using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class SoundingGeneratorTests
{
    private readonly SoundingGenerator _generator = new();

    [Fact]
    public void Generate_ProducesSmoothTemperatureAndDewpointCurves()
    {
        var state = new WeatherState
        {
            TemperatureCelsius = 20.0,
            DewpointCelsius = 14.0,
            FreezingLevelFeet = 11000.0,
            WindsAloft = new()
            {
                new WindLayer { AltitudeMeters = 1000, DirectionDegrees = 270, SpeedKnots = 15, TemperatureCelsius = 15 },
                new WindLayer { AltitudeMeters = 3000, DirectionDegrees = 280, SpeedKnots = 35, TemperatureCelsius = 0 },
                new WindLayer { AltitudeMeters = 9000, DirectionDegrees = 300, SpeedKnots = 75, TemperatureCelsius = -45 }
            }
        };

        var result = _generator.Generate(state, aircraftAltitudeFeet: 5000.0, canvasWidth: 360.0, canvasHeight: 240.0);

        Assert.NotNull(result);
        Assert.NotEmpty(result.TemperaturePath);
        Assert.StartsWith("M", result.TemperaturePath);
        Assert.Contains("L", result.TemperaturePath);

        Assert.NotEmpty(result.DewpointPath);
        Assert.StartsWith("M", result.DewpointPath);
        Assert.Contains("L", result.DewpointPath);

        Assert.True(result.FreezingLevelFeet > 0);
        Assert.True(result.FreezingLevelY > 0 && result.FreezingLevelY < 240.0);
        Assert.Equal(5000.0, result.AircraftAltitudeFeet);
        Assert.NotNull(result.AircraftY);
    }

    [Fact]
    public void Generate_IncludesCloudBlocks_MappedToCorrectPixelHeights()
    {
        var state = new WeatherState
        {
            TemperatureCelsius = 15.0,
            DewpointCelsius = 10.0,
            CloudLayers = new()
            {
                new CloudLayer
                {
                    BaseMeters = 1000,
                    TopMeters = 2500,
                    BaseFeetAgl = 3280,
                    TopFeetAgl = 8200,
                    CoveragePercent = 0.75,
                    Density = 0.8,
                    Type = CloudType.BKN
                }
            }
        };

        var result = _generator.Generate(state, null, 360.0, 240.0);

        Assert.Single(result.CloudBlocks);
        var block = result.CloudBlocks[0];
        Assert.True(block.BaseAltitudeFeet > 3000 && block.BaseAltitudeFeet < 3500);
        Assert.True(block.TopAltitudeFeet > 8000 && block.TopAltitudeFeet < 8500);
        Assert.True(block.Height > 0);
        Assert.True(block.Opacity > 0);
        Assert.Contains("BKN", block.Label);
    }

    [Fact]
    public void Generate_IncludesHazardBands_ForIcingAndTurbulence()
    {
        var state = new WeatherState
        {
            TemperatureCelsius = 10.0,
            DewpointCelsius = 8.0,
            IcingLayers = new()
            {
                new IcingLayer
                {
                    BaseFeet = 6000,
                    TopFeet = 12000,
                    Severity = IcingSeverity.Moderate,
                    IcingType = IcingType.Mixed
                }
            },
            TurbulenceLayers = new()
            {
                new TurbulenceLayer
                {
                    BaseFeet = 18000,
                    TopFeet = 24000,
                    Intensity = TurbulenceIntensity.Severe,
                    Type = TurbulenceType.Convective
                }
            }
        };

        var result = _generator.Generate(state, null, 360.0, 240.0);

        Assert.Equal(2, result.HazardBands.Count);
        Assert.Contains(result.HazardBands, h => h.Type == "ICING" && h.Severity == "Moderate");
        Assert.Contains(result.HazardBands, h => h.Type == "TURB" && h.Severity == "Severe");
    }

    [Fact]
    public void Generate_ProvidesStandardFlightLevels_ForAviationProfile()
    {
        var state = new WeatherState { TemperatureCelsius = 15.0, DewpointCelsius = 10.0 };
        var result = _generator.Generate(state, null, 360.0, 240.0);

        Assert.NotEmpty(result.StandardLevels);
        Assert.Contains(result.StandardLevels, lvl => lvl.FlightLevelName == "FL100");
        Assert.Contains(result.StandardLevels, lvl => lvl.FlightLevelName == "FL180");
        Assert.Contains(result.StandardLevels, lvl => lvl.FlightLevelName == "FL300");
        Assert.Contains(result.StandardLevels, lvl => lvl.FlightLevelName == "FL390");
    }
}
