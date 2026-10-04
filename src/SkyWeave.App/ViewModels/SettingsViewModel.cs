using CommunityToolkit.Mvvm.ComponentModel;
using System;
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
}
