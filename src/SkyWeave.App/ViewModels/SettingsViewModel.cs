using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.Core.Plugins;
using SkyWeave.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Wpf.Ui.Appearance;

namespace SkyWeave.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private bool _autoConnect;

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
    private bool _allowLanEfbAccess;

    // Pilot Preferences & Units
    [ObservableProperty]
    private string _pressureUnit = "inHg"; // "inHg" | "hPa"

    [ObservableProperty]
    private string _temperatureUnit = "C"; // "C" | "F"

    [ObservableProperty]
    private string _windSpeedUnit = "kt"; // "kt" | "m/s" | "km/h"

    [ObservableProperty]
    private bool _streamerMode;

    // Sky Anchor Corridor Options
    [ObservableProperty]
    private bool _departureHoldEnabled = true;

    [ObservableProperty]
    private bool _arrivalHoldEnabled = true;

    [ObservableProperty]
    private bool _autoFreezeOnApproach = true;

    // SimBrief Configuration
    [ObservableProperty]
    private string _simBriefPilotId = string.Empty;

    [ObservableProperty]
    private bool _autoLoadSimBriefAtLaunch;

    // Online ATC Networks (VATSIM / IVAO / SayIntentions)
    [ObservableProperty]
    private bool _autoMatchOnlineAtcWeather = true;

    [ObservableProperty]
    private bool _preferOnlineAtisQnh = true;

    [ObservableProperty]
    private bool _preferIvaoMetar;

    [ObservableProperty]
    private bool _syncWithSayIntentions = true;

    [ObservableProperty]
    private string _navigraphUsername = string.Empty;

    // Aeronautical Charts (Airmate Free / Navigraph) & MSFS Planner
    [ObservableProperty]
    private string _chartProvider = "Airmate"; // "Airmate" | "Navigraph" | "ChartFox"

    [ObservableProperty]
    private string _airmateUsername = string.Empty;

    [ObservableProperty]
    private string _airmatePassword = string.Empty;

    [ObservableProperty]
    private string _airmateStatusMessage = "Airmate provides free worldwide official aeronautical AIP charts without subscription.";

    // In-Sim Community Bridge Management
    private readonly MsfsCommunityBridgeManager _bridgeManager = new();

    [ObservableProperty]
    private string _bridgeStatusText = "Checking...";

    [ObservableProperty]
    private string _bridgeStatusColor = "#94A3B8";

    [ObservableProperty]
    private string _communityPathText = string.Empty;

    [ObservableProperty]
    private bool _isBridgeInstalled;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        RefreshBridgeStatus();
    }

    [RelayCommand]
    public void RefreshBridgeStatus()
    {
        var installed = _bridgeManager.CheckBridgeStatus(out var commPath, out var version, out var upToDate);
        CommunityPathText = commPath ?? "MSFS 2024 Community folder not detected";
        IsBridgeInstalled = installed;

        if (!installed)
        {
            BridgeStatusText = "Not Installed in Community";
            BridgeStatusColor = "#EF4444";
        }
        else if (!upToDate)
        {
            BridgeStatusText = $"Installed ({version}) — Update Available (v0.7.0)";
            BridgeStatusColor = "#F59E0B";
        }
        else
        {
            BridgeStatusText = $"Installed & Active (v{version})";
            BridgeStatusColor = "#10B981";
        }
    }

    [RelayCommand]
    public void DeployBridge()
    {
        var success = _bridgeManager.DeployBridge(null, out var message);
        RefreshBridgeStatus();
        _main.AppendLog($"[Bridge] {message}");
    }

    partial void OnGlassOpacityPercentChanged(double value) => GlassOpacity = Math.Clamp(value / 100.0, 0.4, 1.0);

    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplicationThemeManager.Apply(value ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    partial void OnAllowLanEfbAccessChanged(bool value)
    {
        _main.SaveSettings();
        _ = _main.RestartEfbAsync();
    }

    partial void OnPressureUnitChanged(string value) => _main.SaveSettings();
    partial void OnTemperatureUnitChanged(string value) => _main.SaveSettings();
    partial void OnWindSpeedUnitChanged(string value) => _main.SaveSettings();
    partial void OnStreamerModeChanged(bool value) => _main.SaveSettings();

    partial void OnDepartureHoldEnabledChanged(bool value)
    {
        _main.ApplySkyAnchorSettings();
        _main.SaveSettings();
    }

    partial void OnArrivalHoldEnabledChanged(bool value)
    {
        _main.ApplySkyAnchorSettings();
        _main.SaveSettings();
    }

    partial void OnAutoFreezeOnApproachChanged(bool value)
    {
        _main.ApplySkyAnchorSettings();
        _main.SaveSettings();
    }

    partial void OnSimBriefPilotIdChanged(string value) => _main.SaveSettings();
    partial void OnAutoLoadSimBriefAtLaunchChanged(bool value) => _main.SaveSettings();

    partial void OnAutoMatchOnlineAtcWeatherChanged(bool value)
    {
        _main.ApplyOnlineAtcSettings();
        _main.SaveSettings();
    }

    partial void OnPreferOnlineAtisQnhChanged(bool value)
    {
        _main.ApplyOnlineAtcSettings();
        _main.SaveSettings();
    }

    partial void OnPreferIvaoMetarChanged(bool value)
    {
        _main.ApplyOnlineAtcSettings();
        _main.SaveSettings();
    }

    partial void OnSyncWithSayIntentionsChanged(bool value)
    {
        _main.ApplyOnlineAtcSettings();
        _main.SaveSettings();
    }

    partial void OnNavigraphUsernameChanged(string value) => _main.SaveSettings();

    partial void OnChartProviderChanged(string value)
    {
        _main.SaveSettings();
        _main.FlightPlan.ChartProvider = value;
    }

    partial void OnAirmateUsernameChanged(string value) => _main.SaveSettings();
    partial void OnAirmatePasswordChanged(string value) => _main.SaveSettings();

    [RelayCommand]
    public void RegisterAirmate()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AirmateChartService.AirmatePortalUrl,
                UseShellExecute = true
            });
            AirmateStatusMessage = "Opened airmate.aero in browser to create your free account.";
        }
        catch (Exception ex)
        {
            AirmateStatusMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    [RelayCommand]
    public void VerifyAirmate()
    {
        var (isValid, message) = AirmateChartService.ValidateAccountDetails(AirmateUsername, AirmatePassword);
        AirmateStatusMessage = message;
        if (isValid)
        {
            _main.SaveSettings();
            _main.AppendLog($"[Airmate] Saved free account credentials for '{AirmateUsername.Trim()}'. Free AIP charts active.");
        }
    }

    [RelayCommand]
    public void OpenMsfsPlanner()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AirmateChartService.MsfsPlannerUrl,
                UseShellExecute = true
            });
            _main.AppendLog("[Planner] Opened MSFS 2024 Web Flight Planner in browser.");
        }
        catch (Exception ex)
        {
            _main.AppendLog($"[Planner] Failed to open MSFS Planner: {ex.Message}");
        }
    }

    // Extensible Community Plugins (FR-E2)
    public ObservableCollection<PluginItemViewModel> InstalledPlugins { get; } = new();

    public void RefreshPlugins()
    {
        InstalledPlugins.Clear();
        var plugins = _main.WeatherEngine.PluginManager.GetInstalledPlugins();
        foreach (var p in plugins)
        {
            InstalledPlugins.Add(new PluginItemViewModel(p, this));
        }
    }

    public void SetPluginEnabled(string pluginId, bool enabled)
    {
        _main.WeatherEngine.PluginManager.SetPluginEnabled(pluginId, enabled);
        _main.AppendLog($"[Plugins] Plugin '{pluginId}' is now {(enabled ? "ENABLED" : "DISABLED")}");
    }

    [RelayCommand]
    public void OpenPluginsFolder()
    {
        try
        {
            var dir = _main.WeatherEngine.PluginManager.AppDataPluginsDirectory;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
            _main.AppendLog($"[Plugins] Opened plugins directory: {dir}");
        }
        catch (Exception ex)
        {
            _main.AppendLog($"[Plugins] Failed to open plugins directory: {ex.Message}");
        }
    }

    [RelayCommand]
    public void RescanPlugins()
    {
        try
        {
            _main.WeatherEngine.PluginManager.DiscoverAll();
            RefreshPlugins();
            _main.AppendLog($"[Plugins] Rescan completed. {InstalledPlugins.Count} plugin(s) found.");
        }
        catch (Exception ex)
        {
            _main.AppendLog($"[Plugins] Rescan failed: {ex.Message}");
        }
    }
}

public partial class PluginItemViewModel : ViewModelBase
{
    private readonly SettingsViewModel _settings;
    public PluginInfo Info { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public string PluginId => Info.PluginId;
    public string PluginName => Info.PluginName;
    public string Version => Info.Version;
    public string Author => Info.Author;
    public string Description => Info.Description;
    public string Status => Info.Status;

    public PluginItemViewModel(PluginInfo info, SettingsViewModel settings)
    {
        Info = info;
        _settings = settings;
        _isEnabled = info.IsEnabled;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _settings.SetPluginEnabled(PluginId, value);
    }
}
