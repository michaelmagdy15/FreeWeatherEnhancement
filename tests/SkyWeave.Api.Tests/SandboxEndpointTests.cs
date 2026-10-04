using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Api.Tests;

public class SandboxEndpointTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;
    private TestWeatherDataProvider? _provider;
    private const int TestPort = 54205;

    public async Task InitializeAsync()
    {
        _provider = new TestWeatherDataProvider();
        _app = WeatherApiServer.BuildWebApplication(
            args: null,
            customProvider: _provider,
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{TestPort}",
            allowPositionOverride: true,
            allowLanAccess: false,
            port: TestPort);

        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{TestPort}") };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetSandbox_ReturnsStatusAndAvailablePresets()
    {
        var response = await _client!.GetAsync("/api/sandbox");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("isSandboxMode", out var isSandbox));
        Assert.False(isSandbox.GetBoolean());

        Assert.True(json.TryGetProperty("availablePresets", out var presets));
        Assert.True(presets.GetArrayLength() >= 6);
    }

    [Fact]
    public async Task PostSandbox_WithEmptyBody_EnablesSandboxWithDefault()
    {
        var response = await _client!.PostAsync("/api/sandbox", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("isSandboxMode").GetBoolean());
        Assert.True(_provider!.IsSandboxMode);
    }

    [Fact]
    public async Task PostSandbox_WithScenario_AppliesScenario()
    {
        var scenario = new SandboxWeatherScenario
        {
            Name = "Custom Test Run",
            SurfaceWindSpeedKnots = 42,
            SurfaceWindDirection = 180,
            TemperatureCelsius = 30
        };

        var response = await _client!.PostAsJsonAsync("/api/sandbox", scenario);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("isSandboxMode").GetBoolean());
        Assert.Equal("Custom Test Run", _provider!.CurrentSandboxScenario?.Name);
        Assert.Equal(42, _provider.CurrentSandboxScenario?.SurfaceWindSpeedKnots);
    }

    [Fact]
    public async Task PostSandboxPreset_AppliesSpecificPreset()
    {
        var response = await _client!.PostAsJsonAsync("/api/sandbox/preset", new { preset = "supercell" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("isSandboxMode").GetBoolean());
        Assert.Equal("Severe Supercell Thunderstorm", _provider!.CurrentSandboxScenario?.Name);
    }

    [Fact]
    public async Task PostSandboxDisable_ReturnsToLiveWeather()
    {
        await _client!.PostAsJsonAsync("/api/sandbox/preset", new { preset = "fog" });
        Assert.True(_provider!.IsSandboxMode);

        var response = await _client!.PostAsync("/api/sandbox/disable", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("isSandboxMode").GetBoolean());
        Assert.False(_provider.IsSandboxMode);
    }
}
