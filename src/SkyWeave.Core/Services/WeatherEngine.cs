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

    private Timer? _refreshTimer;
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
        _metarFetcher = new MetarFetcher(_httpClient);
        _windsAloftFetcher = new WindsAloftFetcher(_httpClient);
        _sigmetFetcher = new SigmetFetcher(_httpClient);
        _lightningFetcher = new LightningFetcher(_httpClient);
        _radarFetcher = new RadarFetcher(_httpClient);
        _metarDecoder = new MetarDecoder();
        _tafFetcher = new TafFetcher(_httpClient);
        _sigmetDecoder = new SigmetDecoder();
        _cloudLayerBuilder = new CloudLayerBuilder();
        _windLayerBuilder = new WindLayerBuilder();
        _icingCalculator = new IcingCalculator();
        _turbulenceCalculator = new TurbulenceCalculator();
        _wprGenerator = new WprGenerator();
        _smoothingPipeline = new SmoothingPipeline();
        _stationFinder = new StationFinder();
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

        _refreshTimer = new Timer(async _ => await UpdateWeatherAsync(), null, RefreshInterval, RefreshInterval);
    }

    public async Task StartAsync(bool passive = false)
    {
        if (_isRunning) return;

        _passiveMode = passive;
        _isRunning = true;
        _lastModelTime = DateTime.UtcNow;

        await UpdateWeatherAsync();

        _refreshTimer = new Timer(async _ => await UpdateWeatherAsync(), null, RefreshInterval, RefreshInterval);
    }

    public void Stop()
    {
        _isRunning = false;
        _refreshTimer?.Dispose();
        _refreshTimer = null;
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

    public string GenerateWpr()
    {
        var state = _smoothingPipeline.GetCurrentState();
        return _wprGenerator.GenerateWprXml(state);
    }

    public async Task<WeatherState?> FetchCurrentWeatherAsync()
    {
        try
        {
            var metarKey = $"metar:{_lastLatitude:F4},{_lastLongitude:F4}";
            var metar = await _cache.GetOrFetchAsync(
                metarKey,
                () => _metarFetcher.FetchMetarByPositionAsync(_lastLatitude, _lastLongitude),
                TimeSpan.FromMinutes(5));
            if (metar == null) return null;

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

            var radarPrecip = await _radarFetcher.GetPrecipitationAtPositionAsync(_lastLatitude, _lastLongitude);

            var cloudLayers = _cloudLayerBuilder.BuildCloudLayers(metar, windsAloft);
            var windLayers = _windLayerBuilder.BuildWindLayers(metar, windsAloft);
            var icingLayers = _icingCalculator.CalculateIcingLayers(cloudLayers, windLayers);
            var stormCells = _stormModeler.ModelStorms(lightning, sigmets, _lastLatitude, _lastLongitude, _lastModelTime, windsAloft?.ConvectiveAvailablePotentialEnergy);
            var turbulenceLayers = _turbulenceCalculator.CalculateTurbulenceLayers(
                windLayers, cloudLayers, stormCells, _lastAltitudeFeet);
            turbulenceLayers = ApplyWakeTurbulence(turbulenceLayers, windLayers);
            ScaleTurbulenceLayers(turbulenceLayers);

            return BuildWeatherState(metar, windsAloft, cloudLayers, windLayers,
                icingLayers, turbulenceLayers, stormCells, sigmets, lightning, radarPrecip, taf);
        }
        catch
        {
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

    private async Task UpdateWeatherAsync()
    {
        try
        {
            var metarKey = $"metar:{_lastLatitude:F4},{_lastLongitude:F4}";
            var metar = await _cache.GetOrFetchAsync(
                metarKey,
                () => _metarFetcher.FetchMetarByPositionAsync(_lastLatitude, _lastLongitude),
                TimeSpan.FromMinutes(5));

            if (metar == null)
            {
                ErrorOccurred?.Invoke(this, "Failed to fetch METAR data");
                return;
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

            var radarPrecip = await _radarFetcher.GetPrecipitationAtPositionAsync(_lastLatitude, _lastLongitude);
            CurrentRadarFrame = await _cache.GetOrFetchAsync(
                "radar-frame",
                () => _radarFetcher.GetLatestRadarFrameAsync(),
                TimeSpan.FromMinutes(2));

            RecentStrikes = lightning;

            var cloudLayers = _cloudLayerBuilder.BuildCloudLayers(metar, windsAloft);
            var windLayers = _windLayerBuilder.BuildWindLayers(metar, windsAloft);
            var icingLayers = _icingCalculator.CalculateIcingLayers(cloudLayers, windLayers);
            var stormCells = _stormModeler.ModelStorms(lightning, sigmets, _lastLatitude, _lastLongitude, _lastModelTime, windsAloft?.ConvectiveAvailablePotentialEnergy);
            DetectedStormCells = stormCells;
            _lastModelTime = DateTime.UtcNow;
            var turbulenceLayers = _turbulenceCalculator.CalculateTurbulenceLayers(
                windLayers, cloudLayers, stormCells, _lastAltitudeFeet);
            turbulenceLayers = ApplyWakeTurbulence(turbulenceLayers, windLayers);
            ScaleTurbulenceLayers(turbulenceLayers);

            var state = BuildWeatherState(metar, windsAloft, cloudLayers, windLayers,
                icingLayers, turbulenceLayers, stormCells, sigmets, lightning, radarPrecip, taf);

            _smoothingPipeline.SetTarget(state);

            var currentState = _smoothingPipeline.GetCurrentState();

            if (_passiveMode)
            {
                var passiveData = new PassiveWeatherData
                {
                    State = currentState,
                    LightningStrikes = lightning,
                    StormCells = stormCells,
                    RadarFrame = null
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
        var thunderstormIntensity = Math.Clamp(CalculateThunderstormIntensity(metar, sigmets, lightning, stormCells) * ThunderstormIntensityScale, 0, 1);
        var gustKnots = metar.WindGustKnots.HasValue ? metar.WindGustKnots.Value * GustEnhancementScale : (double?)null;

        return new WeatherState
        {
            ObservationTime = metar.ObservationTime,
            StationId = metar.StationId,
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
            PrecipitationRate = (CalculatePrecipitationRate(metar) + radarPrecipitation * 10) * PrecipitationScale,
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

    private List<StormCell> DetectStormCellsFromLightning(List<LightningStrike> strikes)
    {
        if (strikes.Count < 3) return new List<StormCell>();

        var clusters = new List<List<LightningStrike>>();
        var used = new HashSet<int>();

        for (int i = 0; i < strikes.Count; i++)
        {
            if (used.Contains(i)) continue;

            var cluster = new List<LightningStrike> { strikes[i] };
            used.Add(i);

            for (int j = i + 1; j < strikes.Count; j++)
            {
                if (used.Contains(j)) continue;

                var dist = CalculateDistanceNm(
                    strikes[i].Latitude, strikes[i].Longitude,
                    strikes[j].Latitude, strikes[j].Longitude);

                if (dist < 15)
                {
                    cluster.Add(strikes[j]);
                    used.Add(j);
                }
            }

            if (cluster.Count >= 3)
                clusters.Add(cluster);
        }

        var stormCells = new List<StormCell>();
        foreach (var cluster in clusters)
        {
            var avgLat = cluster.Average(s => s.Latitude);
            var avgLon = cluster.Average(s => s.Longitude);
            var minDist = cluster.Min(s => s.DistanceNm);

            var cell = new StormCell
            {
                Latitude = avgLat,
                Longitude = avgLon,
                AltitudeFeet = 0,
                MotionDirectionDegrees = 0,
                MotionSpeedKnots = 0,
                Intensity = Math.Min(1.0, cluster.Count / 10.0),
                RadiusNm = 5,
                Type = cluster.Count >= 10 ? CellType.MultiCell : CellType.Core,
                NearbyStrikes = cluster
            };

            stormCells.Add(cell);
        }

        return stormCells;
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
        double baseIndex = 0;

        if (turbulenceLayers.Count > 0)
            baseIndex = Math.Min(0.7, turbulenceLayers.Count / 5.0);

        if (lightning.Count > 5)
            baseIndex = Math.Max(baseIndex, 0.6);

        return Math.Min(1.0, baseIndex);
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
