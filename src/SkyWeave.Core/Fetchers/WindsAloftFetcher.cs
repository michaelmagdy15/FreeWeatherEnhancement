using System.Net.Http.Json;
using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class WindsAloftFetcher
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";
    private const double FEET_TO_METERS = 0.3048;
    private const double METERS_TO_FEET = 1 / FEET_TO_METERS;

    private static readonly int[] PressureLevels =
    {
        1000, 975, 950, 925, 900, 850, 800, 700, 600, 500,
        400, 300, 250, 200, 150, 100, 70, 50, 30
    };

    public WindsAloftFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WindsAloftData?> FetchWindsAloftAsync(double latitude, double longitude)
    {
        var regional = SelectRegionalModelCore(latitude, longitude);
        if (regional != null)
        {
            var result = await FetchRetry.WithRetryAsync(
                () => FetchInternalAsync(latitude, longitude, regional.Value.model, regional.Value.name),
                maxRetries: 3);

            if (result != null)
                return result;
        }

        return await FetchRetry.WithRetryAsync(
            () => FetchInternalAsync(latitude, longitude, "ncep_gfs025", "GFS 0.25°"),
            maxRetries: 3);
    }

    public string SelectRegionalModel(double latitude, double longitude)
    {
        var model = SelectRegionalModelCore(latitude, longitude);
        return model?.name ?? "GFS 0.25°";
    }

    private (string model, string name)? SelectRegionalModelCore(double latitude, double longitude)
    {
        if (latitude is >= 21 and <= 53 && longitude is >= -130 and <= -60)
            return ("ncep_hrrr_conus", "HRRR CONUS 3 km");

        if (latitude is >= 30 and <= 73 && longitude is >= -30 and <= 42)
            return ("dwd_icon_eu", "ICON-EU 13 km");

        return null;
    }

    private async Task<WindsAloftData?> FetchInternalAsync(double latitude, double longitude, string model, string modelName)
    {
        try
        {
            var tempParams = string.Join(",", PressureLevels.Select(p => $"temperature_{p}hPa"));
            var windSpeedParams = string.Join(",", PressureLevels.Select(p => $"wind_speed_{p}hPa"));
            var windDirParams = string.Join(",", PressureLevels.Select(p => $"wind_direction_{p}hPa"));
            var humidityParams = string.Join(",", PressureLevels.Select(p => $"relative_humidity_{p}hPa"));
            var cloudCoverParams = string.Join(",", PressureLevels.Select(p => $"cloud_cover_{p}hPa"));
            var geopotentialParams = string.Join(",", PressureLevels.Select(p => $"geopotential_height_{p}hPa"));

            var url = $"{BaseUrl}?latitude={latitude:F4}&longitude={longitude:F4}" +
                     $"&hourly={tempParams},{windSpeedParams},{windDirParams},{humidityParams},{cloudCoverParams},{geopotentialParams}," +
                     $"cape,lifted_index,freezing_level_height" +
                     $"&pressure_level={string.Join(",", PressureLevels)}" +
                     $"&models={model}" +
                     $"&wind_speed_unit=kn" +
                     $"&forecast_days=1";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cts.Token);

            return ParseWindsAloftResponse(response, latitude, longitude, modelName);
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

    public static WindsAloftData ParseWindsAloftResponse(JsonElement response, double latitude, double longitude, string modelName)
    {
        var data = new WindsAloftData
        {
            Latitude = latitude,
            Longitude = longitude,
            ForecastTime = DateTime.UtcNow,
            SourceModel = modelName,
            ModelResolutionKm = GetModelResolutionKm(modelName)
        };

        if (!response.TryGetProperty("hourly", out var hourly))
            return data;

        var timeArray = hourly.TryGetProperty("time", out var time)
            ? time.EnumerateArray().Select(t => DateTime.Parse(t.GetString()!, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)).ToArray()
            : Array.Empty<DateTime>();

        if (timeArray.Length == 0)
            return data;

        var currentTime = timeArray.LastOrDefault(t => t <= DateTime.UtcNow);
        if (currentTime == default) currentTime = timeArray[0];
        var timeIndex = Array.IndexOf(timeArray, currentTime);
        if (timeIndex < 0) timeIndex = 0;

        if (response.TryGetProperty("model_run", out var modelRun) &&
            modelRun.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(modelRun.GetString(), out var runOffset))
        {
            var runTime = runOffset.UtcDateTime;
            data.ForecastTime = runTime;
            data.DataAgeMinutes = Math.Max(0, (DateTime.UtcNow - runTime).TotalMinutes);
        }

        TryGetDouble(hourly, "cape", timeIndex, out var cape);
        data.ConvectiveAvailablePotentialEnergy = cape;

        TryGetDouble(hourly, "lifted_index", timeIndex, out var liftedIndex);
        data.LiftedIndex = liftedIndex;

        TryGetDouble(hourly, "freezing_level_height", timeIndex, out var freezingLevel);
        data.FreezingLevelHeightMeters = freezingLevel;

        var requestedLevels = PressureLevels;
        if (response.TryGetProperty("pressure_levels", out var levels) && levels.ValueKind == JsonValueKind.Array)
        {
            var listed = levels.EnumerateArray()
                .Select(l => l.GetInt32())
                .Where(requestedLevels.Contains)
                .ToArray();
            if (listed.Length > 0)
                requestedLevels = listed;
        }

        for (int i = 0; i < requestedLevels.Length; i++)
        {
            var pressure = requestedLevels[i];
            var levelData = new PressureLevelData
            {
                PressureHpa = pressure,
                AltitudeMeters = GetApproximateAltitude(pressure),
                AltitudeFeet = GetApproximateAltitude(pressure) * METERS_TO_FEET
            };

            var tempKey = $"temperature_{pressure}hPa";
            if (TryGetArrayDouble(hourly, tempKey, timeIndex, out var tempValue))
                levelData.TemperatureCelsius = tempValue;

            var windSpeedKey = $"wind_speed_{pressure}hPa";
            if (TryGetArrayDouble(hourly, windSpeedKey, timeIndex, out var windSpeedValue))
                levelData.WindSpeedKnots = windSpeedValue;

            var windDirKey = $"wind_direction_{pressure}hPa";
            if (TryGetArrayDouble(hourly, windDirKey, timeIndex, out var windDirValue))
                levelData.WindDirectionDegrees = windDirValue;

            var humidityKey = $"relative_humidity_{pressure}hPa";
            if (TryGetArrayDouble(hourly, humidityKey, timeIndex, out var humidityValue))
                levelData.RelativeHumidity = humidityValue;

            var cloudCoverKey = $"cloud_cover_{pressure}hPa";
            if (TryGetArrayDouble(hourly, cloudCoverKey, timeIndex, out var cloudCoverValue))
                levelData.CloudCoverPercent = cloudCoverValue;

            var geopotentialKey = $"geopotential_height_{pressure}hPa";
            if (TryGetArrayDouble(hourly, geopotentialKey, timeIndex, out var gphValue) && gphValue > 0)
            {
                levelData.GeopotentialHeightMeters = gphValue;
                levelData.AltitudeMeters = gphValue;
                levelData.AltitudeFeet = gphValue * METERS_TO_FEET;
            }

            data.PressureLevels.Add(levelData);
        }

        return data;
    }

    private static bool TryGetArrayDouble(JsonElement hourly, string key, int timeIndex, out double value)
    {
        value = 0;
        if (!hourly.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() <= timeIndex)
            return false;

        var raw = arr[timeIndex];
        if (raw.ValueKind != JsonValueKind.Number)
            return false;

        value = raw.GetDouble();
        return true;
    }

    private static void TryGetDouble(JsonElement hourly, string key, int timeIndex, out double? value)
    {
        value = null;
        if (!hourly.TryGetProperty(key, out var arr) || arr.GetArrayLength() <= timeIndex)
            return;

        var raw = arr[timeIndex];
        if (raw.ValueKind == JsonValueKind.Number)
            value = raw.GetDouble();
    }

    private static double GetModelResolutionKm(string modelName)
    {
        if (modelName.StartsWith("HRRR")) return 3;
        if (modelName.StartsWith("ICON")) return 13;
        return 25;
    }

    private static double GetApproximateAltitude(double pressureHpa)
    {
        return pressureHpa switch
        {
            1000 => 110,
            975 => 320,
            950 => 500,
            925 => 800,
            900 => 1000,
            850 => 1500,
            800 => 1900,
            700 => 3000,
            600 => 4200,
            500 => 5600,
            400 => 7200,
            300 => 9200,
            250 => 10400,
            200 => 11800,
            150 => 13500,
            100 => 15800,
            70 => 17700,
            50 => 19300,
            30 => 22000,
            _ => 0
        };
    }
}