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
using SkyWeave.Core.Plugins;
using Xunit;

namespace SkyWeave.Api.Tests;

public class DispatchAndPluginEndpointTests : IAsyncLifetime
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
        _baseAddress = addressesFeature?.Addresses.First() ?? "http://127.0.0.1:54178";

        _client = new HttpClient { BaseAddress = new Uri(_baseAddress) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetDispatchBriefing_WhenNoBriefing_ReturnsBadRequest()
    {
        _testProvider.MockBriefing = null;

        var res = await _client.GetAsync("/api/dispatch/briefing");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.Contains("No flight plan loaded", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetDispatchBriefing_WhenBriefingAvailable_Returns200Ok()
    {
        _testProvider.MockBriefing = new DispatchBriefing
        {
            FlightNumber = "BAW142",
            AircraftType = "B772",
            OriginIcao = "EGLL",
            DestinationIcao = "KJFK",
            CruiseAltitudeFt = 38000,
            EstimatedTimeEnrouteMinutes = 430,
            RouteString = "EGLL CPT UL9 KENET UN460 UNLIT KJFK"
        };

        var res = await _client.GetAsync("/api/dispatch/briefing");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.Equal("BAW142", doc.RootElement.GetProperty("flightNumber").GetString());
        Assert.Equal("EGLL", doc.RootElement.GetProperty("originIcao").GetString());
        Assert.Equal("KJFK", doc.RootElement.GetProperty("destinationIcao").GetString());
    }

    [Fact]
    public async Task GetDispatchBriefingHtml_AndBriefingRoot_ReturnsHtml()
    {
        _testProvider.MockBriefingHtml = "<!DOCTYPE html><html><body><h1>SkyWeave Dispatch Release</h1></body></html>";

        var res1 = await _client.GetAsync("/api/dispatch/briefing/html");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        Assert.Equal("text/html", res1.Content.Headers.ContentType?.MediaType);
        var html1 = await res1.Content.ReadAsStringAsync();
        Assert.Contains("SkyWeave Dispatch Release", html1);

        var res2 = await _client.GetAsync("/briefing");
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        Assert.Equal("text/html", res2.Content.Headers.ContentType?.MediaType);
        var html2 = await res2.Content.ReadAsStringAsync();
        Assert.Contains("SkyWeave Dispatch Release", html2);
    }

    [Fact]
    public async Task GetPlugins_ReturnsInstalledPlugins()
    {
        _testProvider.MockPlugins = new List<PluginInfo>
        {
            new PluginInfo
            {
                PluginId = "radar.nexrad.hd",
                PluginName = "NEXRAD High-Res Radar",
                Version = "1.2.0",
                Author = "WeatherTeam",
                Description = "High resolution Doppler radar data",
                IsEnabled = true,
                Status = "Loaded"
            }
        };

        var res = await _client.GetAsync("/api/plugins");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());

        var p = doc.RootElement.GetProperty("plugins").EnumerateArray().First();
        Assert.Equal("radar.nexrad.hd", p.GetProperty("id").GetString());
        Assert.Equal("NEXRAD High-Res Radar", p.GetProperty("name").GetString());
        Assert.True(p.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task PostPluginToggle_TogglesPluginState()
    {
        _testProvider.MockPlugins = new List<PluginInfo>
        {
            new PluginInfo
            {
                PluginId = "thermal.soaring",
                PluginName = "Thermal Soaring Engine",
                IsEnabled = true,
                Status = "Loaded"
            }
        };

        var toggleReq = new { pluginId = "thermal.soaring", enabled = false };
        var res = await _client.PostAsJsonAsync("/api/plugins/toggle", toggleReq);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
        Assert.False(_testProvider.MockPlugins[0].IsEnabled);
    }

    [Fact]
    public async Task GetWxr_ReturnsAirborneTiltRadarSweepAndVerticalProfile()
    {
        var res = await _client.GetAsync("/api/wxr?tilt=1.5&range=40&mode=1&gain=0");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.Equal(1, doc.RootElement.GetProperty("mode").GetInt32());
        Assert.Equal(40.0, doc.RootElement.GetProperty("rangeNm").GetDouble());
        Assert.Equal(1.5, doc.RootElement.GetProperty("tiltDegrees").GetDouble());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("sweepBins").ValueKind);
        Assert.True(doc.RootElement.GetProperty("verticalProfile").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetFlightWatch_AndSquelchWav_ReturnsBroadcastAndValidRiffAudio()
    {
        var res = await _client.GetAsync("/api/flightwatch?channel=1&com1=122.000");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.Equal("122.000 MHz", doc.RootElement.GetProperty("frequencyDisplay").GetString());
        Assert.Contains("Kilo Juliet Foxtrot Kilo", doc.RootElement.GetProperty("spokenPhoneticScript").GetString());

        var wavRes = await _client.GetAsync("/api/flightwatch/squelch.wav");
        Assert.Equal(HttpStatusCode.OK, wavRes.StatusCode);
        Assert.Equal("audio/wav", wavRes.Content.Headers.ContentType?.MediaType);
        var wavBytes = await wavRes.Content.ReadAsByteArrayAsync();
        Assert.True(wavBytes.Length > 44);
        Assert.Equal((byte)'R', wavBytes[0]);
        Assert.Equal((byte)'I', wavBytes[1]);
        Assert.Equal((byte)'F', wavBytes[2]);
        Assert.Equal((byte)'F', wavBytes[3]);
    }

    [Fact]
    public async Task GetChallenges_GetPostFlight_AndGetOverlay_ReturnExpectedPayloads()
    {
        var chRes = await _client.GetAsync("/api/challenges");
        Assert.Equal(HttpStatusCode.OK, chRes.StatusCode);
        var chDoc = await chRes.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(chDoc);
        Assert.True(chDoc.RootElement.GetProperty("famousHistoricalStorms").GetArrayLength() >= 6);

        var pfRes = await _client.GetAsync("/api/postflight");
        Assert.Equal(HttpStatusCode.OK, pfRes.StatusCode);
        var pfDoc = await pfRes.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(pfDoc);
        Assert.Contains("SkyWeave v0.8.0", pfDoc.RootElement.GetProperty("shareableMarkdownCard").GetString());

        var ovRes = await _client.GetAsync("/overlay");
        Assert.Equal(HttpStatusCode.OK, ovRes.StatusCode);
        Assert.Equal("text/html", ovRes.Content.Headers.ContentType?.MediaType);
        var ovHtml = await ovRes.Content.ReadAsStringAsync();
        Assert.Contains("SkyWeave OBS Streamer HUD", ovHtml);
    }
}

