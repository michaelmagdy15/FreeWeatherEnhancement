using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class IcingCalculatorTests
{
    private readonly IcingCalculator _calc = new();

    [Fact]
    public void CalculateIcingLayers_NoClouds_ReturnsEmpty()
    {
        var clouds = new List<CloudLayer>();
        var winds = new List<WindLayer>();
        var layers = _calc.CalculateIcingLayers(clouds, winds);
        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateIcingLayers_CloudsBelowFreezing_ReturnsIcing()
    {
        var clouds = new List<CloudLayer>
        {
            new() { BaseFeetAgl = 5000, TopFeetAgl = 8000, Type = CloudType.OVC, Density = 0.9 }
        };
        var winds = new List<WindLayer>
        {
            new() { AltitudeFeet = 6000, TemperatureCelsius = -8 }
        };

        var layers = _calc.CalculateIcingLayers(clouds, winds);
        Assert.NotEmpty(layers);
        Assert.Equal(IcingSeverity.Severe, layers[0].Severity);
    }

    [Fact]
    public void CalculateIcingLayers_CloudsAboveFreezing_NoIcing()
    {
        var clouds = new List<CloudLayer>
        {
            new() { BaseFeetAgl = 5000, TopFeetAgl = 8000, Type = CloudType.OVC, Density = 0.8 }
        };
        var winds = new List<WindLayer>
        {
            new() { AltitudeFeet = 6000, TemperatureCelsius = 5 }
        };

        var layers = _calc.CalculateIcingLayers(clouds, winds);
        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateIcingLayers_MultipleCloudLayers_ReturnsMultipleLayers()
    {
        var clouds = new List<CloudLayer>
        {
            new() { BaseFeetAgl = 3000, TopFeetAgl = 5000, Type = CloudType.BKN, Density = 0.6 },
            new() { BaseFeetAgl = 8000, TopFeetAgl = 12000, Type = CloudType.OVC, Density = 0.9 }
        };
        var winds = new List<WindLayer>
        {
            new() { AltitudeFeet = 4000, TemperatureCelsius = -5 },
            new() { AltitudeFeet = 10000, TemperatureCelsius = -12 }
        };

        var layers = _calc.CalculateIcingLayers(clouds, winds);
        Assert.Equal(2, layers.Count);
    }

    [Fact]
    public void CalculateIcingIndex_ReturnsZeroAboveFreezing()
    {
        var index = _calc.CalculateIcingIndex(5.0, true, 0.8);
        Assert.Equal(0.0, index);
    }

    [Fact]
    public void CalculateIcingIndex_ReturnsZeroBelowMinus40()
    {
        var index = _calc.CalculateIcingIndex(-45.0, true, 0.8);
        Assert.Equal(0.0, index);
    }

    [Fact]
    public void CalculateIcingIndex_PeaksAtMinus15()
    {
        var index = _calc.CalculateIcingIndex(-15.0, true, 1.0);
        Assert.Equal(1.0, index);
    }
}
