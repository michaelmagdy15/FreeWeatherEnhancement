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

    [Fact]
    public void BuildCloudLayers_WithFogPhenomenon_SynthesizesSurfaceDeck()
    {
        var metar = new MetarData
        {
            StationId = "EGLL",
            RawText = "EGLL 120650Z 00000KT 0300 FG VV/// 04/04 Q1022",
            VisibilityMeters = 300,
            WeatherConditions = new List<string> { "FG" }
        };

        var layers = _builder.BuildCloudLayers(metar, null, stationElevationFeet: 83);

        Assert.NotEmpty(layers);
        var surfaceFog = layers.First();
        Assert.Equal(0, surfaceFog.BaseFeetAgl);
        Assert.Equal(400, surfaceFog.TopFeetAgl);
        Assert.True(surfaceFog.Density >= 0.9);
        Assert.True(surfaceFog.Scattering <= 0.05);
        Assert.Equal(CloudType.ST, surfaceFog.Type);
    }

    [Fact]
    public void BuildCloudLayers_WithLowVisibility_SynthesizesSurfaceDeck()
    {
        var metar = new MetarData
        {
            StationId = "KJFK",
            RawText = "KJFK 120650Z 00000KT 1/2SM BR 10/10 A2992",
            VisibilityMeters = 800
        };

        var layers = _builder.BuildCloudLayers(metar, null, stationElevationFeet: 13);

        Assert.Contains(layers, l => l.BaseFeetAgl == 0 && l.Density >= 0.9);
    }

    [Fact]
    public void PrioritizeCloudLayers_PreservesAircraftEnclosingLayer()
    {
        var layers = new List<CloudLayer>
        {
            new() { BaseMeters = 300, TopMeters = 600, Density = 0.5 },    // ~1000-2000 ft
            new() { BaseMeters = 3000, TopMeters = 4000, Density = 0.6 },  // ~10000-13000 ft
            new() { BaseMeters = 7600, TopMeters = 8500, Density = 0.7 },  // ~25000-28000 ft
            new() { BaseMeters = 10600, TopMeters = 11500, Density = 0.4 } // ~35000-38000 ft
        };

        // Aircraft at FL260 (26,000 ft)
        var prioritized = CloudLayerBuilder.PrioritizeCloudLayers(layers, aircraftAltitudeFeet: 26000, maxSlots: 3);

        Assert.Equal(3, prioritized.Count);
        // Lowest layer must be preserved
        Assert.Equal(300, prioritized[0].BaseMeters);
        // Aircraft enclosing layer must be preserved
        Assert.Contains(prioritized, l => l.BaseMeters == 7600);
    }
}