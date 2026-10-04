using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.Core.Plugins;
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

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
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
