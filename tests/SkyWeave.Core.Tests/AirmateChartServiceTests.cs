using System.Linq;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class AirmateChartServiceTests
{
    [Fact]
    public void GetAirmateAirportUrl_ValidIcao_ReturnsCorrectUrl()
    {
        var url = AirmateChartService.GetAirmateAirportUrl("kjfk");
        Assert.Equal("https://fly.airmate.aero/#/aerodrome/KJFK", url);
    }

    [Fact]
    public void GetChartFoxAirportUrl_ValidIcao_ReturnsCorrectUrl()
    {
        var url = AirmateChartService.GetChartFoxAirportUrl("egll");
        Assert.Equal("https://chartfox.org/EGLL", url);
    }

    [Fact]
    public void GetFaaDtppUrl_UsAirport_ReturnsFaaUrl()
    {
        var url = AirmateChartService.GetFaaDtppUrl("KORD");
        Assert.NotNull(url);
        Assert.Contains("cycle=current&ident=KORD", url);
    }

    [Fact]
    public void GetFaaDtppUrl_EuropeanAirport_ReturnsNull()
    {
        var url = AirmateChartService.GetFaaDtppUrl("LFPG");
        Assert.Null(url);
    }

    [Fact]
    public void GetPrimaryChartUrl_AirmateProvider_ReturnsAirmateUrl()
    {
        var url = AirmateChartService.GetPrimaryChartUrl("EDDF", "Airmate");
        Assert.Equal("https://fly.airmate.aero/#/aerodrome/EDDF", url);
    }

    [Fact]
    public void GetPrimaryChartUrl_NavigraphProvider_ReturnsNavigraphUrl()
    {
        var url = AirmateChartService.GetPrimaryChartUrl("EDDF", "Navigraph");
        Assert.Equal("https://charts.navigraph.com/airport/EDDF", url);
    }

    [Fact]
    public void GetMsfsPlannerUrl_ReturnsOfficialPlannerUrl()
    {
        var url = AirmateChartService.GetMsfsPlannerUrl();
        Assert.Equal("https://planner.flightsimulator.com/", url);
    }

    [Fact]
    public void GetStandardAirportCharts_GeneratesExpectedCategories()
    {
        var charts = AirmateChartService.GetStandardAirportCharts("KJFK", "Airmate");
        Assert.NotEmpty(charts);
        Assert.Contains(charts, c => c.Category == ChartCategory.AirportDiagram);
        Assert.Contains(charts, c => c.Category == ChartCategory.Approach);
        Assert.Contains(charts, c => c.Category == ChartCategory.Departure);
        Assert.Contains(charts, c => c.Category == ChartCategory.Arrival);
        Assert.Contains(charts, c => c.Category == ChartCategory.VisualApproach);
        Assert.All(charts, c => Assert.True(c.IsFree));
    }

    [Theory]
    [InlineData("", "password123", false)]
    [InlineData("ab", "password123", false)]
    [InlineData("user@example.com", "", false)]
    [InlineData("pilot_free", "securePass123", true)]
    public void ValidateAccountDetails_ValidatesProperly(string username, string password, bool expectedValid)
    {
        var (isValid, message) = AirmateChartService.ValidateAccountDetails(username, password);
        Assert.Equal(expectedValid, isValid);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }
}
