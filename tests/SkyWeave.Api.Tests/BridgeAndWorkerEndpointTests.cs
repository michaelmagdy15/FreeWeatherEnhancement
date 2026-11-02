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

public class BridgeAndWorkerEndpointTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string _baseAddress = string.Empty;
    private TestWeatherDataProvider _testProvider = null!;

    public async Task InitializeAsync()
    {
        _testProvider = new TestWeatherDataProvider();

        WeatherApiServer.HotPatchScriptProvider = () => "console.log('hotpatch-test');";
        WeatherApiServer.BridgeDiagVerified = false;
        WeatherApiServer.BridgePanelPatched = false;

        _app = WeatherApiServer.BuildWebApplication(
            args: null,
            customProvider: _testProvider,
            customEngine: null,
            listenUrl: "http://127.0.0.1:0");

        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        _baseAddress = addressesFeature?.Addresses.First() ?? "http://127.0.0.1:54179";

        _client = new HttpClient { BaseAddress = new Uri(_baseAddress) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        WeatherApiServer.HotPatchScriptProvider = null;
        WeatherApiServer.BridgeDiagVerified = false;
        WeatherApiServer.BridgePanelPatched = false;
    }

    [Fact]
    public async Task GetBridgePatchJs_ReturnsJavaScriptContent()
    {
        var res = await _client.GetAsync("/api/bridge/patch.js");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/javascript", res.Content.Headers.ContentType?.MediaType);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("hotpatch-test", content);
    }

    [Fact]
    public async Task GetBridgeDiag_WithAppliedStatus_SetsVerified()
    {
        WeatherApiServer.BridgeDiagVerified = false;

        var res = await _client.GetAsync("/api/bridge/diag?stage=push-ok&status=APPLIED");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("verified").GetBoolean());
        Assert.True(WeatherApiServer.BridgeDiagVerified);
    }

    [Fact]
    public async Task GetV1WeatherStatus_ReturnsOkAndRunning()
    {
        var res = await _client.GetAsync("/api/v1/weather/status");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("running").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task GetV1WeatherDiag_WithPushOkStage_SetsVerified()
    {
        WeatherApiServer.BridgeDiagVerified = false;

        var res = await _client.GetAsync("/api/v1/weather/diag?stage=push-ok&applied=1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(WeatherApiServer.BridgeDiagVerified);
    }

    [Fact]
    public async Task GetV1WeatherCurrent_WhenNoState_Returns503()
    {
        _testProvider.MockSnapshot = new AircraftWeatherSnapshot
        {
            HasPositionFix = false,
            State = null
        };

        var res = await _client.GetAsync("/api/v1/weather/current");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    [Fact]
    public async Task GetV1WeatherCurrent_WithValidState_ReturnsStrataPayloadWith14WindLayers()
    {
        var state = new WeatherState
        {
            Latitude = 47.45,
            Longitude = -122.31,
            TemperatureCelsius = 15.0,
            AltimeterHpa = 1013.25,
            WindDirectionDegrees = 270.0,
            WindSpeedKnots = 12.0,
            VisibilityMeters = 5000,
            PrecipitationRate = 1.5,
            ThunderstormIntensity = 0.2,
            WindsAloft = new List<WindLayer>
            {
                new() { AltitudeMeters = 100.0, DirectionDegrees = 270.0, SpeedKnots = 12.0, IsSurfaceLayer = true },
                new() { AltitudeMeters = 1000.0, DirectionDegrees = 280.0, SpeedKnots = 20.0, IsSurfaceLayer = false }
            },
            CloudLayers = new List<CloudLayer>
            {
                new() { BaseMeters = 600.0, TopMeters = 1200.0, CoveragePercent = 50.0, Density = 0.8, IsConvective = false }
            }
        };

        _testProvider.MockSnapshot = new AircraftWeatherSnapshot
        {
            HasPositionFix = true,
            State = state
        };

        var res = await _client.GetAsync("/api/v1/weather/current");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(101325.0, root.GetProperty("qnhPa").GetDouble(), 1);
        Assert.True(root.GetProperty("temperatureC").GetDouble() >= 15.0);

        var windLayers = root.GetProperty("windLayers");
        Assert.Equal(14, windLayers.GetArrayLength());

        var cloudLayers = root.GetProperty("cloudLayers");
        Assert.True(cloudLayers.GetArrayLength() >= 1);
    }
}
