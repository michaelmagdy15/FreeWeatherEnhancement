using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class TurbulenceCalculatorTests
{
    private readonly TurbulenceCalculator _calc = new();

    [Fact]
    public void CalculateTurbulenceLayers_NoInputs_ReturnsEmpty()
    {
        var layers = _calc.CalculateTurbulenceLayers(new(), new(), new(), 0);
        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateTurbulenceLayers_HighWindShear_ReturnsMechanicalTurbulence()
    {
        var winds = new List<WindLayer>
        {
            new() { AltitudeFeet = 0, SpeedKnots = 10, DirectionDegrees = 270 },
            new() { AltitudeFeet = 5000, SpeedKnots = 60, DirectionDegrees = 180 }
        };
        var clouds = new List<CloudLayer>();

        var layers = _calc.CalculateTurbulenceLayers(winds, clouds, new(), 0);
        Assert.NotEmpty(layers);
        Assert.Equal(TurbulenceType.Mechanical, layers[0].Type);
    }

    [Fact]
    public void CalculateTurbulenceLayers_Cumulonimbus_ReturnsThermalTurbulence()
    {
        var winds = new List<WindLayer>();
        var clouds = new List<CloudLayer>
        {
            new() { BaseFeetAgl = 5000, TopFeetAgl = 40000, BaseMeters = 5000 * WeatherUnits.FeetToMeters, TopMeters = 40000 * WeatherUnits.FeetToMeters, Type = CloudType.CB, Density = 0.9 }
        };

        var layers = _calc.CalculateTurbulenceLayers(winds, clouds, new(), 0);
        Assert.NotEmpty(layers);
        Assert.Equal(TurbulenceType.Thermal, layers[0].Type);
    }

    [Fact]
    public void CalculateTurbulenceLayers_StormCell_ReturnsConvectiveTurbulence()
    {
        var winds = new List<WindLayer>();
        var clouds = new List<CloudLayer>();
        var storms = new List<StormCell>
        {
            new() { Intensity = 0.8, AltitudeFeet = 15000 }
        };

        var layers = _calc.CalculateTurbulenceLayers(winds, clouds, storms, 0);
        Assert.NotEmpty(layers);
        Assert.Equal(TurbulenceType.Convective, layers[0].Type);
    }

    [Fact]
    public void CalculateTurbulenceIndex_HighGusts_ReturnsHighIndex()
    {
        var index = _calc.CalculateTurbulenceIndex(
            windSpeedKnots: 30,
            windShear: 40,
            inCloud: true,
            nearStorm: false,
            gustSpeed: 25);

        Assert.True(index > 0.3);
    }

    [Fact]
    public void CalculateTurbulenceIndex_CalmConditions_ReturnsLowIndex()
    {
        var index = _calc.CalculateTurbulenceIndex(
            windSpeedKnots: 5,
            windShear: 0,
            inCloud: false,
            nearStorm: false,
            gustSpeed: null);

        Assert.Equal(0.0, index);
    }
}
