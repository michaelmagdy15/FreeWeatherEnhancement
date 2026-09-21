using System.Xml.Linq;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class AltitudeReferenceTests
{
    [Fact]
    public void IcingAndCloudTurbulence_UseMslHeights()
    {
        var cloud = new CloudLayer { BaseMeters = 6000 * 0.3048, TopMeters = 8000 * 0.3048,
            BaseFeetAgl = 1000, TopFeetAgl = 3000, Density = 0.9, Type = CloudType.CB };
        var winds = new List<WindLayer> {
            new() { AltitudeFeet = 1000, TemperatureCelsius = 10 },
            new() { AltitudeFeet = 6000, TemperatureCelsius = -10 },
            new() { AltitudeFeet = 8000, TemperatureCelsius = -15 } };
        var ice = Assert.Single(new IcingCalculator().CalculateIcingLayers(new() { cloud }, winds));
        Assert.Equal(6000, ice.BaseFeet, 6);
        Assert.Equal(8000, ice.TopFeet, 6);
        var turbulence = Assert.Single(new TurbulenceCalculator().CalculateTurbulenceLayers(new(), new() { cloud }, new(), 6000));
        Assert.Equal(6000, turbulence.BaseFeet, 6);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(5434)]
    public void ObservedClouds_ConvertStationRelativeBasesToMsl(double elevation)
    {
        var metar = new MetarData { Clouds = new() { new() { BaseFeet = 1000, Coverage = "BKN", Type = "BKN" } } };
        var cloud = Assert.Single(new CloudLayerBuilder().BuildCloudLayers(metar, null, elevation));
        Assert.Equal((elevation + 1000) * 0.3048, cloud.BaseMeters, 6);
        Assert.Equal(1000, cloud.BaseFeetAgl);
        var xml = XDocument.Parse(new WprGenerator().GenerateWprXml(new WeatherState { CloudLayers = new() { cloud } }));
        Assert.Equal(Math.Round((elevation + 1000) * 0.3048), (double)xml.Descendants("CloudLayerAltitudeBot").Single().Attribute("Value")!);
    }

    [Fact]
    public void MergedClouds_KeepStationRelativeDisplayHeights()
    {
        var metar = new MetarData { Clouds = new() {
            new() { BaseFeet = 1000, Coverage = "BKN", Type = "BKN" },
            new() { BaseFeet = 2000, Coverage = "OVC", Type = "OVC" } } };
        var cloud = Assert.Single(new CloudLayerBuilder().BuildCloudLayers(metar, null, 5434));
        Assert.Equal(1000, cloud.BaseFeetAgl, 6);
        Assert.Equal(6000, cloud.TopFeetAgl, 6);
        Assert.Equal(6434 * 0.3048, cloud.BaseMeters, 6);
    }

    [Fact]
    public void ModelClouds_RemainMslAndExcludeBelowStationLevels()
    {
        var winds = new WindsAloftData { PressureLevels = new() {
            new() { AltitudeMeters = 1000, GeopotentialHeightMeters = 1000, CloudCoverPercent = 90 },
            new() { AltitudeMeters = 5000, GeopotentialHeightMeters = 5000, CloudCoverPercent = 90 } } };
        var cloud = Assert.Single(new CloudLayerBuilder().BuildCloudLayers(new MetarData(), winds, 5434));
        Assert.Equal(4500, cloud.BaseMeters);
        Assert.Equal(4500 / 0.3048 - 5434, cloud.BaseFeetAgl, 6);
    }

    [Fact]
    public void ElevatedSurfaceWind_IsAnchoredAtStationAndProtectedFromBoosts()
    {
        var metar = new MetarData { WindSpeedKnots = 15, WindGustKnots = 25, WindDirectionDegrees = 210 };
        var profile = new WindsAloftData { PressureLevels = new() {
            new() { AltitudeFeet = 4000, AltitudeMeters = 1219.2, GeopotentialHeightMeters = 1219.2 },
            new() { AltitudeFeet = 10000, AltitudeMeters = 3048, GeopotentialHeightMeters = 3048, WindSpeedKnots = 40 } } };
        var winds = new WindLayerBuilder().BuildWindLayers(metar, profile, 5434);
        Assert.Equal(5434, winds[0].AltitudeFeet);
        Assert.Equal(2, winds.Count);
        var state = new WeatherState { WindsAloft = winds };
        new AtmosphericModeler().ApplySpatialGridAndThermals(state, 0, (14 - DateTime.UtcNow.TimeOfDay.TotalHours) * 15);
        Assert.Equal(25, winds[0].GustSpeedKnots);
        var xml = XDocument.Parse(new WprGenerator().GenerateWprXml(state, 20));
        Assert.Equal("25", xml.Descendants("WindLayer").First().Element("GustWave")?.Element("GustWaveSpeed")?.Attribute("Value")?.Value);
    }
}
