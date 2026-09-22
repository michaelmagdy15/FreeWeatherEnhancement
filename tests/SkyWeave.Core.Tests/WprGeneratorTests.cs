using System.Xml.Linq;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class WprGeneratorTests
{
    private readonly WprGenerator _generator = new();

    [Fact]
    public void GenerateWprXml_IsStableForAnUnchangedWeatherState()
    {
        var state = new WeatherState
        {
            TemperatureCelsius = 12,
            PressureHpa = 1018,
            WindSpeedKnots = 8,
            WindDirectionDegrees = 230
        };

        Assert.Equal(_generator.GenerateWprXml(state), _generator.GenerateWprXml(state));
    }

    [Fact]
    public void GenerateWprXml_ReturnsValidXml()
    {
        var state = CreateTestState();
        var xml = _generator.GenerateWprXml(state);

        Assert.NotNull(xml);
        Assert.Contains("SimBase.Document", xml);
        Assert.Contains("WeatherPreset", xml);
    }

    [Fact]
    public void GenerateWprXml_ContainsCloudLayers()
    {
        var state = CreateTestState();
        state.CloudLayers = new List<CloudLayer>
        {
            new() { BaseMeters = 100, TopMeters = 300, Density = 0.5, Scattering = 0.3 }
        };

        var xml = _generator.GenerateWprXml(state);
        Assert.Contains("CloudLayer", xml);
        Assert.Contains("100", xml);
    }

    [Fact]
    public void GenerateWprXml_ContainsWindLayers()
    {
        var state = CreateTestState();
        state.WindsAloft = new List<WindLayer>
        {
            new() { AltitudeMeters = 3000, DirectionDegrees = 270, SpeedKnots = 25 }
        };

        var xml = _generator.GenerateWprXml(state);
        Assert.Contains("WindLayer", xml);
        Assert.Contains("270", xml);
    }

    [Fact]
    public void GenerateWprXml_ContainsAerosolDensity()
    {
        var state = CreateTestState();
        state.AerosolDensity = 0.5;

        var xml = _generator.GenerateWprXml(state);
        Assert.Contains("AerosolDensity", xml);
        Assert.Contains("0.500", xml);
    }

    [Fact]
    public void GenerateWprXml_ContainsPrecipitation()
    {
        var state = CreateTestState();
        state.PrecipitationRate = 5.0;

        var xml = _generator.GenerateWprXml(state);
        Assert.Contains("Precipitations", xml);
        Assert.Contains("5.000", xml);
    }

    [Fact]
    public void GenerateWprXml_ContainsThunderstormIntensity()
    {
        var state = CreateTestState();
        state.ThunderstormIntensity = 0.7;

        var xml = _generator.GenerateWprXml(state);
        Assert.Contains("ThunderstormIntensity", xml);
        Assert.Contains("0.700", xml);
    }

    [Fact]
    public void GenerateWprXml_UsesWeatherPresetSurfaceUnits()
    {
        var state = CreateTestState();
        var preset = XDocument.Parse(_generator.GenerateWprXml(state)).Descendants("WeatherPreset.Preset").Single();

        var pressure = preset.Element("MSLPressure")!;
        var temperature = preset.Element("MSLTemperature")!;

        Assert.Equal("pa", pressure.Attribute("Unit")!.Value);
        Assert.Equal("101325", pressure.Attribute("Value")!.Value);
        Assert.Equal("k", temperature.Attribute("Unit")!.Value);
        Assert.Equal("293.15", temperature.Attribute("Value")!.Value);
    }

    [Fact]
    public void GenerateWprXml_UsesSchemaGustWave()
    {
        var state = CreateTestState();
        state.WindsAloft = new List<WindLayer>
        {
            new() { AltitudeFeet = 2000, AltitudeMeters = 609.6, SpeedKnots = 10, DirectionDegrees = 270, GustSpeedKnots = 20 }
        };

        var xml = _generator.GenerateWprXml(state);

        Assert.Contains("GustWave", xml);
        Assert.DoesNotContain("WindLayerGusts", xml);
        Assert.Contains("GustWaveSpeed", xml);
        Assert.Contains("GustAngle", xml);
    }

    [Fact]
    public void GenerateWprXml_LimitsCloudLayersToMsfsMaximum()
    {
        var state = CreateTestState();
        state.CloudLayers = Enumerable.Range(0, 30)
            .Select(i => new CloudLayer
            {
                BaseMeters = i * 100,
                TopMeters = i * 100 + 100,
                Density = 0.5,
                Scattering = 0.2
            })
            .ToList();

        var cloudCount = XDocument.Parse(_generator.GenerateWprXml(state))
            .Descendants("CloudLayer")
            .Count();

        Assert.Equal(24, cloudCount);
    }

    [Fact]
    public void GenerateWprXml_WithTurbulenceGustBoost_IncludesGustSpeed()
    {
        var state = CreateTestState();
        state.WindsAloft = new List<WindLayer>
        {
            new() { AltitudeFeet = 2000, AltitudeMeters = 2000 * 0.3048, SpeedKnots = 10, DirectionDegrees = 270 }
        };

        var xml = _generator.GenerateWprXml(state, 12);
        var gustSpeed = XDocument.Parse(xml)
            .Descendants("GustWaveSpeed")
            .Single()
            .Attribute("Value")!.Value;

        Assert.True(double.Parse(gustSpeed) >= 20);
    }

    [Fact]
    public void GenerateWprXml_ZeroBoost_MatchesDefaultOutput()
    {
        var state = CreateTestState();
        Assert.Equal(_generator.GenerateWprXml(state), _generator.GenerateWprXml(state, 0));
    }

    private WeatherState CreateTestState()
    {
        return new WeatherState
        {
            StationId = "KJFK",
            TemperatureCelsius = 20.0,
            DewpointCelsius = 10.0,
            PressureHpa = 1013.25,
            AltimeterHpa = 1013.25,
            VisibilityMeters = 10000,
            FlightCategory = "VFR",
            WindDirectionDegrees = 270,
            WindSpeedKnots = 12,
            CloudLayers = new List<CloudLayer>(),
            WindsAloft = new List<WindLayer>(),
            Hazards = new List<WeatherHazard>(),
            PrecipitationRate = 0,
            ThunderstormIntensity = 0,
            AerosolDensity = 0.1
        };
    }
}
