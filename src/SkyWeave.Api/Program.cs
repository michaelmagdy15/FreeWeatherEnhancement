using SkyWeave.Api;
using SkyWeave.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(54170);
});

builder.Services.AddSingleton<WeatherEngine>();
builder.Services.AddSingleton<IWeatherDataProvider, EngineWeatherDataProvider>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var engine = app.Services.GetRequiredService<WeatherEngine>();
engine.ErrorOccurred += (_, message) => logger.LogWarning("{Message}", message);
await engine.StartAsync(40.6399, -73.7787);

var provider = app.Services.GetRequiredService<IWeatherDataProvider>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "SkyWeave",
    version = typeof(WeatherEngine).Assembly.GetName().Version?.ToString() ?? "0.4.0-beta",
    lastError = engine.LastError,
    timeUtc = DateTime.UtcNow
}));

app.MapGet("/state", async (double? lat, double? lon) =>
{
    var state = await provider.GetStateAsync(lat ?? 40.6399, lon ?? -73.7787);
    return state == null ? Results.Json(new { error = "weather data unavailable" }, statusCode: 503) : Results.Json(state);
});

app.MapGet("/metar", async (double? lat, double? lon) =>
{
    var metar = await provider.GetMetarAsync(lat ?? 40.6399, lon ?? -73.7787);
    return metar == null ? Results.Json(new { error = "metar unavailable" }, statusCode: 503) : Results.Json(metar);
});

app.MapGet("/hazards", async (double? lat, double? lon) =>
{
    var hazards = await provider.GetHazardsAsync(lat ?? 40.6399, lon ?? -73.7787);
    return Results.Json(hazards);
});

app.Run();