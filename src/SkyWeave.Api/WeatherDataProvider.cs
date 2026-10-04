using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SkyWeave.Core;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Plugins;
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

    [JsonPropertyName("isHistoricalMode")]
    public bool IsHistoricalMode { get; set; }

    [JsonPropertyName("historicalTargetUtc")]
    public DateTime? HistoricalTargetUtc { get; set; }

    [JsonPropertyName("isSandboxMode")]
    public bool IsSandboxMode { get; set; }

    [JsonPropertyName("sandboxScenarioName")]
    public string? SandboxScenarioName { get; set; }

    [JsonPropertyName("trafficCount")]
    public int TrafficCount { get; set; }

    [JsonPropertyName("hasWakeEncounter")]
    public bool HasWakeEncounter { get; set; }
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

    [JsonPropertyName("atis")]
    public VatsimAtisInfo? Atis { get; set; }

    [JsonPropertyName("isHistoricalMode")]
    public bool IsHistoricalMode { get; set; }

    [JsonPropertyName("historicalTargetUtc")]
    public DateTime? HistoricalTargetUtc { get; set; }

    [JsonPropertyName("isSandboxMode")]
    public bool IsSandboxMode { get; set; }

    [JsonPropertyName("sandboxScenarioName")]
    public string? SandboxScenarioName { get; set; }
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

    [JsonPropertyName("atis")]
    public VatsimAtisInfo? Atis { get; set; }

    [JsonPropertyName("isHistoricalMode")]
    public bool IsHistoricalMode { get; set; }

    [JsonPropertyName("historicalTargetUtc")]
    public DateTime? HistoricalTargetUtc { get; set; }

    [JsonPropertyName("isSandboxMode")]
    public bool IsSandboxMode { get; set; }

    [JsonPropertyName("sandboxScenarioName")]
    public string? SandboxScenarioName { get; set; }

    [JsonPropertyName("trafficCount")]
    public int TrafficCount { get; set; }

    [JsonPropertyName("hasWakeEncounter")]
    public bool HasWakeEncounter { get; set; }
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
    Task<VatsimAtisInfo?> GetAtisAsync(string? station = null);

    bool IsHistoricalMode => false;
    DateTime? HistoricalTargetUtc => null;
    Task SetHistoricalModeAsync(bool enabled, DateTime? targetUtc = null) => Task.CompletedTask;
    Task<WeatherState?> FetchHistoricalWeatherAsync(double latitude, double longitude, DateTime targetUtc, string? stationId = null) => Task.FromResult<WeatherState?>(null);

    bool IsSandboxMode => false;
    SandboxWeatherScenario? CurrentSandboxScenario => null;
    Task SetSandboxModeAsync(bool enabled, SandboxWeatherScenario? scenario = null) => Task.CompletedTask;
    Task ApplySandboxScenarioAsync(SandboxWeatherScenario scenario) => Task.CompletedTask;
    Task ApplySandboxPresetAsync(string presetId) => Task.CompletedTask;

    bool IsWeatherFrozen => false;
    void SetWeatherFrozen(bool frozen) { }
    SkyAnchorState? GetAnchorState() => null;
    SimBriefPlan? GetFlightPlan() => null;
    void SetFlightPlan(SimBriefPlan? plan) { }
    SoundingProfileData? GetSoundingData(double? aircraftAltFeet = null) => null;
    SynopticMapData? GetSynopticData(double rangeMiles = 100.0) => null;
    IReadOnlyList<AircraftTraffic> GetNearbyTraffic() => Array.Empty<AircraftTraffic>();
    void SetTrafficSnapshot(IReadOnlyList<AircraftTraffic> traffic) { }
    int TrafficCount => 0;
    bool HasWakeEncounter => false;

    Task<DispatchBriefing?> GenerateDispatchBriefingAsync() => Task.FromResult<DispatchBriefing?>(null);
    Task<string?> GenerateDispatchBriefingHtmlAsync(bool darkMode = false) => Task.FromResult<string?>(null);
    IReadOnlyList<PluginInfo> GetInstalledPlugins() => Array.Empty<PluginInfo>();
    bool SetPluginEnabled(string pluginId, bool enabled) => false;
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

    public bool IsHistoricalMode
    {
        get => _engine.IsHistoricalMode;
        set => _ = _engine.SetHistoricalModeAsync(value);
    }

    public DateTime? HistoricalTargetUtc
    {
        get => _engine.HistoricalTargetUtc;
        set { if (value.HasValue) _ = _engine.SetHistoricalTargetUtcAsync(value.Value); }
    }

    public Task SetHistoricalModeAsync(bool enabled, DateTime? targetUtc = null)
    {
        return _engine.SetHistoricalModeAsync(enabled, targetUtc);
    }

    public Task<WeatherState?> FetchHistoricalWeatherAsync(double latitude, double longitude, DateTime targetUtc, string? stationId = null)
    {
        return _engine.FetchHistoricalWeatherAsync(latitude, longitude, targetUtc, stationId);
    }

    public bool IsSandboxMode => _engine.IsSandboxMode;
    public SandboxWeatherScenario? CurrentSandboxScenario => _engine.CurrentSandboxScenario;
    public Task SetSandboxModeAsync(bool enabled, SandboxWeatherScenario? scenario = null) => _engine.SetSandboxModeAsync(enabled, scenario);
    public Task ApplySandboxScenarioAsync(SandboxWeatherScenario scenario) => _engine.ApplySandboxScenarioAsync(scenario);
    public Task ApplySandboxPresetAsync(string presetId) => _engine.ApplySandboxPresetAsync(presetId);

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

    private IReadOnlyList<AircraftTraffic> _trafficSnapshot = Array.Empty<AircraftTraffic>();

    public IReadOnlyList<AircraftTraffic> GetNearbyTraffic() => _trafficSnapshot;

    public void SetTrafficSnapshot(IReadOnlyList<AircraftTraffic> traffic)
    {
        _trafficSnapshot = traffic ?? Array.Empty<AircraftTraffic>();
    }

    public int TrafficCount => _trafficSnapshot.Count;

    public bool HasWakeEncounter =>
        _engine.CurrentState?.TurbulenceLayers.Any(l => l.Type == TurbulenceType.Wake) == true ||
        _trafficSnapshot.Any(t => t.IsInWakeZone);

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

    public IReadOnlyList<PluginInfo> GetInstalledPlugins() => _engine.PluginManager.GetInstalledPlugins();
    public bool SetPluginEnabled(string pluginId, bool enabled) => _engine.PluginManager.SetPluginEnabled(pluginId, enabled);

    public async Task<DispatchBriefing?> GenerateDispatchBriefingAsync()
    {
        var plan = GetFlightPlan();
        if (plan == null) return null;

        var depAirport = !string.IsNullOrWhiteSpace(plan.Origin) ? _stationFinder.FindStation(plan.Origin) : null;
        var destAirport = !string.IsNullOrWhiteSpace(plan.Destination) ? _stationFinder.FindStation(plan.Destination) : null;
        var altAirport = !string.IsNullOrWhiteSpace(plan.Alternate) ? _stationFinder.FindStation(plan.Alternate) : null;

        MetarData? depMetar = null, destMetar = null, altMetar = null;
        TafData? depTaf = null, destTaf = null, altTaf = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(plan.Origin))
            {
                var depState = await _engine.FetchBriefingWeatherByStationAsync(plan.Origin);
                depMetar = depState.ToMetarData();
                depTaf = depState?.Taf;
            }
        }
        catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(plan.Destination))
            {
                var destState = await _engine.FetchBriefingWeatherByStationAsync(plan.Destination);
                destMetar = destState.ToMetarData();
                destTaf = destState?.Taf;
            }
        }
        catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(plan.Alternate))
            {
                var altState = await _engine.FetchBriefingWeatherByStationAsync(plan.Alternate);
                altMetar = altState.ToMetarData();
                altTaf = altState?.Taf;
            }
        }
        catch { }

        var analyzer = new RouteHazardAnalyzer();
        var hazardProfile = analyzer.AnalyzeRoute(
            plan,
            hazards: _engine.CurrentState?.Hazards,
            stormCells: _engine.CurrentState?.StormCells,
            turbulenceLayers: _engine.CurrentState?.TurbulenceLayers,
            icingLayers: _engine.CurrentState?.IcingLayers);

        var generator = new DispatchBriefingGenerator();
        return generator.BuildBriefing(
            plan,
            depMetar, depTaf,
            destMetar, destTaf,
            altMetar, altTaf,
            hazardProfile,
            depAirport, destAirport, altAirport);
    }

    public async Task<string?> GenerateDispatchBriefingHtmlAsync(bool darkMode = false)
    {
        var briefing = await GenerateDispatchBriefingAsync();
        if (briefing == null) return null;
        var generator = new DispatchBriefingGenerator();
        return generator.GenerateHtml(briefing, darkMode);
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
                OnlineNetworkName = OnlineNetworkName,
                Atis = state.Atis,
                IsHistoricalMode = state.IsHistorical,
                HistoricalTargetUtc = state.HistoricalUtc,
                IsSandboxMode = state.IsSandbox,
                SandboxScenarioName = state.SandboxScenarioName
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
                OnlineNetworkName = OnlineNetworkName,
                IsHistoricalMode = _engine.IsHistoricalMode,
                HistoricalTargetUtc = _engine.HistoricalTargetUtc,
                IsSandboxMode = _engine.IsSandboxMode,
                SandboxScenarioName = _engine.CurrentSandboxScenario?.Name,
                TrafficCount = TrafficCount,
                HasWakeEncounter = HasWakeEncounter
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
            onlineNetworkName: OnlineNetworkName,
            trafficCount: TrafficCount,
            hasWakeEncounter: HasWakeEncounter);
    }

    public async Task<VatsimAtisInfo?> GetAtisAsync(string? station = null)
    {
        var targetStation = !string.IsNullOrWhiteSpace(station)
            ? station.Trim().ToUpperInvariant()
            : (!string.IsNullOrWhiteSpace(CurrentStation) ? CurrentStation : _latestAircraftSnapshot?.StationId);

        if (string.IsNullOrWhiteSpace(targetStation))
            return null;

        return await _engine.GetVatsimAtisAsync(targetStation);
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
        string? onlineNetworkName = null,
        int trafficCount = 0,
        bool hasWakeEncounter = false)
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
            Atis = state.Atis,
            IsOnlineNetworkActive = isOnlineNetworkActive,
            OnlineNetworkName = onlineNetworkName,
            IsHistoricalMode = state.IsHistorical,
            HistoricalTargetUtc = state.HistoricalUtc,
            IsSandboxMode = state.IsSandbox,
            SandboxScenarioName = state.SandboxScenarioName,
            TrafficCount = trafficCount > 0 ? trafficCount : (state.TurbulenceLayers?.Any(l => l.Type == TurbulenceType.Wake) == true ? 1 : 0),
            HasWakeEncounter = hasWakeEncounter || state.TurbulenceLayers?.Any(l => l.Type == TurbulenceType.Wake) == true
        };
    }
}
