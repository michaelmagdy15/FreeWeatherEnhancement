using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using SkyWeave.Api;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Api.Tests;

public class EfbEndpointTests : IAsyncLifetime
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
        _baseAddress = addressesFeature?.Addresses.First() ?? "http://127.0.0.1:54170";

        _client = new HttpClient { BaseAddress = new Uri(_baseAddress) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetRoot_ReturnsOk_WithHtmlContent()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SkyWeave", content);
        Assert.Contains("EFB COMPANION", content);
    }

    [Fact]
    public async Task GetApiStatus_ReturnsValidJsonWithExpectedFields()
    {
        var response = await _client.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("isRunning", out var isRunningProp));
        Assert.True(isRunningProp.GetBoolean());

        Assert.True(root.TryGetProperty("version", out var versionProp));
        Assert.Equal("0.6.0", versionProp.GetString());

        Assert.True(root.TryGetProperty("simConnected", out var simConnProp));
        Assert.True(simConnProp.GetBoolean());

        Assert.True(root.TryGetProperty("isInjecting", out var isInjProp));
        Assert.True(isInjProp.GetBoolean());

        Assert.True(root.TryGetProperty("currentStation", out var stationProp));
        Assert.Equal("KJFK", stationProp.GetString());
    }

    [Fact]
    public async Task GetApiEfb_ReturnsValidEfbSnapshot()
    {
        var response = await _client.GetAsync("/api/efb?station=KJFK");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("KJFK", root.GetProperty("stationId").GetString());
        Assert.Equal("VFR", root.GetProperty("flightCategory").GetString());
        Assert.Contains("KJFK", root.GetProperty("rawMetar").GetString());
        Assert.Equal(180, root.GetProperty("windDirectionDegrees").GetDouble());
        Assert.Equal(12, root.GetProperty("windSpeedKnots").GetDouble());

        Assert.True(root.TryGetProperty("windsAloft", out var aloft));
        Assert.Equal(2, aloft.GetArrayLength());

        Assert.True(root.TryGetProperty("hazards", out var hazards));
        Assert.Equal(1, hazards.GetArrayLength());

        Assert.True(root.TryGetProperty("radarTimestamp", out _));
        Assert.True(root.TryGetProperty("radarTileUrl", out _));
    }

    [Fact]
    public async Task GetApiSnapshot_WhenSimPositionFixed_Returns200WithAircraftSnapshot()
    {
        _testProvider.HasPositionFix = true;
        var response = await _client.GetAsync("/api/snapshot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("snapshotId", out var snapshotIdProp));
        Assert.False(string.IsNullOrWhiteSpace(snapshotIdProp.GetString()));

        Assert.True(root.TryGetProperty("sequenceNumber", out var seqProp));
        Assert.True(seqProp.GetInt64() >= 1);

        Assert.True(root.TryGetProperty("hasPositionFix", out var fixProp));
        Assert.True(fixProp.GetBoolean());

        Assert.True(root.TryGetProperty("state", out var stateProp));
        Assert.Equal("KJFK", stateProp.GetProperty("stationId").GetString());
    }

    [Fact]
    public async Task GetApiSnapshot_WhenNoPositionFix_Returns503AwaitingSimPosition()
    {
        try
        {
            _testProvider.HasPositionFix = false;
            var response = await _client.GetAsync("/api/snapshot");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("awaiting_sim_position", root.GetProperty("reason").GetString());
            Assert.Equal("weather data unavailable", root.GetProperty("error").GetString());
        }
        finally
        {
            _testProvider.HasPositionFix = true;
        }
    }

    [Fact]
    public async Task GetApiEfb_WhenNoPositionFixAndNoStation_Returns503AwaitingSimPosition()
    {
        try
        {
            _testProvider.HasPositionFix = false;
            var response = await _client.GetAsync("/api/efb");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("awaiting_sim_position", root.GetProperty("reason").GetString());
        }
        finally
        {
            _testProvider.HasPositionFix = true;
        }
    }

    [Fact]
    public async Task GetApiEfb_WithZeroCoordinates_PreservesNullIsland()
    {
        var response = await _client.GetAsync("/api/efb?lat=0&lon=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(0.0, root.GetProperty("latitude").GetDouble());
        Assert.Equal(0.0, root.GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task GetApiState_WithZeroCoordinates_PreservesNullIsland()
    {
        var response = await _client.GetAsync("/api/state?lat=0&lon=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(0.0, root.GetProperty("latitude").GetDouble());
        Assert.Equal(0.0, root.GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task GetApiState_WhenNoPositionFixAndNoStation_Returns503AwaitingSimPosition()
    {
        try
        {
            _testProvider.HasPositionFix = false;
            var response = await _client.GetAsync("/api/state");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("awaiting_sim_position", root.GetProperty("reason").GetString());
        }
        finally
        {
            _testProvider.HasPositionFix = true;
        }
    }

    [Fact]
    public async Task GetStaticCssAndJs_ReturnsOk_WithProperMimeTypes()
    {
        var cssRes = await _client.GetAsync("/style.css");
        Assert.Equal(HttpStatusCode.OK, cssRes.StatusCode);
        Assert.Equal("text/css", cssRes.Content.Headers.ContentType?.MediaType);

        var jsRes = await _client.GetAsync("/app.js");
        Assert.Equal(HttpStatusCode.OK, jsRes.StatusCode);
        Assert.Contains("javascript", jsRes.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetApiStations_ReturnsNonEmptyList()
    {
        var response = await _client.GetAsync("/api/stations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
    }

    private sealed class TestWeatherDataProvider : IWeatherDataProvider
    {
        public bool HasPositionFix { get; set; } = true;

        public Task<ApiStatus> GetStatusAsync()
        {
            return Task.FromResult(new ApiStatus
            {
                IsRunning = true,
                Version = "0.6.0",
                SimConnected = true,
                IsInjecting = true,
                CurrentStation = "KJFK",
                HasPositionFix = HasPositionFix,
                SequenceNumber = 1
            });
        }

        public Task<AircraftWeatherSnapshot?> GetAircraftSnapshotAsync()
        {
            if (!HasPositionFix) return Task.FromResult<AircraftWeatherSnapshot?>(null);

            var state = new WeatherState
            {
                StationId = "KJFK",
                Latitude = 40.6399,
                Longitude = -73.7787,
                TemperatureCelsius = 22.5,
                DewpointCelsius = 15.0,
                AltimeterHpa = 1015.0,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 180,
                WindSpeedKnots = 12,
                FlightCategory = "VFR"
            };

            return Task.FromResult<AircraftWeatherSnapshot?>(new AircraftWeatherSnapshot
            {
                SnapshotId = "test-snapshot-123",
                SequenceNumber = 1,
                TimestampUtc = DateTime.UtcNow,
                SimConnected = true,
                IsInjecting = true,
                HasPositionFix = true,
                Latitude = 40.6399,
                Longitude = -73.7787,
                AltitudeFeet = 5000,
                StationId = "KJFK",
                State = state
            });
        }

        public Task<WeatherState?> GetCurrentAircraftStateAsync()
        {
            if (!HasPositionFix) return Task.FromResult<WeatherState?>(null);

            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = "KJFK",
                Latitude = 40.6399,
                Longitude = -73.7787,
                TemperatureCelsius = 22.5,
                DewpointCelsius = 15.0,
                AltimeterHpa = 1015.0,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 180,
                WindSpeedKnots = 12,
                FlightCategory = "VFR"
            });
        }

        public Task<WeatherState?> GetStateAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            if (latitude.HasValue && longitude.HasValue)
            {
                return Task.FromResult<WeatherState?>(new WeatherState
                {
                    StationId = station ?? "CUSTOM",
                    Latitude = latitude.Value,
                    Longitude = longitude.Value,
                    TemperatureCelsius = 22.5,
                    DewpointCelsius = 15.0,
                    AltimeterHpa = 1015.0,
                    VisibilityMeters = 16093.44,
                    WindDirectionDegrees = 180,
                    WindSpeedKnots = 12,
                    FlightCategory = "VFR"
                });
            }

            if (!string.IsNullOrWhiteSpace(station))
            {
                return Task.FromResult<WeatherState?>(new WeatherState
                {
                    StationId = station,
                    Latitude = 40.6399,
                    Longitude = -73.7787,
                    TemperatureCelsius = 22.5,
                    DewpointCelsius = 15.0,
                    AltimeterHpa = 1015.0,
                    VisibilityMeters = 16093.44,
                    WindDirectionDegrees = 180,
                    WindSpeedKnots = 12,
                    FlightCategory = "VFR"
                });
            }

            if (!HasPositionFix) return Task.FromResult<WeatherState?>(null);

            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = "KJFK",
                Latitude = 40.6399,
                Longitude = -73.7787,
                TemperatureCelsius = 22.5,
                DewpointCelsius = 15.0,
                AltimeterHpa = 1015.0,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 180,
                WindSpeedKnots = 12,
                FlightCategory = "VFR"
            });
        }

        public Task<MetarData?> GetMetarAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            if (!latitude.HasValue && !longitude.HasValue && string.IsNullOrWhiteSpace(station) && !HasPositionFix)
            {
                return Task.FromResult<MetarData?>(null);
            }

            return Task.FromResult<MetarData?>(new MetarData
            {
                StationId = station ?? "KJFK",
                TemperatureCelsius = 22.5,
                FlightCategory = "VFR"
            });
        }

        public Task<List<WeatherHazard>?> GetHazardsAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            if (!latitude.HasValue && !longitude.HasValue && string.IsNullOrWhiteSpace(station) && !HasPositionFix)
            {
                return Task.FromResult<List<WeatherHazard>?>(null);
            }

            return Task.FromResult<List<WeatherHazard>?>(new List<WeatherHazard>
            {
                new() { Type = HazardType.TurbulenceSigmet, Description = "Moderate clear air turbulence", Severity = 0.5 }
            });
        }

        public Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            bool isBriefing = (latitude.HasValue && longitude.HasValue) || !string.IsNullOrWhiteSpace(station);
            if (!isBriefing && !HasPositionFix)
            {
                return Task.FromResult<EfbSnapshot?>(null);
            }

            double effLat = latitude ?? 40.6399;
            double effLon = longitude ?? -73.7787;

            return Task.FromResult<EfbSnapshot?>(new EfbSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString("N"),
                SequenceNumber = 1,
                TimestampUtc = DateTime.UtcNow,
                IsAircraftFollower = !isBriefing,
                HasPositionFix = true,
                StationId = station ?? "KJFK",
                RawMetar = "METAR KJFK 211200Z 18012KT 10SM CLR 22/15 A2997",
                ObservationTime = DateTime.UtcNow,
                FlightCategory = "VFR",
                TemperatureCelsius = 22.5,
                DewpointCelsius = 15.0,
                AltimeterHpa = 1015.0,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 180,
                WindSpeedKnots = 12,
                Latitude = effLat,
                Longitude = effLon,
                CeilingFeet = 30000,
                FreezingLevelFeet = 14000,
                HumidityPercent = 60,
                WindsAloft = new List<WindLayer>
                {
                    new() { AltitudeFeet = 3000, DirectionDegrees = 190, SpeedKnots = 15, TemperatureCelsius = 16 },
                    new() { AltitudeFeet = 6000, DirectionDegrees = 200, SpeedKnots = 22, TemperatureCelsius = 10 }
                },
                Hazards = new List<WeatherHazard>
                {
                    new() { Type = HazardType.TurbulenceSigmet, Description = "Moderate clear air turbulence", Severity = 0.5 }
                },
                RadarTimestamp = DateTime.UtcNow,
                RadarTileUrl = "https://tilecache.rainviewer.com/v2/radar/1690000000/256/2/1/1/2/1_1.png",
                SourceModelName = "HRRR CONUS 3 km"
            });
        }
    }
}
