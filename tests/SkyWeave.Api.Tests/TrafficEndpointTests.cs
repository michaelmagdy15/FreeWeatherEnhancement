using System;
using System.Collections.Generic;
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

public class TrafficEndpointTests : IAsyncLifetime
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
        _baseAddress = addressesFeature?.Addresses.First() ?? "http://127.0.0.1:54175";

        _client = new HttpClient { BaseAddress = new Uri(_baseAddress) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetTraffic_Empty_ReturnsZeroCountAndNoWake()
    {
        _testProvider.MockTraffic.Clear();

        var response = await _client.GetAsync("/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("count").GetInt32());
        Assert.False(root.GetProperty("hasWakeEncounter").GetBoolean());
        Assert.Equal(0, root.GetProperty("traffic").GetArrayLength());
    }

    [Fact]
    public async Task GetTraffic_WithTraffic_ReturnsTrafficListAndAttributes()
    {
        _testProvider.MockTraffic = new List<AircraftTraffic>
        {
            new AircraftTraffic
            {
                Callsign = "BAW123",
                Latitude = 51.47,
                Longitude = -0.45,
                AltitudeFeet = 3500,
                HeadingDegrees = 270,
                SpeedKnots = 180,
                GroundSpeedKnots = 185,
                WeightClass = AircraftWeightClass.Heavy,
                OnGround = false,
                DistanceNm = 3.5,
                RelativeBearingDegrees = 15.0,
                AltitudeDeltaFeet = -500,
                IsInWakeZone = false
            },
            new AircraftTraffic
            {
                Callsign = "EZY456",
                Latitude = 51.48,
                Longitude = -0.42,
                AltitudeFeet = 1200,
                HeadingDegrees = 090,
                SpeedKnots = 140,
                GroundSpeedKnots = 140,
                WeightClass = AircraftWeightClass.Medium,
                OnGround = false,
                DistanceNm = 5.2,
                RelativeBearingDegrees = -30.0,
                AltitudeDeltaFeet = -1200,
                IsInWakeZone = false
            }
        };

        var response = await _client.GetAsync("/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("count").GetInt32());
        Assert.False(root.GetProperty("hasWakeEncounter").GetBoolean());

        var list = root.GetProperty("traffic").EnumerateArray().ToList();
        Assert.Equal(2, list.Count);

        var first = list[0];
        Assert.Equal("BAW123", first.GetProperty("callsign").GetString());
        Assert.Equal(3500, first.GetProperty("altitudeFeet").GetDouble());
        Assert.Equal("Heavy", first.GetProperty("weightClass").GetString());
        Assert.Equal(3.5, first.GetProperty("distanceNm").GetDouble());
        Assert.False(first.GetProperty("isInWakeZone").GetBoolean());
    }

    [Fact]
    public async Task GetTraffic_WithWakeZoneActive_ReturnsHasWakeEncounterTrue()
    {
        _testProvider.MockTraffic = new List<AircraftTraffic>
        {
            new AircraftTraffic
            {
                Callsign = "AFR001",
                Latitude = 49.0,
                Longitude = 2.5,
                AltitudeFeet = 4000,
                HeadingDegrees = 180,
                SpeedKnots = 210,
                GroundSpeedKnots = 205,
                WeightClass = AircraftWeightClass.Super,
                OnGround = false,
                DistanceNm = 1.2,
                RelativeBearingDegrees = 0,
                AltitudeDeltaFeet = -250,
                IsInWakeZone = true
            }
        };

        var response = await _client.GetAsync("/api/traffic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("count").GetInt32());
        Assert.True(root.GetProperty("hasWakeEncounter").GetBoolean());

        var traffic = root.GetProperty("traffic").EnumerateArray().First();
        Assert.Equal("AFR001", traffic.GetProperty("callsign").GetString());
        Assert.Equal("Super", traffic.GetProperty("weightClass").GetString());
        Assert.True(traffic.GetProperty("isInWakeZone").GetBoolean());
    }
}
