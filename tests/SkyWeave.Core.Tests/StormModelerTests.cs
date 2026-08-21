using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class StormModelerTests
{
    private readonly StormModeler _modeler = new();

    [Theory]
    [InlineData(4000.0, 0.25)]
    [InlineData(2500.0, 0.20)]
    [InlineData(1500.0, 0.15)]
    [InlineData(1000.0, 0.10)]
    [InlineData(500.0, 0.05)]
    [InlineData(100.0, 0.0)]
    [InlineData(null, 0.0)]
    public void CalculateCapeBoost_ReturnsExpectedBoost(double? cape, double expected)
    {
        Assert.Equal(expected, _modeler.CalculateCapeBoost(cape), 3);
    }

    [Fact]
    public void ModelStorms_ClusteredStrikesWithCape_IntensityHigherThanWithoutCape()
    {
        var strikes = new List<LightningStrike>
        {
            new() { Latitude = 40.0, Longitude = -74.0, Timestamp = DateTime.UtcNow },
            new() { Latitude = 40.01, Longitude = -74.01, Timestamp = DateTime.UtcNow },
            new() { Latitude = 40.02, Longitude = -74.02, Timestamp = DateTime.UtcNow }
        };

        var withoutCape = _modeler.ModelStorms(strikes, new List<WeatherHazard>(), 40.0, -74.0, default, null);
        var windsWithCape = new WindsAloftData { ConvectiveAvailablePotentialEnergy = 4000 };
        var withCape = _modeler.ModelStorms(strikes, new List<WeatherHazard>(), 40.0, -74.0, default, windsWithCape);

        Assert.NotEmpty(withCape);
        Assert.True(withCape[0].Intensity > withoutCape[0].Intensity);
    }
}