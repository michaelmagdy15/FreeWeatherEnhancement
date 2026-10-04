using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.App.Models;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using SkyWeave.SimBridge;
using SkyWeave.Api;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkyWeave.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly WeatherEngine _weatherEngine;
    private readonly SimConnectManager _simConnect;
    private readonly StationFinder _stationFinder;
    private WeatherInjector? _injector;
    private SkyWeave.App.Models.UserSettings? _settings;
    private Timer? _passiveReadbackTimer;
    private Timer? _clockTimer;
    private WeatherApiServer? _efbServer;
    private EngineWeatherDataProvider? _efbProvider;
    private readonly NetworkClientDetector _networkDetector;
    private readonly LruCache<string, BitmapSource> _tileCache = new(50);
    private static readonly HttpClient _radarHttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private bool _startWhenPositionAvailable;
    private bool _startPassiveWhenPositionAvailable;
    private string? _lastAutoDetectedAirport;

    public ConnectionViewModel Connection { get; }
    public WeatherDisplayViewModel WeatherDisplay { get; }
    public RadarViewModel Radar { get; }
    public TafViewModel Taf { get; }
    public InjectionViewModel Injection { get; }
    public SettingsViewModel Settings { get; }
    public FlightPlanViewModel FlightPlan { get; }
    public SoundingViewModel Sounding { get; }
    public WeatherEngine WeatherEngine => _weatherEngine;

    [ObservableProperty]
    private string _logMessages = string.Empty;

    [ObservableProperty]
    private string _zuluTimeText = $"{DateTime.UtcNow:HH:mm:ss} Z";

    [ObservableProperty]
    private bool _isOnlineNetworkActive;

    [ObservableProperty]
    private string? _onlineNetworkName;

    [ObservableProperty]
    private bool _isHistoricalMode;

    [ObservableProperty]
    private DateTime _historicalDate = DateTime.UtcNow.Date.AddDays(-1);

    [ObservableProperty]
    private int _historicalHour = 12;

    [ObservableProperty]
    private string _historicalStatusText = "LIVE WEATHER (REAL-TIME)";

    public DateTime HistoricalTargetUtc => new DateTime(HistoricalDate.Year, HistoricalDate.Month, HistoricalDate.Day, Math.Clamp(HistoricalHour, 0, 23), 0, 0, DateTimeKind.Utc);

    [ObservableProperty]
    private bool _isSandboxMode;

    [ObservableProperty]
    private string _sandboxScenarioName = "Severe Crosswind Landing";

    [ObservableProperty]
    private double _sandboxWindDirection = 90;

    [ObservableProperty]
    private double _sandboxWindSpeed = 35;

    [ObservableProperty]
    private double _sandboxWindGust = 50;

    [ObservableProperty]
    private double _sandboxTemperature = 18;

    [ObservableProperty]
    private double _sandboxDewpoint = 8;

    [ObservableProperty]
    private double _sandboxPressure = 1005;

    [ObservableProperty]
    private double _sandboxVisibility = 15000;

    [ObservableProperty]
    private double _sandboxTurbulence = 0.8;

    [ObservableProperty]
    private double _sandboxIcing = 0.0;

    [ObservableProperty]
    private bool _sandboxThunderstorm = false;

    [ObservableProperty]
    private bool _sandboxInstantTransition = false;

    [ObservableProperty]
    private string _sandboxStatusText = "STANDBY (READY)";

    [ObservableProperty]
    private int _trafficCount;

    [ObservableProperty]
    private bool _hasWakeEncounter;

    [ObservableProperty]
    private string _wakeStatusText = "CLEAR (NO TRAFFIC WAKE)";

    [ObservableProperty]
    private string _trafficStatusText = "0 AIRCRAFT TRACKED (15 NM)";

    public ObservableCollection<AircraftTraffic> NearbyTrafficList { get; } = new();

    public string VersionText { get; } = $"v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}-beta";

    public ObservableCollection<MapStationViewModel> MapStations { get; } = new();
    public ObservableCollection<AirportViewModel> NearbyAirports { get; } = new();

    public MainViewModel()
    {
        _weatherEngine = new WeatherEngine();
        _simConnect = new SimConnectManager();
        _stationFinder = new StationFinder();
        _networkDetector = new NetworkClientDetector(TimeSpan.FromSeconds(3));
        _networkDetector.OnlineClientStatusChanged += OnOnlineClientStatusChanged;

        Connection = new ConnectionViewModel(_simConnect, _weatherEngine, this);
        WeatherDisplay = new WeatherDisplayViewModel();
        Radar = new RadarViewModel(this);
        Taf = new TafViewModel();
        Injection = new InjectionViewModel(this);
        Settings = new SettingsViewModel(this);
        FlightPlan = new FlightPlanViewModel(this, _weatherEngine);
        Sounding = new SoundingViewModel();

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
        _simConnect.TrafficUpdated += OnTrafficUpdated;

        LoadSettings();
        ApplyInjectionSettings();
        ApplySkyAnchorSettings();
        LoadNearbyAirports();
        Settings.RefreshPlugins();

        _clockTimer = new Timer(_ =>
        {
            RunOnUIThread(() =>
            {
                ZuluTimeText = $"{DateTime.UtcNow:HH:mm:ss} Z";
                UpdateSkyAnchorTelemetry();
            });
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        AppendLog($"=== SkyWeave session start (v{GetType().Assembly.GetName().Version}) ===");
        AppendDiagnostics();
        _ = StartEfbAsync();
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

    private async Task StartEfbAsync()
    {
        int[] candidatePorts = [WeatherApiServer.DefaultPort, WeatherApiServer.DefaultPort + 1, WeatherApiServer.DefaultPort + 2];
        foreach (var port in candidatePorts)
        {
            try
            {
                var server = new WeatherApiServer(
                    customEngine: _weatherEngine,
                    allowPositionOverride: false,
                    allowLanAccess: Settings.AllowLanEfbAccess,
                    port: port);

                _efbServer = server;
                _efbProvider = server.App.Services.GetService(typeof(IWeatherDataProvider)) as EngineWeatherDataProvider;
                UpdateEfbStatus();

                await server.StartAsync();

                NotifyEfbProperties();

                var lanMsg = Settings.AllowLanEfbAccess
                    ? $"LAN access ENABLED ({string.Join(", ", server.LanUrls)})"
                    : "LAN access DISABLED (localhost only)";
                AppendLog($"Web EFB started at {server.LocalUrl} — {lanMsg}");
                return;
            }
            catch (Exception ex)
            {
                if (_efbServer?.IsPortConflict == true)
                {
                    try { await _efbServer.DisposeAsync(); } catch { }
                    _efbServer = null;
                    continue;
                }
                AppendLog($"Web EFB unavailable: {ex.Message}");
                break;
            }
        }
        NotifyEfbProperties();
    }

    public async Task RestartEfbAsync()
    {
        try
        {
            if (_efbServer != null)
            {
                await _efbServer.DisposeAsync();
                _efbServer = null;
                _efbProvider = null;
            }
        }
        catch { }

        await StartEfbAsync();
    }

    private void NotifyEfbProperties()
    {
        OnPropertyChanged(nameof(EfbStatusText));
        OnPropertyChanged(nameof(IsEfbRunning));
        OnPropertyChanged(nameof(IsEfbConflict));
        OnPropertyChanged(nameof(EfbLocalUrl));
        OnPropertyChanged(nameof(EfbLanAddressText));
    }

    private void OnOnlineClientStatusChanged(bool isRunning, string? clientName)
    {
        RunOnUIThread(() =>
        {
            IsOnlineNetworkActive = isRunning;
            OnlineNetworkName = clientName;
            ApplyOnlineAtcSettings();
            if (_efbProvider != null)
            {
                _efbProvider.IsOnlineNetworkActive = isRunning;
                _efbProvider.OnlineNetworkName = clientName;
            }
            if (isRunning)
            {
                AppendLog($"[ATC] Online network/AI client detected: {clientName}");
                if (clientName != null && clientName.Contains("SayIntentions", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("[ATC] SayIntentions.AI active — simulator atmospheric alignment engaged (calibrated QNH & surface wind for ATC clearances)");
                }
                else if (Settings.AutoMatchOnlineAtcWeather)
                {
                    AppendLog("[ATC] Online ATC weather matching active (VATSIM/IVAO METAR & controller ATIS prioritized)");
                }
            }
            else
            {
                AppendLog("[ATC] Online network client offline: restoring standard NOAA/AWC METAR priority");
            }
        });
    }

    public void ApplyOnlineAtcSettings()
    {
        if (_settings != null)
        {
            _settings.AutoMatchOnlineAtcWeather = Settings.AutoMatchOnlineAtcWeather;
            _settings.PreferOnlineAtisQnh = Settings.PreferOnlineAtisQnh;
            _settings.PreferIvaoMetar = Settings.PreferIvaoMetar;
            _settings.SyncWithSayIntentions = Settings.SyncWithSayIntentions;
            _settings.NavigraphUsername = Settings.NavigraphUsername;
        }

        _weatherEngine.AutoMatchOnlineAtcWeather = Settings.AutoMatchOnlineAtcWeather;
        _weatherEngine.PreferOnlineAtisQnh = Settings.PreferOnlineAtisQnh;
        _weatherEngine.PreferIvaoMetar = Settings.PreferIvaoMetar;
        _weatherEngine.PreferVatsimMetar = (IsOnlineNetworkActive && Settings.AutoMatchOnlineAtcWeather) && !Settings.PreferIvaoMetar;
    }

    private void UpdateEfbStatus()
    {
        if (_efbProvider == null)
            return;

        _efbProvider.SimConnected = _simConnect.IsConnected;
        _efbProvider.IsInjecting = _injector?.IsInjecting == true;
        _efbProvider.IsOnlineNetworkActive = IsOnlineNetworkActive;
        _efbProvider.OnlineNetworkName = OnlineNetworkName;
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

    public void ApplySkyAnchorSettings()
    {
        _weatherEngine.AnchorManager.DepartureHoldEnabled = Settings.DepartureHoldEnabled;
        _weatherEngine.AnchorManager.ArrivalHoldEnabled = Settings.ArrivalHoldEnabled;
        _weatherEngine.AnchorManager.AutoFreezeOnApproach = Settings.AutoFreezeOnApproach;
    }

    private void UpdateSkyAnchorTelemetry()
    {
        var pos = _simConnect.GetAircraftPosition();
        if (pos.HasValue)
        {
            var anchor = _weatherEngine.AnchorManager.Evaluate(pos.Value.Latitude, pos.Value.Longitude, pos.Value.AltitudeFeet);
            Connection.SkyAnchorPhaseBadge = anchor.Phase switch
            {
                SkyAnchorPhase.DepartureHold => "DEP HOLD",
                SkyAnchorPhase.ArrivalHold => "ARR HOLD",
                SkyAnchorPhase.FinalFreeze => "FINAL FREEZE",
                SkyAnchorPhase.ManualFreeze => "HOLD FROZEN",
                _ => "EN-ROUTE"
            };
            Connection.IsFrozen = _weatherEngine.IsFrozen;
            FlightPlan.UpdateAnchorTelemetry();
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

        try
        {
            Clipboard.SetText(text);
            Connection.StatusText = "Log copied to clipboard";
            AppendLog($"Log copied to clipboard ({text.Length} chars)");
        }
        catch (Exception ex)
        {
            Connection.StatusText = "Clipboard unavailable";
            AppendLog($"Copy log: clipboard error: {ex.Message}");
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

    [RelayCommand]
    public async Task ApplyHistoricalReplayAsync()
    {
        var targetUtc = HistoricalTargetUtc;
        HistoricalStatusText = $"HISTORICAL: {targetUtc:yyyy-MM-dd HH:00}Z (ERA5 ARCHIVE)";
        IsHistoricalMode = true;
        await _weatherEngine.SetHistoricalModeAsync(true, targetUtc);
        if (_efbProvider != null)
        {
            _efbProvider.IsHistoricalMode = true;
            _efbProvider.HistoricalTargetUtc = targetUtc;
        }
        AppendLog($"[HISTORICAL] ERA5 weather replay active for {targetUtc:yyyy-MM-dd HH:00}Z (fetching reanalysis atmosphere)");
    }

    [RelayCommand]
    public async Task ReturnToLiveWeatherAsync()
    {
        IsHistoricalMode = false;
        HistoricalStatusText = "LIVE WEATHER (REAL-TIME)";
        await _weatherEngine.SetHistoricalModeAsync(false);
        if (_efbProvider != null)
        {
            _efbProvider.IsHistoricalMode = false;
            _efbProvider.HistoricalTargetUtc = null;
        }
        AppendLog("[HISTORICAL] Replay deactivated: returned to live real-time weather.");
    }

    [RelayCommand]
    public void SetHistoricalPreset(string preset)
    {
        var now = DateTime.UtcNow;
        switch (preset?.ToUpperInvariant())
        {
            case "YESTERDAY":
                HistoricalDate = now.Date.AddDays(-1);
                HistoricalHour = 12;
                break;
            case "LASTWEEK":
                HistoricalDate = now.Date.AddDays(-7);
                HistoricalHour = 12;
                break;
            case "SUMMER":
                HistoricalDate = new DateTime(now.Year - 1, 7, 15);
                HistoricalHour = 14;
                break;
            case "WINTER":
                HistoricalDate = new DateTime(now.Year - 1, 1, 15);
                HistoricalHour = 8;
                break;
        }
        _ = ApplyHistoricalReplayAsync();
    }

    [RelayCommand]
    public async Task ApplySandboxPresetAsync(string presetId)
    {
        IsSandboxMode = true;
        IsHistoricalMode = false;
        await _weatherEngine.ApplySandboxPresetAsync(presetId);
        if (_efbProvider != null)
        {
            await _efbProvider.ApplySandboxPresetAsync(presetId);
        }
        var sc = _weatherEngine.CurrentSandboxScenario;
        if (sc != null)
        {
            SandboxScenarioName = sc.Name;
            SandboxWindDirection = sc.SurfaceWindDirection;
            SandboxWindSpeed = sc.SurfaceWindSpeedKnots;
            SandboxWindGust = sc.SurfaceWindGustKnots ?? 0;
            SandboxTemperature = sc.TemperatureCelsius;
            SandboxDewpoint = sc.DewpointCelsius;
            SandboxPressure = sc.PressureHpa;
            SandboxVisibility = sc.VisibilityMeters;
            SandboxTurbulence = sc.TurbulenceIntensity;
            SandboxIcing = sc.IcingSeverity;
            SandboxThunderstorm = sc.Thunderstorm;
            SandboxInstantTransition = sc.InstantTransition;
            SandboxStatusText = $"ACTIVE: {sc.Name}";
        }
        AppendLog($"[SANDBOX] Applied preset '{presetId}' ({SandboxScenarioName})");
    }

    [RelayCommand]
    public async Task ApplyCustomSandboxAsync()
    {
        IsSandboxMode = true;
        IsHistoricalMode = false;
        var scenario = new SandboxWeatherScenario
        {
            Name = SandboxScenarioName,
            SurfaceWindDirection = SandboxWindDirection,
            SurfaceWindSpeedKnots = SandboxWindSpeed,
            SurfaceWindGustKnots = SandboxWindGust > SandboxWindSpeed ? SandboxWindGust : null,
            TemperatureCelsius = SandboxTemperature,
            DewpointCelsius = SandboxDewpoint,
            PressureHpa = SandboxPressure,
            VisibilityMeters = SandboxVisibility,
            TurbulenceIntensity = SandboxTurbulence,
            IcingSeverity = SandboxIcing,
            Thunderstorm = SandboxThunderstorm,
            InstantTransition = SandboxInstantTransition
        };

        await _weatherEngine.ApplySandboxScenarioAsync(scenario);
        if (_efbProvider != null)
        {
            await _efbProvider.ApplySandboxScenarioAsync(scenario);
        }
        SandboxStatusText = $"ACTIVE: {scenario.Name}";
        AppendLog($"[SANDBOX] Injected custom scenario: {scenario.SurfaceWindDirection:000}/{scenario.SurfaceWindSpeedKnots:00}KT, QNH {scenario.PressureHpa:0000}, Temp {scenario.TemperatureCelsius:0}°C");
    }

    [RelayCommand]
    public async Task ExitSandboxModeAsync()
    {
        IsSandboxMode = false;
        SandboxStatusText = "STANDBY (READY)";
        await _weatherEngine.SetSandboxModeAsync(false);
        if (_efbProvider != null)
        {
            await _efbProvider.SetSandboxModeAsync(false);
        }
        AppendLog("[SANDBOX] Deactivated: returned to live weather.");
    }

    public string EfbStatusText
    {
        get
        {
            if (_efbServer == null) return "Offline";
            if (_efbServer.IsPortConflict) return $"Port Conflict (Port {WeatherApiServer.DefaultPort} in use)";
            if (_efbServer.State == ServerState.Faulted) return $"Faulted: {_efbServer.LastError ?? "Error"}";
            if (_efbServer.IsRunning)
            {
                return Settings.AllowLanEfbAccess
                    ? $"Online (LAN Enabled) — {_efbServer.LocalUrl}"
                    : $"Online (Localhost) — {_efbServer.LocalUrl}";
            }
            return _efbServer.State.ToString();
        }
    }

    public bool IsEfbRunning => _efbServer?.IsRunning == true;
    public bool IsEfbConflict => _efbServer?.IsPortConflict == true;
    public string EfbLocalUrl => _efbServer?.LocalUrl ?? $"http://127.0.0.1:{WeatherApiServer.DefaultPort}";

    public string EfbLanAddressText
    {
        get
        {
            if (!Settings.AllowLanEfbAccess) return "Disabled (Localhost Only)";
            if (_efbServer == null || !_efbServer.IsRunning) return "Server Offline";
            var urls = _efbServer.LanUrls;
            return urls.Count > 0 ? string.Join(", ", urls) : "No active Wi-Fi/LAN IPv4 interface found";
        }
    }

    [RelayCommand]
    private void OpenEfbInBrowser()
    {
        try
        {
            var url = _efbServer?.LocalUrl ?? $"http://127.0.0.1:{WeatherApiServer.DefaultPort}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            AppendLog($"Opened Web EFB in browser: {url}");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to open browser: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CopyEfbUrl()
    {
        try
        {
            var url = Settings.AllowLanEfbAccess && _efbServer?.LanUrls.Count > 0
                ? _efbServer.LanUrls[0]
                : (_efbServer?.LocalUrl ?? $"http://127.0.0.1:{WeatherApiServer.DefaultPort}");
            Clipboard.SetText(url);
            AppendLog($"Copied EFB URL to clipboard: {url}");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to copy URL: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RestartEfb()
    {
        AppendLog("Restarting Web EFB server...");
        await RestartEfbAsync();
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
                _startWhenPositionAvailable = true;
                _startPassiveWhenPositionAvailable = false;
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
            UpdateEfbStatus();
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
                _startPassiveWhenPositionAvailable = true;
                _startWhenPositionAvailable = false;
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
        _startWhenPositionAvailable = false;
        _startPassiveWhenPositionAvailable = false;
        _passiveReadbackTimer?.Dispose();
        _passiveReadbackTimer = null;
        Connection.IsInjecting = false;
        UpdateEfbStatus();
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

    private static void RunOnUIThread(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    private void OnPositionUpdated(object? sender, AircraftPositionData pos)
    {
        RunOnUIThread(() =>
        {
            Connection.AircraftPosition = $"{pos.Latitude:F4}, {pos.Longitude:F4} @ {pos.AltitudeFeet:F0} ft";

            var airport = _stationFinder.FindNearestAirport(pos.Latitude, pos.Longitude);
            if (airport != null && !string.Equals(_lastAutoDetectedAirport, airport.IcaoId, StringComparison.OrdinalIgnoreCase))
            {
                _lastAutoDetectedAirport = airport.IcaoId;
                Connection.SelectedAirportName = $"Auto-detected: {airport.IcaoId}";
                AppendLog($"Auto-detected aircraft airport: {airport.IcaoId}");
                LoadNearbyAirports();
            }

            if (_startWhenPositionAvailable)
            {
                _startWhenPositionAvailable = false;
                _ = StartWeatherAsync();
            }
            else if (_startPassiveWhenPositionAvailable)
            {
                _startPassiveWhenPositionAvailable = false;
                _ = StartPassiveAsync();
            }
        });
    }

    private void OnWeatherReadback(object? sender, AmbientWeatherData readback)
    {
        RunOnUIThread(() =>
        {
            Connection.HasSimWeatherReadback = true;
            Connection.SimWeatherInfo = $"SIM: {readback.TemperatureCelsius:F1}\u00b0C, " +
                             $"{readback.WindDirectionDegrees:F0}\u00b0 @ {readback.WindSpeedKnots:F0} kt, " +
                             $"{readback.SeaLevelPressureHpa:F1} hPa";
        });
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
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UpdateUI(state));
            return;
        }

        Connection.LastUpdate = $"Updated: {DateTime.Now:HH:mm:ss}";

        if (state.IsHistorical && state.HistoricalUtc.HasValue)
        {
            HistoricalStatusText = $"HISTORICAL: {state.HistoricalUtc.Value:yyyy-MM-dd HH:00}Z (ERA5 REPLAY)";
            IsHistoricalMode = true;
        }
        else if (!IsHistoricalMode)
        {
            HistoricalStatusText = "LIVE WEATHER (REAL-TIME)";
        }

        if (state.IsSandbox)
        {
            IsSandboxMode = true;
            SandboxStatusText = $"ACTIVE: {state.SandboxScenarioName ?? "CUSTOM SCENARIO"}";
        }
        else if (!IsSandboxMode)
        {
            SandboxStatusText = "STANDBY (READY)";
        }

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
            var (tileX, tileY, pixelX, pixelY) = RadarTileCalculator.PositionToTile(state.Latitude, state.Longitude, 6);
            var ppm = RadarTileCalculator.PixelsPerNm(state.Latitude, 6);

            Radar.RadarTiles.Clear();
            foreach (var (x, y) in RadarTileCalculator.MosaicTiles(tileX, tileY, 6))
            {
                var tileVm = new RadarTileViewModel
                {
                    Url = $"{radarFrame.TileUrl}/256/6/{x}/{y}/2/1_1.png",
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
            RunOnUIThread(() => Radar.RadarFade = 1.0);
        }
        else
        {
            Radar.RadarHasData = false;
            Radar.RadarTimestamp = "no data";
            Radar.RadarTiles.Clear();
        }

        var nearbyAirports = _stationFinder.FindNearbyAirports(state.Latitude, state.Longitude);
        MapStations.Clear();
        foreach (var ap in nearbyAirports)
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

        // Update Synoptic Map (Isobars, Pressure Centers, Wind Barbs)
        Radar.UpdateSynoptic(state, nearbyAirports);

        // Update Vertical Atmospheric Sounding Diagram
        var acftAlt = _simConnect.GetAircraftPosition()?.AltitudeFeet;
        Sounding.Update(state, acftAlt);

        // Apply Units and Pilot Preferences
        if (Settings.PressureUnit == "inHg")
        {
            var inHg = state.AltimeterHpa * 0.0295299830714;
            WeatherDisplay.Altimeter = $"{inHg:F2} inHg ({state.AltimeterHpa:F1} hPa)";
        }
        else
        {
            WeatherDisplay.Altimeter = $"{state.AltimeterHpa:F1} hPa";
        }

        if (Settings.TemperatureUnit == "F")
        {
            var tempF = (state.TemperatureCelsius * 9.0 / 5.0) + 32.0;
            var dewF = (state.DewpointCelsius * 9.0 / 5.0) + 32.0;
            WeatherDisplay.Temperature = $"{tempF:F1}°F ({state.TemperatureCelsius:F1}°C)";
            WeatherDisplay.Dewpoint = $"{dewF:F1}°F ({state.DewpointCelsius:F1}°C)";
        }

        if (Settings.WindSpeedUnit == "m/s")
        {
            var ms = state.WindSpeedKnots * 0.514444;
            WeatherDisplay.Wind = $"{state.WindDirectionDegrees:F0}° @ {ms:F1} m/s ({state.WindSpeedKnots:F0} kt)";
        }

        UpdateSkyAnchorTelemetry();
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
            var bytes = await _radarHttpClient.GetByteArrayAsync(tile.Url);
            // RainViewer returns a 1370-byte "Zoom Level Not Supported" image if zoom is unavailable
            if (bytes == null || bytes.Length == 1370 || bytes.Length < 100)
            {
                return;
            }

            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            if (_tileCache.TryAdd(tile.Url, bitmap))
            {
                RunOnUIThread(() => tile.Image = bitmap);
            }
        }
        catch
        {
            // Ignore missing tiles
        }
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        RunOnUIThread(() =>
        {
            Connection.IsConnected = true;
            UpdateEfbStatus();
            Connection.StatusText = "Connected to MSFS";
            AppendLog("Connected to MSFS 2024 via SimConnect");
        });
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        RunOnUIThread(() =>
        {
            Connection.IsConnected = false;
            Connection.IsInjecting = false;
            UpdateEfbStatus();
            Connection.HasSimWeatherReadback = false;
            Connection.SimWeatherInfo = "---";
            Connection.StatusText = "Disconnected";
            TrafficCount = 0;
            HasWakeEncounter = false;
            WakeStatusText = "CLEAR (NO TRAFFIC WAKE)";
            TrafficStatusText = "0 AIRCRAFT TRACKED (15 NM)";
            NearbyTrafficList.Clear();
            _efbProvider?.SetTrafficSnapshot(Array.Empty<AircraftTraffic>());
            AppendLog("SimConnect disconnected");
        });
    }

    private void OnTrafficUpdated(object? sender, IReadOnlyList<AircraftTraffic> traffic)
    {
        RunOnUIThread(() =>
        {
            TrafficCount = traffic.Count;
            _efbProvider?.SetTrafficSnapshot(traffic);

            NearbyTrafficList.Clear();
            foreach (var t in traffic.OrderBy(t => t.DistanceNm))
            {
                NearbyTrafficList.Add(t);
            }

            var wakeAc = traffic.FirstOrDefault(t => t.IsInWakeZone);
            if (wakeAc != null)
            {
                if (!HasWakeEncounter)
                {
                    AppendLog($"[WAKE] ⚠️ Wake vortex encounter detected: {wakeAc.WeightClass} {wakeAc.Callsign} ({wakeAc.DistanceNm:F1} NM ahead, {wakeAc.AltitudeDeltaFeet:+0;-0} ft)");
                }
                HasWakeEncounter = true;
                WakeStatusText = $"⚠️ WAKE ENCOUNTER: {wakeAc.WeightClass} {wakeAc.Callsign} ({wakeAc.DistanceNm:F1} NM ahead)";
            }
            else
            {
                HasWakeEncounter = false;
                WakeStatusText = "CLEAR (NO TRAFFIC WAKE)";
            }

            TrafficStatusText = traffic.Count == 1
                ? "1 AIRCRAFT TRACKED (15 NM)"
                : $"{traffic.Count} AIRCRAFT TRACKED (15 NM)";
        });
    }

    public void AppendLog(string message)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => AppendLog(message));
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
                    Settings.AllowLanEfbAccess = _settings.AllowLanEfbAccess;

                    Settings.DepartureHoldEnabled = _settings.DepartureHoldEnabled;
                    Settings.ArrivalHoldEnabled = _settings.ArrivalHoldEnabled;
                    Settings.AutoFreezeOnApproach = _settings.AutoFreezeOnApproach;
                    Settings.PressureUnit = _settings.PressureUnit ?? "inHg";
                    Settings.TemperatureUnit = _settings.TemperatureUnit ?? "C";
                    Settings.WindSpeedUnit = _settings.WindSpeedUnit ?? "kt";
                    Settings.StreamerMode = _settings.StreamerMode;
                    Settings.SimBriefPilotId = _settings.SimBriefPilotId ?? string.Empty;
                    Settings.AutoLoadSimBriefAtLaunch = _settings.AutoLoadSimBriefAtLaunch;
                    Settings.AutoMatchOnlineAtcWeather = _settings.AutoMatchOnlineAtcWeather;
                    Settings.PreferOnlineAtisQnh = _settings.PreferOnlineAtisQnh;
                    Settings.PreferIvaoMetar = _settings.PreferIvaoMetar;
                    Settings.SyncWithSayIntentions = _settings.SyncWithSayIntentions;
                    Settings.NavigraphUsername = _settings.NavigraphUsername ?? string.Empty;
                    FlightPlan.PilotId = Settings.SimBriefPilotId;
                    ApplySkyAnchorSettings();
                    ApplyOnlineAtcSettings();

                    if (Settings.AutoLoadSimBriefAtLaunch && !string.IsNullOrWhiteSpace(Settings.SimBriefPilotId))
                    {
                        _ = FlightPlan.FetchPlanAsync();
                    }

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
            _settings.AllowLanEfbAccess = Settings.AllowLanEfbAccess;
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

            _settings.DepartureHoldEnabled = Settings.DepartureHoldEnabled;
            _settings.ArrivalHoldEnabled = Settings.ArrivalHoldEnabled;
            _settings.AutoFreezeOnApproach = Settings.AutoFreezeOnApproach;
            _settings.PressureUnit = Settings.PressureUnit;
            _settings.TemperatureUnit = Settings.TemperatureUnit;
            _settings.WindSpeedUnit = Settings.WindSpeedUnit;
            _settings.StreamerMode = Settings.StreamerMode;
            _settings.SimBriefPilotId = FlightPlan.PilotId;
            _settings.AutoLoadSimBriefAtLaunch = Settings.AutoLoadSimBriefAtLaunch;
            _settings.AutoMatchOnlineAtcWeather = Settings.AutoMatchOnlineAtcWeather;
            _settings.PreferOnlineAtisQnh = Settings.PreferOnlineAtisQnh;
            _settings.PreferIvaoMetar = Settings.PreferIvaoMetar;
            _settings.SyncWithSayIntentions = Settings.SyncWithSayIntentions;
            _settings.NavigraphUsername = Settings.NavigraphUsername;

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
        _clockTimer?.Dispose();
        _networkDetector?.Dispose();
        _injector?.Dispose();
        _weatherEngine?.Dispose();
        _simConnect?.Dispose();
        _passiveReadbackTimer?.Dispose();
        if (_efbServer != null)
        {
            _efbServer.Dispose();
            _efbServer = null;
        }
    }
}
