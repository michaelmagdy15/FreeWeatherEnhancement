using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class StationFinderTests
{
    [Fact]
    public void FindNearestAirport_ReturnsKdenForKdenRampPosition()
    {
        var airport = new StationFinder().FindNearestAirport(39.87725, -104.61984);

        Assert.NotNull(airport);
        Assert.Equal("KDEN", airport.IcaoId);
    }
}
