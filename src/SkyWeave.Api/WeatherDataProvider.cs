using SkyWeave.Core.Models;

namespace SkyWeave.Api;

public interface IWeatherDataProvider
{
    Task<WeatherState?> GetStateAsync(double latitude, double longitude);
    Task<MetarData?> GetMetarAsync(double latitude, double longitude);
    Task<List<WeatherHazard>> GetHazardsAsync(double latitude, double longitude);
}

public class EngineWeatherDataProvider : IWeatherDataProvider
{
    private readonly SkyWeave.Core.Services.WeatherEngine _engine;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public EngineWeatherDataProvider(SkyWeave.Core.Services.WeatherEngine engine)
    {
        _engine = engine;
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