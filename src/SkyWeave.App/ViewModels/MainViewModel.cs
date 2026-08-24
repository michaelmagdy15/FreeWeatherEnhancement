using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.App.Models;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using SkyWeave.SimBridge;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace SkyWeave.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly WeatherEngine _weatherEngine;
    private readonly SimConnectManager _simConnect;
    private readonly StationFinder _stationFinder;
    private WeatherInjector? _injector;
    private SkyWeave.App.Models.UserSettings? _settings;
    private Timer? _passiveReadbackTimer;
    private readonly LruCache<string, Bitmap> _tileCache = new(50);

    public ConnectionViewModel Connection { get; }
    public WeatherDisplayViewModel WeatherDisplay { get; }
    public RadarViewModel Radar { get; }
    public TafViewModel Taf { get; }
    public InjectionViewModel Injection { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _logMessages = string.Empty;

    public string VersionText { get; } = $"v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}-beta";

    public ObservableCollection<MapStationViewModel> MapStations { get; } = new();
    public ObservableCollection<AirportViewModel> NearbyAirports { get; } = new();

    public MainViewModel()
    {
        _weatherEngine = new WeatherEngine();
        _simConnect = new SimConnectManager();
        _stationFinder = new StationFinder();

        Connection = new ConnectionViewModel(_simConnect, _weatherEngine, this);
        WeatherDisplay = new WeatherDisplayViewModel();
        Radar = new RadarViewModel(this);
        Taf = new TafViewModel();
        Injection = new InjectionViewModel(this);
        Settings = new SettingsViewModel(this);

        _weatherEngine.WeatherUpdated += OnWeatherUpdated;
        _weatherEngine.PassiveDataReceived += OnPassiveData;
        _weatherEngine.ErrorOccurred += OnError;
        _weatherEngine.WprGenerated += OnWprGenerated;
        
        _simConnect.Connected += OnConnected;
        _simConnect.Disconnected += OnDisconnected;
        _simConnect.ErrorOccurred += OnError;
        _simConnect.PositionUpdated += OnPositionUpdated;
        _simConnect.WeatherReadbackReceived += OnWeatherReadback;
        _simConnect.LogMessage += OnSimConnectLogMessage;

        LoadSettings();
        ApplyInjectionSettings();
        LoadNearbyAirports();

        AppendLog($"=== SkyWeave session start (v{GetType().Assembly.GetName().Version}) ===");
        AppendDiagnostics();
    }

    private void AppendDiagnostics()
    {
        AppendLog($"OS: {RuntimeInformation.OSDescription} | .NET: {RuntimeInformation.FrameworkDescription}");
        AppendLog($"Log file: {LogFilePath}");
        AppendLog($"MSFS process running: {_simConnect.IsSimRunning()} | WPR presets folder: {WprFileWriter.CurrentPresetsFolder ?? "not found"}");
        AppendLog(SimConnectManager.IsCommBusSupported()
            ? "In-sim JS bridge: supported by installed SimConnect SDK"
            : "In-sim JS bridge: UNAVAILABLE — installed SimConnect SDK has no CallCommBusEvent (WPR fallback only)");
    }

    public void ApplyInjectionSettings()
    {
        _weatherEngine.TurbulenceIntensityScale = Injection.TurbulenceIntensityPercent / 100.0;
        _weatherEngine.WakeTurbulenceEnabled = Injection.WakeTurbulenceEnabled;
        _weatherEngine.WakeTurbulenceScale = Injection.WakeTurbulencePercent / 100.0;
        _weatherEngine.GustEnhancementScale = Injection.GustEnhancementPercent / 100.0;
        _weatherEngine.ThunderstormIntensityScale = Injection.ThunderstormIntensityPercent / 100.0;
        _weatherEngine.PrecipitationScale = Injection.PrecipitationPercent / 100.0;
        _weatherEngine.AerosolScale = Injection.AerosolPercent / 100.0;
        _weatherEngine.SmoothingDurationMinutes = Injection.SmoothingDurationMinutes;
        
        if (_injector != null)
        {
            _injector.InjectionInterval = TimeSpan.FromSeconds(Injection.InjectionIntervalSeconds);
        }
    }

    [RelayCommand]
    private async Task CopyLogAsync()
    {
        string text;
        try
        {
            text = File.Exists(LogFilePath)
                ? await File.ReadAllTextAsync(LogFilePath)
                : LogMessages;
        }
        catch (Exception ex)
        {
            AppendLog($"Copy log failed to read file: {ex.Message}");
            text = LogMessages;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Connection.StatusText = "Log is empty";
            AppendLog("Copy log: nothing to copy yet");
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow } &&
            TopLevel.GetTopLevel(mainWindow)?.Clipboard is { } clipboard)
        {
            // await clipboard.SetTextAsync(text);
            Connection.StatusText = "Log copied to clipboard (disabled)";
            AppendLog($"Log copied to clipboard ({text.Length} chars)");
        }
        else
        {
            Connection.StatusText = "Clipboard unavailable";
            AppendLog("Copy log: clipboard unavailable");
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogFolderPath);
            Process.Start(new ProcessStartInfo("explorer.exe", LogFolderPath) { UseShellExecute = true });
            AppendLog($"Opened log folder: {LogFolderPath}");
        }
        catch (Exception ex)
        {
            AppendLog($"Open log folder failed: {ex.Message}");
        }
    }

    public async Task ConnectAsync()
    {
        var attemptStarted = _simConnect.Connect();

        if (!attemptStarted)
            return;

        Connection.StatusText = "Connecting...";
        AppendLog("SimConnect attempt started — waiting for simulator acknowledgement");

        _injector?.Dispose();
        _injector = new WeatherInjector(_simConnect, _weatherEngine);
        _injector.InjectionInterval = TimeSpan.FromSeconds(Injection.InjectionIntervalSeconds);
        _injector.InjectionStatus += (s, msg) => AppendLog($"Injector: {msg}");

        Connection.AircraftPosition = "awaiting sim position...";

        await Task.CompletedTask;
    }

    public void Disconnect()
    {
        _injector?.StopInjection();
        _simConnect.Disconnect();
        _weatherEngine.Stop();
        Connection.IsInjecting = false;
        Connection.StatusText = "Disconnected";
        AppendLog("Disconnected from MSFS 2024");
    }

    public async Task StartWeatherAsync()
    {
        try
        {
            _weatherEngine.RefreshInterval = TimeSpan.FromSeconds(Injection.RefreshIntervalSeconds);

            var resolved = ResolveStartCoordinates();
            if (!resolved.HasValue)
            {
                Connection.StatusText = "Awaiting sim position";
                AppendLog("No position fix yet — connect with the sim running, or select an airport. Weather engine will start on the first fix.");
                return;
            }

            Connection.StatusText = "Starting weather engine...";
            AppendLog($"Starting weather engine (interval: {_weatherEngine.RefreshInterval.TotalSeconds}s, position: {resolved.Value.lat:F4}, {resolved.Value.lon:F4})");

            await _weatherEngine.StartAsync(resolved.Value.lat, resolved.Value.lon, Connection.IsPassiveMode);

            if (!Connection.IsPassiveMode && _injector != null)
            {
                await _injector.StartInjectionAsync();
            }

            Connection.IsInjecting = !Connection.IsPassiveMode;
            Connection.StatusText = Connection.IsPassiveMode ? "Passive mode active" : "Weather engine running";
            AppendLog(Connection.IsPassiveMode ? "Started in passive mode (monitoring only)" : "Weather injection started");
        }
        catch (Exception ex)
        {
            Connection.StatusText = $"Error: {ex.Message}";
            AppendLog($"Start error: {ex.Message}");
        }
    }

    public async Task StartPassiveAsync()
    {
        try
        {
            var resolved = ResolveStartCoordinates();
            if (!resolved.HasValue)
            {
                Connection.StatusText = "Awaiting sim position";
                AppendLog("No position fix yet — connect with the sim running, or select an airport.");
                return;
            }

            Connection.IsPassiveMode = true;
            Connection.StatusText = "Starting passive mode...";
            await _weatherEngine.StartAsync(resolved.Value.lat, resolved.Value.lon, true);
            Connection.IsInjecting = false;
            Connection.StatusText = "Passive mode active - monitoring only";
            AppendLog("Started passive mode - weather data displayed but not injected");

            _passiveReadbackTimer?.Dispose();
            _passiveReadbackTimer = new Timer(
                _ => { try { _simConnect.RequestWeatherReadback(); } catch { /* best-effort */ } },
                null,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Connection.StatusText = $"Error: {ex.Message}";
            AppendLog($"Passive mode error: {ex.Message}");
        }
    }

    public void StopWeather()
    {
        _injector?.StopInjection();
        _weatherEngine.Stop();
        _passiveReadbackTimer?.Dispose();
        _passiveReadbackTimer = null;
        Connection.IsInjecting = false;
        Connection.IsPassiveMode = false;
        Connection.HasSimWeatherReadback = false;
        Connection.SimWeatherInfo = "---";
        Connection.StatusText = "Weather engine stopped";
        AppendLog("Weather engine stopped");
    }

    public async Task RefreshNowAsync()
    {
        try
        {
            Connection.StatusText = "Refreshing...";
            var state = await _weatherEngine.FetchCurrentWeatherAsync();
            if (state != null)
            {
                UpdateUI(state);
                Connection.StatusText = "Refresh complete";
                AppendLog($"Refreshed {state.StationId} — {state.TemperatureCelsius:F1}\u00b0C, wind {state.WindDirectionDegrees:F0}\u00b0/{state.WindSpeedKnots:F0} kt, " +
                          $"vis {state.VisibilityMeters / 1609.344:F1} SM, data age {(int)state.DataAgeMinutes} min, model {state.SourceModelName}");
            }
            else
            {
                Connection.StatusText = "No data available - check network/API status";
                AppendLog("Refresh failed - no data returned. Check if NOAA/Open-Meteo APIs are reachable.");
            }
        }
        catch (Exception ex)
        {
            Connection.StatusText = $"Refresh error: {ex.Message}";
            AppendLog($"Refresh error: {ex.Message}");
        }
    }

    public async Task SelectAirportAsync(string? icaoId)
    {
        if (string.IsNullOrEmpty(icaoId)) return;

        Connection.ManualStation = icaoId;
        var airports = _stationFinder.AllAirports.ToList();
        var match = airports.FirstOrDefault(a => a.IcaoId == icaoId);
        if (match != null)
        {
            Connection.SelectedAirportName = $"{match.IcaoId} - {match.Name}";
            Connection.StatusText = $"Selected {match.IcaoId}";
            AppendLog($"Selected airport: {match.IcaoId} ({match.Name})");

            await _weatherEngine.UpdatePositionAsync(match.Latitude, match.Longitude);
        }
    }

    public async Task UseManualStationAsync()
    {
        if (string.IsNullOrWhiteSpace(Connection.ManualStation)) return;

        var icao = Connection.ManualStation.Trim().ToUpper();
        Connection.StatusText = $"Fetching weather for {icao}...";
        AppendLog($"Manual station lookup: {icao}");

        var airports = _stationFinder.AllAirports.ToList();
        var match = airports.FirstOrDefault(a => a.IcaoId == icao);
        if (match != null)
        {
            Connection.SelectedAirportName = $"{match.IcaoId} - {match.Name}";
            await _weatherEngine.UpdatePositionAsync(match.Latitude, match.Longitude);
        }
        else
        {
            AppendLog($"Airport {icao} not found in database, using coordinates");
        }

        await Task.CompletedTask;
    }

    private (double lat, double lon)? ResolveStartCoordinates()
    {
        var pos = _simConnect.GetAircraftPosition();
        if (pos.HasValue)
            return (pos.Value.Latitude, pos.Value.Longitude);

        if (!string.IsNullOrWhiteSpace(Connection.ManualStation))
        {
            var match = _stationFinder.AllAirports.FirstOrDefault(a =>
                a.IcaoId.Equals(Connection.ManualStation.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return (match.Latitude, match.Longitude);
        }

        return null;
    }

    private void LoadNearbyAirports()
    {
        NearbyAirports.Clear();
        var pos = _simConnect.GetAircraftPosition();
        var airports = pos.HasValue
            ? _stationFinder.FindNearbyAirports(pos.Value.Latitude, pos.Value.Longitude)
            : _stationFinder.AllAirports.Take(12).ToList();
        foreach (var airport in airports)
        {
            NearbyAirports.Add(new AirportViewModel
            {
                IcaoId = airport.IcaoId,
                Name = airport.Name,
                IataId = airport.IataId ?? "---"
            });
        }
    }

    private void OnWeatherUpdated(object? sender, WeatherState state)
    {
        UpdateUI(state);
    }

    private void OnPassiveData(object? sender, PassiveWeatherData data)
    {
        UpdateUI(data.State);
    }

    private void OnWprGenerated(object? sender, string wpr)
    {
        AppendLog($"WPR generated ({wpr.Length} chars)");
    }

    private void OnPositionUpdated(object? sender, AircraftPositionData pos)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPositionUpdated(sender, pos));
            return;
        }
        Connection.AircraftPosition = $"{pos.Latitude:F4}, {pos.Longitude:F4} @ {pos.AltitudeFeet:F0} ft";
    }

    private void OnWeatherReadback(object? sender, AmbientWeatherData readback)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnWeatherReadback(sender, readback));
            return;
        }
        Connection.HasSimWeatherReadback = true;
        Connection.SimWeatherInfo = $"SIM: {readback.TemperatureCelsius:F1}\u00b0C, " +
                         $"{readback.WindDirectionDegrees:F0}\u00b0 @ {readback.WindSpeedKnots:F0} kt, " +
                         $"{readback.SeaLevelPressureHpa:F1} hPa";
    }

    private void OnSimConnectLogMessage(object? sender, string message)
    {
        AppendLog($"[SimConnect] {message}");
    }

    private void OnError(object? sender, string message)
    {
        AppendLog($"ERROR: {message}");
    }

    private void UpdateUI(WeatherState state)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateUI(state));
            return;
        }

        Connection.LastUpdate = $"Updated: {DateTime.Now:HH:mm:ss}";

        WeatherDisplay.Update(state, _weatherEngine.RecentStrikes.Count, 
            _weatherEngine.RecentStrikes.Count > 0 ? $"{_weatherEngine.RecentStrikes.Min(s => s.DistanceNm):F1} nm" : "---", 
            _weatherEngine.DetectedStormCells.Count);

        if (state.Taf != null)
        {
            Taf.TafText = state.Taf.RawText;
            Taf.TafStation = state.Taf.StationId;
            Taf.TafValidity = $"{state.Taf.ValidFrom:dd HH:mm}Z - {state.Taf.ValidTo:dd HH:mm}Z";
            Taf.TafFlightCategory = state.Taf.FlightCategory;
            Taf.TafWind = state.Taf.WindSpeedKnots > 0
                ? $"{state.Taf.WindDirectionDegrees:F0}° @ {state.Taf.WindSpeedKnots:F0} kt"
                : "---";
            Taf.TafVisibility = state.Taf.VisibilityMeters > 0
                ? $"{state.Taf.VisibilityMeters / 1609.344:F1} SM"
                : "---";
            Taf.TafDivergence = state.Taf.FlightCategory != state.FlightCategory &&
                            !string.IsNullOrEmpty(state.Taf.FlightCategory);

            Taf.TafGroups.Clear();
            foreach (var group in new SkyWeave.Core.Decoders.TafDecoder().DecodeChangeGroups(state.Taf.RawText))
            {
                Taf.TafGroups.Add(new TafGroupViewModel
                {
                    Type = group.Type,
                    ValidFrom = group.ValidFrom,
                    ValidTo = group.ValidTo,
                    FlightCategory = group.FlightCategory,
                    Wind = group.WindSpeedKnots is double ws ? $"{group.WindDirectionDegrees:F0}/{ws:F0}" : "---",
                    CloudSummary = group.Clouds.Count > 0
                        ? string.Join(" ", group.Clouds.Select(c => $"{c.Coverage}{c.BaseFeet / 100:000}"))
                        : "SKC"
                });
            }
        }
        else
        {
            Taf.TafText = "No TAF available for this station.";
            Taf.TafStation = "---";
            Taf.TafValidity = "---";
            Taf.TafFlightCategory = "---";
            Taf.TafWind = "---";
            Taf.TafVisibility = "---";
            Taf.TafDivergence = false;
            Taf.TafGroups.Clear();
        }

        var radarFrame = _weatherEngine.CurrentRadarFrame;
        if (radarFrame != null)
        {
            var (tileX, tileY, pixelX, pixelY) = RadarTileCalculator.PositionToTile(state.Latitude, state.Longitude, 9);
            var ppm = RadarTileCalculator.PixelsPerNm(state.Latitude, 9);

            Radar.RadarTiles.Clear();
            foreach (var (x, y) in RadarTileCalculator.MosaicTiles(tileX, tileY, 9))
            {
                var tileVm = new RadarTileViewModel
                {
                    Url = $"{radarFrame.TileUrl}/256/9/{x}/{y}/2/1_1.png",
                    X = 384 - pixelX + (x - tileX) * 256.0,
                    Y = 384 - pixelY + (y - tileY) * 256.0
                };
                Radar.RadarTiles.Add(tileVm);
                _ = LoadTileBitmapAsync(tileVm);
            }

            Radar.Ring25Diameter = 2 * 25 * ppm;
            Radar.Ring25Left = 384 - 25 * ppm;
            Radar.Ring50Diameter = 2 * 50 * ppm;
            Radar.Ring50Left = 384 - 50 * ppm;
            Radar.Ring100Diameter = 2 * 100 * ppm;
            Radar.Ring100Left = 384 - 100 * ppm;
            Radar.Ring250Diameter = 2 * 250 * ppm;
            Radar.Ring250Left = 384 - 250 * ppm;

            Radar.RadarTimestamp = radarFrame.Timestamp.ToString("HH:mm") + "Z";
            Radar.RadarHasData = true;
            Radar.RadarFade = 0.3;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Radar.RadarFade = 1.0);
        }
        else
        {
            Radar.RadarHasData = false;
            Radar.RadarTimestamp = "no data";
            Radar.RadarTiles.Clear();
        }

        MapStations.Clear();
        foreach (var ap in _stationFinder.FindNearbyAirports(state.Latitude, state.Longitude))
        {
            var dx = (ap.Longitude - state.Longitude) * 60 * Math.Cos(state.Latitude * Math.PI / 180);
            var dy = (state.Latitude - ap.Latitude) * 60;
            
            MapStations.Add(new MapStationViewModel
            {
                IcaoId = ap.IcaoId,
                X = 384 + dx * (radarFrame != null ? RadarTileCalculator.PixelsPerNm(state.Latitude, 9) : 3.0),
                Y = 384 + dy * (radarFrame != null ? RadarTileCalculator.PixelsPerNm(state.Latitude, 9) : 3.0)
            });
        }
    }

    private async Task LoadTileBitmapAsync(RadarTileViewModel tile)
    {
        if (_tileCache.TryGetValue(tile.Url, out var bmp))
        {
            tile.Image = bmp;
            return;
        }

        try
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(tile.Url);
            using var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            
            if (_tileCache.TryAdd(tile.Url, bitmap))
            {
                tile.Image = bitmap;
            }
        }
        catch
        {
            // Ignore missing tiles
        }
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnConnected(sender, e));
            return;
        }
        Connection.IsConnected = true;
        Connection.StatusText = "Connected to MSFS";
        AppendLog("Connected to MSFS 2024 via SimConnect");
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnDisconnected(sender, e));
            return;
        }
        Connection.IsConnected = false;
        Connection.IsInjecting = false;
        Connection.HasSimWeatherReadback = false;
        Connection.SimWeatherInfo = "---";
        Connection.StatusText = "Disconnected";
        AppendLog("SimConnect disconnected");
    }

    private void AppendLog(string message)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendLog(message));
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss");
        LogMessages = $"[{time}] {message}\n{LogMessages}";

        try
        {
            Directory.CreateDirectory(LogFolderPath);
            File.AppendAllText(LogFilePath, $"[{time}] {message}\n");
        }
        catch
        {
            // best-effort
        }
    }

    private string LogFolderPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyWeave", "Logs");
    private string LogFilePath => Path.Combine(LogFolderPath, $"skyweave_{DateTime.Now:yyyyMMdd}.log");

    private void LoadSettings()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyWeave");
            var path = Path.Combine(dir, "settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _settings = System.Text.Json.JsonSerializer.Deserialize<SkyWeave.App.Models.UserSettings>(json);
                if (_settings != null)
                {
                    Settings.AutoConnect = _settings.AutoConnect;
                    Settings.IsDarkTheme = _settings.IsDarkTheme;
                    Settings.WindowWidth = _settings.WindowWidth;
                    Settings.WindowHeight = _settings.WindowHeight;
                    Settings.GlassOpacityPercent = _settings.GlassOpacityPercent;
                    Injection.TurbulenceIntensityPercent = _settings.TurbulenceIntensityPercent;
                    Injection.WakeTurbulenceEnabled = _settings.WakeTurbulenceEnabled;
                    Injection.WakeTurbulencePercent = _settings.WakeTurbulencePercent;
                    Injection.GustEnhancementPercent = _settings.GustEnhancementPercent;
                    Injection.ThunderstormIntensityPercent = _settings.ThunderstormIntensityPercent;
                    Injection.PrecipitationPercent = _settings.PrecipitationPercent;
                    Injection.AerosolPercent = _settings.AerosolPercent;
                    Injection.RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
                    Injection.InjectionIntervalSeconds = _settings.InjectionIntervalSeconds;
                    Injection.SmoothingDurationMinutes = _settings.SmoothingDurationMinutes;

                    if (Settings.AutoConnect)
                    {
                        _ = ConnectAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to load settings: {ex.Message}");
        }
    }

    public void SaveSettings()
    {
        try
        {
            _settings ??= new SkyWeave.App.Models.UserSettings();
            _settings.AutoConnect = Settings.AutoConnect;
            _settings.IsDarkTheme = Settings.IsDarkTheme;
            _settings.WindowWidth = (int)Math.Round(Settings.WindowWidth);
            _settings.WindowHeight = (int)Math.Round(Settings.WindowHeight);
            _settings.GlassOpacityPercent = Settings.GlassOpacityPercent;
            _settings.TurbulenceIntensityPercent = Injection.TurbulenceIntensityPercent;
            _settings.WakeTurbulenceEnabled = Injection.WakeTurbulenceEnabled;
            _settings.WakeTurbulencePercent = Injection.WakeTurbulencePercent;
            _settings.GustEnhancementPercent = Injection.GustEnhancementPercent;
            _settings.ThunderstormIntensityPercent = Injection.ThunderstormIntensityPercent;
            _settings.PrecipitationPercent = Injection.PrecipitationPercent;
            _settings.AerosolPercent = Injection.AerosolPercent;
            _settings.RefreshIntervalSeconds = (int)Math.Round(Injection.RefreshIntervalSeconds);
            _settings.InjectionIntervalSeconds = (int)Math.Round(Injection.InjectionIntervalSeconds);
            _settings.SmoothingDurationMinutes = (int)Math.Round(Injection.SmoothingDurationMinutes);

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyWeave");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");
            var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to save settings: {ex.Message}");
        }
    }

    public void Dispose()
    {
        SaveSettings();
        _injector?.Dispose();
        _weatherEngine?.Dispose();
        _simConnect?.Dispose();
        _passiveReadbackTimer?.Dispose();
    }
}
