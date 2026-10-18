using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Api.Tests;

public class RunwayEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string _baseAddress = string.Empty;
    private TestWeatherDataProvider _testProvider = null!;

    public async Task InitializeAsync()
    {
        _testProvider = new TestWeatherDataProvider();

        _app = WeatherApiServer.BuildWebApplication(
            args: null,
            customProvider: _testProvider,
            customEngine: null,
            listenUrl: "http://127.0.0.1:0");

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        _baseAddress = addressesFeature?.Addresses.First() ?? "http://127.0.0.1:54181";

        _client = new HttpClient { BaseAddress = new Uri(_baseAddress) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetRunways_CurrentStation_ReturnsOkWithRunwayReport()
    {
        var res = await _client.GetAsync("/api/runways");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.TryGetProperty("stationId", out var stationElem));
        Assert.Equal("KJFK", stationElem.GetString());

        Assert.True(doc.TryGetProperty("recommendedRunway", out var recElem));
        Assert.NotNull(recElem.GetString());

        Assert.True(doc.TryGetProperty("runways", out var runwaysElem));
        Assert.Equal(JsonValueKind.Array, runwaysElem.ValueKind);
        Assert.True(runwaysElem.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetRunways_SpecificStation_ReturnsOkForRequestedIcao()
    {
        var res = await _client.GetAsync("/api/runways/EGLL");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.TryGetProperty("stationId", out var stationElem));
        Assert.Equal("EGLL", stationElem.GetString());

        Assert.True(doc.TryGetProperty("runways", out var runwaysElem));
        Assert.True(runwaysElem.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetRunways_WithCustomMockReport_ReturnsMockedData()
    {
        _testProvider.MockRunwayReport = new RunwayWindReport
        {
            StationId = "LOWI",
            WindDirectionDegrees = 260,
            WindSpeedKnots = 14,
            RecommendedRunway = "26",
            BestRunway = new RunwayAnalysis
            {
                Runway = "26",
                HeadingDegrees = 255,
                HeadwindKnots = 13.9,
                CrosswindKnots = 1.2,
                CrosswindSide = "Left",
                Suitability = RunwaySuitability.Recommended,
                SuitabilityText = "Recommended"
            },
            AdvisoryMessage = "Recommended RWY 26: 14 kt Headwind, 1 kt Left Crosswind"
        };

        var res = await _client.GetAsync("/api/runways/LOWI");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOWI", doc.GetProperty("stationId").GetString());
        Assert.Equal("26", doc.GetProperty("recommendedRunway").GetString());
    }
}
