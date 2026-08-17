using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.App.Models;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using SkyWeave.SimBridge;

namespace SkyWeave.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly WeatherEngine _weatherEngine;
    private readonly SimConnectManager _simConnect;
    private readonly StationFinder _stationFinder;
    private WeatherInjector? _injector;
    private SkyWeave.App.Models.UserSettings? _settings;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _stationId = "---";

    [ObservableProperty]
    private string _temperature = "--°C";

    [ObservableProperty]
    private string _dewpoint = "--°C";

    [ObservableProperty]
    private string _wind = "---";

    [ObservableProperty]
    private string _visibility = "---";

    [ObservableProperty]
    private string _altimeter = "---";

    [ObservableProperty]
    private string _flightCategory = "---";

    [ObservableProperty]
    private string _freezingLevel = "---";

    [ObservableProperty]
    private string _ceiling = "---";

    [ObservableProperty]
    private string _icingIndex = "---";

    [ObservableProperty]
    private string _turbulenceIndex = "---";

    [ObservableProperty]
    private string _thunderstormIntensity = "---";

    [ObservableProperty]
    private string _precipitation = "---";

    [ObservableProperty]
    private string _humidity = "---";

    [ObservableProperty]
    private string _aerosolDensity = "---";

    [ObservableProperty]
    private string _lightningCount = "---";

    [ObservableProperty]
    private string _closestStrike = "---";

    [ObservableProperty]
    private string _stormCellCount = "---";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isInjecting;

    [ObservableProperty]
    private bool _isPassiveMode;

    [ObservableProperty]
    private string _rawMetar = "---";

    [ObservableProperty]
    private string _lastUpdate = "---";

    public string VersionText { get; } = $"v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}-beta";

    [ObservableProperty]
    private string _manualStation = string.Empty;

    [ObservableProperty]
    private string _refreshIntervalText = "20";

    [ObservableProperty]
    private string _logMessages = string.Empty;

    [ObservableProperty]
    private string _tafText = "---";

    [ObservableProperty]
    private string _selectedAirportName = "---";

    [ObservableProperty]
    private string _aircraftPosition = "---";

    [ObservableProperty]
    private double _refreshIntervalSeconds = 20;

    [ObservableProperty]
    private double _smoothingDurationMinutes = 3;

    [ObservableProperty]
    private double _injectionIntervalSeconds = 5;

    [ObservableProperty]
    private bool _autoConnect;

    [ObservableProperty]
    private double _turbulenceIntensityPercent = 100;

    [ObservableProperty]
    private bool _wakeTurbulenceEnabled = true;

    [ObservableProperty]
    private double _wakeTurbulencePercent = 100;

    [ObservableProperty]
    private double _gustEnhancementPercent = 100;

    [ObservableProperty]
    private double _thunderstormIntensityPercent = 100;

    [ObservableProperty]
    private double _precipitationPercent = 100;

    [ObservableProperty]
    private double _aerosolPercent = 100;

    [ObservableProperty]
    private double _glassOpacityPercent = 85;

    [ObservableProperty]
    private double _glassOpacity = 0.85;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private double _windowWidth = 1400;

    [ObservableProperty]
    private double _windowHeight = 900;

    [ObservableProperty]
    private string _sourceModel = "---";

    [ObservableProperty]
    private string _dataAge = "---";

    [ObservableProperty]
    private string _capeValue = "---";

    [ObservableProperty]
    private string _liftedIndexValue = "---";

    [ObservableProperty]
    private string _tafStation = "---";

    [ObservableProperty]
    private string _tafValidity = "---";

    [ObservableProperty]
    private string _tafFlightCategory = "---";

    [ObservableProperty]
    private string _tafWind = "---";

    [ObservableProperty]
    private string _tafVisibility = "---";

    [ObservableProperty]
    private bool _tafDivergence;

    [ObservableProperty]
    private string _radarTimestamp = "---";

    [ObservableProperty]
    private bool _radarHasData;

    [ObservableProperty]
    private double _radarFade = 1.0;

    [ObservableProperty]
    private double _selectedRangeNm = 100;

    public string RangeText => $"Range: {SelectedRangeNm:F0} nm · center at aircraft";

    public ObservableCollection<TafGroupViewModel> TafGroups { get; } = new();
    public ObservableCollection<RadarTileViewModel> RadarTiles { get; } = new();

    public double Ring25Diameter { get; private set; }
    public double Ring25Left { get; private set; }
    public double Ring50Diameter { get; private set; }
    public double Ring50Left { get; private set; }
    public double Ring100Diameter { get; private set; }
    public double Ring100Left { get; private set; }
    public double Ring250Diameter { get; private set; }
    public double Ring250Left { get; private set; }

    public ObservableCollection<CloudLayerViewModel> CloudLayers { get; } = new();
    public ObservableCollection<WindLayerViewModel> WindLayers { get; } = new();
    public ObservableCollection<LightningViewModel> LightningStrikes { get; } = new();
    public ObservableCollection<StormCellViewModel> StormCells { get; } = new();
    public ObservableCollection<IcingLayerViewModel> IcingLayers { get; } = new();
    public ObservableCollection<TurbulenceLayerViewModel> TurbulenceLayers { get; } = new();
    public ObservableCollection<HazardViewModel> Hazards { get; } = new();
    public ObservableCollection<AirportViewModel> NearbyAirports { get; } = new();

    public MainViewModel()
    {
        _weatherEngine = new WeatherEngine();
        _simConnect = new SimConnectManager();
        _stationFinder = new StationFinder();

        _weatherEngine.WeatherUpdated += OnWeatherUpdated;
        _weatherEngine.PassiveDataReceived += OnPassiveData;
        _weatherEngine.ErrorOccurred += OnError;
        _weatherEngine.WprGenerated += OnWprGenerated;
        _simConnect.Connected += OnConnected;
        _simConnect.Disconnected += OnDisconnected;
        _simConnect.ErrorOccurred += OnError;
        _simConnect.PositionUpdated += OnPositionUpdated;

        LoadSettings();
        ApplyEngineCustomization();
        LoadNearbyAirports();
    }

    private void ApplyEngineCustomization()
    {
        _weatherEngine.TurbulenceIntensityScale = TurbulenceIntensityPercent / 100.0;
        _weatherEngine.WakeTurbulenceEnabled = WakeTurbulenceEnabled;
        _weatherEngine.WakeTurbulenceScale = WakeTurbulencePercent / 100.0;
        _weatherEngine.GustEnhancementScale = GustEnhancementPercent / 100.0;
        _weatherEngine.ThunderstormIntensityScale = ThunderstormIntensityPercent / 100.0;
        _weatherEngine.PrecipitationScale = PrecipitationPercent / 100.0;
        _weatherEngine.AerosolScale = AerosolPercent / 100.0;
        _weatherEngine.SmoothingDurationMinutes = SmoothingDurationMinutes;
    }

    partial void OnRefreshIntervalSecondsChanged(double value) => RefreshIntervalText = value.ToString("0");

    partial void OnRefreshIntervalTextChanged(string value)
    {
        if (double.TryParse(value, out var seconds))
            RefreshIntervalSeconds = seconds;
    }

    partial void OnTurbulenceIntensityPercentChanged(double value) => _weatherEngine.TurbulenceIntensityScale = value / 100.0;

    partial void OnWakeTurbulenceEnabledChanged(bool value) => _weatherEngine.WakeTurbulenceEnabled = value;

    partial void OnWakeTurbulencePercentChanged(double value) => _weatherEngine.WakeTurbulenceScale = value / 100.0;

    partial void OnGustEnhancementPercentChanged(double value) => _weatherEngine.GustEnhancementScale = value / 100.0;

    partial void OnThunderstormIntensityPercentChanged(double value) => _weatherEngine.ThunderstormIntensityScale = value / 100.0;

    partial void OnPrecipitationPercentChanged(double value) => _weatherEngine.PrecipitationScale = value / 100.0;

    partial void OnAerosolPercentChanged(double value) => _weatherEngine.AerosolScale = value / 100.0;

    partial void OnSmoothingDurationMinutesChanged(double value) => _weatherEngine.SmoothingDurationMinutes = value;

    partial void OnInjectionIntervalSecondsChanged(double value)
    {
        if (_injector != null)
            _injector.InjectionInterval = TimeSpan.FromSeconds(value);
    }

    partial void OnGlassOpacityPercentChanged(double value) => GlassOpacity = Math.Clamp(value / 100.0, 0.4, 1.0);

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    partial void OnSelectedRangeNmChanged(double value) => OnPropertyChanged(nameof(RangeText));

    [RelayCommand]
    private void SetRange(double nm) => SelectedRangeNm = nm;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var attemptStarted = _simConnect.Connect();

        if (!attemptStarted)
            return;

        StatusText = "Connecting...";
        AppendLog("SimConnect attempt started — waiting for simulator acknowledgement");

        _injector = new WeatherInjector(_simConnect, _weatherEngine);
        _injector.InjectionInterval = TimeSpan.FromSeconds(InjectionIntervalSeconds);
        _injector.InjectionStatus += (s, msg) => AppendLog($"Injector: {msg}");

        AircraftPosition = "awaiting sim position...";

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void Disconnect()
    {
        _injector?.StopInjection();
        _simConnect.Disconnect();
        _weatherEngine.Stop();
        IsInjecting = false;
        StatusText = "Disconnected";
        AppendLog("Disconnected from MSFS 2024");
    }

    [RelayCommand]
    private async Task StartWeatherAsync()
    {
        try
        {
            _weatherEngine.RefreshInterval = TimeSpan.FromSeconds(RefreshIntervalSeconds);

            var resolved = ResolveStartCoordinates();
            if (!resolved.HasValue)
            {
                StatusText = "Awaiting sim position";
                AppendLog("No position fix yet — connect with the sim running, or select an airport. Weather engine will start on the first fix.");
                return;
            }

            StatusText = "Starting weather engine...";
            AppendLog($"Starting weather engine (interval: {_weatherEngine.RefreshInterval.TotalSeconds}s, position: {resolved.Value.lat:F4}, {resolved.Value.lon:F4})");

            await _weatherEngine.StartAsync(resolved.Value.lat, resolved.Value.lon, IsPassiveMode);

            if (!IsPassiveMode && _injector != null)
            {
                await _injector.StartInjectionAsync();
            }

            IsInjecting = !IsPassiveMode;
            StatusText = IsPassiveMode ? "Passive mode active" : "Weather engine running";
            AppendLog(IsPassiveMode ? "Started in passive mode (monitoring only)" : "Weather injection started");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"Start error: {ex.Message}");
        }
    }

    /// <summary>
    /// Real sim position if we have a fix; otherwise a manually selected
    /// airport; otherwise null — we never seed a default position (audit C4).
    /// </summary>
    private (double lat, double lon)? ResolveStartCoordinates()
    {
        var pos = _simConnect.GetAircraftPosition();
        if (pos.HasValue)
            return (pos.Value.Latitude, pos.Value.Longitude);

        if (!string.IsNullOrWhiteSpace(ManualStation))
        {
            var match = GetAirportList().FirstOrDefault(a =>
                a.IcaoId.Equals(ManualStation.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return (match.Latitude, match.Longitude);
        }

        return null;
    }

    [RelayCommand]
    private async Task StartPassiveAsync()
    {
        try
        {
            var resolved = ResolveStartCoordinates();
            if (!resolved.HasValue)
            {
                StatusText = "Awaiting sim position";
                AppendLog("No position fix yet — connect with the sim running, or select an airport.");
                return;
            }

            IsPassiveMode = true;
            StatusText = "Starting passive mode...";
            await _weatherEngine.StartAsync(resolved.Value.lat, resolved.Value.lon, true);
            IsInjecting = false;
            StatusText = "Passive mode active - monitoring only";
            AppendLog("Started passive mode - weather data displayed but not injected");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"Passive mode error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void StopWeather()
    {
        _injector?.StopInjection();
        _weatherEngine.Stop();
        IsInjecting = false;
        StatusText = "Weather engine stopped";
        AppendLog("Weather engine stopped");
    }

    [RelayCommand]
    private async Task RefreshNowAsync()
    {
        try
        {
            StatusText = "Refreshing...";
            var state = await _weatherEngine.FetchCurrentWeatherAsync();
            if (state != null)
            {
                UpdateUI(state);
                StatusText = "Refresh complete";
                AppendLog($"Refreshed weather for {state.StationId}");
            }
            else
            {
                StatusText = "No data available - check network/API status";
                AppendLog("Refresh failed - no data returned. Check if NOAA/Open-Meteo APIs are reachable.");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh error: {ex.Message}";
            AppendLog($"Refresh error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SelectAirportAsync(string? icaoId)
    {
        if (string.IsNullOrEmpty(icaoId)) return;

        ManualStation = icaoId;
        var airports = GetAirportList();
        var match = airports.FirstOrDefault(a => a.IcaoId == icaoId);
        if (match != null)
        {
            SelectedAirportName = $"{match.IcaoId} - {match.Name}";
            StatusText = $"Selected {match.IcaoId}";
            AppendLog($"Selected airport: {match.IcaoId} ({match.Name})");

            await _weatherEngine.UpdatePositionAsync(match.Latitude, match.Longitude);
        }
    }

    [RelayCommand]
    private async Task UseManualStationAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualStation)) return;

        var icao = ManualStation.Trim().ToUpper();
        StatusText = $"Fetching weather for {icao}...";
        AppendLog($"Manual station lookup: {icao}");

        var airports = GetAirportList();
        var match = airports.FirstOrDefault(a => a.IcaoId == icao);
        if (match != null)
        {
            SelectedAirportName = $"{match.IcaoId} - {match.Name}";
            await _weatherEngine.UpdatePositionAsync(match.Latitude, match.Longitude);
        }
        else
        {
            AppendLog($"Airport {icao} not found in database, using coordinates");
        }

        await Task.CompletedTask;
    }

    private void LoadNearbyAirports()
    {
        NearbyAirports.Clear();
        var airports = GetAirportList();
        foreach (var airport in airports.Take(12))
        {
            NearbyAirports.Add(new AirportViewModel
            {
                IcaoId = airport.IcaoId,
                Name = airport.Name,
                IataId = airport.IataId ?? "---"
            });
        }
    }

    private List<AirportData> GetAirportList()
    {
        return _stationFinder.AllAirports.ToList();
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
        AircraftPosition = $"{pos.Latitude:F4}, {pos.Longitude:F4} @ {pos.AltitudeFeet:F0} ft";
    }

    private void UpdateUI(WeatherState state)
    {
        StationId = state.StationId;
        Temperature = $"{state.TemperatureCelsius:F1}°C";
        Dewpoint = $"{state.DewpointCelsius:F1}°C";
        Wind = $"{state.WindDirectionDegrees:F0}° @ {state.WindSpeedKnots:F0} kt";
        Visibility = $"{state.VisibilityMeters / 1609.344:F1} SM";
        Altimeter = $"{state.AltimeterHpa:F1} hPa";
        FlightCategory = state.FlightCategory;
        FreezingLevel = $"{state.FreezingLevelFeet:F0} ft";
        Ceiling = $"{state.CeilingFeet:F0} ft";
        IcingIndex = $"{state.IcingIndex:P0}";
        TurbulenceIndex = $"{state.TurbulenceIndex:P0}";
        ThunderstormIntensity = $"{state.ThunderstormIntensity:P0}";
        Precipitation = $"{state.PrecipitationRate:F1} mm/hr";
        Humidity = $"{state.HumidityPercent:F0}%";
        AerosolDensity = $"{state.AerosolDensity:P0}";
        LightningCount = $"{_weatherEngine.RecentStrikes.Count}";
        ClosestStrike = _weatherEngine.RecentStrikes.Count > 0
            ? $"{_weatherEngine.RecentStrikes.Min(s => s.DistanceNm):F1} nm"
            : "---";
        StormCellCount = $"{_weatherEngine.DetectedStormCells.Count}";
        RawMetar = $"Station: {state.StationId} | Temp: {state.TemperatureCelsius:F1}°C | Dew: {state.DewpointCelsius:F1}°C | Wind: {state.WindDirectionDegrees:F0}@{state.WindSpeedKnots:F0}";
        LastUpdate = $"Updated: {DateTime.Now:HH:mm:ss}";
        SourceModel = state.SourceModelName;
        DataAge = $"{(int)state.DataAgeMinutes} min";
        CapeValue = state.ConvectiveAvailablePotentialEnergy is double c ? $"{c:F0} J/kg" : "---";
        LiftedIndexValue = state.LiftedIndex is double li ? $"{li:F1}" : "---";

        if (state.Taf != null)
        {
            TafText = state.Taf.RawText;
            TafStation = state.Taf.StationId;
            TafValidity = $"{state.Taf.ValidFrom:dd HH:mm}Z - {state.Taf.ValidTo:dd HH:mm}Z";
            TafFlightCategory = state.Taf.FlightCategory;
            TafWind = state.Taf.WindSpeedKnots > 0
                ? $"{state.Taf.WindDirectionDegrees:F0}° @ {state.Taf.WindSpeedKnots:F0} kt"
                : "---";
            TafVisibility = state.Taf.VisibilityMeters > 0
                ? $"{state.Taf.VisibilityMeters / 1609.344:F1} SM"
                : "---";
            TafDivergence = state.Taf.FlightCategory != state.FlightCategory &&
                            !string.IsNullOrEmpty(state.Taf.FlightCategory);

            TafGroups.Clear();
            foreach (var group in new SkyWeave.Core.Decoders.TafDecoder().DecodeChangeGroups(state.Taf.RawText))
            {
                TafGroups.Add(new TafGroupViewModel
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
            TafText = "No TAF available for this station.";
            TafStation = "---";
            TafValidity = "---";
            TafFlightCategory = "---";
            TafWind = "---";
            TafVisibility = "---";
            TafDivergence = false;
            TafGroups.Clear();
        }

        var radarFrame = _weatherEngine.CurrentRadarFrame;
        if (radarFrame != null)
        {
            var (tileX, tileY, _, _) = RadarTileCalculator.PositionToTile(state.Latitude, state.Longitude, 9);
            var ppm = RadarTileCalculator.PixelsPerNm(state.Latitude, 9);

            RadarTiles.Clear();
            foreach (var (x, y) in RadarTileCalculator.MosaicTiles(tileX, tileY, 9))
            {
                RadarTiles.Add(new RadarTileViewModel
                {
                    Url = $"{radarFrame.TileUrl}/256/256/9/{x}/{y}/2/1_1.png",
                    X = (x - tileX + 1) * 256.0,
                    Y = (y - tileY + 1) * 256.0
                });
            }

            Ring25Diameter = 2 * 25 * ppm;
            Ring25Left = 384 - 25 * ppm;
            Ring50Diameter = 2 * 50 * ppm;
            Ring50Left = 384 - 50 * ppm;
            Ring100Diameter = 2 * 100 * ppm;
            Ring100Left = 384 - 100 * ppm;
            Ring250Diameter = 2 * 250 * ppm;
            Ring250Left = 384 - 250 * ppm;

            RadarTimestamp = radarFrame.Timestamp.ToString("HH:mm") + "Z";
            RadarHasData = true;
            RadarFade = 0.3;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RadarFade = 1.0);
        }
        else
        {
            RadarHasData = false;
            RadarTimestamp = "no data";
            RadarTiles.Clear();
        }

        CloudLayers.Clear();
        foreach (var layer in state.CloudLayers)
        {
            CloudLayers.Add(new CloudLayerViewModel
            {
                Base = $"{layer.BaseFeetAgl:F0} ft",
                Top = $"{layer.TopFeetAgl:F0} ft",
                Type = layer.Type.ToString(),
                Density = $"{layer.Density:P0}",
                Scattering = $"{layer.Scattering:F2}"
            });
        }

        WindLayers.Clear();
        foreach (var layer in state.WindsAloft)
        {
            WindLayers.Add(new WindLayerViewModel
            {
                Altitude = $"{layer.AltitudeFeet:F0} ft",
                Direction = $"{layer.DirectionDegrees:F0}°",
                Speed = $"{layer.SpeedKnots:F0} kt",
                Temperature = $"{layer.TemperatureCelsius:F1}°C"
            });
        }

        LightningStrikes.Clear();
        foreach (var strike in _weatherEngine.RecentStrikes.Take(20))
        {
            LightningStrikes.Add(new LightningViewModel
            {
                Latitude = $"{strike.Latitude:F3}",
                Longitude = $"{strike.Longitude:F3}",
                Distance = $"{strike.DistanceNm:F1} nm",
                Time = strike.Timestamp.ToString("HH:mm:ss")
            });
        }

        StormCells.Clear();
        foreach (var cell in _weatherEngine.DetectedStormCells)
        {
            StormCells.Add(new StormCellViewModel
            {
                Latitude = $"{cell.Latitude:F3}",
                Longitude = $"{cell.Longitude:F3}",
                Intensity = $"{cell.Intensity:P0}",
                Type = cell.Type.ToString(),
                Strikes = $"{cell.NearbyStrikes.Count}"
            });
        }

        IcingLayers.Clear();
        foreach (var layer in state.IcingLayers)
        {
            IcingLayers.Add(new IcingLayerViewModel
            {
                Base = $"{layer.BaseFeet:F0} ft",
                Top = $"{layer.TopFeet:F0} ft",
                Severity = layer.Severity.ToString(),
                Type = "---"
            });
        }

        TurbulenceLayers.Clear();
        foreach (var layer in state.TurbulenceLayers)
        {
            TurbulenceLayers.Add(new TurbulenceLayerViewModel
            {
                Base = $"{layer.BaseFeet:F0} ft",
                Top = $"{layer.TopFeet:F0} ft",
                Severity = layer.Intensity.ToString(),
                Type = layer.Type.ToString()
            });
        }

        Hazards.Clear();
        foreach (var hazard in state.Hazards)
        {
            Hazards.Add(new HazardViewModel
            {
                Type = hazard.Type.ToString(),
                Severity = $"{hazard.Severity:P0}",
                ValidTo = hazard.ValidTo.ToString("HH:mm"),
                Description = hazard.Description.Length > 40
                    ? hazard.Description[..40] + "..."
                    : hazard.Description
            });
        }
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        IsConnected = true;
        StatusText = "Connected to MSFS (verified)";
        AppendLog("Connected to MSFS — sim acknowledged via OnRecvOpen");
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _injector?.StopInjection();
        IsConnected = false;
        IsInjecting = false;
        AircraftPosition = "awaiting sim position...";
        StatusText = "Disconnected";
        AppendLog("Disconnected from MSFS");
    }

    private void OnError(object? sender, string error)
    {
        StatusText = error;
        AppendLog($"ERROR: {error}");
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogMessages = $"[{timestamp}] {message}\n{LogMessages}";

        var lines = LogMessages.Split('\n');
        if (lines.Length > 50)
        {
            LogMessages = string.Join('\n', lines.Take(50));
        }
    }

    private string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SkyWeave",
        "settings.json");

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;

            var json = File.ReadAllText(SettingsPath);
            _settings = System.Text.Json.JsonSerializer.Deserialize<SkyWeave.App.Models.UserSettings>(json);

            if (_settings == null) return;

            RefreshIntervalSeconds = Math.Clamp(_settings.RefreshIntervalSeconds, 5, 300);
            IsPassiveMode = _settings.PassiveMode;
            ManualStation = _settings.ManualStationOverride;
            SmoothingDurationMinutes = Math.Clamp(_settings.SmoothingDurationMinutes, 1, 60);
            InjectionIntervalSeconds = Math.Clamp(_settings.InjectionIntervalSeconds, 1, 300);
            AutoConnect = _settings.AutoConnect;
            WindowWidth = _settings.WindowWidth;
            WindowHeight = _settings.WindowHeight;
            TurbulenceIntensityPercent = Math.Clamp(_settings.TurbulenceIntensityPercent, 0, 200);
            WakeTurbulenceEnabled = _settings.WakeTurbulenceEnabled;
            WakeTurbulencePercent = Math.Clamp(_settings.WakeTurbulencePercent, 0, 200);
            GustEnhancementPercent = Math.Clamp(_settings.GustEnhancementPercent, 0, 200);
            ThunderstormIntensityPercent = Math.Clamp(_settings.ThunderstormIntensityPercent, 0, 200);
            PrecipitationPercent = Math.Clamp(_settings.PrecipitationPercent, 0, 200);
            AerosolPercent = Math.Clamp(_settings.AerosolPercent, 0, 200);
            GlassOpacityPercent = Math.Clamp(_settings.GlassOpacityPercent, 40, 100);
        }
        catch
        {
            // Ignore settings load errors
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settings ??= new SkyWeave.App.Models.UserSettings();

            _settings.RefreshIntervalSeconds = (int)Math.Round(RefreshIntervalSeconds);
            _settings.PassiveMode = IsPassiveMode;
            _settings.ManualStationOverride = ManualStation;
            _settings.SmoothingDurationMinutes = (int)Math.Round(SmoothingDurationMinutes);
            _settings.InjectionIntervalSeconds = (int)Math.Round(InjectionIntervalSeconds);
            _settings.AutoConnect = AutoConnect;
            _settings.WindowWidth = (int)Math.Round(WindowWidth);
            _settings.WindowHeight = (int)Math.Round(WindowHeight);
            _settings.TurbulenceIntensityPercent = TurbulenceIntensityPercent;
            _settings.WakeTurbulenceEnabled = WakeTurbulenceEnabled;
            _settings.WakeTurbulencePercent = WakeTurbulencePercent;
            _settings.GustEnhancementPercent = GustEnhancementPercent;
            _settings.ThunderstormIntensityPercent = ThunderstormIntensityPercent;
            _settings.PrecipitationPercent = PrecipitationPercent;
            _settings.AerosolPercent = AerosolPercent;
            _settings.GlassOpacityPercent = GlassOpacityPercent;

            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore settings save errors
        }
    }

    public void SaveSettingsNow()
    {
        SaveSettings();
    }

    public void Dispose()
    {
        SaveSettings();
        _weatherEngine?.Dispose();
        _simConnect?.Dispose();
        _injector?.Dispose();
    }
}

public class CloudLayerViewModel
{
    public string Base { get; set; } = string.Empty;
    public string Top { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Density { get; set; } = string.Empty;
    public string Scattering { get; set; } = string.Empty;
}

public class WindLayerViewModel
{
    public string Altitude { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
}

public class LightningViewModel
{
    public string Latitude { get; set; } = string.Empty;
    public string Longitude { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class StormCellViewModel
{
    public string Latitude { get; set; } = string.Empty;
    public string Longitude { get; set; } = string.Empty;
    public string Intensity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Strikes { get; set; } = string.Empty;
}

public class IcingLayerViewModel
{
    public string Base { get; set; } = string.Empty;
    public string Top { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class TurbulenceLayerViewModel
{
    public string Base { get; set; } = string.Empty;
    public string Top { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class HazardViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ValidTo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AirportViewModel
{
    public string IcaoId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IataId { get; set; } = string.Empty;
}

public class TafGroupViewModel
{
    public string Type { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string FlightCategory { get; set; } = string.Empty;
    public string Wind { get; set; } = string.Empty;
    public string CloudSummary { get; set; } = string.Empty;
    public double DurationHours => (ValidTo - ValidFrom)?.TotalHours ?? 2;
    public bool IsTempo => Type == "TEMPO";
    public double BlockOpacity => IsTempo ? 0.6 : 1.0;
}

public class RadarTileViewModel
{
    public string Url { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
}
