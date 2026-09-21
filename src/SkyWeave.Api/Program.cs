using SkyWeave.Api;
using SkyWeave.Core.Services;

var app = WeatherApiServer.BuildWebApplication(args);

var engine = app.Services.GetService<WeatherEngine>();
if (engine != null)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    engine.ErrorOccurred += (_, message) => logger.LogWarning("{Message}", message);
    await engine.StartAsync(40.6399, -73.7787);
}

await app.RunAsync();

public partial class Program { }