using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

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

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
    }

    partial void OnGlassOpacityPercentChanged(double value) => GlassOpacity = Math.Clamp(value / 100.0, 0.4, 1.0);

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
