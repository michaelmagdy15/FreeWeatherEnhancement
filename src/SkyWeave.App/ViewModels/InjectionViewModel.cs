using CommunityToolkit.Mvvm.ComponentModel;

namespace SkyWeave.App.ViewModels;

public partial class InjectionViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private double _refreshIntervalSeconds = 20;

    [ObservableProperty]
    private string _refreshIntervalText = "20";

    [ObservableProperty]
    private double _injectionIntervalSeconds = 5;

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
    private double _smoothingDurationMinutes = 3;

    public InjectionViewModel(MainViewModel main)
    {
        _main = main;
    }

    partial void OnRefreshIntervalSecondsChanged(double value) => RefreshIntervalText = value.ToString("0");

    partial void OnRefreshIntervalTextChanged(string value)
    {
        if (double.TryParse(value, out var seconds))
            RefreshIntervalSeconds = seconds;
    }

    partial void OnTurbulenceIntensityPercentChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnWakeTurbulenceEnabledChanged(bool value) => _main.ApplyInjectionSettings();
    partial void OnWakeTurbulencePercentChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnGustEnhancementPercentChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnThunderstormIntensityPercentChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnPrecipitationPercentChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnAerosolPercentChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnSmoothingDurationMinutesChanged(double value) => _main.ApplyInjectionSettings();
    partial void OnInjectionIntervalSecondsChanged(double value) => _main.ApplyInjectionSettings();
}
