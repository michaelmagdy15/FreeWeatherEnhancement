using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SkyWeave.Api;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Api.Tests;

public class WeatherDataProviderTests
{
    private sealed class FakeProvider : IWeatherDataProvider
    {
        public int Calls { get; private set; }

        public Task<WeatherState?> GetStateAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            Calls++;
            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = station ?? "KJFK",
                Latitude = latitude ?? 40.6399,
                Longitude = longitude ?? -73.7787,
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

        public Task<MetarData?> GetMetarAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            Calls++;
            return Task.FromResult<MetarData?>(new MetarData
            {
                StationId = station ?? "KJFK",
                TemperatureCelsius = 27.2,
                FlightCategory = "VFR"
            });
        }

        public Task<List<WeatherHazard>?> GetHazardsAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            Calls++;
            return Task.FromResult<List<WeatherHazard>?>(new List<WeatherHazard>
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

        public Task<AircraftWeatherSnapshot?> GetAircraftSnapshotAsync()
        {
            Calls++;
            return Task.FromResult<AircraftWeatherSnapshot?>(new AircraftWeatherSnapshot
            {
                SnapshotId = "fake-snap-1",
                SequenceNumber = 1,
                StationId = "KJFK",
                HasPositionFix = true
            });
        }

        public Task<WeatherState?> GetCurrentAircraftStateAsync()
        {
            Calls++;
            return GetStateAsync();
        }

        public Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null, string? station = null)
        {
            Calls++;
            return Task.FromResult<EfbSnapshot?>(new EfbSnapshot
            {
                StationId = station ?? "KJFK",
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

    private sealed class TestWeatherPipeline : WeatherPipeline
    {
        public TestWeatherPipeline() : base(
            new WeatherCache(),
            new MetarFetcher(new System.Net.Http.HttpClient()),
            new WindsAloftFetcher(new System.Net.Http.HttpClient()),
            new SigmetFetcher(new System.Net.Http.HttpClient()),
            new LightningFetcher(new System.Net.Http.HttpClient()),
            new RadarFetcher(new System.Net.Http.HttpClient()),
            new TafFetcher(new System.Net.Http.HttpClient(), new StationFinder()),
            new CloudLayerBuilder(),
            new WindLayerBuilder(),
            new IcingCalculator(),
            new TurbulenceCalculator(),
            new StormModeler(),
            new WakeTurbulenceEngine(),
            new StationFinder(),
            new HazardAggregator())
        {
        }

        public override Task<PipelineData> FetchAllDataAsync(double latitude, double longitude, double altitudeFeet, DateTime modelTime, List<AircraftTraffic> traffic)
        {
            string stationId = (latitude > 35 && latitude < 40 && longitude < -120) ? "KSFO" : "KJFK";
            if (Math.Abs(latitude) < 0.001 && Math.Abs(longitude) < 0.001) stationId = "NULL_ISLAND";

            var metar = new MetarData
            {
                StationId = stationId,
                ObservationTime = DateTime.UtcNow,
                TemperatureCelsius = 18.0,
                DewpointCelsius = 10.0,
                WindDirectionDegrees = 270,
                WindSpeedKnots = 10,
                VisibilityMeters = 16093.44,
                AltimeterHpa = 1013.25,
                FlightCategory = "VFR",
                RawText = $"METAR {stationId} 221200Z 27010KT 10SM CLR 18/10 A2992"
            };

            return Task.FromResult(new PipelineData(
                metar,
                null,
                new List<CloudLayer>(),
                new List<WindLayer>(),
                new List<IcingLayer>(),
                new List<TurbulenceLayer>(),
                new List<StormCell>(),
                new List<WeatherHazard>(),
                new List<LightningStrike>(),
                0,
                new RadarFrame { Timestamp = DateTime.UtcNow, TileUrl = "http://test.radar" },
                null));
        }

        public override WeatherState BuildWeatherState(PipelineData data, double latitude, double longitude, double altitudeFeet)
        {
            return new WeatherState
            {
                StationId = data.Metar.StationId,
                Latitude = latitude,
                Longitude = longitude,
                TemperatureCelsius = data.Metar.TemperatureCelsius,
                DewpointCelsius = data.Metar.DewpointCelsius,
                WindDirectionDegrees = data.Metar.WindDirectionDegrees,
                WindSpeedKnots = data.Metar.WindSpeedKnots,
                AltimeterHpa = data.Metar.AltimeterHpa,
                VisibilityMeters = data.Metar.VisibilityMeters,
                FlightCategory = data.Metar.FlightCategory,
                RawMetar = data.Metar.RawText,
                SourceModelName = "TEST-MODEL"
            };
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

        Assert.NotNull(hazards);
        Assert.Single(hazards!);
        Assert.Equal(HazardType.ConvectiveSigmet, hazards![0].Type);
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

    [Fact]
    public async Task EngineWeatherDataProvider_WhenNoPositionFix_ReturnsNullForAircraftQueries()
    {
        using var engine = new WeatherEngine(new TestWeatherPipeline());
        var provider = new EngineWeatherDataProvider(engine);

        Assert.False(engine.HasPositionFix);

        var snapshot = await provider.GetAircraftSnapshotAsync();
        Assert.Null(snapshot);

        var currentState = await provider.GetCurrentAircraftStateAsync();
        Assert.Null(currentState);

        var state = await provider.GetStateAsync();
        Assert.Null(state);

        var metar = await provider.GetMetarAsync();
        Assert.Null(metar);

        var hazards = await provider.GetHazardsAsync();
        Assert.Null(hazards);

        var efb = await provider.GetEfbSnapshotAsync();
        Assert.Null(efb);
    }

    [Fact]
    public async Task EngineWeatherDataProvider_WhenPositionUpdated_ReturnsVersionedSnapshot()
    {
        using var engine = new WeatherEngine(new TestWeatherPipeline());
        var provider = new EngineWeatherDataProvider(engine);

        await engine.UpdatePositionAsync(37.6188, -122.3750, 2500);

        Assert.True(engine.HasPositionFix);
        Assert.Equal(37.6188, engine.AircraftLatitude);
        Assert.Equal(-122.3750, engine.AircraftLongitude);
        Assert.Equal(2500, engine.AircraftAltitudeFeet);

        var snapshot = await provider.GetAircraftSnapshotAsync();
        Assert.NotNull(snapshot);
        Assert.Equal("KSFO", snapshot!.StationId);
        Assert.Equal(37.6188, snapshot.Latitude);
        Assert.Equal(-122.3750, snapshot.Longitude);
        Assert.Equal(2500, snapshot.AltitudeFeet);
        Assert.True(snapshot.HasPositionFix);
        Assert.True(snapshot.SequenceNumber >= 1);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.SnapshotId));
    }

    [Fact]
    public async Task EngineWeatherDataProvider_BriefingQuery_LeavesAircraftPositionAndSnapshotUntouched()
    {
        using var engine = new WeatherEngine(new TestWeatherPipeline());
        var provider = new EngineWeatherDataProvider(engine);

        // Aircraft initialized at KSFO
        await engine.UpdatePositionAsync(37.6188, -122.3750, 1500);
        var initialSnapshot = await provider.GetAircraftSnapshotAsync();
        Assert.NotNull(initialSnapshot);
        var initialSeq = initialSnapshot!.SequenceNumber;

        // Tablet user requests a briefing for KJFK
        var briefing = await provider.GetEfbSnapshotAsync(station: "KJFK");
        Assert.NotNull(briefing);
        Assert.Equal("KJFK", briefing!.StationId);
        Assert.False(briefing.IsAircraftFollower);

        // Verify aircraft position in engine is completely untouched
        Assert.Equal(37.6188, engine.AircraftLatitude);
        Assert.Equal(-122.3750, engine.AircraftLongitude);
        Assert.Equal(1500, engine.AircraftAltitudeFeet);

        // Verify active aircraft snapshot is still KSFO and sequence number did NOT increment
        var currentAircraftSnapshot = await provider.GetAircraftSnapshotAsync();
        Assert.NotNull(currentAircraftSnapshot);
        Assert.Equal("KSFO", currentAircraftSnapshot!.StationId);
        Assert.Equal(37.6188, currentAircraftSnapshot.Latitude);
        Assert.Equal(initialSeq, currentAircraftSnapshot.SequenceNumber);
    }

    [Fact]
    public async Task EngineWeatherDataProvider_NullIslandCoordinates_PreservedWithoutDefaulting()
    {
        using var engine = new WeatherEngine(new TestWeatherPipeline());
        var provider = new EngineWeatherDataProvider(engine);

        // Request briefing for Null Island (0.0, 0.0)
        var briefing = await provider.GetEfbSnapshotAsync(latitude: 0.0, longitude: 0.0);

        Assert.NotNull(briefing);
        Assert.Equal(0.0, briefing!.Latitude);
        Assert.Equal(0.0, briefing.Longitude);
        Assert.Equal("NULL_ISLAND", briefing.StationId);
    }

    [Fact]
    public async Task EngineWeatherDataProvider_ConcurrentBriefingQueries_DoNotCorruptAircraftState()
    {
        using var engine = new WeatherEngine(new TestWeatherPipeline());
        var provider = new EngineWeatherDataProvider(engine);

        // Set aircraft position at KSFO
        await engine.UpdatePositionAsync(37.6188, -122.3750, 3000);

        // Fire 20 parallel briefing requests for different coordinates and stations
        var tasks = new List<Task<EfbSnapshot?>>();
        for (int i = 0; i < 20; i++)
        {
            if (i % 2 == 0)
            {
                tasks.Add(provider.GetEfbSnapshotAsync(station: "KJFK"));
            }
            else
            {
                tasks.Add(provider.GetEfbSnapshotAsync(latitude: 0.0, longitude: 0.0));
            }
        }

        var results = await Task.WhenAll(tasks);

        // Check that all requests succeeded and returned their respective data
        foreach (var res in results)
        {
            Assert.NotNull(res);
            Assert.False(res!.IsAircraftFollower);
        }

        // Verify the aircraft state was never corrupted
        Assert.Equal(37.6188, engine.AircraftLatitude);
        Assert.Equal(-122.3750, engine.AircraftLongitude);

        var aircraftSnapshot = await provider.GetAircraftSnapshotAsync();
        Assert.NotNull(aircraftSnapshot);
        Assert.Equal("KSFO", aircraftSnapshot!.StationId);
        Assert.Equal(37.6188, aircraftSnapshot.Latitude);
    }
}

