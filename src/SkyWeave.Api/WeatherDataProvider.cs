using System.Text.Json.Serialization;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;

namespace SkyWeave.Api;

public class ApiStatus
{
    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; } = true;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.5.0";

    [JsonPropertyName("simConnected")]
    public bool SimConnected { get; set; }

    [JsonPropertyName("isInjecting")]
    public bool IsInjecting { get; set; }

    [JsonPropertyName("currentStation")]
    public string CurrentStation { get; set; } = "KJFK";
}

public class EfbSnapshot
{
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
}

public interface IWeatherDataProvider
{
    Task<WeatherState?> GetStateAsync(double latitude, double longitude);
    Task<MetarData?> GetMetarAsync(double latitude, double longitude);
    Task<List<WeatherHazard>> GetHazardsAsync(double latitude, double longitude);
    Task<ApiStatus> GetStatusAsync();
    Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null);
}

public class EngineWeatherDataProvider : IWeatherDataProvider
{
    private readonly SkyWeave.Core.Services.WeatherEngine _engine;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool SimConnected { get; set; }
    public bool IsInjecting { get; set; }
    public string CurrentStation { get; set; } = "KJFK";

    public EngineWeatherDataProvider(SkyWeave.Core.Services.WeatherEngine engine)
    {
        _engine = engine;
        _engine.WeatherUpdated += (_, state) =>
        {
            if (!string.IsNullOrWhiteSpace(state.StationId))
            {
                CurrentStation = state.StationId;
            }
        };
    }

    public async Task<WeatherState?> GetStateAsync(double latitude, double longitude)
    {
        return await ExecuteAsync(latitude, longitude, () => _engine.FetchCurrentWeatherAsync());
    }

    public async Task<MetarData?> GetMetarAsync(double latitude, double longitude)
    {
        var state = await ExecuteAsync(latitude, longitude, () => _engine.FetchCurrentWeatherAsync());
        if (state == null) return null;

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
            Clouds = new List<MetarCloud>()
        };
    }

    public async Task<List<WeatherHazard>> GetHazardsAsync(double latitude, double longitude)
    {
        var state = await ExecuteAsync(latitude, longitude, () => _engine.FetchCurrentWeatherAsync());
        return state?.Hazards ?? new List<WeatherHazard>();
    }

    public Task<ApiStatus> GetStatusAsync()
    {
        var station = _engine.CurrentState?.StationId;
        if (string.IsNullOrWhiteSpace(station))
            station = CurrentStation;

        return Task.FromResult(new ApiStatus
        {
            IsRunning = _engine.IsRunning,
            Version = "0.5.0",
            SimConnected = SimConnected,
            IsInjecting = IsInjecting || (_engine.IsRunning && !_engine.PassiveMode),
            CurrentStation = station ?? "KJFK"
        });
    }

    public async Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null)
    {
        WeatherState? state;
        if (latitude.HasValue && longitude.HasValue)
        {
            state = await GetStateAsync(latitude.Value, longitude.Value);
        }
        else
        {
            state = _engine.CurrentState ?? await _engine.FetchCurrentWeatherAsync();
        }

        if (state == null) return null;

        var radar = _engine.CurrentRadarFrame;

        var rawMetar = !string.IsNullOrWhiteSpace(state.RawMetar)
            ? state.RawMetar
            : $"{state.StationId} {state.ObservationTime:ddHHmm}Z {state.WindDirectionDegrees:000}{state.WindSpeedKnots:00}KT {state.VisibilityMeters / 1609.344:0}SM {state.FlightCategory} {state.TemperatureCelsius:0}/{state.DewpointCelsius:0} A{(state.AltimeterHpa * 0.02952998751 * 100):0000}";

        return new EfbSnapshot
        {
            StationId = !string.IsNullOrWhiteSpace(state.StationId) ? state.StationId : CurrentStation,
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
            Latitude = state.Latitude != 0 ? state.Latitude : (latitude ?? 40.6399),
            Longitude = state.Longitude != 0 ? state.Longitude : (longitude ?? -73.7787),
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
            Taf = state.Taf
        };
    }

    private async Task<T?> ExecuteAsync<T>(double latitude, double longitude, Func<Task<T?>> fetch)
    {
        await _gate.WaitAsync();
        try
        {
            _engine.SetPosition(latitude, longitude);
            return await fetch();
        }
        finally
        {
            _gate.Release();
        }
    }
}
