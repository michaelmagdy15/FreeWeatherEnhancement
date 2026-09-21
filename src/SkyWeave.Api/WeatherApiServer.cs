using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SkyWeave.Core.Services;

namespace SkyWeave.Api;

public class WeatherApiServer : IAsyncDisposable
{
    public const int DefaultPort = 54170;

    private static readonly StationFinder _stationFinder = new();
    private readonly WebApplication _app;

    public WebApplication App => _app;

    public WeatherApiServer(
        string[]? args = null,
        IWeatherDataProvider? customProvider = null,
        WeatherEngine? customEngine = null,
        string? listenUrl = null)
    {
        _app = BuildWebApplication(args, customProvider, customEngine, listenUrl);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => _app.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _app.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    public static WebApplication BuildWebApplication(
        string[]? args = null,
        IWeatherDataProvider? customProvider = null,
        WeatherEngine? customEngine = null,
        string? listenUrl = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        // Configure Kestrel / URLs
        if (!string.IsNullOrWhiteSpace(listenUrl))
        {
            builder.WebHost.UseUrls(listenUrl);
        }
        else
        {
            var urlsArg = args?.FirstOrDefault(a => a.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase));
            if (urlsArg != null)
            {
                builder.WebHost.UseUrls(urlsArg.Substring(7));
            }
            else
            {
                builder.WebHost.ConfigureKestrel(options =>
                {
                    // Bind to 0.0.0.0:54170 so loopback and LAN tablets (iPads) can access
                    options.Listen(IPAddress.Any, DefaultPort);
                });
            }
        }

        // Configure Services
        if (customEngine != null)
        {
            builder.Services.AddSingleton(customEngine);
        }
        else if (customProvider == null)
        {
            builder.Services.AddSingleton<WeatherEngine>();
        }

        if (customProvider != null)
        {
            builder.Services.AddSingleton(customProvider);
        }
        else
        {
            builder.Services.AddSingleton<IWeatherDataProvider, EngineWeatherDataProvider>();
        }

        var app = builder.Build();

        ConfigurePipeline(app, builder.Environment.ContentRootPath);

        return app;
    }

    public static void ConfigurePipeline(WebApplication app, string contentRoot)
    {
        var wwwrootDir = FindWwwRoot(contentRoot);

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".html"] = "text/html";
        contentTypeProvider.Mappings[".css"] = "text/css";
        contentTypeProvider.Mappings[".js"] = "application/javascript";
        contentTypeProvider.Mappings[".mjs"] = "application/javascript";
        contentTypeProvider.Mappings[".json"] = "application/json";
        contentTypeProvider.Mappings[".svg"] = "image/svg+xml";
        contentTypeProvider.Mappings[".png"] = "image/png";
        contentTypeProvider.Mappings[".jpg"] = "image/jpeg";
        contentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
        contentTypeProvider.Mappings[".ico"] = "image/x-icon";
        contentTypeProvider.Mappings[".woff2"] = "font/woff2";
        contentTypeProvider.Mappings[".woff"] = "font/woff";
        contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

        if (wwwrootDir != null && Directory.Exists(wwwrootDir))
        {
            var physicalProvider = new PhysicalFileProvider(wwwrootDir);
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = physicalProvider,
                DefaultFileNames = new List<string> { "index.html" }
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = physicalProvider,
                ContentTypeProvider = contentTypeProvider
            });
        }
        else
        {
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = contentTypeProvider
            });
        }

        // Fallback for root GET / if not served by static files
        app.MapGet("/", () =>
        {
            if (wwwrootDir != null)
            {
                var indexPath = Path.Combine(wwwrootDir, "index.html");
                if (File.Exists(indexPath))
                {
                    return Results.File(indexPath, "text/html; charset=utf-8");
                }
            }
            return Results.Content(
                "<!DOCTYPE html><html><head><title>SkyWeave EFB</title></head><body><h1>SkyWeave EFB Companion</h1></body></html>",
                "text/html; charset=utf-8");
        });

        // Health check endpoint
        app.MapGet("/health", (IWeatherDataProvider provider, IServiceProvider sp) =>
        {
            var engine = sp.GetService<WeatherEngine>();
            return Results.Ok(new
            {
                status = "ok",
                service = "SkyWeave",
                version = "0.5.0",
                lastError = engine?.LastError,
                timeUtc = DateTime.UtcNow
            });
        });

        // Status endpoint: { isRunning, version, simConnected, isInjecting, currentStation }
        var statusHandler = async (IWeatherDataProvider provider) =>
        {
            var status = await provider.GetStatusAsync();
            return Results.Ok(status);
        };
        app.MapGet("/api/status", statusHandler);
        app.MapGet("/status", statusHandler);

        // EFB tablet snapshot endpoint
        var efbHandler = async (IWeatherDataProvider provider, double? lat, double? lon, string? station) =>
        {
            if (!string.IsNullOrWhiteSpace(station) && (!lat.HasValue || !lon.HasValue))
            {
                var airport = _stationFinder.AllAirports.FirstOrDefault(a =>
                    string.Equals(a.IcaoId, station.Trim(), StringComparison.OrdinalIgnoreCase));
                if (airport != null)
                {
                    lat = airport.Latitude;
                    lon = airport.Longitude;
                }
            }

            var snapshot = await provider.GetEfbSnapshotAsync(lat, lon);
            return snapshot == null
                ? Results.Json(new { error = "efb snapshot unavailable" }, statusCode: 503)
                : Results.Ok(snapshot);
        };
        app.MapGet("/api/efb", efbHandler);
        app.MapGet("/efb", efbHandler);

        // State endpoint
        var stateHandler = async (IWeatherDataProvider provider, double? lat, double? lon) =>
        {
            var state = await provider.GetStateAsync(lat ?? 40.6399, lon ?? -73.7787);
            return state == null
                ? Results.Json(new { error = "weather data unavailable" }, statusCode: 503)
                : Results.Json(state);
        };
        app.MapGet("/api/state", stateHandler);
        app.MapGet("/state", stateHandler);

        // METAR endpoint
        var metarHandler = async (IWeatherDataProvider provider, double? lat, double? lon) =>
        {
            var metar = await provider.GetMetarAsync(lat ?? 40.6399, lon ?? -73.7787);
            return metar == null
                ? Results.Json(new { error = "metar unavailable" }, statusCode: 503)
                : Results.Json(metar);
        };
        app.MapGet("/api/metar", metarHandler);
        app.MapGet("/metar", metarHandler);

        // Hazards endpoint
        var hazardsHandler = async (IWeatherDataProvider provider, double? lat, double? lon) =>
        {
            var hazards = await provider.GetHazardsAsync(lat ?? 40.6399, lon ?? -73.7787);
            return Results.Json(hazards);
        };
        app.MapGet("/api/hazards", hazardsHandler);
        app.MapGet("/hazards", hazardsHandler);

        // Airport stations list for quick station switcher
        app.MapGet("/api/stations", () =>
        {
            var stations = _stationFinder.AllAirports.Select(a => new
            {
                icao = a.IcaoId,
                name = a.Name,
                lat = a.Latitude,
                lon = a.Longitude
            });
            return Results.Ok(stations);
        });
    }

    public static string? FindWwwRoot(string? contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            var path = Path.Combine(contentRoot, "wwwroot");
            if (Directory.Exists(path)) return path;
        }

        var basePath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(basePath)) return basePath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SkyWeave.Api", "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
