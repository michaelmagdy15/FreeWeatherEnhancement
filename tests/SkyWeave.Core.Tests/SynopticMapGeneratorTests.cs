using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class SynopticMapGeneratorTests
{
    private readonly SynopticMapGenerator _generator = new();

    [Fact]
    public void Generate_ProducesIsobars_AtFourHpaIntervals()
    {
        // KJFK 40.64, -73.78, 1016 hPa, 270 deg, 15 kt
        var result = _generator.Generate(40.64, -73.78, 1016.0, 270.0, 15.0, null, 100.0, 768.0, 768.0);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Isobars);

        // Every isobar value should be a multiple of 4 hPa
        foreach (var iso in result.Isobars)
        {
            Assert.True(iso.PressureHpa % 4 == 0, $"Isobar pressure {iso.PressureHpa} should be a multiple of 4 hPa");
            Assert.NotEmpty(iso.SvgPath);
            Assert.StartsWith("M", iso.SvgPath);
            Assert.Contains(iso.PressureHpa.ToString(), iso.Label);
        }
    }

    [Fact]
    public void Generate_DetectsHighAndLowPressureCenters()
    {
        // 1024 hPa should generate High pressure center
        var resultHigh = _generator.Generate(40.64, -73.78, 1024.0, 270.0, 10.0, null, 150.0);
        Assert.Contains(resultHigh.Centers, pc => pc.Type == "H");

        // 996 hPa should generate Low pressure center
        var resultLow = _generator.Generate(40.64, -73.78, 996.0, 090.0, 30.0, null, 150.0);
        Assert.Contains(resultLow.Centers, pc => pc.Type == "L");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(50)]
    [InlineData(65)]
    public void Generate_GeneratesValidWindBarbs_ForVariousSpeeds(double speedKt)
    {
        var result = _generator.Generate(40.64, -73.78, 1013.25, 180.0, speedKt, null, 50.0);

        Assert.NotEmpty(result.Barbs);
        var centerBarb = result.Barbs[0];
        Assert.Equal(180.0, centerBarb.DirectionDegrees);
        Assert.Equal(speedKt, centerBarb.SpeedKnots);
        Assert.NotEmpty(centerBarb.SvgPath);

        if (speedKt < 2.5)
        {
            // Calm ring has 'a' for relative SVG arc
            Assert.Contains("a", centerBarb.SvgPath);
        }
        else
        {
            // Moving barb has line 'L' elements
            Assert.Contains("L", centerBarb.SvgPath);
            if (speedKt >= 50.0)
            {
                // 50kt pennant has closed 'Z' polygon
                Assert.Contains("Z", centerBarb.SvgPath);
            }
        }
    }

    [Fact]
    public void Generate_FromWeatherState_ProducesCohesiveSynopticMap()
    {
        var state = new WeatherState
        {
            Latitude = 51.47,
            Longitude = -0.45,
            AltimeterHpa = 1008.0,
            WindDirectionDegrees = 240.0,
            WindSpeedKnots = 18.0
        };

        var result = _generator.Generate(state, 100.0);

        Assert.NotNull(result);
        Assert.Equal(51.47, result.CenterLatitude);
        Assert.Equal(-0.45, result.CenterLongitude);
        Assert.Equal(100.0, result.RangeNm);
        Assert.NotEmpty(result.Barbs);
        Assert.NotEmpty(result.Isobars);
    }
}
