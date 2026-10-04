using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.Api;

public class ApiStatus
{
    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; } = true;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.6.0";

    [JsonPropertyName("simConnected")]
    public bool SimConnected { get; set; }

    [JsonPropertyName("isInjecting")]
    public bool IsInjecting { get; set; }

    [JsonPropertyName("currentStation")]
    public string CurrentStation { get; set; } = string.Empty;

    [JsonPropertyName("hasPositionFix")]
    public bool HasPositionFix { get; set; }

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("isOnlineNetworkActive")]
    public bool IsOnlineNetworkActive { get; set; }

    [JsonPropertyName("onlineNetworkName")]
    public string? OnlineNetworkName { get; set; }
}

public class AircraftWeatherSnapshot
{
    [JsonPropertyName("snapshotId")]
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("simConnected")]
    public bool SimConnected { get; set; }

    [JsonPropertyName("isInjecting")]
    public bool IsInjecting { get; set; }

    [JsonPropertyName("hasPositionFix")]
    public bool HasPositionFix { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("altitudeFeet")]
    public double? AltitudeFeet { get; set; }

    [JsonPropertyName("stationId")]
    public string StationId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public WeatherState? State { get; set; }

    [JsonPropertyName("radarTimestamp")]
    public DateTime? RadarTimestamp { get; set; }

    [JsonPropertyName("radarTileUrl")]
    public string? RadarTileUrl { get; set; }

    [JsonPropertyName("isOnlineNetworkActive")]
    public bool IsOnlineNetworkActive { get; set; }

    [JsonPropertyName("onlineNetworkName")]
    public string? OnlineNetworkName { get; set; }
}

public class EfbSnapshot
{
    [JsonPropertyName("snapshotId")]
    public string SnapshotId { get; set; } = string.Empty;

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isAircraftFollower")]
    public bool IsAircraftFollower { get; set; } = true;

    [JsonPropertyName("hasPositionFix")]
    public bool HasPositionFix { get; set; }

    [JsonPropertyName("stationId")]
    public string StationId { get; set; } = string.Empty;

    [JsonPropertyName("rawMetar")]
    public string RawMetar { get; set; } = string.Empty;

    [JsonPropertyName("observationTime")]
    public DateTime ObservationTime { get; set; }

    [JsonPropertyName("flightCategory")]
    public string FlightCategory { get; set; } = "VFR";

    [JsonPropertyName("temperatureCelsius")]
    public double TemperatureCelsius { get; set; }

    [JsonPropertyName("dewpointCelsius")]
    public double DewpointCelsius { get; set; }

    [JsonPropertyName("altimeterHpa")]
    public double AltimeterHpa { get; set; }

    [JsonPropertyName("visibilityMeters")]
    public double VisibilityMeters { get; set; }

    [JsonPropertyName("windDirectionDegrees")]
    public double WindDirectionDegrees { get; set; }

    [JsonPropertyName("windSpeedKnots")]
    public double WindSpeedKnots { get; set; }

    [JsonPropertyName("windGustKnots")]
    public double? WindGustKnots { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("ceilingFeet")]
    public double CeilingFeet { get; set; }

    [JsonPropertyName("freezingLevelFeet")]
    public double FreezingLevelFeet { get; set; }

    [JsonPropertyName("humidityPercent")]
    public double HumidityPercent { get; set; }

    [JsonPropertyName("cloudLayers")]
    public List<CloudLayer> CloudLayers { get; set; } = new();

    [JsonPropertyName("windsAloft")]
    public List<WindLayer> WindsAloft { get; set; } = new();

    [JsonPropertyName("hazards")]
    public List<WeatherHazard> Hazards { get; set; } = new();

    [JsonPropertyName("stormCells")]
    public List<StormCell> StormCells { get; set; } = new();

    [JsonPropertyName("radarTimestamp")]
    public DateTime? RadarTimestamp { get; set; }

    [JsonPropertyName("radarTileUrl")]
    public string? RadarTileUrl { get; set; }

    [JsonPropertyName("sourceModelName")]
    public string SourceModelName { get; set; } = "HRRR";

    [JsonPropertyName("taf")]
    public TafData? Taf { get; set; }

    [JsonPropertyName("isOnlineNetworkActive")]
    public bool IsOnlineNetworkActive { get; set; }

    [JsonPropertyName("onlineNetworkName")]
    public string? OnlineNetworkName { get; set; }
}

public interface IWeatherDataProvider
{
    Task<ApiStatus> GetStatusAsync();
    Task<AircraftWeatherSnapshot?> GetAircraftSnapshotAsync();
    Task<WeatherState?> GetCurrentAircraftStateAsync();
    Task<WeatherState?> GetStateAsync(double? latitude = null, double? longitude = null, string? station = null);
    Task<MetarData?> GetMetarAsync(double? latitude = null, double? longitude = null, string? station = null);
    Task<List<WeatherHazard>?> GetHazardsAsync(double? latitude = null, double? longitude = null, string? station = null);
    Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null, string? station = null);

    bool IsWeatherFrozen => false;
    void SetWeatherFrozen(bool frozen) { }
    SkyAnchorState? GetAnchorState() => null;
    SimBriefPlan? GetFlightPlan() => null;
    void SetFlightPlan(SimBriefPlan? plan) { }
    SoundingProfileData? GetSoundingData(double? aircraftAltFeet = null) => null;
    SynopticMapData? GetSynopticData(double rangeMiles = 100.0) => null;
}

public class EngineWeatherDataProvider : IWeatherDataProvider
{
    private readonly WeatherEngine _engine;
    private readonly StationFinder _stationFinder;
    private readonly object _snapshotLock = new();
    private long _sequenceNumber;
    private AircraftWeatherSnapshot? _latestAircraftSnapshot;

    public bool SimConnected { get; set; }
    public bool IsInjecting { get; set; }
    public string CurrentStation { get; set; } = string.Empty;
    public bool IsOnlineNetworkActive { get; set; }
    public string? OnlineNetworkName { get; set; }

    public bool IsWeatherFrozen => _engine.IsFrozen;

    public void SetWeatherFrozen(bool frozen)
    {
        _engine.IsFrozen = frozen;
    }

    public SkyAnchorState? GetAnchorState()
    {
        return _engine.CurrentAnchorState;
    }

    public SimBriefPlan? GetFlightPlan()
    {
        return _engine.AnchorManager.FlightPlan;
    }

    public void SetFlightPlan(SimBriefPlan? plan)
    {
        _engine.SetFlightPlan(plan);
    }

    public SoundingProfileData? GetSoundingData(double? aircraftAltFeet = null)
    {
        var state = _engine.CurrentState;
        if (state == null) return null;
        var generator = new SoundingGenerator();
        return generator.Generate(state, aircraftAltFeet ?? _engine.AircraftAltitudeFeet);
    }

    public SynopticMapData? GetSynopticData(double rangeMiles = 100.0)
    {
        var state = _engine.CurrentState;
        if (state == null) return null;
        var generator = new SynopticMapGenerator();
        return generator.Generate(state, rangeMiles);
    }

    public EngineWeatherDataProvider(WeatherEngine engine, bool allowPositionOverride = false)
    {
        _engine = engine;
        _stationFinder = engine.StationFinder ?? new StationFinder();

        _engine.WeatherUpdated += OnWeatherUpdated;

        if (_engine.CurrentState != null)
        {
            UpdateSnapshot(_engine.CurrentState);
        }
    }

    private void OnWeatherUpdated(object? sender, WeatherState state)
    {
        UpdateSnapshot(state);
    }

    private void UpdateSnapshot(WeatherState state)
    {
        lock (_snapshotLock)
        {
            _sequenceNumber++;
            if (!string.IsNullOrWhiteSpace(state.StationId))
            {
                CurrentStation = state.StationId;
            }

            var radar = _engine.CurrentRadarFrame;

            _latestAircraftSnapshot = new AircraftWeatherSnapshot
            {
                SnapshotId = Guid.NewGuid().ToString("N"),
                SequenceNumber = _sequenceNumber,
                TimestampUtc = DateTime.UtcNow,
                SimConnected = SimConnected,
                IsInjecting = IsInjecting || (_engine.IsRunning && !_engine.PassiveMode),
                HasPositionFix = _engine.HasPositionFix,
                Latitude = state.Latitude,
                Longitude = state.Longitude,
                AltitudeFeet = _engine.AircraftAltitudeFeet,
                StationId = !string.IsNullOrWhiteSpace(state.StationId) ? state.StationId : CurrentStation,
                State = state,
                RadarTimestamp = radar?.Timestamp,
                RadarTileUrl = radar?.TileUrl,
                IsOnlineNetworkActive = IsOnlineNetworkActive,
                OnlineNetworkName = OnlineNetworkName
            };
        }
    }

    public Task<ApiStatus> GetStatusAsync()
    {
        lock (_snapshotLock)
        {
            var station = _latestAircraftSnapshot?.StationId;
            if (string.IsNullOrWhiteSpace(station))
                station = CurrentStation;

            return Task.FromResult(new ApiStatus
            {
                IsRunning = _engine.IsRunning,
                Version = "0.6.0",
                SimConnected = SimConnected,
                IsInjecting = IsInjecting || (_engine.IsRunning && !_engine.PassiveMode),
                CurrentStation = station ?? string.Empty,
                HasPositionFix = _engine.HasPositionFix,
                SequenceNumber = _sequenceNumber,
                IsOnlineNetworkActive = IsOnlineNetworkActive,
                OnlineNetworkName = OnlineNetworkName
            });
        }
    }

    public Task<AircraftWeatherSnapshot?> GetAircraftSnapshotAsync()
    {
        lock (_snapshotLock)
        {
            if (!_engine.HasPositionFix)
            {
                return Task.FromResult<AircraftWeatherSnapshot?>(null);
            }

            if (_latestAircraftSnapshot == null && _engine.CurrentState != null)
            {
                UpdateSnapshot(_engine.CurrentState);
            }

            return Task.FromResult(_latestAircraftSnapshot);
        }
    }

    public Task<WeatherState?> GetCurrentAircraftStateAsync()
    {
        lock (_snapshotLock)
        {
            if (!_engine.HasPositionFix)
            {
                return Task.FromResult<WeatherState?>(null);
            }

            return Task.FromResult(_latestAircraftSnapshot?.State ?? _engine.CurrentState);
        }
    }

    public async Task<WeatherState?> GetStateAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        if (TryResolveBriefingTarget(latitude, longitude, station, out var reqLat, out var reqLon, out var reqAlt, out var invalidStation))
        {
            return await _engine.FetchBriefingWeatherAsync(reqLat, reqLon, reqAlt);
        }

        if (invalidStation)
        {
            return null;
        }

        // Return active aircraft state (null if awaiting position)
        lock (_snapshotLock)
        {
            if (!_engine.HasPositionFix) return null;
            return _latestAircraftSnapshot?.State ?? _engine.CurrentState;
        }
    }

    public async Task<MetarData?> GetMetarAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        var state = await GetStateAsync(latitude, longitude, station);
        if (state == null) return null;

        return BuildMetarFromState(state);
    }

    public async Task<List<WeatherHazard>?> GetHazardsAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        var state = await GetStateAsync(latitude, longitude, station);
        return state?.Hazards;
    }

    public async Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        if (TryResolveBriefingTarget(latitude, longitude, station, out var reqLat, out var reqLon, out var reqAlt, out var invalidStation))
        {
            var briefingState = await _engine.FetchBriefingWeatherAsync(reqLat, reqLon, reqAlt);
            if (briefingState == null) return null;

            return BuildEfbSnapshot(
                briefingState,
                snapshotId: Guid.NewGuid().ToString("N"),
                sequenceNumber: 0,
                timestampUtc: DateTime.UtcNow,
                isAircraftFollower: false,
                hasPositionFix: true,
                radar: _engine.CurrentRadarFrame,
                isOnlineNetworkActive: IsOnlineNetworkActive,
                onlineNetworkName: OnlineNetworkName);
        }

        if (invalidStation)
        {
            return null;
        }

        // Return active aircraft snapshot
        var aircraftSnapshot = await GetAircraftSnapshotAsync();
        if (aircraftSnapshot?.State == null)
        {
            return null;
        }

        return BuildEfbSnapshot(
            aircraftSnapshot.State,
            snapshotId: aircraftSnapshot.SnapshotId,
            sequenceNumber: aircraftSnapshot.SequenceNumber,
            timestampUtc: aircraftSnapshot.TimestampUtc,
            isAircraftFollower: true,
            hasPositionFix: aircraftSnapshot.HasPositionFix,
            radar: _engine.CurrentRadarFrame,
            isOnlineNetworkActive: IsOnlineNetworkActive,
            onlineNetworkName: OnlineNetworkName);
    }

    private bool TryResolveBriefingTarget(
        double? latitude, double? longitude, string? station,
        out double reqLat, out double reqLon, out double reqAlt,
        out bool briefingRequestedButInvalid)
    {
        reqLat = 0;
        reqLon = 0;
        reqAlt = 0;
        briefingRequestedButInvalid = false;

        if (latitude.HasValue && longitude.HasValue)
        {
            reqLat = latitude.Value;
            reqLon = longitude.Value;
            if (!string.IsNullOrWhiteSpace(station))
            {
                var airport = _stationFinder.AllAirports.FirstOrDefault(a =>
                    string.Equals(a.IcaoId, station.Trim(), StringComparison.OrdinalIgnoreCase));
                if (airport != null)
                {
                    reqAlt = airport.ElevationFeet;
                }
            }
            return true;
        }

        if (!string.IsNullOrWhiteSpace(station))
        {
            var airport = _stationFinder.AllAirports.FirstOrDefault(a =>
                string.Equals(a.IcaoId, station.Trim(), StringComparison.OrdinalIgnoreCase));
            if (airport != null)
            {
                reqLat = airport.Latitude;
                reqLon = airport.Longitude;
                reqAlt = airport.ElevationFeet;
                return true;
            }

            briefingRequestedButInvalid = true;
            return false;
        }

        return false;
    }

    public static MetarData BuildMetarFromState(WeatherState state)
    {
        return new MetarData
        {
            StationId = state.StationId,
            ObservationTime = state.ObservationTime,
            TemperatureCelsius = state.TemperatureCelsius,
            DewpointCelsius = state.DewpointCelsius,
            WindDirectionDegrees = state.WindDirectionDegrees,
            WindSpeedKnots = state.WindSpeedKnots,
            WindGustKnots = state.WindGustKnots,
            VisibilityMeters = state.VisibilityMeters,
            AltimeterHpa = state.AltimeterHpa,
            FlightCategory = state.FlightCategory,
            RawText = state.RawMetar ?? string.Empty,
            Clouds = state.CloudLayers?.Select(c => new MetarCloud
            {
                Coverage = c.Type.ToString(),
                BaseFeet = (int)(c.BaseFeetAgl > 0 ? c.BaseFeetAgl : c.BaseMeters / WeatherUnits.FeetToMeters),
                Type = c.Type.ToString()
            }).ToList() ?? new List<MetarCloud>()
        };
    }

    public static EfbSnapshot BuildEfbSnapshot(
        WeatherState state,
        string snapshotId,
        long sequenceNumber,
        DateTime timestampUtc,
        bool isAircraftFollower,
        bool hasPositionFix,
        RadarFrame? radar,
        bool isOnlineNetworkActive = false,
        string? onlineNetworkName = null)
    {
        var rawMetar = !string.IsNullOrWhiteSpace(state.RawMetar)
            ? state.RawMetar
            : $"{state.StationId} {state.ObservationTime:ddHHmm}Z {state.WindDirectionDegrees:000}{state.WindSpeedKnots:00}KT {state.VisibilityMeters / 1609.344:0}SM {state.FlightCategory} {state.TemperatureCelsius:0}/{state.DewpointCelsius:0} A{(state.AltimeterHpa * 0.02952998751 * 100):0000}";

        return new EfbSnapshot
        {
            SnapshotId = snapshotId,
            SequenceNumber = sequenceNumber,
            TimestampUtc = timestampUtc,
            IsAircraftFollower = isAircraftFollower,
            HasPositionFix = hasPositionFix,
            StationId = state.StationId,
            RawMetar = rawMetar,
            ObservationTime = state.ObservationTime != default ? state.ObservationTime : DateTime.UtcNow,
            FlightCategory = !string.IsNullOrWhiteSpace(state.FlightCategory) ? state.FlightCategory : "VFR",
            TemperatureCelsius = state.TemperatureCelsius,
            DewpointCelsius = state.DewpointCelsius,
            AltimeterHpa = state.AltimeterHpa > 0 ? state.AltimeterHpa : 1013.25,
            VisibilityMeters = state.VisibilityMeters,
            WindDirectionDegrees = state.WindDirectionDegrees,
            WindSpeedKnots = state.WindSpeedKnots,
            WindGustKnots = state.WindGustKnots,
            Latitude = state.Latitude,
            Longitude = state.Longitude,
            CeilingFeet = state.CeilingFeet,
            FreezingLevelFeet = state.FreezingLevelFeet,
            HumidityPercent = state.HumidityPercent,
            CloudLayers = state.CloudLayers ?? new(),
            WindsAloft = state.WindsAloft ?? new(),
            Hazards = state.Hazards ?? new(),
            StormCells = state.StormCells ?? new(),
            RadarTimestamp = radar?.Timestamp,
            RadarTileUrl = radar?.TileUrl,
            SourceModelName = state.SourceModelName ?? "HRRR",
            Taf = state.Taf,
            IsOnlineNetworkActive = isOnlineNetworkActive,
            OnlineNetworkName = onlineNetworkName
        };
    }
}
