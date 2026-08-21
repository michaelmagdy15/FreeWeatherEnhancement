using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class WeatherEngine : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WeatherCache _cache;
    private readonly MetarFetcher _metarFetcher;
    private readonly WindsAloftFetcher _windsAloftFetcher;
    private readonly SigmetFetcher _sigmetFetcher;
    private readonly LightningFetcher _lightningFetcher;
    private readonly RadarFetcher _radarFetcher;
    private readonly MetarDecoder _metarDecoder;
    private readonly TafFetcher _tafFetcher;
    private readonly SigmetDecoder _sigmetDecoder;
    private readonly CloudLayerBuilder _cloudLayerBuilder;
    private readonly WindLayerBuilder _windLayerBuilder;
    private readonly IcingCalculator _icingCalculator;
    private readonly TurbulenceCalculator _turbulenceCalculator;
    private readonly WprGenerator _wprGenerator;
    private readonly SmoothingPipeline _smoothingPipeline;
    private readonly StationFinder _stationFinder;
    private readonly StormModeler _stormModeler;
    private readonly WakeTurbulenceEngine _wakeTurbulenceEngine;

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

    public string? LastError { get; private set; }

    public WeatherState? CurrentState => _smoothingPipeline.GetCurrentState();
    public bool IsRunning => _isRunning;
    public bool PassiveMode => _passiveMode;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(20);
    public List<LightningStrike> RecentStrikes { get; private set; } = new();
    public List<StormCell> DetectedStormCells { get; private set; } = new();
    public List<AircraftTraffic> TrafficSnapshot { get; set; } = new();
    public RadarFrame? CurrentRadarFrame { get; private set; }

    public double TurbulenceIntensityScale { get; set; } = 1.0;
    public bool WakeTurbulenceEnabled { get; set; } = true;
    public double WakeTurbulenceScale { get; set; } = 1.0;
    public double GustEnhancementScale { get; set; } = 1.0;
    public double ThunderstormIntensityScale { get; set; } = 1.0;
    public double PrecipitationScale { get; set; } = 1.0;
    public double AerosolScale { get; set; } = 1.0;
    public double SmoothingDurationMinutes
    {
        get => _smoothingPipeline.TransitionDuration.TotalMinutes;
        set => _smoothingPipeline.TransitionDuration = TimeSpan.FromMinutes(value);
    }

    public WeatherEngine()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SkyWeave/1.0");

        _cache = new WeatherCache();
        _stationFinder = new StationFinder();
        _metarFetcher = new MetarFetcher(_httpClient, _stationFinder);
        _windsAloftFetcher = new WindsAloftFetcher(_httpClient);
        _sigmetFetcher = new SigmetFetcher(_httpClient);
        _lightningFetcher = new LightningFetcher(_httpClient);
        _radarFetcher = new RadarFetcher(_httpClient);
        _metarDecoder = new MetarDecoder();
        _tafFetcher = new TafFetcher(_httpClient, _stationFinder);
        _sigmetDecoder = new SigmetDecoder();
        _cloudLayerBuilder = new CloudLayerBuilder();
        _windLayerBuilder = new WindLayerBuilder();
        _icingCalculator = new IcingCalculator();
        _turbulenceCalculator = new TurbulenceCalculator();
        _wprGenerator = new WprGenerator();
        _smoothingPipeline = new SmoothingPipeline();
        _stormModeler = new StormModeler();
        _wakeTurbulenceEngine = new WakeTurbulenceEngine();
    }

    public async Task StartAsync(double latitude, double longitude, bool passive = false)
    {
        if (_isRunning) return;

        _lastLatitude = latitude;
        _lastLongitude = longitude;
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
        await UpdateWeatherAsync();
    }

    public void SetPosition(double latitude, double longitude)
    {
        _lastLatitude = latitude;
        _lastLongitude = longitude;
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
            var data = await FetchAllDataAsync();
            if (data.Metar == null) return null;

            return BuildWeatherState(data.Metar, data.Winds, data.CloudLayers, data.WindLayers,
                data.IcingLayers, data.TurbulenceLayers, data.StormCells, data.Sigmets,
                data.Lightning, data.RadarPrecip, data.Taf);
        }
        catch (Exception ex)
        {
            LastError = $"FetchCurrentWeatherAsync failed: {ex.Message}";
            ErrorOccurred?.Invoke(this, LastError);
            return null;
        }
    }

    private async Task<TafData?> FetchTafAsync()
    {
        var tafKey = $"taf:{_lastLatitude:F4},{_lastLongitude:F4}";
        return await _cache.GetOrFetchAsync(
            tafKey,
            () => _tafFetcher.FetchTafByPositionAsync(_lastLatitude, _lastLongitude),
            TimeSpan.FromMinutes(30));
    }

    private async Task<PipelineData> FetchAllDataAsync()
    {
        var metarKey = $"metar:{_lastLatitude:F4},{_lastLongitude:F4}";
        var metar = await _cache.GetOrFetchAsync(
            metarKey,
            () => _metarFetcher.FetchMetarByPositionAsync(_lastLatitude, _lastLongitude),
            TimeSpan.FromMinutes(5));

        if (metar == null)
        {
            LastError = "Failed to fetch METAR data";
            ErrorOccurred?.Invoke(this, LastError);
            return PipelineData.Empty;
        }

        var windsKey = $"winds:{_lastLatitude:F4},{_lastLongitude:F4}";
        var windsAloft = await _cache.GetOrFetchAsync(
            windsKey,
            () => _windsAloftFetcher.FetchWindsAloftAsync(_lastLatitude, _lastLongitude),
            TimeSpan.FromMinutes(30));

        var taf = await FetchTafAsync();

        var sigmetsKey = "sigmets";
        var sigmets = await _cache.GetOrFetchAsync(
            sigmetsKey,
            () => _sigmetFetcher.FetchSigmetsAsync(),
            TimeSpan.FromMinutes(10)) ?? new List<WeatherHazard>();

        var lightningKey = $"lightning:{_lastLatitude:F4},{_lastLongitude:F4}";
        var lightning = await _cache.GetOrFetchAsync(
            lightningKey,
            () => _lightningFetcher.FetchNearbyStrikesAsync(_lastLatitude, _lastLongitude, 100, 50),
            TimeSpan.FromMinutes(1)) ?? new List<LightningStrike>();

        var radarPrecipKey = $"radar-precip:{_lastLatitude:F4},{_lastLongitude:F4}";
        var radarPrecip = await _cache.GetOrFetchAsync(
            radarPrecipKey,
            () => _radarFetcher.GetPrecipitationAtPositionAsync(_lastLatitude, _lastLongitude),
            TimeSpan.FromMinutes(1));

        var radarFrame = await _cache.GetOrFetchAsync(
            "radar-frame",
            () => _radarFetcher.GetLatestRadarFrameAsync(),
            TimeSpan.FromMinutes(2));

        var cloudLayers = _cloudLayerBuilder.BuildCloudLayers(metar, windsAloft);
        var windLayers = _windLayerBuilder.BuildWindLayers(metar, windsAloft);
        var icingLayers = _icingCalculator.CalculateIcingLayers(cloudLayers, windLayers);
        var stormCells = _stormModeler.ModelStorms(lightning, sigmets, _lastLatitude, _lastLongitude, _lastModelTime, windsAloft);
        var turbulenceLayers = _turbulenceCalculator.CalculateTurbulenceLayers(
            windLayers, cloudLayers, stormCells, _lastAltitudeFeet);
        turbulenceLayers = ApplyWakeTurbulence(turbulenceLayers, windLayers);
        ScaleTurbulenceLayers(turbulenceLayers);

        return new PipelineData(metar, windsAloft, cloudLayers, windLayers, icingLayers,
            turbulenceLayers, stormCells, sigmets, lightning, radarPrecip, radarFrame, taf);
    }

    private async Task UpdateWeatherAsync()
    {
        try
        {
            var data = await FetchAllDataAsync();
            if (data.Metar == null) return;

            CurrentRadarFrame = data.RadarFrame;
            RecentStrikes = data.Lightning;
            DetectedStormCells = data.StormCells;
            _lastModelTime = DateTime.UtcNow;

            var state = BuildWeatherState(data.Metar, data.Winds, data.CloudLayers, data.WindLayers,
                data.IcingLayers, data.TurbulenceLayers, data.StormCells, data.Sigmets,
                data.Lightning, data.RadarPrecip, data.Taf);

            ApplySpatialGridAndThermals(state, _lastLatitude, _lastLongitude);

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
            LastError = ex.Message;
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    private sealed record PipelineData(
        MetarData Metar,
        WindsAloftData? Winds,
        List<CloudLayer> CloudLayers,
        List<WindLayer> WindLayers,
        List<IcingLayer> IcingLayers,
        List<TurbulenceLayer> TurbulenceLayers,
        List<StormCell> StormCells,
        List<WeatherHazard> Sigmets,
        List<LightningStrike> Lightning,
        double RadarPrecip,
        RadarFrame? RadarFrame,
        TafData? Taf)
    {
        public static readonly PipelineData Empty = new(
            null!, null, new List<CloudLayer>(), new List<WindLayer>(),
            new List<IcingLayer>(), new List<TurbulenceLayer>(),
            new List<StormCell>(), new List<WeatherHazard>(),
            new List<LightningStrike>(), 0, null, null);
    }

    private WeatherState BuildWeatherState(
        MetarData metar,
        WindsAloftData? windsAloft,
        List<CloudLayer> cloudLayers,
        List<WindLayer> windLayers,
        List<IcingLayer> icingLayers,
        List<TurbulenceLayer> turbulenceLayers,
        List<StormCell> stormCells,
        List<WeatherHazard> sigmets,
        List<LightningStrike> lightning,
        double radarPrecipitation,
        TafData? taf)
    {
        var metarPrecip = CalculatePrecipitationRate(metar);
        double precipRate;
        if (metarPrecip > 0)
        {
            precipRate = metarPrecip + (radarPrecipitation > 0 ? radarPrecipitation * 5.0 : 0);
        }
        else if (metar.Clouds.Any(c => c.Coverage == "OVC" || c.Coverage == "BKN") && radarPrecipitation > 1.0)
        {
            precipRate = radarPrecipitation;
        }
        else
        {
            precipRate = 0.0;
        }

        var thunderstormIntensity = Math.Clamp(CalculateThunderstormIntensity(metar, sigmets, lightning, stormCells) * ThunderstormIntensityScale, 0, 1);
        var gustKnots = metar.WindGustKnots.HasValue ? metar.WindGustKnots.Value * GustEnhancementScale : (double?)null;

        return new WeatherState
        {
            ObservationTime = metar.ObservationTime,
            StationId = metar.StationId,
            RawMetar = metar.RawText,
            Latitude = _lastLatitude,
            Longitude = _lastLongitude,
            TemperatureCelsius = metar.TemperatureCelsius,
            DewpointCelsius = metar.DewpointCelsius,
            PressureHpa = metar.AltimeterHpa,
            AltimeterHpa = metar.AltimeterHpa,
            VisibilityMeters = metar.VisibilityMeters,
            FlightCategory = metar.FlightCategory,
            WindDirectionDegrees = metar.WindDirectionDegrees,
            WindSpeedKnots = metar.WindSpeedKnots,
            WindGustKnots = gustKnots,
            CloudLayers = cloudLayers,
            WindsAloft = windLayers,
            Hazards = sigmets,
            IcingLayers = icingLayers,
            TurbulenceLayers = turbulenceLayers,
            StormCells = stormCells,
            PrecipitationRate = precipRate * PrecipitationScale,
            HumidityPercent = Meteorology.CalculateRelativeHumidity(metar.TemperatureCelsius, metar.DewpointCelsius),
            FreezingLevelFeet = CalculateFreezingLevel(windLayers),
            CeilingFeet = CalculateCeiling(metar),
            IcingIndex = CalculateIcingIndex(icingLayers),
            TurbulenceIndex = CalculateTurbulenceIndex(turbulenceLayers, lightning),
            ThunderstormIntensity = thunderstormIntensity,
            AerosolDensity = Math.Clamp(CalculateAerosolDensity(metar.VisibilityMeters) * AerosolScale, 0, 1),
            ConvectiveAvailablePotentialEnergy = windsAloft?.ConvectiveAvailablePotentialEnergy,
            LiftedIndex = windsAloft?.LiftedIndex,
            SourceModelName = windsAloft?.SourceModel ?? "Unknown",
            DataAgeMinutes = windsAloft?.DataAgeMinutes ?? 0,
            Taf = taf
        };
    }

    private List<TurbulenceLayer> ApplyWakeTurbulence(List<TurbulenceLayer> turbulenceLayers, List<WindLayer> windLayers)
    {
        if (!WakeTurbulenceEnabled) return turbulenceLayers;

        var airport = _stationFinder.FindNearestAirport(_lastLatitude, _lastLongitude);
        if (airport == null) return turbulenceLayers;

        var airportDistanceNm = CalculateDistanceNm(_lastLatitude, _lastLongitude, airport.Latitude, airport.Longitude);

        var wakeLayers = _wakeTurbulenceEngine.CalculateWakeLayers(
            _lastLatitude, _lastLongitude, _lastAltitudeFeet,
            TrafficSnapshot, windLayers,
            airport.Latitude, airport.Longitude, airportDistanceNm, 0.4);

        if (wakeLayers.Count == 0) return turbulenceLayers;

        var combined = new List<TurbulenceLayer>(turbulenceLayers);
        combined.AddRange(wakeLayers);
        return combined;
    }

    private void ScaleTurbulenceLayers(List<TurbulenceLayer> turbulenceLayers)
    {
        foreach (var layer in turbulenceLayers)
        {
            var scale = layer.Type == TurbulenceType.Wake ? WakeTurbulenceScale : TurbulenceIntensityScale;
            if (scale <= 0)
            {
                layer.Intensity = TurbulenceIntensity.None;
                continue;
            }

            var scaled = (int)Math.Round((int)layer.Intensity * scale);
            layer.Intensity = (TurbulenceIntensity)Math.Clamp(scaled, (int)TurbulenceIntensity.None, (int)TurbulenceIntensity.Extreme);
        }
    }

    private void ApplySpatialGridAndThermals(WeatherState state, double latitude, double longitude)
    {
        // 1. Spatial Grid Micro-variations
        var latRad = latitude * Math.PI / 180.0;
        var lonRad = longitude * Math.PI / 180.0;
        
        // Pseudo-random spatial variation using sine waves based on coordinates
        var spatialVarTemp = Math.Sin(latRad * 50) * Math.Cos(lonRad * 50) * 1.5; // +/- 1.5 C
        var spatialVarPress = Math.Cos(latRad * 30) * Math.Sin(lonRad * 30) * 1.0; // +/- 1.0 hPa
        
        state.TemperatureCelsius += spatialVarTemp;
        state.PressureHpa += spatialVarPress;
        state.AltimeterHpa += spatialVarPress;

        // 2. Thermal Generation (Surface Heating)
        // Highest thermals around 14:00 local solar time
        var solarTimeHours = (DateTime.UtcNow.TimeOfDay.TotalHours + (longitude / 15.0)) % 24;
        if (solarTimeHours < 0) solarTimeHours += 24;

        double thermalBoost = 0;
        if (solarTimeHours >= 10 && solarTimeHours <= 18)
        {
            // Peak at 14:00 (14.0)
            var thermalCurve = Math.Sin((solarTimeHours - 10) / 8.0 * Math.PI);
            
            // Add CAPE influence if available
            double capeFactor = state.ConvectiveAvailablePotentialEnergy.HasValue 
                ? Math.Min(1.0, state.ConvectiveAvailablePotentialEnergy.Value / 2000.0) 
                : 0.2;

            thermalBoost = thermalCurve * (10 + capeFactor * 20); // 10-30 knots of thermal draft
        }

        // Apply thermal boost to the lowest wind layers (surface to 6000ft)
        foreach (var layer in state.WindsAloft.Where(l => l.AltitudeFeet <= 6000))
        {
            layer.GustSpeedKnots = Math.Max(layer.GustSpeedKnots ?? 0, layer.SpeedKnots + thermalBoost);
        }
    }

    private double CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 3440.065;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;

    private double CalculatePrecipitationRate(MetarData metar)
    {
        foreach (var condition in metar.WeatherConditions)
        {
            var upper = condition.ToUpper();
            if (upper.Contains("+RA")) return 25.0;
            if (upper.Contains("RA")) return 5.0;
            if (upper.Contains("-RA")) return 1.0;
            if (upper.Contains("+SN")) return 15.0;
            if (upper.Contains("SN")) return 3.0;
            if (upper.Contains("-SN")) return 0.5;
            if (upper.Contains("TS")) return 30.0;
        }
        return 0;
    }

    private double CalculateFreezingLevel(List<WindLayer> windLayers)
    {
        for (int i = 0; i < windLayers.Count - 1; i++)
        {
            if (windLayers[i].TemperatureCelsius >= 0 &&
                windLayers[i + 1].TemperatureCelsius < 0)
            {
                var t = windLayers[i].TemperatureCelsius /
                        (windLayers[i].TemperatureCelsius - windLayers[i + 1].TemperatureCelsius);
                return windLayers[i].AltitudeFeet +
                       t * (windLayers[i + 1].AltitudeFeet - windLayers[i].AltitudeFeet);
            }
        }
        return 10000;
    }

    private double CalculateCeiling(MetarData metar)
    {
        var bknOvc = metar.Clouds
            .Where(c => c.Coverage == "BKN" || c.Coverage == "OVC" || c.Coverage == "VV")
            .OrderBy(c => c.BaseFeet)
            .FirstOrDefault();
        return bknOvc?.BaseFeet ?? 99999;
    }

    private double CalculateThunderstormIntensity(
        MetarData metar, List<WeatherHazard> sigmets,
        List<LightningStrike> lightning, List<StormCell> stormCells)
    {
        double intensity = 0;

        if (metar.WeatherConditions.Any(w => w.Contains("TS")))
            intensity = Math.Max(intensity, 0.4);

        if (sigmets.Any(h => h.Type == HazardType.ConvectiveSigmet))
            intensity = Math.Max(intensity, 0.7);

        if (lightning.Count > 0)
        {
            var strikeIntensity = Math.Min(0.8, lightning.Count / 20.0);
            intensity = Math.Max(intensity, strikeIntensity);
        }

        if (stormCells.Count > 0)
        {
            var cellIntensity = stormCells.Max(c => c.Intensity);
            intensity = Math.Max(intensity, cellIntensity);
        }

        return Math.Min(1.0, intensity);
    }

    private double CalculateIcingIndex(List<IcingLayer> icingLayers)
    {
        if (icingLayers.Count == 0) return 0;
        return Math.Min(1.0, icingLayers.Count / 5.0);
    }

    private double CalculateTurbulenceIndex(List<TurbulenceLayer> turbulenceLayers, List<LightningStrike> lightning)
    {
        if (turbulenceLayers.Count == 0) return 0;

        var maxIntensity = turbulenceLayers.Max(l => l.Intensity switch
        {
            TurbulenceIntensity.Extreme => 1.0,
            TurbulenceIntensity.Severe => 0.8,
            TurbulenceIntensity.Moderate => 0.45,
            TurbulenceIntensity.Light => 0.15,
            _ => 0.0
        });

        if (lightning.Count > 5)
            maxIntensity = Math.Max(maxIntensity, 0.6);

        return Math.Min(1.0, maxIntensity);
    }

    private double CalculateAerosolDensity(double visibilityMeters)
    {
        if (visibilityMeters > 10000) return 0.1;
        if (visibilityMeters > 5000) return 0.3;
        if (visibilityMeters > 2000) return 0.5;
        if (visibilityMeters > 1000) return 0.7;
        return 0.9;
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
