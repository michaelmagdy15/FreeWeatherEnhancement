using SkyWeave.Api;
var app = WeatherApiServer.BuildWebApplication(args);
await app.RunAsync();

public partial class Program { }
