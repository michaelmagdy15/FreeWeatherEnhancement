using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.Core.Fetchers;

public class Era5HistoricalFetcher
{
    private readonly HttpClient _httpClient;
    private readonly WeatherCache? _cache;
    private const string BaseUrl = "https://archive-api.open-meteo.com/v1/archive";
    private const double FEET_TO_METERS = 0.3048;
    private const double METERS_TO_FEET = 1.0 / FEET_TO_METERS;

    public static readonly DateTime MinHistoricalDate = new(1940, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTime MaxHistoricalDate => DateTime.UtcNow.Date.AddDays(-5);

    private static readonly int[] PressureLevels =
    {
        1000, 925, 850, 700, 500, 300, 250, 200
    };

    public Era5HistoricalFetcher() : this(new HttpClient(), null)
    {
    }

    public Era5HistoricalFetcher(HttpClient httpClient, WeatherCache? cache = null)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public static string BuildHistoricalArchiveUrl(double latitude, double longitude, DateTime targetUtc)
    {
        var dateStr = targetUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var tempParams = string.Join(",", PressureLevels.Select(p => $"temperature_{p}hPa"));
        var windSpeedParams = string.Join(",", PressureLevels.Select(p => $"wind_speed_{p}hPa"));
        var windDirParams = string.Join(",", PressureLevels.Select(p => $"wind_direction_{p}hPa"));
        var geopotentialParams = string.Join(",", PressureLevels.Select(p => $"geopotential_height_{p}hPa"));

        return $"{BaseUrl}?latitude={latitude.ToString("F4", CultureInfo.InvariantCulture)}" +
               $"&longitude={longitude.ToString("F4", CultureInfo.InvariantCulture)}" +
               $"&start_date={dateStr}&end_date={dateStr}" +
               $"&hourly=temperature_2m,relative_humidity_2m,dew_point_2m,surface_pressure,pressure_msl," +
               $"cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high,wind_speed_10m,wind_direction_10m," +
               $"wind_gusts_10m,precipitation,weather_code,{tempParams},{windSpeedParams},{windDirParams},{geopotentialParams}" +
               $"&wind_speed_unit=kn";
    }

    public async Task<WeatherState?> FetchHistoricalWeatherAsync(
        double latitude,
        double longitude,
        DateTime targetUtc,
        string? stationId = null,
        double stationElevationMeters = 0,
        CancellationToken cancellationToken = default)
    {
        targetUtc = DateTime.SpecifyKind(targetUtc, DateTimeKind.Utc);

        var cacheKey = $"era5_hist_{latitude:F2}_{longitude:F2}_{targetUtc:yyyyMMddHH}";
        if (_cache != null && _cache.TryGet<WeatherState>(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        var result = await FetchRetry.WithRetryAsync(
            () => FetchInternalAsync(latitude, longitude, targetUtc, stationId, stationElevationMeters, cancellationToken),
            maxRetries: 3);

        if (result != null && _cache != null)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
        }

        return result;
    }

    private async Task<WeatherState?> FetchInternalAsync(
        double latitude,
        double longitude,
        DateTime targetUtc,
        string? stationId,
        double stationElevationMeters,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = BuildHistoricalArchiveUrl(latitude, longitude, targetUtc);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(20));

            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cts.Token);
            return ParseHistoricalResponse(response, latitude, longitude, targetUtc, stationId, stationElevationMeters);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static WeatherState ParseHistoricalResponse(
        JsonElement response,
        double latitude,
        double longitude,
        DateTime targetUtc,
        string? stationId = null,
        double stationElevationMeters = 0)
    {
        var targetStation = !string.IsNullOrWhiteSpace(stationId) ? stationId.Trim().ToUpperInvariant() : "HIST";
        var state = new WeatherState
        {
            ObservationTime = targetUtc,
            StationId = targetStation,
            Latitude = latitude,
            Longitude = longitude,
            SourceModelName = "ERA5 Reanalysis (Historical)",
            DataAgeMinutes = 0,
            IsHistorical = true,
            HistoricalUtc = targetUtc
        };

        if (!response.TryGetProperty("hourly", out var hourly))
        {
            state.RawMetar = $"METAR {targetStation} {targetUtc:ddHH}00Z AUTO 00000KT 9999 CLR 15/10 Q1013";
            state.TemperatureCelsius = 15.0;
            state.DewpointCelsius = 10.0;
            state.AltimeterHpa = 1013.25;
            state.PressureHpa = 1013.25;
            state.VisibilityMeters = 16093.44;
            state.FlightCategory = "VFR";
            return state;
        }

        var timeArray = hourly.TryGetProperty("time", out var timeProp) && timeProp.ValueKind == JsonValueKind.Array
            ? timeProp.EnumerateArray().Select(t => t.GetString()).ToArray()
            : Array.Empty<string?>();

        var timeIndex = Math.Clamp(targetUtc.Hour, 0, Math.Max(0, timeArray.Length - 1));

        if (TryGetArrayDouble(hourly, "temperature_2m", timeIndex, out var temp2m))
            state.TemperatureCelsius = Math.Clamp(temp2m, -90.0, 60.0);
        else
            state.TemperatureCelsius = 15.0;

        if (TryGetArrayDouble(hourly, "dew_point_2m", timeIndex, out var dewp2m))
            state.DewpointCelsius = Math.Clamp(dewp2m, -90.0, state.TemperatureCelsius);
        else
            state.DewpointCelsius = state.TemperatureCelsius - 5.0;

        if (TryGetArrayDouble(hourly, "pressure_msl", timeIndex, out var mslp) && mslp > 0)
        {
            state.AltimeterHpa = Math.Clamp(mslp, 870.0, 1085.0);
            state.PressureHpa = state.AltimeterHpa;
        }
        else if (TryGetArrayDouble(hourly, "surface_pressure", timeIndex, out var surfP) && surfP > 0)
        {
            state.AltimeterHpa = Math.Clamp(surfP, 870.0, 1085.0);
            state.PressureHpa = state.AltimeterHpa;
        }
        else
        {
            state.AltimeterHpa = 1013.25;
            state.PressureHpa = 1013.25;
        }

        if (TryGetArrayDouble(hourly, "wind_direction_10m", timeIndex, out var wd))
            state.WindDirectionDegrees = ((wd % 360) + 360) % 360;

        if (TryGetArrayDouble(hourly, "wind_speed_10m", timeIndex, out var ws))
            state.WindSpeedKnots = Math.Max(0.0, ws);

        if (TryGetArrayDouble(hourly, "wind_gusts_10m", timeIndex, out var wg) && wg > state.WindSpeedKnots + 3.0)
            state.WindGustKnots = wg;

        if (TryGetArrayDouble(hourly, "relative_humidity_2m", timeIndex, out var rh))
            state.HumidityPercent = Math.Clamp(rh, 0.0, 100.0);

        if (TryGetArrayDouble(hourly, "precipitation", timeIndex, out var precip))
            state.PrecipitationRate = Math.Max(0.0, precip);

        int weatherCode = 0;
        if (hourly.TryGetProperty("weather_code", out var wcArr) &&
            wcArr.ValueKind == JsonValueKind.Array &&
            wcArr.GetArrayLength() > timeIndex &&
            wcArr[timeIndex].ValueKind == JsonValueKind.Number)
        {
            weatherCode = wcArr[timeIndex].GetInt32();
        }

        ApplyWeatherCode(state, weatherCode);

        // Synthesize clouds from low/mid/high cover
        TryGetArrayDouble(hourly, "cloud_cover_low", timeIndex, out var lowCover);
        TryGetArrayDouble(hourly, "cloud_cover_mid", timeIndex, out var midCover);
        TryGetArrayDouble(hourly, "cloud_cover_high", timeIndex, out var highCover);
        TryGetArrayDouble(hourly, "cloud_cover", timeIndex, out var totalCover);

        var cloudLayers = SynthesizeCloudLayers(lowCover, midCover, highCover, totalCover, stationElevationMeters);
        state.CloudLayers = cloudLayers;

        // Winds aloft from pressure levels
        var windsAloft = SynthesizeWindsAloft(hourly, timeIndex, state, stationElevationMeters);
        state.WindsAloft = windsAloft;

        // Ceiling & Flight Category
        ComputeCeilingAndCategory(state);

        // Build synthetic METAR
        state.RawMetar = BuildSyntheticMetar(state, targetUtc);

        return state;
    }

    private static void ApplyWeatherCode(WeatherState state, int code)
    {
        switch (code)
        {
            case 0: // Clear sky
            case 1: // Mainly clear
                state.Precipitation = PrecipitationType.None;
                state.VisibilityMeters = 16093.44; // 10 SM
                state.AerosolDensity = 0.05;
                break;
            case 2: // Partly cloudy
            case 3: // Overcast
                state.Precipitation = PrecipitationType.None;
                state.VisibilityMeters = 16093.44;
                state.AerosolDensity = 0.1;
                break;
            case 45: // Fog
            case 48: // Depositing rime fog
                state.Precipitation = PrecipitationType.Fog;
                state.VisibilityMeters = 800.0;
                state.AerosolDensity = 0.9;
                break;
            case 51: // Drizzle: light
            case 53: // Drizzle: moderate
            case 55: // Drizzle: dense
            case 56: // Freezing drizzle light
            case 57: // Freezing drizzle dense
                state.Precipitation = (code is 56 or 57) ? PrecipitationType.FreezingRain : PrecipitationType.Drizzle;
                state.VisibilityMeters = 4000.0;
                state.AerosolDensity = 0.4;
                break;
            case 61: // Rain: slight
            case 80: // Rain showers slight
                state.Precipitation = PrecipitationType.Rain;
                state.VisibilityMeters = 8000.0;
                state.AerosolDensity = 0.3;
                break;
            case 63: // Rain: moderate
            case 81: // Rain showers moderate
                state.Precipitation = PrecipitationType.Rain;
                state.VisibilityMeters = 4800.0;
                state.AerosolDensity = 0.5;
                break;
            case 65: // Rain: heavy
            case 82: // Rain showers violent
                state.Precipitation = PrecipitationType.Rain;
                state.VisibilityMeters = 2400.0;
                state.AerosolDensity = 0.7;
                break;
            case 66: // Freezing rain light
            case 67: // Freezing rain heavy
                state.Precipitation = PrecipitationType.FreezingRain;
                state.VisibilityMeters = 3000.0;
                state.AerosolDensity = 0.6;
                state.IcingIndex = 0.8;
                break;
            case 71: // Snow fall slight
            case 73: // Snow fall moderate
            case 75: // Snow fall heavy
            case 77: // Snow grains
            case 85: // Snow showers slight
            case 86: // Snow showers heavy
                state.Precipitation = PrecipitationType.Snow;
                state.VisibilityMeters = code == 75 ? 1200.0 : 3500.0;
                state.AerosolDensity = 0.6;
                break;
            case 95: // Thunderstorm slight or moderate
            case 96: // Thunderstorm with slight hail
            case 99: // Thunderstorm with heavy hail
                state.Precipitation = PrecipitationType.Rain;
                state.ThunderstormIntensity = 0.8;
                state.VisibilityMeters = 2000.0;
                state.AerosolDensity = 0.8;
                state.TurbulenceIndex = 0.75;
                break;
            default:
                state.Precipitation = PrecipitationType.None;
                state.VisibilityMeters = 16093.44;
                state.AerosolDensity = 0.1;
                break;
        }
    }

    private static List<CloudLayer> SynthesizeCloudLayers(
        double lowCover,
        double midCover,
        double highCover,
        double totalCover,
        double stationElevationMeters)
    {
        var layers = new List<CloudLayer>();
        var baseElevFeet = stationElevationMeters * METERS_TO_FEET;
        int layerId = 1;

        if (lowCover > 8.0)
        {
            var baseFeet = baseElevFeet + 2000.0;
            var topFeet = baseFeet + 2500.0;
            layers.Add(new CloudLayer
            {
                Id = layerId++,
                BaseMeters = baseFeet * FEET_TO_METERS,
                TopMeters = topFeet * FEET_TO_METERS,
                BaseFeetAgl = 2000.0,
                TopFeetAgl = 4500.0,
                CoveragePercent = Math.Clamp(lowCover, 1.0, 100.0),
                Type = lowCover >= 87.5 ? CloudType.OVC : (lowCover >= 50.0 ? CloudType.BKN : (lowCover >= 25.0 ? CloudType.SCT : CloudType.FEW)),
                Density = Math.Clamp(0.5 + (lowCover / 200.0), 0.2, 1.0),
                Scattering = 0.65
            });
        }

        if (midCover > 12.0)
        {
            var baseFeet = Math.Max(baseElevFeet + 6000.0, 8000.0);
            var topFeet = baseFeet + 5000.0;
            layers.Add(new CloudLayer
            {
                Id = layerId++,
                BaseMeters = baseFeet * FEET_TO_METERS,
                TopMeters = topFeet * FEET_TO_METERS,
                BaseFeetAgl = baseFeet - baseElevFeet,
                TopFeetAgl = topFeet - baseElevFeet,
                CoveragePercent = Math.Clamp(midCover, 1.0, 100.0),
                Type = midCover >= 87.5 ? CloudType.OVC : (midCover >= 50.0 ? CloudType.BKN : (midCover >= 25.0 ? CloudType.SCT : CloudType.FEW)),
                Density = Math.Clamp(0.4 + (midCover / 250.0), 0.2, 0.9),
                Scattering = 0.5
            });
        }

        if (highCover > 15.0)
        {
            var baseFeet = 24000.0;
            var topFeet = 29000.0;
            layers.Add(new CloudLayer
            {
                Id = layerId++,
                BaseMeters = baseFeet * FEET_TO_METERS,
                TopMeters = topFeet * FEET_TO_METERS,
                BaseFeetAgl = baseFeet - baseElevFeet,
                TopFeetAgl = topFeet - baseElevFeet,
                CoveragePercent = Math.Clamp(highCover, 1.0, 100.0),
                Type = highCover >= 87.5 ? CloudType.OVC : (highCover >= 50.0 ? CloudType.BKN : (highCover >= 25.0 ? CloudType.SCT : CloudType.FEW)),
                Density = Math.Clamp(0.2 + (highCover / 300.0), 0.1, 0.6),
                Scattering = 0.35
            });
        }

        if (layers.Count == 0 && totalCover > 10.0)
        {
            var baseFeet = baseElevFeet + 3500.0;
            var topFeet = baseFeet + 3000.0;
            layers.Add(new CloudLayer
            {
                Id = layerId++,
                BaseMeters = baseFeet * FEET_TO_METERS,
                TopMeters = topFeet * FEET_TO_METERS,
                BaseFeetAgl = 3500.0,
                TopFeetAgl = 6500.0,
                CoveragePercent = Math.Clamp(totalCover, 1.0, 100.0),
                Type = totalCover >= 87.5 ? CloudType.OVC : (totalCover >= 50.0 ? CloudType.BKN : (totalCover >= 25.0 ? CloudType.SCT : CloudType.FEW)),
                Density = 0.5,
                Scattering = 0.5
            });
        }

        return layers;
    }

    private static List<WindLayer> SynthesizeWindsAloft(
        JsonElement hourly,
        int timeIndex,
        WeatherState state,
        double stationElevationMeters)
    {
        var layers = new List<WindLayer>();
        var stationElevFeet = stationElevationMeters * METERS_TO_FEET;

        // Surface layer anchor
        layers.Add(new WindLayer
        {
            Id = 0,
            IsSurfaceLayer = true,
            AltitudeMeters = stationElevationMeters,
            AltitudeFeet = stationElevFeet,
            DirectionDegrees = state.WindDirectionDegrees,
            SpeedKnots = state.WindSpeedKnots,
            GustSpeedKnots = state.WindGustKnots,
            TemperatureCelsius = state.TemperatureCelsius
        });

        int layerId = 1;
        foreach (var p in PressureLevels)
        {
            var tempKey = $"temperature_{p}hPa";
            var windSpeedKey = $"wind_speed_{p}hPa";
            var windDirKey = $"wind_direction_{p}hPa";
            var gphKey = $"geopotential_height_{p}hPa";

            TryGetArrayDouble(hourly, tempKey, timeIndex, out var lvlTemp);
            TryGetArrayDouble(hourly, windSpeedKey, timeIndex, out var lvlSpeed);
            TryGetArrayDouble(hourly, windDirKey, timeIndex, out var lvlDir);
            TryGetArrayDouble(hourly, gphKey, timeIndex, out var lvlGph);

            double altitudeMeters = lvlGph > 0 ? lvlGph : ApproximateAltitudeMeters(p);
            double altitudeFeet = altitudeMeters * METERS_TO_FEET;

            if (altitudeFeet <= stationElevFeet + 150)
                continue; // Skip pressure levels underground / below station

            layers.Add(new WindLayer
            {
                Id = layerId++,
                IsSurfaceLayer = false,
                AltitudeMeters = altitudeMeters,
                AltitudeFeet = altitudeFeet,
                DirectionDegrees = ((lvlDir % 360) + 360) % 360,
                SpeedKnots = Math.Max(0.0, lvlSpeed),
                TemperatureCelsius = Math.Clamp(lvlTemp, -90.0, 50.0)
            });
        }

        return layers.OrderBy(l => l.AltitudeFeet).ToList();
    }

    private static double ApproximateAltitudeMeters(int pressureHpa)
    {
        return pressureHpa switch
        {
            1000 => 110,
            925 => 760,
            850 => 1460,
            700 => 3010,
            500 => 5570,
            300 => 9160,
            250 => 10360,
            200 => 11780,
            _ => 0
        };
    }

    private static void ComputeCeilingAndCategory(WeatherState state)
    {
        // Ceiling is lowest layer with coverage >= 50% (BKN / OVC)
        var ceilingLayer = state.CloudLayers
            .Where(c => c.CoveragePercent >= 50.0 || c.Type == CloudType.BKN || c.Type == CloudType.OVC)
            .OrderBy(c => c.BaseMeters)
            .FirstOrDefault();

        if (ceilingLayer != null)
        {
            state.CeilingFeet = ceilingLayer.BaseMeters * METERS_TO_FEET;
        }
        else
        {
            state.CeilingFeet = 30000; // Unlimited
        }

        var visSm = state.VisibilityMeters / 1609.344;
        if (state.CeilingFeet < 500 || visSm < 1.0)
        {
            state.FlightCategory = "LIFR";
        }
        else if (state.CeilingFeet < 1000 || visSm < 3.0)
        {
            state.FlightCategory = "IFR";
        }
        else if (state.CeilingFeet <= 3000 || visSm <= 5.0)
        {
            state.FlightCategory = "MVFR";
        }
        else
        {
            state.FlightCategory = "VFR";
        }
    }

    private static string BuildSyntheticMetar(WeatherState state, DateTime targetUtc)
    {
        var windDir = ((int)Math.Round(state.WindDirectionDegrees) % 360);
        var windSpd = (int)Math.Round(state.WindSpeedKnots);
        string windPart = windSpd == 0 ? "00000KT" : $"{windDir:D3}{windSpd:D2}KT";
        if (state.WindGustKnots.HasValue && state.WindGustKnots.Value > windSpd + 3)
        {
            windPart += $"G{(int)Math.Round(state.WindGustKnots.Value):D2}KT";
        }

        var visSm = Math.Round(state.VisibilityMeters / 1609.344);
        string visPart = visSm >= 10 ? "10SM" : $"{(int)Math.Max(1, visSm)}SM";

        var cloudTokens = new List<string>();
        foreach (var c in state.CloudLayers.OrderBy(c => c.BaseMeters))
        {
            var baseHundreds = (int)Math.Round((c.BaseMeters * METERS_TO_FEET) / 100.0);
            string cov = c.Type.ToString();
            cloudTokens.Add($"{cov}{baseHundreds:D3}");
        }
        string cloudPart = cloudTokens.Count > 0 ? string.Join(" ", cloudTokens) : "CLR";

        int temp = (int)Math.Round(state.TemperatureCelsius);
        int dewp = (int)Math.Round(state.DewpointCelsius);
        string tempStr = temp < 0 ? $"M{Math.Abs(temp):D2}" : $"{temp:D2}";
        string dewpStr = dewp < 0 ? $"M{Math.Abs(dewp):D2}" : $"{dewp:D2}";

        int altimInHg = (int)Math.Round(state.AltimeterHpa * 0.029529983 * 100.0);
        string altimPart = $"A{altimInHg:D4}";

        return $"METAR {state.StationId} {targetUtc:ddHH}00Z AUTO {windPart} {visPart} {cloudPart} {tempStr}/{dewpStr} {altimPart}";
    }

    private static bool TryGetArrayDouble(JsonElement hourly, string key, int timeIndex, out double value)
    {
        value = 0;
        if (!hourly.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() <= timeIndex)
            return false;

        var elem = arr[timeIndex];
        if (elem.ValueKind == JsonValueKind.Null || elem.ValueKind != JsonValueKind.Number)
            return false;

        value = elem.GetDouble();
        return true;
    }
}
