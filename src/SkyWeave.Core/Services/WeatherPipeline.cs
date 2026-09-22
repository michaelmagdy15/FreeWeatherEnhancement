using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public sealed record PipelineData(
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

public class WeatherPipeline
{
    private readonly WeatherCache _cache;
    private readonly MetarFetcher _metarFetcher;
    private readonly WindsAloftFetcher _windsAloftFetcher;
    private readonly SigmetFetcher _sigmetFetcher;
    private readonly LightningFetcher _lightningFetcher;
    private readonly RadarFetcher _radarFetcher;
    private readonly TafFetcher _tafFetcher;
    
    private readonly CloudLayerBuilder _cloudLayerBuilder;
    private readonly WindLayerBuilder _windLayerBuilder;
    private readonly IcingCalculator _icingCalculator;
    private readonly TurbulenceCalculator _turbulenceCalculator;
    private readonly StormModeler _stormModeler;
    private readonly WakeTurbulenceEngine _wakeTurbulenceEngine;
    private readonly StationFinder _stationFinder;
    private readonly HazardAggregator _hazardAggregator;

    public string? LastError { get; private set; }
    public double TurbulenceIntensityScale { get; set; } = 1.0;
    public bool WakeTurbulenceEnabled { get; set; } = true;
    public double WakeTurbulenceScale { get; set; } = 1.0;
    public double GustEnhancementScale { get; set; } = 1.0;
    public double ThunderstormIntensityScale { get; set; } = 1.0;
    public double PrecipitationScale { get; set; } = 1.0;
    public double AerosolScale { get; set; } = 1.0;

    public WeatherPipeline(
        WeatherCache cache,
        MetarFetcher metarFetcher,
        WindsAloftFetcher windsAloftFetcher,
        SigmetFetcher sigmetFetcher,
        LightningFetcher lightningFetcher,
        RadarFetcher radarFetcher,
        TafFetcher tafFetcher,
        CloudLayerBuilder cloudLayerBuilder,
        WindLayerBuilder windLayerBuilder,
        IcingCalculator icingCalculator,
        TurbulenceCalculator turbulenceCalculator,
        StormModeler stormModeler,
        WakeTurbulenceEngine wakeTurbulenceEngine,
        StationFinder stationFinder,
        HazardAggregator hazardAggregator)
    {
        _cache = cache;
        _metarFetcher = metarFetcher;
        _windsAloftFetcher = windsAloftFetcher;
        _sigmetFetcher = sigmetFetcher;
        _lightningFetcher = lightningFetcher;
        _radarFetcher = radarFetcher;
        _tafFetcher = tafFetcher;
        _cloudLayerBuilder = cloudLayerBuilder;
        _windLayerBuilder = windLayerBuilder;
        _icingCalculator = icingCalculator;
        _turbulenceCalculator = turbulenceCalculator;
        _stormModeler = stormModeler;
        _wakeTurbulenceEngine = wakeTurbulenceEngine;
        _stationFinder = stationFinder;
        _hazardAggregator = hazardAggregator;
    }

    private async Task<TafData?> FetchTafAsync(double latitude, double longitude)
    {
        var tafKey = $"taf:{latitude:F4},{longitude:F4}";
        return await _cache.GetOrFetchAsync(
            tafKey,
            () => _tafFetcher.FetchTafByPositionAsync(latitude, longitude),
            TimeSpan.FromMinutes(30));
    }

    public virtual async Task<PipelineData> FetchAllDataAsync(double latitude, double longitude, double altitudeFeet, DateTime modelTime, List<AircraftTraffic> traffic)
    {
        var metarKey = $"metar:{latitude:F4},{longitude:F4}";
        var metar = await _cache.GetOrFetchAsync(
            metarKey,
            () => _metarFetcher.FetchMetarByPositionAsync(latitude, longitude),
            TimeSpan.FromMinutes(5));

        if (metar == null)
        {
            LastError = "Failed to fetch METAR data";
            return PipelineData.Empty;
        }

        var windsKey = $"winds:{latitude:F4},{longitude:F4}";
        var windsAloft = await _cache.GetOrFetchAsync(
            windsKey,
            () => _windsAloftFetcher.FetchWindsAloftAsync(latitude, longitude),
            TimeSpan.FromMinutes(30));

        var taf = await FetchTafAsync(latitude, longitude);

        var sigmetsKey = "sigmets";
        var sigmets = await _cache.GetOrFetchAsync(
            sigmetsKey,
            () => _sigmetFetcher.FetchSigmetsAsync(),
            TimeSpan.FromMinutes(10)) ?? new List<WeatherHazard>();

        var lightningKey = $"lightning:{latitude:F4},{longitude:F4}";
        var lightning = await _cache.GetOrFetchAsync(
            lightningKey,
            () => _lightningFetcher.FetchNearbyStrikesAsync(latitude, longitude, 100, 50),
            TimeSpan.FromMinutes(1)) ?? new List<LightningStrike>();

        var radarPrecipKey = $"radar-precip:{latitude:F4},{longitude:F4}";
        var radarPrecip = await _cache.GetOrFetchAsync(
            radarPrecipKey,
            () => _radarFetcher.GetPrecipitationAtPositionAsync(latitude, longitude),
            TimeSpan.FromMinutes(1));

        var radarFrame = await _cache.GetOrFetchAsync(
            "radar-frame",
            () => _radarFetcher.GetLatestRadarFrameAsync(),
            TimeSpan.FromMinutes(2));

        var station = _stationFinder.AllAirports.FirstOrDefault(a =>
            string.Equals(a.IcaoId, metar.StationId, StringComparison.OrdinalIgnoreCase));
        if (station == null)
        {
            LastError = $"Station elevation unavailable for {metar.StationId}; cannot build MSL weather layers";
            throw new InvalidOperationException(LastError);
        }
        var cloudLayers = _cloudLayerBuilder.BuildCloudLayers(metar, windsAloft, station.ElevationFeet);
        var windLayers = _windLayerBuilder.BuildWindLayers(metar, windsAloft, station.ElevationFeet);
        var icingLayers = _icingCalculator.CalculateIcingLayers(cloudLayers, windLayers);
        var stormCells = _stormModeler.ModelStorms(lightning, sigmets, latitude, longitude, modelTime, windsAloft);
        var turbulenceLayers = _turbulenceCalculator.CalculateTurbulenceLayers(
            windLayers, cloudLayers, stormCells, altitudeFeet);
        
        turbulenceLayers = ApplyWakeTurbulence(turbulenceLayers, windLayers, latitude, longitude, altitudeFeet, traffic);
        ScaleTurbulenceLayers(turbulenceLayers);

        return new PipelineData(metar, windsAloft, cloudLayers, windLayers, icingLayers,
            turbulenceLayers, stormCells, sigmets, lightning, radarPrecip, radarFrame, taf);
    }

    public virtual WeatherState BuildWeatherState(
        PipelineData data, double latitude, double longitude, double altitudeFeet)
    {
        var metar = data.Metar;
        var metarPrecip = _hazardAggregator.CalculatePrecipitationRate(metar);
        double precipRate;
        if (metarPrecip > 0)
        {
            precipRate = metarPrecip + (data.RadarPrecip > 0 ? data.RadarPrecip * 5.0 : 0);
        }
        else if (metar.Clouds.Any(c => c.Coverage == "OVC" || c.Coverage == "BKN") && data.RadarPrecip > 1.0)
        {
            precipRate = data.RadarPrecip;
        }
        else
        {
            precipRate = 0.0;
        }

        var thunderstormIntensity = Math.Clamp(_hazardAggregator.CalculateThunderstormIntensity(metar, data.Sigmets, data.Lightning, data.StormCells) * ThunderstormIntensityScale, 0, 1);
        var gustKnots = metar.WindGustKnots.HasValue ? metar.WindGustKnots.Value * GustEnhancementScale : (double?)null;

        var state = new WeatherState
        {
            ObservationTime = metar.ObservationTime,
            StationId = metar.StationId,
            RawMetar = metar.RawText,
            Latitude = latitude,
            Longitude = longitude,
            TemperatureCelsius = metar.TemperatureCelsius,
            DewpointCelsius = metar.DewpointCelsius,
            PressureHpa = metar.AltimeterHpa,
            AltimeterHpa = metar.AltimeterHpa,
            VisibilityMeters = metar.VisibilityMeters,
            FlightCategory = metar.FlightCategory,
            WindDirectionDegrees = metar.WindDirectionDegrees,
            WindSpeedKnots = metar.WindSpeedKnots,
            WindGustKnots = gustKnots,
            CloudLayers = data.CloudLayers,
            WindsAloft = data.WindLayers,
            Hazards = data.Sigmets,
            IcingLayers = data.IcingLayers,
            TurbulenceLayers = data.TurbulenceLayers,
            StormCells = data.StormCells,
            PrecipitationRate = precipRate * PrecipitationScale,
            HumidityPercent = Meteorology.CalculateRelativeHumidity(metar.TemperatureCelsius, metar.DewpointCelsius),
            FreezingLevelFeet = _hazardAggregator.CalculateFreezingLevel(data.WindLayers),
            CeilingFeet = _hazardAggregator.CalculateCeiling(metar),
            IcingIndex = _hazardAggregator.CalculateIcingIndex(data.IcingLayers),
            TurbulenceIndex = _hazardAggregator.CalculateTurbulenceIndex(data.TurbulenceLayers, data.Lightning, altitudeFeet),
            ThunderstormIntensity = thunderstormIntensity,
            AerosolDensity = Math.Clamp(_hazardAggregator.CalculateAerosolDensity(metar.VisibilityMeters) * AerosolScale, 0, 1),
            ConvectiveAvailablePotentialEnergy = data.Winds?.ConvectiveAvailablePotentialEnergy,
            LiftedIndex = data.Winds?.LiftedIndex,
            SourceModelName = data.Winds?.SourceModel ?? "Unknown",
            DataAgeMinutes = data.Winds?.DataAgeMinutes ?? 0,
            Taf = data.Taf
        };

        // TAF remains available for briefing; forecasts must not overwrite observations.
        return state;
    }

    private List<TurbulenceLayer> ApplyWakeTurbulence(
        List<TurbulenceLayer> turbulenceLayers, List<WindLayer> windLayers, double latitude, double longitude, double altitudeFeet, List<AircraftTraffic> traffic)
    {
        if (!WakeTurbulenceEnabled) return turbulenceLayers;

        var airport = _stationFinder.FindNearestAirport(latitude, longitude);
        if (airport == null) return turbulenceLayers;

        var airportDistanceNm = CalculateDistanceNm(latitude, longitude, airport.Latitude, airport.Longitude);

        var wakeLayers = _wakeTurbulenceEngine.CalculateWakeLayers(
            latitude, longitude, altitudeFeet,
            traffic, windLayers,
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
}
