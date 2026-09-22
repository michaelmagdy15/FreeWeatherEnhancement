using System.Text.Json;
using SkyWeave.Api;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Api.Tests;

public class WeatherDataProviderTests
{
    private sealed class FakeProvider : IWeatherDataProvider
    {
        public int Calls { get; private set; }

        public Task<WeatherState?> GetStateAsync(double latitude, double longitude)
        {
            Calls++;
            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = "KJFK",
                Latitude = latitude,
                Longitude = longitude,
                TemperatureCelsius = 27.2,
                DewpointCelsius = 21.1,
                WindDirectionDegrees = 40,
                WindSpeedKnots = 9,
                FlightCategory = "VFR",
                VisibilityMeters = 16093.44,
                AltimeterHpa = 1013.25,
                SourceModelName = "HRRR CONUS 3 km",
                Taf = new SkyWeave.Core.Decoders.TafData { StationId = "KJFK", RawText = "TAF KJFK ..." }
            });
        }

        public Task<MetarData?> GetMetarAsync(double latitude, double longitude)
        {
            Calls++;
            return Task.FromResult<MetarData?>(new MetarData
            {
                StationId = "KJFK",
                TemperatureCelsius = 27.2,
                FlightCategory = "VFR"
            });
        }

        public Task<List<WeatherHazard>> GetHazardsAsync(double latitude, double longitude)
        {
            Calls++;
            return Task.FromResult(new List<WeatherHazard>
            {
                new() { Type = HazardType.ConvectiveSigmet, Description = "Area of embedded thunderstorms", Severity = 0.7 }
            });
        }

        public Task<ApiStatus> GetStatusAsync()
        {
            Calls++;
            return Task.FromResult(new ApiStatus
            {
                IsRunning = true,
                Version = "0.6.0",
                SimConnected = true,
                IsInjecting = true,
                CurrentStation = "KJFK"
            });
        }

        public Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null)
        {
            Calls++;
            return Task.FromResult<EfbSnapshot?>(new EfbSnapshot
            {
                StationId = "KJFK",
                RawMetar = "METAR KJFK 211200Z 04009KT 10SM FEW025 27/21 A2992",
                ObservationTime = DateTime.UtcNow,
                FlightCategory = "VFR",
                TemperatureCelsius = 27.2,
                DewpointCelsius = 21.1,
                AltimeterHpa = 1013.25,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 40,
                WindSpeedKnots = 9,
                WindGustKnots = 14,
                Latitude = latitude ?? 40.6399,
                Longitude = longitude ?? -73.7787,
                CeilingFeet = 25000,
                FreezingLevelFeet = 12000,
                HumidityPercent = 65,
                WindsAloft = new List<WindLayer>
                {
                    new() { AltitudeFeet = 3000, DirectionDegrees = 50, SpeedKnots = 15, TemperatureCelsius = 18 }
                },
                Hazards = new List<WeatherHazard>
                {
                    new() { Type = HazardType.ConvectiveSigmet, Description = "Area of embedded thunderstorms", Severity = 0.7 }
                },
                RadarTimestamp = DateTime.UtcNow,
                RadarTileUrl = "https://tilecache.rainviewer.com/v2/radar/1690000000/256/2/1/1/2/1_1.png",
                SourceModelName = "HRRR CONUS 3 km"
            });
        }
    }

    [Fact]
    public async Task GetStateAsync_ReturnsStateWithPositionAndTaf()
    {
        var provider = new FakeProvider();
        var state = await provider.GetStateAsync(40.6399, -73.7787);

        Assert.NotNull(state);
        Assert.Equal("KJFK", state!.StationId);
        Assert.Equal(40.6399, state.Latitude);
        Assert.Equal("VFR", state.FlightCategory);
        Assert.NotNull(state.Taf);
    }

    [Fact]
    public async Task GetStateAsync_SerializesToJsonWithPascalCaseFields()
    {
        var provider = new FakeProvider();
        var state = await provider.GetStateAsync(40.6399, -73.7787);
        var json = JsonSerializer.Serialize(state);

        Assert.Contains("\"StationId\"", json);
        Assert.Contains("\"FlightCategory\"", json);
        Assert.Contains("\"Taf\"", json);
    }

    [Fact]
    public async Task GetHazardsAsync_ReturnsList()
    {
        var provider = new FakeProvider();
        var hazards = await provider.GetHazardsAsync(40.6399, -73.7787);

        Assert.Single(hazards);
        Assert.Equal(HazardType.ConvectiveSigmet, hazards[0].Type);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsValidStatus()
    {
        var provider = new FakeProvider();
        var status = await provider.GetStatusAsync();

        Assert.True(status.IsRunning);
        Assert.Equal("0.6.0", status.Version);
        Assert.True(status.SimConnected);
        Assert.True(status.IsInjecting);
        Assert.Equal("KJFK", status.CurrentStation);
    }

    [Fact]
    public async Task GetEfbSnapshotAsync_ReturnsSnapshot()
    {
        var provider = new FakeProvider();
        var efb = await provider.GetEfbSnapshotAsync(40.6399, -73.7787);

        Assert.NotNull(efb);
        Assert.Equal("KJFK", efb!.StationId);
        Assert.Equal("VFR", efb.FlightCategory);
        Assert.Single(efb.WindsAloft);
        Assert.Single(efb.Hazards);
        Assert.NotNull(efb.RadarTimestamp);
    }
}
