using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;

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

    public WeatherEngine(StationFinder? stationFinder = null)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SkyWeave/1.0");

        var cache = new WeatherCache();
        _stationFinder = stationFinder ?? new StationFinder();
        var hazardAggregator = new HazardAggregator();

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
            hazardAggregator);

        _modeler = new AtmosphericModeler();
        _smoothingPipeline = new SmoothingPipeline();
        _wprGenerator = new WprGenerator();
        _anchorManager = new SkyAnchorManager(_stationFinder);
    }

    public WeatherEngine(
        WeatherPipeline pipeline,
        AtmosphericModeler? modeler = null,
        SmoothingPipeline? smoothingPipeline = null,
        WprGenerator? wprGenerator = null,
        StationFinder? stationFinder = null)
    {
        _httpClient = new HttpClient();
        _pipeline = pipeline;
        _stationFinder = stationFinder ?? new StationFinder();
        _modeler = modeler ?? new AtmosphericModeler();
        _smoothingPipeline = smoothingPipeline ?? new SmoothingPipeline();
        _wprGenerator = wprGenerator ?? new WprGenerator();
        _anchorManager = new SkyAnchorManager(_stationFinder);
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

            var data = await _pipeline.FetchAllDataAsync(fetchLat, fetchLon, _lastAltitudeFeet, _lastModelTime, TrafficSnapshot);
            if (data.Metar == null) return;

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
