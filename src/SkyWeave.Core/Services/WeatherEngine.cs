using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Plugins;

namespace SkyWeave.Core.Services;

public class WeatherEngine : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WeatherPipeline _pipeline;
    private readonly AtmosphericModeler _modeler;
    private readonly SmoothingPipeline _smoothingPipeline;
    private readonly WprGenerator _wprGenerator;
    private readonly StationFinder _stationFinder;
    private readonly SkyAnchorManager _anchorManager;
    private readonly PluginManager _pluginManager;

    private CancellationTokenSource? _refreshCts;
    private bool _isUpdating;
    private bool _isRunning;
    private bool _passiveMode;
    private double _lastLatitude;
    private double _lastLongitude;
    private double _lastAltitudeFeet;
    private DateTime _lastModelTime;

    public event EventHandler<WeatherState>? WeatherUpdated;
    public event EventHandler<string>? WprGenerated;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<PassiveWeatherData>? PassiveDataReceived;

    public string? LastError => _pipeline.LastError;
    public WeatherState? CurrentState => _smoothingPipeline.GetCurrentState();
    public bool IsRunning => _isRunning;
    public bool PassiveMode => _passiveMode;
    public bool HasPositionFix { get; private set; }
    public double AircraftLatitude => _lastLatitude;
    public double AircraftLongitude => _lastLongitude;
    public double AircraftAltitudeFeet => _lastAltitudeFeet;
    public StationFinder StationFinder => _stationFinder;
    public SkyAnchorManager AnchorManager => _anchorManager;
    public SkyAnchorState CurrentAnchorState { get; private set; } = new();
    public PluginManager PluginManager => _pluginManager;

    public bool IsFrozen
    {
        get => _anchorManager.IsManualFrozen;
        set
        {
            _anchorManager.IsManualFrozen = value;
            if (!value)
            {
                _ = UpdateWeatherAsync();
            }
        }
    }

    public void Freeze() => IsFrozen = true;
    public void Unfreeze() => IsFrozen = false;

    public void SetFlightPlan(SimBriefPlan? plan) => _anchorManager.SetFlightPlan(plan);
    public void SetRoute(string? originIcao, string? destinationIcao) => _anchorManager.SetRoute(originIcao, destinationIcao);

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(15);
    public List<LightningStrike> RecentStrikes { get; private set; } = new();
    public List<StormCell> DetectedStormCells { get; private set; } = new();
    public List<AircraftTraffic> TrafficSnapshot { get; set; } = new();
    public RadarFrame? CurrentRadarFrame { get; private set; }

    public double TurbulenceIntensityScale
    {
        get => _pipeline.TurbulenceIntensityScale;
        set => _pipeline.TurbulenceIntensityScale = value;
    }
    public bool WakeTurbulenceEnabled
    {
        get => _pipeline.WakeTurbulenceEnabled;
        set => _pipeline.WakeTurbulenceEnabled = value;
    }
    public double WakeTurbulenceScale
    {
        get => _pipeline.WakeTurbulenceScale;
        set => _pipeline.WakeTurbulenceScale = value;
    }
    public double GustEnhancementScale
    {
        get => _pipeline.GustEnhancementScale;
        set => _pipeline.GustEnhancementScale = value;
    }
    public double ThunderstormIntensityScale
    {
        get => _pipeline.ThunderstormIntensityScale;
        set => _pipeline.ThunderstormIntensityScale = value;
    }
    public double PrecipitationScale
    {
        get => _pipeline.PrecipitationScale;
        set => _pipeline.PrecipitationScale = value;
    }
    public double AerosolScale
    {
        get => _pipeline.AerosolScale;
        set => _pipeline.AerosolScale = value;
    }
    public double SmoothingDurationMinutes
    {
        get => _smoothingPipeline.TransitionDuration.TotalMinutes;
        set => _smoothingPipeline.TransitionDuration = TimeSpan.FromMinutes(value);
    }
    public double SimulationRate
    {
        get => _smoothingPipeline.SimulationRate;
        set => _smoothingPipeline.SimulationRate = value;
    }

    private readonly VatsimAtisFetcher _vatsimAtisFetcher;
    public VatsimAtisFetcher VatsimAtisFetcher => _vatsimAtisFetcher;

    private readonly Era5HistoricalFetcher _era5HistoricalFetcher;
    public Era5HistoricalFetcher Era5Fetcher => _era5HistoricalFetcher;

    public bool IsHistoricalMode { get; private set; }
    public DateTime? HistoricalTargetUtc { get; private set; }

    public bool IsSandboxMode { get; private set; }
    public SandboxWeatherScenario? CurrentSandboxScenario { get; private set; }

    public async Task SetHistoricalModeAsync(bool enabled, DateTime? targetUtc = null)
    {
        IsHistoricalMode = enabled;
        if (enabled)
        {
            IsSandboxMode = false; // Mutually exclusive with Sandbox
            HistoricalTargetUtc = targetUtc ?? DateTime.UtcNow.Date.AddDays(-7).AddHours(12);
        }
        else
        {
            HistoricalTargetUtc = null;
        }

        await UpdateWeatherAsync();
    }

    public async Task SetHistoricalTargetUtcAsync(DateTime targetUtc)
    {
        HistoricalTargetUtc = targetUtc;
        if (IsHistoricalMode)
        {
            await UpdateWeatherAsync();
        }
    }

    public Task<WeatherState?> FetchHistoricalWeatherAsync(
        double latitude,
        double longitude,
        DateTime targetUtc,
        string? stationId = null,
        CancellationToken ct = default)
    {
        double stationElevM = 0;
        if (!string.IsNullOrEmpty(stationId))
        {
            var st = _stationFinder.FindStation(stationId);
            if (st != null) stationElevM = st.ElevationFeet * 0.3048;
        }
        return _era5HistoricalFetcher.FetchHistoricalWeatherAsync(latitude, longitude, targetUtc, stationId, stationElevM, ct);
    }

    public async Task SetSandboxModeAsync(bool enabled, SandboxWeatherScenario? scenario = null)
    {
        IsSandboxMode = enabled;
        if (enabled)
        {
            IsHistoricalMode = false; // Mutually exclusive with Historical
            CurrentSandboxScenario = scenario ?? SandboxWeatherScenario.CreateCrosswindLanding();
        }
        else
        {
            CurrentSandboxScenario = null;
        }

        await UpdateWeatherAsync();
    }

    public async Task ApplySandboxScenarioAsync(SandboxWeatherScenario scenario)
    {
        CurrentSandboxScenario = scenario;
        IsSandboxMode = true;
        IsHistoricalMode = false;
        await UpdateWeatherAsync();
    }

    public async Task ApplySandboxPresetAsync(string presetId)
    {
        var preset = presetId.ToLowerInvariant() switch
        {
            "cat3_fog" or "fog" => SandboxWeatherScenario.CreateCat3Fog(),
            "crosswind" => SandboxWeatherScenario.CreateCrosswindLanding(),
            "supercell" or "thunderstorm" => SandboxWeatherScenario.CreateSupercellThunderstorm(),
            "mountain_wave" or "cat" => SandboxWeatherScenario.CreateMountainWaveCat(),
            "severe_icing" or "icing" => SandboxWeatherScenario.CreateSevereIcing(),
            "clear_calm" or "clear" => SandboxWeatherScenario.CreateClearAndCalm(),
            _ => SandboxWeatherScenario.CreateCrosswindLanding()
        };

        await ApplySandboxScenarioAsync(preset);
    }

    public bool PreferVatsimMetar
    {
        get => _pipeline.PreferVatsimMetar;
        set => _pipeline.PreferVatsimMetar = value;
    }

    public bool PreferIvaoMetar
    {
        get => _pipeline.PreferIvaoMetar;
        set => _pipeline.PreferIvaoMetar = value;
    }

    public bool AutoMatchOnlineAtcWeather
    {
        get => _pipeline.AutoMatchOnlineAtcWeather;
        set => _pipeline.AutoMatchOnlineAtcWeather = value;
    }

    public bool PreferOnlineAtisQnh
    {
        get => _pipeline.PreferOnlineAtisQnh;
        set => _pipeline.PreferOnlineAtisQnh = value;
    }

    public Task<VatsimAtisInfo?> GetVatsimAtisAsync(string icao, CancellationToken ct = default)
    {
        return _vatsimAtisFetcher.GetAtisAsync(icao, ct);
    }

    public WeatherEngine(StationFinder? stationFinder = null)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SkyWeave/1.0");

        var cache = new WeatherCache();
        _stationFinder = stationFinder ?? new StationFinder();
        var hazardAggregator = new HazardAggregator();
        _vatsimAtisFetcher = new VatsimAtisFetcher(_httpClient, cache);
        _era5HistoricalFetcher = new Era5HistoricalFetcher(_httpClient, cache);

        _pipeline = new WeatherPipeline(
            cache,
            new MetarFetcher(_httpClient, _stationFinder),
            new WindsAloftFetcher(_httpClient),
            new SigmetFetcher(_httpClient),
            new LightningFetcher(_httpClient),
            new RadarFetcher(_httpClient),
            new TafFetcher(_httpClient, _stationFinder),
            new CloudLayerBuilder(),
            new WindLayerBuilder(),
            new IcingCalculator(),
            new TurbulenceCalculator(),
            new StormModeler(),
            new WakeTurbulenceEngine(),
            _stationFinder,
            hazardAggregator,
            _vatsimAtisFetcher);

        _modeler = new AtmosphericModeler();
        _smoothingPipeline = new SmoothingPipeline();
        _wprGenerator = new WprGenerator();
        _anchorManager = new SkyAnchorManager(_stationFinder);
        _pluginManager = new PluginManager();
    }

    public WeatherEngine(
        WeatherPipeline pipeline,
        AtmosphericModeler? modeler = null,
        SmoothingPipeline? smoothingPipeline = null,
        WprGenerator? wprGenerator = null,
        StationFinder? stationFinder = null,
        Era5HistoricalFetcher? era5HistoricalFetcher = null,
        PluginManager? pluginManager = null)
    {
        _httpClient = new HttpClient();
        _pipeline = pipeline;
        _vatsimAtisFetcher = pipeline.VatsimAtisFetcher ?? new VatsimAtisFetcher(_httpClient);
        _stationFinder = stationFinder ?? new StationFinder();
        _era5HistoricalFetcher = era5HistoricalFetcher ?? new Era5HistoricalFetcher(_httpClient, pipeline.Cache);
        _modeler = modeler ?? new AtmosphericModeler();
        _smoothingPipeline = smoothingPipeline ?? new SmoothingPipeline();
        _wprGenerator = wprGenerator ?? new WprGenerator();
        _anchorManager = new SkyAnchorManager(_stationFinder);
        _pluginManager = pluginManager ?? new PluginManager();
    }

    public async Task StartAsync(double latitude, double longitude, bool passive = false)
    {
        if (_isRunning) return;

        _lastLatitude = latitude;
        _lastLongitude = longitude;
        HasPositionFix = true;
        _passiveMode = passive;
        _isRunning = true;
        _lastModelTime = DateTime.UtcNow;

        await UpdateWeatherAsync();

        _refreshCts = new CancellationTokenSource();
        _ = RunRefreshLoopAsync(_refreshCts.Token);
    }

    public async Task StartAsync(bool passive = false)
    {
        if (_isRunning) return;

        _passiveMode = passive;
        _isRunning = true;
        _lastModelTime = DateTime.UtcNow;

        await UpdateWeatherAsync();

        _refreshCts = new CancellationTokenSource();
        _ = RunRefreshLoopAsync(_refreshCts.Token);
    }

    public void Stop()
    {
        _isRunning = false;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private async Task RunRefreshLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (_isUpdating) continue;
                _isUpdating = true;
                try
                {
                    await UpdateWeatherAsync();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void SetPassiveMode(bool enabled)
    {
        _passiveMode = enabled;
    }

    public async Task UpdatePositionAsync(double latitude, double longitude, double altitudeFeet = 0)
    {
        _lastLatitude = latitude;
        _lastLongitude = longitude;
        _lastAltitudeFeet = altitudeFeet;
        HasPositionFix = true;

        CurrentAnchorState = _anchorManager.Evaluate(latitude, longitude, altitudeFeet);
        if (CurrentAnchorState.IsFrozen)
        {
            return;
        }

        await UpdateWeatherAsync();
    }

    public void SetPosition(double latitude, double longitude)
    {
        _lastLatitude = latitude;
        _lastLongitude = longitude;
        HasPositionFix = true;
    }

    public void ResetPositionFix()
    {
        HasPositionFix = false;
    }

    public string GenerateWpr()
    {
        var state = _smoothingPipeline.GetCurrentState();
        return _wprGenerator.GenerateWprXml(state);
    }

    public async Task<WeatherState?> FetchCurrentWeatherAsync()
    {
        try
        {
            var data = await _pipeline.FetchAllDataAsync(_lastLatitude, _lastLongitude, _lastAltitudeFeet, _lastModelTime, TrafficSnapshot);
            if (data.Metar == null) return null;

            return _pipeline.BuildWeatherState(data, _lastLatitude, _lastLongitude, _lastAltitudeFeet);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"FetchCurrentWeatherAsync failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fetches a standalone weather briefing for an arbitrary position without
    /// modifying the aircraft's active position, injection targets, smoothing pipeline, or firing events.
    /// </summary>
    public async Task<WeatherState?> FetchBriefingWeatherAsync(double latitude, double longitude, double altitudeFeet = 0, CancellationToken ct = default)
    {
        try
        {
            var data = await _pipeline.FetchAllDataAsync(latitude, longitude, altitudeFeet, DateTime.UtcNow, new List<AircraftTraffic>());
            if (data.Metar == null) return null;

            var state = _pipeline.BuildWeatherState(data, latitude, longitude, altitudeFeet);
            _modeler.ApplySpatialGridAndThermals(state, latitude, longitude);
            return state;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"FetchBriefingWeatherAsync failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fetches a standalone weather briefing for an airport station ICAO code without
    /// modifying the aircraft's active position or injection targets.
    /// </summary>
    public async Task<WeatherState?> FetchBriefingWeatherByStationAsync(string stationIcao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stationIcao)) return null;

        var airport = _stationFinder.AllAirports.FirstOrDefault(a =>
            string.Equals(a.IcaoId, stationIcao.Trim(), StringComparison.OrdinalIgnoreCase));

        if (airport != null)
        {
            return await FetchBriefingWeatherAsync(airport.Latitude, airport.Longitude, airport.ElevationFeet, ct);
        }

        return null;
    }

    private async Task UpdateWeatherAsync()
    {
        if (CurrentAnchorState.IsFrozen)
            return;

        try
        {
            double fetchLat = _lastLatitude;
            double fetchLon = _lastLongitude;

            if (!string.IsNullOrEmpty(CurrentAnchorState.ActiveAnchorIcao))
            {
                var anchor = _stationFinder.FindStation(CurrentAnchorState.ActiveAnchorIcao);
                if (anchor != null)
                {
                    fetchLat = anchor.Latitude;
                    fetchLon = anchor.Longitude;
                }
            }

            if (IsSandboxMode && CurrentSandboxScenario != null)
            {
                var station = !string.IsNullOrEmpty(CurrentAnchorState.ActiveAnchorIcao)
                    ? CurrentAnchorState.ActiveAnchorIcao
                    : _stationFinder.FindNearestStation(fetchLat, fetchLon);

                double stationElevM = 0;
                if (!string.IsNullOrEmpty(station))
                {
                    var st = _stationFinder.FindStation(station);
                    if (st != null) stationElevM = st.ElevationFeet * 0.3048;
                }

                var sandboxState = SandboxWeatherBuilder.BuildWeatherState(
                    CurrentSandboxScenario,
                    fetchLat,
                    fetchLon,
                    _lastAltitudeFeet,
                    stationElevM,
                    station ?? "SAND");

                if (CurrentSandboxScenario.InstantTransition)
                {
                    _smoothingPipeline.SnapToState(sandboxState);
                }
                else
                {
                    _smoothingPipeline.SetTarget(sandboxState);
                }

                var curState = _smoothingPipeline.GetCurrentState();

                if (_passiveMode)
                {
                    var passiveData = new PassiveWeatherData
                    {
                        State = curState,
                        LightningStrikes = new(),
                        StormCells = curState.StormCells,
                        RadarFrame = null
                    };
                    PassiveDataReceived?.Invoke(this, passiveData);
                }
                else
                {
                    WeatherUpdated?.Invoke(this, curState);
                    var wpr = _wprGenerator.GenerateWprXml(curState);
                    WprGenerated?.Invoke(this, wpr);
                }
                return;
            }

            if (IsHistoricalMode && HistoricalTargetUtc.HasValue)
            {
                var station = !string.IsNullOrEmpty(CurrentAnchorState.ActiveAnchorIcao)
                    ? CurrentAnchorState.ActiveAnchorIcao
                    : _stationFinder.FindNearestStation(fetchLat, fetchLon);

                double stationElevM = 0;
                if (!string.IsNullOrEmpty(station))
                {
                    var st = _stationFinder.FindStation(station);
                    if (st != null) stationElevM = st.ElevationFeet * 0.3048;
                }

                var histState = await _era5HistoricalFetcher.FetchHistoricalWeatherAsync(
                    fetchLat, fetchLon, HistoricalTargetUtc.Value, station, stationElevM);

                if (histState != null)
                {
                    _modeler.ApplySpatialGridAndThermals(histState, _lastLatitude, _lastLongitude);
                    _smoothingPipeline.SetTarget(histState);
                    var curState = _smoothingPipeline.GetCurrentState();

                    if (_passiveMode)
                    {
                        var passiveData = new PassiveWeatherData
                        {
                            State = curState,
                            LightningStrikes = new(),
                            StormCells = new(),
                            RadarFrame = null
                        };
                        PassiveDataReceived?.Invoke(this, passiveData);
                    }
                    else
                    {
                        WeatherUpdated?.Invoke(this, curState);
                        var wpr = _wprGenerator.GenerateWprXml(curState);
                        WprGenerated?.Invoke(this, wpr);
                    }
                    return;
                }
            }

            var data = await _pipeline.FetchAllDataAsync(fetchLat, fetchLon, _lastAltitudeFeet, _lastModelTime, TrafficSnapshot);
            if (data.Metar == null) return;

            // Fuse community plugin contributions safely
            var pluginContributions = await _pluginManager.FetchAllContributionsAsync(fetchLat, fetchLon, _lastAltitudeFeet);
            foreach (var contrib in pluginContributions)
            {
                if (contrib.CloudLayers != null && contrib.CloudLayers.Count > 0)
                    data.CloudLayers.AddRange(contrib.CloudLayers);
                if (contrib.WindLayers != null && contrib.WindLayers.Count > 0)
                    data.WindLayers.AddRange(contrib.WindLayers);
                if (contrib.IcingLayers != null && contrib.IcingLayers.Count > 0)
                    data.IcingLayers.AddRange(contrib.IcingLayers);
                if (contrib.TurbulenceLayers != null && contrib.TurbulenceLayers.Count > 0)
                    data.TurbulenceLayers.AddRange(contrib.TurbulenceLayers);
                if (contrib.Hazards != null && contrib.Hazards.Count > 0)
                    data.Sigmets.AddRange(contrib.Hazards);
                if (contrib.StormCells != null && contrib.StormCells.Count > 0)
                    data.StormCells.AddRange(contrib.StormCells);
            }

            CurrentRadarFrame = data.RadarFrame;
            RecentStrikes = data.Lightning;
            DetectedStormCells = data.StormCells;
            _lastModelTime = DateTime.UtcNow;

            var state = _pipeline.BuildWeatherState(data, _lastLatitude, _lastLongitude, _lastAltitudeFeet);

            _modeler.ApplySpatialGridAndThermals(state, _lastLatitude, _lastLongitude);

            _smoothingPipeline.SetTarget(state);

            var currentState = _smoothingPipeline.GetCurrentState();

            if (_passiveMode)
            {
                var passiveData = new PassiveWeatherData
                {
                    State = currentState,
                    LightningStrikes = data.Lightning,
                    StormCells = data.StormCells,
                    RadarFrame = data.RadarFrame
                };
                PassiveDataReceived?.Invoke(this, passiveData);
            }
            else
            {
                WeatherUpdated?.Invoke(this, currentState);

                var wpr = _wprGenerator.GenerateWprXml(currentState);
                WprGenerated?.Invoke(this, wpr);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    public void Dispose()
    {
        Stop();
        _pluginManager?.Dispose();
        _httpClient?.Dispose();
    }
}

public class PassiveWeatherData
{
    public WeatherState State { get; set; } = new();
    public List<LightningStrike> LightningStrikes { get; set; } = new();
    public List<StormCell> StormCells { get; set; } = new();
    public RadarFrame? RadarFrame { get; set; }
}
