using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Plugins;
using SkyWeave.Core.Services;

namespace SkyWeave.Api;

public class WeatherApiServer : IAsyncDisposable, IDisposable
{
    public const int DefaultPort = 54170;

    private static readonly StationFinder _stationFinder = new();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private WebApplication _app;
    private bool _hasStartedOrFaulted;

    private readonly string[]? _args;
    private readonly IWeatherDataProvider? _customProvider;
    private readonly WeatherEngine? _customEngine;
    private readonly string? _listenUrl;
    private readonly bool _allowPositionOverride;
    private bool _allowLanAccess;
    private readonly int _port;
    private readonly string? _wwwrootDir;
    private readonly bool _hasStaticAssets;

    public WebApplication App => _app;
    public ServerState State { get; private set; } = ServerState.Stopped;
    public bool IsRunning => State == ServerState.Running;
    public bool IsPortConflict { get; private set; }
    public string? LastError { get; private set; }
    public int Port => _port;
    public bool AllowLanAccess => _allowLanAccess;
    public bool AllowPositionOverride => _allowPositionOverride;
    public string LocalUrl => EfbConnectionHelper.GetLocalUrl(_port);
    public IReadOnlyList<string> LanUrls => _allowLanAccess ? EfbConnectionHelper.GetLanUrls(_port) : Array.Empty<string>();
    public bool HasStaticAssets => _hasStaticAssets;
    public string? WwwRootDir => _wwwrootDir;

    public EfbServerInfo ServerInfo => new()
    {
        ServerState = State.ToString(),
        Port = _port,
        AllowLanAccess = _allowLanAccess,
        HasStaticAssets = _hasStaticAssets,
        LocalUrl = LocalUrl,
        LanUrls = LanUrls,
        WwwRootDir = _wwwrootDir,
        IsPortConflict = IsPortConflict,
        LastError = LastError
    };

    private readonly string? _wwwrootDirOverride;

    public WeatherApiServer(
        string[]? args = null,
        IWeatherDataProvider? customProvider = null,
        WeatherEngine? customEngine = null,
        string? listenUrl = null,
        bool allowPositionOverride = true,
        bool allowLanAccess = false,
        int port = DefaultPort,
        string? wwwrootDirOverride = null)
    {
        _args = args;
        _customProvider = customProvider;
        _customEngine = customEngine;
        _listenUrl = listenUrl;
        _allowPositionOverride = allowPositionOverride;
        _allowLanAccess = allowLanAccess;
        _port = port > 0 ? port : DefaultPort;
        _wwwrootDirOverride = wwwrootDirOverride;

        _wwwrootDir = wwwrootDirOverride != null
            ? (string.IsNullOrEmpty(wwwrootDirOverride) ? null : (Directory.Exists(wwwrootDirOverride) ? wwwrootDirOverride : null))
            : FindWwwRoot(null);
        _hasStaticAssets = _wwwrootDir != null && File.Exists(Path.Combine(_wwwrootDir, "index.html"));

        _app = BuildWebApplication(_args, _customProvider, _customEngine, _listenUrl, _allowPositionOverride, _allowLanAccess, _port, _wwwrootDirOverride);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (State == ServerState.Disposed)
                throw new ObjectDisposedException(nameof(WeatherApiServer));

            if (State == ServerState.Running)
                return; // Idempotent start

            cancellationToken.ThrowIfCancellationRequested();

            State = ServerState.Starting;
            IsPortConflict = false;
            LastError = null;

            if (_hasStartedOrFaulted)
            {
                try
                {
                    await _app.DisposeAsync();
                }
                catch { }

                _app = BuildWebApplication(_args, _customProvider, _customEngine, _listenUrl, _allowPositionOverride, _allowLanAccess, _port);
            }

            _hasStartedOrFaulted = true;

            await _app.StartAsync(cancellationToken);
            State = ServerState.Running;
        }
        catch (OperationCanceledException)
        {
            State = ServerState.Stopped;
            throw;
        }
        catch (Exception ex)
        {
            State = ServerState.Faulted;
            LastError = ex.Message;

            if (EfbConnectionHelper.IsPortConflictException(ex))
            {
                IsPortConflict = true;
                LastError = $"Port {_port} is already in use by another application or previous SkyWeave instance.";
            }

            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (State == ServerState.Stopped || State == ServerState.Disposed || State == ServerState.Faulted)
                return;

            State = ServerState.Stopping;
            await _app.StopAsync(cancellationToken);
            State = ServerState.Stopped;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task RestartAsync(bool? allowLanAccess = null, CancellationToken cancellationToken = default)
    {
        if (allowLanAccess.HasValue)
        {
            _allowLanAccess = allowLanAccess.Value;
        }

        if (State == ServerState.Running || State == ServerState.Starting)
        {
            await StopAsync(cancellationToken);
        }

        await StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (State == ServerState.Disposed)
                return;

            if (State == ServerState.Running || State == ServerState.Starting)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _app.StopAsync(cts.Token);
                }
                catch { }
            }

            await _app.DisposeAsync();
            State = ServerState.Disposed;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
        }
        catch { }
    }

    public static WebApplication BuildWebApplication(
        string[]? args = null,
        IWeatherDataProvider? customProvider = null,
        WeatherEngine? customEngine = null,
        string? listenUrl = null,
        bool allowPositionOverride = true)
    {
        return BuildWebApplication(args, customProvider, customEngine, listenUrl, allowPositionOverride, allowLanAccess: false, port: DefaultPort);
    }

    public static WebApplication BuildWebApplication(
        string[]? args,
        IWeatherDataProvider? customProvider,
        WeatherEngine? customEngine,
        string? listenUrl,
        bool allowPositionOverride,
        bool allowLanAccess,
        int port = DefaultPort,
        string? wwwrootDirOverride = null)
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
                var lanArg = args?.Any(a => a.Equals("--lan", StringComparison.OrdinalIgnoreCase) || a.Equals("-lan", StringComparison.OrdinalIgnoreCase)) == true;
                var effectiveLan = allowLanAccess || lanArg;

                builder.WebHost.ConfigureKestrel(options =>
                {
                    var bindAddress = effectiveLan ? IPAddress.Any : IPAddress.Loopback;
                    options.Listen(bindAddress, port > 0 ? port : DefaultPort);
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
            builder.Services.AddSingleton<IWeatherDataProvider>(sp => new EngineWeatherDataProvider(
                sp.GetRequiredService<WeatherEngine>(),
                allowPositionOverride));
        }

        var app = builder.Build();

        ConfigurePipeline(app, builder.Environment.ContentRootPath, port > 0 ? port : DefaultPort, allowLanAccess, wwwrootDirOverride);

        return app;
    }

    public static void ConfigurePipeline(WebApplication app, string contentRoot)
    {
        ConfigurePipeline(app, contentRoot, DefaultPort, allowLanAccess: false, wwwrootDirOverride: null);
    }

    public static void ConfigurePipeline(WebApplication app, string contentRoot, int port, bool allowLanAccess, string? wwwrootDirOverride = null)
    {
        var wwwrootDir = wwwrootDirOverride != null
            ? (string.IsNullOrEmpty(wwwrootDirOverride) ? null : (Directory.Exists(wwwrootDirOverride) ? wwwrootDirOverride : null))
            : FindWwwRoot(contentRoot);

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

            var lanText = allowLanAccess
                ? $"<span class=\"badge lan\">LAN ENABLED</span>"
                : $"<span class=\"badge local\">LOCALHOST ONLY</span>";

            var fallbackHtml = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <title>SkyWeave EFB — Server Running</title>
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <style>
                    body { background: #0b0f19; color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; padding: 20px; }
                    .card { background: rgba(15, 23, 42, 0.85); border: 1px solid rgba(255, 255, 255, 0.12); border-radius: 12px; padding: 32px; max-width: 560px; box-shadow: 0 16px 40px rgba(0,0,0,0.6); backdrop-filter: blur(16px); }
                    h1 { font-size: 20px; margin-top: 0; color: #38bdf8; display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
                    p { color: #94a3b8; font-size: 13px; line-height: 1.6; }
                    .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 10px; font-weight: bold; }
                    .badge.api { background: #0284c7; color: white; }
                    .badge.lan { background: #059669; color: white; }
                    .badge.local { background: #475569; color: #e2e8f0; }
                    ul { list-style: none; padding: 0; margin: 20px 0; }
                    li { margin-bottom: 10px; }
                    a { color: #38bdf8; text-decoration: none; font-weight: 500; font-family: Consolas, monospace; font-size: 13px; }
                    a:hover { text-decoration: underline; }
                    .desc { color: #64748b; font-size: 12px; margin-left: 6px; }
                    .footer { font-size: 11px; color: #64748b; margin-top: 24px; border-top: 1px solid rgba(255, 255, 255, 0.08); padding-top: 12px; }
                  </style>
                </head>
                <body>
                  <div class="card">
                    <h1>SkyWeave EFB Server <span class="badge api">ACTIVE</span> {{lanText}}</h1>
                    <p>The SkyWeave weather and cockpit companion server is running on port {{port}}. Static assets (<code>wwwroot</code>) were not found at the default paths. All REST API endpoints are fully operational:</p>
                    <ul>
                      <li><a href="/api/status">/api/status</a> <span class="desc">— Engine & SimConnect state</span></li>
                      <li><a href="/api/snapshot">/api/snapshot</a> <span class="desc">— Aircraft weather snapshot</span></li>
                      <li><a href="/api/efb">/api/efb</a> <span class="desc">— Tablet briefing snapshot</span></li>
                      <li><a href="/health">/health</a> <span class="desc">— Service & port health</span></li>
                    </ul>
                    <div class="footer">SkyWeave v0.6.0 &bull; Free, Open-Source Weather Engine for MSFS 2024</div>
                  </div>
                </body>
                </html>
                """;

            return Results.Content(fallbackHtml, "text/html; charset=utf-8");
        });

        // Health check endpoint
        app.MapGet("/health", (IWeatherDataProvider provider, IServiceProvider sp) =>
        {
            var engine = sp.GetService<WeatherEngine>();
            var localUrl = EfbConnectionHelper.GetLocalUrl(port);
            var lanUrls = allowLanAccess ? EfbConnectionHelper.GetLanUrls(port) : Array.Empty<string>();

            return Results.Ok(new
            {
                status = "ok",
                service = "SkyWeave",
                version = "0.6.0",
                port,
                allowLanAccess,
                hasStaticAssets = wwwrootDir != null && File.Exists(Path.Combine(wwwrootDir, "index.html")),
                localUrl,
                lanUrls,
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

        // Snapshot endpoint: versioned snapshot of active aircraft weather state
        var snapshotHandler = async (IWeatherDataProvider provider) =>
        {
            var snapshot = await provider.GetAircraftSnapshotAsync();
            return snapshot == null
                ? Results.Json(new
                {
                    error = "weather data unavailable",
                    reason = "awaiting_sim_position",
                    message = "Awaiting simulator aircraft position fix. Connect MSFS or provide coordinates/station."
                }, statusCode: 503)
                : Results.Ok(snapshot);
        };
        app.MapGet("/api/snapshot", snapshotHandler);
        app.MapGet("/snapshot", snapshotHandler);

        // EFB tablet snapshot endpoint
        var efbHandler = async (IWeatherDataProvider provider, double? lat, double? lon, string? station) =>
        {
            var snapshot = await provider.GetEfbSnapshotAsync(lat, lon, station);
            return snapshot == null
                ? Results.Json(new
                {
                    error = "weather data unavailable",
                    reason = "awaiting_sim_position",
                    message = "Awaiting simulator aircraft position fix. Connect MSFS or provide coordinates/station."
                }, statusCode: 503)
                : Results.Ok(snapshot);
        };
        app.MapGet("/api/efb", efbHandler);
        app.MapGet("/efb", efbHandler);

        // State endpoint
        var stateHandler = async (IWeatherDataProvider provider, double? lat, double? lon, string? station) =>
        {
            var state = await provider.GetStateAsync(lat, lon, station);
            return state == null
                ? Results.Json(new
                {
                    error = "weather data unavailable",
                    reason = "awaiting_sim_position",
                    message = "Awaiting simulator aircraft position fix. Connect MSFS or provide coordinates/station."
                }, statusCode: 503)
                : Results.Json(state);
        };
        app.MapGet("/api/state", stateHandler);
        app.MapGet("/state", stateHandler);

        // METAR endpoint
        var metarHandler = async (IWeatherDataProvider provider, double? lat, double? lon, string? station) =>
        {
            var metar = await provider.GetMetarAsync(lat, lon, station);
            return metar == null
                ? Results.Json(new
                {
                    error = "metar unavailable",
                    reason = "awaiting_sim_position",
                    message = "Awaiting simulator aircraft position fix. Connect MSFS or provide coordinates/station."
                }, statusCode: 503)
                : Results.Json(metar);
        };
        app.MapGet("/api/metar", metarHandler);
        app.MapGet("/metar", metarHandler);

        // Hazards endpoint
        var hazardsHandler = async (IWeatherDataProvider provider, double? lat, double? lon, string? station) =>
        {
            var hazards = await provider.GetHazardsAsync(lat, lon, station);
            return hazards == null
                ? Results.Json(new
                {
                    error = "hazards unavailable",
                    reason = "awaiting_sim_position",
                    message = "Awaiting simulator aircraft position fix. Connect MSFS or provide coordinates/station."
                }, statusCode: 503)
                : Results.Json(hazards);
        };
        app.MapGet("/api/hazards", hazardsHandler);
        app.MapGet("/hazards", hazardsHandler);

        // Traffic endpoint: returns live SimConnect AI/multiplayer traffic within 15 NM and wake status
        app.MapGet("/api/traffic", (IWeatherDataProvider provider) =>
        {
            var traffic = provider.GetNearbyTraffic();
            return Results.Ok(new
            {
                count = traffic.Count,
                hasWakeEncounter = provider.HasWakeEncounter,
                traffic = traffic.Select(t => new
                {
                    callsign = t.Callsign,
                    latitude = t.Latitude,
                    longitude = t.Longitude,
                    altitudeFeet = t.AltitudeFeet,
                    headingDegrees = t.HeadingDegrees,
                    speedKnots = t.SpeedKnots,
                    groundSpeedKnots = t.GroundSpeedKnots,
                    weightClass = t.WeightClass.ToString(),
                    onGround = t.OnGround,
                    distanceNm = t.DistanceNm,
                    relativeBearingDegrees = t.RelativeBearingDegrees,
                    altitudeDeltaFeet = t.AltitudeDeltaFeet,
                    isInWakeZone = t.IsInWakeZone
                })
            });
        });

        // Online ATC Network ATIS endpoints
        Func<HttpRequest, IWeatherDataProvider, string?, Task<IResult>> atisHandler = async (HttpRequest request, IWeatherDataProvider provider, string? station) =>
        {
            var reqStation = !string.IsNullOrWhiteSpace(station) ? station : request.Query["station"].FirstOrDefault();
            var atis = await provider.GetAtisAsync(reqStation);
            if (atis == null)
            {
                return Results.Json(new
                {
                    error = "No ATIS available for station",
                    station = reqStation
                }, statusCode: 404);
            }
            return Results.Json(atis);
        };
        app.MapGet("/api/atis", atisHandler);
        app.MapGet("/atis", atisHandler);
        app.MapGet("/api/vatsim/atis", atisHandler);

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

        // Weather Freeze endpoints
        app.MapGet("/api/weather/freeze", (IWeatherDataProvider provider) =>
        {
            return Results.Ok(new
            {
                isFrozen = provider.IsWeatherFrozen,
                anchorState = provider.GetAnchorState()
            });
        });

        app.MapPost("/api/weather/freeze", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            bool targetFrozen;
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                {
                    targetFrozen = !provider.IsWeatherFrozen;
                }
                else
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("frozen", out var prop))
                    {
                        targetFrozen = prop.GetBoolean();
                    }
                    else
                    {
                        targetFrozen = !provider.IsWeatherFrozen;
                    }
                }
            }
            catch
            {
                targetFrozen = !provider.IsWeatherFrozen;
            }

            provider.SetWeatherFrozen(targetFrozen);
            return Results.Ok(new
            {
                isFrozen = provider.IsWeatherFrozen,
                message = targetFrozen ? "Weather frozen" : "Weather dynamic"
            });
        });

        // ERA5 Historical Weather Replay endpoints
        app.MapGet("/api/historical", (IWeatherDataProvider provider) =>
        {
            return Results.Ok(new
            {
                isHistoricalMode = provider.IsHistoricalMode,
                historicalTargetUtc = provider.HistoricalTargetUtc
            });
        });

        app.MapPost("/api/historical", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            bool enabled = true;
            DateTime? targetUtc = null;
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("enabled", out var enabledProp))
                    {
                        enabled = enabledProp.GetBoolean();
                    }
                    if (json.RootElement.TryGetProperty("targetUtc", out var dateProp))
                    {
                        if (DateTime.TryParse(dateProp.GetString(), out var parsedDate))
                        {
                            targetUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                        }
                    }
                }
            }
            catch { }

            await provider.SetHistoricalModeAsync(enabled, targetUtc);
            return Results.Ok(new
            {
                isHistoricalMode = provider.IsHistoricalMode,
                historicalTargetUtc = provider.HistoricalTargetUtc,
                message = enabled ? $"Historical replay active ({provider.HistoricalTargetUtc:yyyy-MM-dd HH:00}Z)" : "Live weather active"
            });
        });

        app.MapGet("/api/historical/weather", async (IWeatherDataProvider provider, double? lat, double? lon, string? station, string? targetUtc) =>
        {
            DateTime queryUtc = DateTime.UtcNow.Date.AddDays(-7).AddHours(12);
            if (!string.IsNullOrWhiteSpace(targetUtc) && DateTime.TryParse(targetUtc, out var parsed))
            {
                queryUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            double queryLat = lat ?? 0;
            double queryLon = lon ?? 0;

            if (queryLat == 0 && queryLon == 0)
            {
                var snap = await provider.GetAircraftSnapshotAsync();
                if (snap?.Latitude != null && snap?.Longitude != null)
                {
                    queryLat = snap.Latitude.Value;
                    queryLon = snap.Longitude.Value;
                }
                else
                {
                    return Results.BadRequest(new { error = "Coordinates required: specify lat and lon or await sim position." });
                }
            }

            var histState = await provider.FetchHistoricalWeatherAsync(queryLat, queryLon, queryUtc, station);
            return histState == null
                ? Results.NotFound(new { error = "Unable to fetch ERA5 historical weather for specified parameters." })
                : Results.Ok(histState);
        });

        // Sandbox / Manual Weather Studio endpoints
        app.MapGet("/api/sandbox", (IWeatherDataProvider provider) =>
        {
            var presets = SandboxWeatherScenario.GetDefaultPresets().Select(p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description
            });

            return Results.Ok(new
            {
                isSandboxMode = provider.IsSandboxMode,
                currentScenario = provider.CurrentSandboxScenario,
                availablePresets = presets
            });
        });

        app.MapPost("/api/sandbox", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                {
                    await provider.SetSandboxModeAsync(true);
                    return Results.Ok(new
                    {
                        isSandboxMode = provider.IsSandboxMode,
                        scenario = provider.CurrentSandboxScenario
                    });
                }

                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var scenario = System.Text.Json.JsonSerializer.Deserialize<SandboxWeatherScenario>(body, options);
                if (scenario != null)
                {
                    await provider.ApplySandboxScenarioAsync(scenario);
                    return Results.Ok(new
                    {
                        isSandboxMode = provider.IsSandboxMode,
                        scenario = provider.CurrentSandboxScenario
                    });
                }

                return Results.BadRequest(new { error = "Invalid scenario payload" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/sandbox/preset", async (HttpRequest request, IWeatherDataProvider provider, string? preset) =>
        {
            string? presetId = preset;
            if (string.IsNullOrWhiteSpace(presetId))
            {
                try
                {
                    using var reader = new StreamReader(request.Body);
                    var body = await reader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var json = System.Text.Json.JsonDocument.Parse(body);
                        if (json.RootElement.TryGetProperty("preset", out var prop))
                        {
                            presetId = prop.GetString();
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(presetId))
            {
                return Results.BadRequest(new { error = "preset identifier is required" });
            }

            await provider.ApplySandboxPresetAsync(presetId);
            return Results.Ok(new
            {
                isSandboxMode = provider.IsSandboxMode,
                scenario = provider.CurrentSandboxScenario
            });
        });

        app.MapPost("/api/sandbox/disable", async (IWeatherDataProvider provider) =>
        {
            await provider.SetSandboxModeAsync(false);
            return Results.Ok(new
            {
                isSandboxMode = provider.IsSandboxMode,
                message = "Sandbox disabled. Returned to live weather."
            });
        });

        // Vertical Sounding Skew-T profile endpoint
        app.MapGet("/api/sounding", (IWeatherDataProvider provider, double? alt) =>
        {
            var sounding = provider.GetSoundingData(alt);
            return sounding == null
                ? Results.Json(new { error = "sounding data unavailable", reason = "awaiting_sim_position" }, statusCode: 503)
                : Results.Ok(sounding);
        });

        // Synoptic Map Isobars & Wind Barbs endpoint
        app.MapGet("/api/synoptic", (IWeatherDataProvider provider, double? range) =>
        {
            var synoptic = provider.GetSynopticData(range ?? 100.0);
            return synoptic == null
                ? Results.Json(new { error = "synoptic data unavailable", reason = "awaiting_sim_position" }, statusCode: 503)
                : Results.Ok(synoptic);
        });

        // SimBrief & Sky Anchor Corridor endpoints
        app.MapGet("/api/simbrief", (IWeatherDataProvider provider) =>
        {
            var plan = provider.GetFlightPlan();
            var anchor = provider.GetAnchorState();
            return Results.Ok(new
            {
                hasPlan = plan != null,
                plan,
                anchorState = anchor
            });
        });

        app.MapPost("/api/simbrief/fetch", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            string? pilotId = null;
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("pilotId", out var prop))
                    {
                        pilotId = prop.GetString();
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(pilotId))
            {
                return Results.BadRequest(new { error = "pilotId is required" });
            }

            using var fetcher = new SimBriefFetcher();
            var plan = await fetcher.FetchPlanAsync(pilotId);
            if (plan == null)
            {
                return Results.NotFound(new { error = "No OFP found for pilot ID" });
            }

            provider.SetFlightPlan(plan);
            return Results.Ok(new
            {
                success = true,
                plan,
                anchorState = provider.GetAnchorState()
            });
        });

        // FMC Winds Aloft Uplink Export endpoint (formats: "pmdg", "fenix", "csv")
        app.MapGet("/api/fmc/export", (IWeatherDataProvider provider, string? format) =>
        {
            var plan = provider.GetFlightPlan();
            if (plan == null)
            {
                return Results.BadRequest(new { error = "No flight plan loaded. Fetch SimBrief OFP first." });
            }

            var fmt = (format ?? "pmdg").ToLowerInvariant();
            switch (fmt)
            {
                case "pmdg":
                    var pmdgText = FmcWindExporter.GeneratePmdgWindFile(plan);
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(pmdgText), "text/plain", $"{plan.Origin}{plan.Destination}01.wx");
                case "fenix":
                    var fenixText = FmcWindExporter.GenerateFenixJson(plan);
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(fenixText), "application/json", $"{plan.Origin}_{plan.Destination}_winds.json");
                case "csv":
                    var csvText = FmcWindExporter.GenerateCsv(plan);
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(csvText), "text/csv", $"{plan.Origin}_{plan.Destination}_winds.csv");
                default:
                    return Results.BadRequest(new { error = "Unsupported format. Use 'pmdg', 'fenix', or 'csv'." });
            }
        });

        // Dispatch Weather Briefing Package endpoints
        app.MapGet("/api/dispatch/briefing", async (IWeatherDataProvider provider) =>
        {
            var briefing = await provider.GenerateDispatchBriefingAsync();
            return briefing == null
                ? Results.BadRequest(new { error = "No flight plan loaded. Fetch SimBrief OFP first." })
                : Results.Ok(briefing);
        });

        app.MapGet("/api/dispatch/briefing/html", async (IWeatherDataProvider provider, bool? dark) =>
        {
            var html = await provider.GenerateDispatchBriefingHtmlAsync(dark ?? false);
            return html == null
                ? Results.BadRequest(new { error = "No flight plan loaded. Fetch SimBrief OFP first." })
                : Results.Content(html, "text/html");
        });

        app.MapGet("/briefing", async (IWeatherDataProvider provider, bool? dark) =>
        {
            var html = await provider.GenerateDispatchBriefingHtmlAsync(dark ?? false);
            return html == null
                ? Results.Content("<html><body style='font-family: sans-serif; padding: 40px; text-align: center;'><h2>No Flight Plan Loaded</h2><p>Please load a SimBrief OFP flight plan in SkyWeave first to view the dispatch weather package.</p></body></html>", "text/html")
                : Results.Content(html, "text/html");
        });

        // Community Plugins endpoints
        app.MapGet("/api/plugins", (IWeatherDataProvider provider) =>
        {
            var plugins = provider.GetInstalledPlugins();
            return Results.Ok(new
            {
                count = plugins.Count,
                plugins = plugins.Select(p => new
                {
                    id = p.PluginId,
                    name = p.PluginName,
                    version = p.Version,
                    author = p.Author,
                    description = p.Description,
                    isEnabled = p.IsEnabled,
                    status = p.Status,
                    lastExecutionUtc = p.LastExecutionUtc,
                    executionCount = p.ExecutionCount,
                    errorCount = p.ErrorCount,
                    lastError = p.LastError
                })
            });
        });

        app.MapPost("/api/plugins/toggle", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("pluginId", out var idProp) &&
                        json.RootElement.TryGetProperty("enabled", out var enabledProp))
                    {
                        var pluginId = idProp.GetString();
                        var enabled = enabledProp.GetBoolean();
                        if (!string.IsNullOrWhiteSpace(pluginId))
                        {
                            var success = provider.SetPluginEnabled(pluginId, enabled);
                            return Results.Ok(new { success, pluginId, enabled });
                        }
                    }
                }
                return Results.BadRequest(new { error = "Invalid request payload. Expected pluginId and enabled." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // GSX Pro Ground Operations & Deicing endpoints
        app.MapGet("/api/gsx", (IWeatherDataProvider provider) =>
        {
            var gsx = provider.GetGsxState();
            return gsx != null ? Results.Ok(gsx) : Results.Ok(new GsxGroundState());
        });

        app.MapPost("/api/gsx/request", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                {
                    return Results.BadRequest(new { error = "missing service parameter" });
                }
                var json = System.Text.Json.JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("service", out var serviceProp))
                {
                    var service = serviceProp.GetString();
                    if (!string.IsNullOrWhiteSpace(service))
                    {
                        await provider.RequestGsxServiceAsync(service);
                        return Results.Ok(new { success = true, service });
                    }
                }
                return Results.BadRequest(new { error = "invalid service" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/gsx/telemetry", async (HttpRequest request, IWeatherDataProvider provider) =>
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    var root = json.RootElement;
                    var isOperating = root.TryGetProperty("isOperating", out var opProp) && opProp.GetBoolean();
                    var deicingState = root.TryGetProperty("deicingState", out var dsProp) ? dsProp.GetInt32() : 0;
                    var deicingType = root.TryGetProperty("deicingType", out var dtProp) ? dtProp.GetInt32() : 0;
                    var boardingState = root.TryGetProperty("boardingState", out var bsProp) ? bsProp.GetInt32() : 0;
                    var passBoarded = root.TryGetProperty("passengersBoarded", out var pbProp) ? pbProp.GetInt32() : 0;
                    var passTotal = root.TryGetProperty("passengersTotal", out var ptProp) ? ptProp.GetInt32() : 0;
                    var refuelingState = root.TryGetProperty("refuelingState", out var rfProp) ? rfProp.GetInt32() : 0;
                    var cateringState = root.TryGetProperty("cateringState", out var catProp) ? catProp.GetInt32() : 0;
                    var pushbackState = root.TryGetProperty("pushbackState", out var pbkProp) ? pbkProp.GetInt32() : 0;

                    var current = provider.GetGsxState() ?? new GsxGroundState();
                    current.IsOperating = isOperating;
                    current.DeicingState = (GsxDeicingState)deicingState;
                    current.DeicingType = (GsxDeicingType)deicingType;
                    current.BoardingState = (GsxBoardingState)boardingState;
                    current.PassengersBoarded = passBoarded;
                    current.PassengersTotal = passTotal;
                    current.RefuelingState = (GsxRefuelingState)refuelingState;
                    current.CateringActive = cateringState > 0;
                    current.PushbackState = (GsxPushbackState)pushbackState;
                    provider.SetGsxState(current);

                    return Results.Ok(new { success = true });
                }
                return Results.BadRequest();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Airborne Weather Radar (Bendix/Collins WXR-2100 Tilt + WX+T Doppler + Vertical Weather Profile)
        var wxrService = new AirborneWeatherRadarService();
        app.MapGet("/api/wxr", async (IWeatherDataProvider provider, double? tilt, double? range, double? gain, int? mode, double? heading, double? alt) =>
        {
            var snapshot = await provider.GetAircraftSnapshotAsync();
            var state = snapshot?.State ?? await provider.GetCurrentAircraftStateAsync();
            var acLat = snapshot?.Latitude ?? state?.Latitude ?? 0;
            var acLon = snapshot?.Longitude ?? state?.Longitude ?? 0;
            var acAlt = alt ?? snapshot?.AltitudeFeet ?? 10000;
            var acHdg = heading ?? 0;
            var radarMode = Enum.IsDefined(typeof(AirborneRadarMode), mode ?? 1)
                ? (AirborneRadarMode)(mode ?? 1)
                : AirborneRadarMode.WxPlusTurb;

            var scan = wxrService.ComputeRadarScan(
                state,
                acLat,
                acLon,
                acAlt,
                acHdg,
                tiltDegrees: tilt ?? 0.0,
                rangeNm: range ?? 80.0,
                gainDb: gain ?? 0.0,
                mode: radarMode);

            return Results.Ok(scan);
        });

        // VHF Flight Watch & Voice ATIS Radio (122.00 / 122.025 / 122.050 MHz)
        var flightWatchService = new FlightWatchRadioService();
        app.MapGet("/api/flightwatch", async (IWeatherDataProvider provider, int? channel, double? com1, double? com2) =>
        {
            var snapshot = await provider.GetAircraftSnapshotAsync();
            var state = snapshot?.State ?? await provider.GetCurrentAircraftStateAsync();
            var detectedChannel = (com1.HasValue || com2.HasValue)
                ? flightWatchService.DetectTunedChannel(com1 ?? 0, com2 ?? 0)
                : (Enum.IsDefined(typeof(FlightWatchChannel), channel ?? 1)
                    ? (FlightWatchChannel)(channel ?? 1)
                    : FlightWatchChannel.EnrouteFlightWatch12200);

            if (detectedChannel == FlightWatchChannel.None)
                detectedChannel = FlightWatchChannel.EnrouteFlightWatch12200;

            var transmission = flightWatchService.BuildTransmission(
                state,
                detectedChannel,
                snapshot?.AltitudeFeet ?? 0);

            return Results.Ok(transmission);
        });

        app.MapGet("/api/flightwatch/squelch.wav", () =>
        {
            var wavBytes = flightWatchService.GenerateVhfSquelchBurstWav(280);
            return Results.File(wavBytes, "audio/wav", "vhf-squelch.wav");
        });

        // Viral Extreme Weather Challenge Hunter & Famous Historical Storms
        var challengeHunter = new ExtremeWeatherHunterService();
        app.MapGet("/api/challenges", async (IWeatherDataProvider provider) =>
        {
            var famousStorms = challengeHunter.GetFamousHistoricalStorms();
            var liveChallenges = new List<ExtremeWeatherChallenge>();

            var currentState = await provider.GetCurrentAircraftStateAsync();
            if (currentState != null && !string.IsNullOrWhiteSpace(currentState.StationId))
            {
                var currentMetar = EngineWeatherDataProvider.BuildMetarFromState(currentState);
                liveChallenges.Add(challengeHunter.ScoreMetar(currentMetar));
            }

            return Results.Ok(new
            {
                famousHistoricalStorms = famousStorms,
                liveChallenges
            });
        });

        // Post-Flight Weather Replay Summary Card
        app.MapGet("/api/postflight", (IWeatherDataProvider provider) =>
        {
            var report = provider.GetPostFlightSummary();
            return Results.Ok(report);
        });

        // Transparent OBS Streamer Weather HUD Overlay (http://localhost:54170/overlay)
        app.MapGet("/overlay", () => Results.Content(BuildObsOverlayHtml(), "text/html"));
    }

    private static string BuildObsOverlayHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>SkyWeave OBS Streamer HUD</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html, body {
    background: transparent !important;
    font-family: 'Segoe UI', Inter, system-ui, sans-serif;
    color: #F8FAFC;
    overflow: hidden;
  }
  .hud-card {
    display: inline-flex;
    align-items: center;
    gap: 16px;
    padding: 12px 18px;
    margin: 12px;
    background: rgba(11, 15, 25, 0.84);
    border: 1px solid rgba(56, 189, 248, 0.35);
    border-left: 4px solid #38BDF8;
    border-radius: 12px;
    box-shadow: 0 10px 28px rgba(0, 0, 0, 0.55);
    backdrop-filter: blur(10px);
    min-width: 440px;
  }
  .brand-col { display: flex; flex-direction: column; gap: 3px; }
  .brand-tag { font-size: 10px; font-weight: 700; letter-spacing: 1.4px; color: #38BDF8; text-transform: uppercase; }
  .station-row { display: flex; align-items: center; gap: 8px; }
  .icao { font-size: 24px; font-weight: 800; letter-spacing: 0.5px; font-family: 'Cascadia Mono', Consolas, monospace; }
  .cat-badge {
    font-size: 11px; font-weight: 800; padding: 2px 8px; border-radius: 99px;
    background: rgba(34, 197, 94, 0.2); color: #4ADE80; border: 1px solid rgba(34, 197, 94, 0.45);
  }
  .cat-MVFR { background: rgba(59, 130, 246, 0.22); color: #60A5FA; border-color: rgba(59, 130, 246, 0.5); }
  .cat-IFR  { background: rgba(239, 68, 68, 0.22); color: #F87171; border-color: rgba(239, 68, 68, 0.5); }
  .cat-LIFR { background: rgba(217, 70, 239, 0.22); color: #E879F9; border-color: rgba(217, 70, 239, 0.5); }
  .blend-sub { font-size: 10.5px; color: #94A3B8; }
  .divider { width: 1px; height: 42px; background: rgba(148, 163, 184, 0.2); }
  .wind-compass {
    width: 44px; height: 44px; border-radius: 50%;
    border: 1.5px solid rgba(56, 189, 248, 0.4);
    display: flex; align-items: center; justify-content: center;
    background: rgba(15, 23, 42, 0.7);
  }
  .wind-arrow {
    font-size: 20px; color: #38BDF8; transform-origin: center;
    transition: transform 0.6s cubic-bezier(0.22, 1, 0.36, 1);
  }
  .metrics-grid { display: grid; grid-template-columns: repeat(3, auto); gap: 6px 16px; }
  .metric { display: flex; flex-direction: column; }
  .metric-lbl { font-size: 9.5px; text-transform: uppercase; letter-spacing: 0.8px; color: #94A3B8; }
  .metric-val { font-size: 13.5px; font-weight: 700; font-family: 'Cascadia Mono', Consolas, monospace; color: #F1F5F9; }
</style>
</head>
<body>
  <div class="hud-card" id="hud">
    <div class="brand-col">
      <span class="brand-tag">SkyWeave Live WX</span>
      <div class="station-row">
        <span class="icao" id="icao">----</span>
        <span class="cat-badge" id="cat">VFR</span>
      </div>
      <span class="blend-sub" id="blend">Awaiting MSFS 2024 telemetry...</span>
    </div>
    <div class="divider"></div>
    <div class="wind-compass"><div class="wind-arrow" id="arrow">&#8593;</div></div>
    <div class="metrics-grid">
      <div class="metric"><span class="metric-lbl">Wind</span><span class="metric-val" id="wind">---/--KT</span></div>
      <div class="metric"><span class="metric-lbl">Temp / Dew</span><span class="metric-val" id="temp">--/--°C</span></div>
      <div class="metric"><span class="metric-lbl">QNH</span><span class="metric-val" id="qnh">---- hPa</span></div>
      <div class="metric"><span class="metric-lbl">Vis</span><span class="metric-val" id="vis">-- km</span></div>
      <div class="metric"><span class="metric-lbl">Ceiling</span><span class="metric-val" id="ceil">CLR</span></div>
      <div class="metric"><span class="metric-lbl">Airframe Ice</span><span class="metric-val" id="ice">0.0%</span></div>
    </div>
  </div>
<script>
async function poll() {
  try {
    const res = await fetch('/api/efb/snapshot');
    if (!res.ok) return;
    const d = await res.json();
    document.getElementById('icao').textContent = d.stationId || '----';
    const cat = d.flightCategory || 'VFR';
    const catEl = document.getElementById('cat');
    catEl.textContent = cat;
    catEl.className = 'cat-badge cat-' + cat;
    document.getElementById('blend').textContent = d.spatialBlendSummary || d.sourceModelName || 'Live Weather';
    const wdir = Math.round(d.windDirectionDegrees || 0);
    const wspd = Math.round(d.windSpeedKnots || 0);
    const wgst = d.windGustKnots ? 'G' + Math.round(d.windGustKnots) : '';
    document.getElementById('wind').textContent = String(wdir).padStart(3, '0') + '°/' + wspd + wgst + 'KT';
    document.getElementById('arrow').style.transform = `rotate(${(wdir + 180) % 360}deg)`;
    document.getElementById('temp').textContent = `${Math.round(d.temperatureCelsius || 0)}/${Math.round(d.dewpointCelsius || 0)}°C`;
    document.getElementById('qnh').textContent = `${Math.round(d.altimeterHpa || 1013)} hPa`;
    const visKm = ((d.visibilityMeters || 10000) / 1000).toFixed(1);
    document.getElementById('vis').textContent = `${visKm} km`;
    document.getElementById('ceil').textContent = (d.ceilingFeet && d.ceilingFeet < 50000) ? `${Math.round(d.ceilingFeet)} ft` : 'CLR';
    const icePct = d.structuralIcePercent != null ? d.structuralIcePercent.toFixed(1) + '%' : '0.0%';
    document.getElementById('ice').textContent = icePct;
  } catch (e) {}
}
setInterval(poll, 2000);
poll();
</script>
</body>
</html>
""";
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
