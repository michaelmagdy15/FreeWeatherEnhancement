using Xunit;

namespace SkyWeave.Core.Tests;

public class MeteorologyTests
{
    [Fact]
    public void CalculateRelativeHumidity_DewpointEqualsTemperature_Returns100()
    {
        Assert.Equal(100.0, Meteorology.CalculateRelativeHumidity(15.0, 15.0));
    }

    [Fact]
    public void CalculateRelativeHumidity_WarmDryAir_ReturnsModerateHumidity()
    {
        var rh = Meteorology.CalculateRelativeHumidity(25.0, 10.0);
        Assert.InRange(rh, 30, 45);
    }

    [Theory]
    [InlineData(-40.0)]
    [InlineData(-20.0)]
    [InlineData(0.0)]
    [InlineData(20.0)]
    [InlineData(40.0)]
    public void CalculateRelativeHumidity_AlwaysInRange(double temperature)
    {
        for (double dewpoint = -40; dewpoint <= 40; dewpoint += 5)
        {
            var rh = Meteorology.CalculateRelativeHumidity(temperature, dewpoint);
            Assert.InRange(rh, 0.0, 100.0);
        }
    }

    [Fact]
    public void CalculateRelativeHumidity_NaNInput_ReturnsZero()
    {
        Assert.Equal(0.0, Meteorology.CalculateRelativeHumidity(double.NaN, 10.0));
    }

    [Fact]
    public void CalculateRelativeHumidity_DewpointAboveTemperature_Returns100()
    {
        Assert.Equal(100.0, Meteorology.CalculateRelativeHumidity(10.0, 20.0));
    }
}