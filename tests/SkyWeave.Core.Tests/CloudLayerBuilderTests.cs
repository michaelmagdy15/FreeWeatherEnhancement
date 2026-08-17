using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class CloudLayerBuilderTests
{
    private readonly CloudLayerBuilder _builder = new();

    private static MetarData CreateMetar()
    {
        return new MetarData
        {
            StationId = "KJFK",
            Clouds = new List<MetarCloud>
            {
                new() { Coverage = "BKN", BaseFeet = 2000, Type = "BKN" }
            }
        };
    }

    private static WindsAloftData CreateWindsAloft(double cloudCoverPercent, double relativeHumidity)
    {
        return new WindsAloftData
        {
            PressureLevels = new List<PressureLevelData>
            {
                new()
                {
                    PressureHpa = 850,
                    AltitudeMeters = 3000,
                    AltitudeFeet = 3000 * 3.28084,
                    CloudCoverPercent = cloudCoverPercent,
                    RelativeHumidity = relativeHumidity
                }
            }
        };
    }

    [Fact]
    public void BuildCloudLayers_CloudCoverHigh_CreatesLayerFromCloudCover()
    {
        var layers = _builder.BuildCloudLayers(CreateMetar(), CreateWindsAloft(cloudCoverPercent: 90, relativeHumidity: 0));

        Assert.Contains(layers, l => l.Density >= 0.6 && l.BaseMeters >= 2000);
    }

    [Fact]
    public void BuildCloudLayers_NoCloudCoverButHumidityHigh_CreatesLayerFromHumidityFallback()
    {
        var layers = _builder.BuildCloudLayers(CreateMetar(), CreateWindsAloft(cloudCoverPercent: 0, relativeHumidity: 90));

        Assert.Contains(layers, l => l.Density >= 0.6 && l.BaseMeters >= 2000);
    }

    [Fact]
    public void BuildCloudLayers_LowCloudCoverAndLowHumidity_OnlyMetarLayer()
    {
        var layers = _builder.BuildCloudLayers(CreateMetar(), CreateWindsAloft(cloudCoverPercent: 10, relativeHumidity: 10));

        Assert.Single(layers);
    }
}