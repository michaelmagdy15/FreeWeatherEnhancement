using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.Core.Services;
using SkyWeave.SimBridge;
using System.Linq;

namespace SkyWeave.App.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly SimConnectManager _simConnect;
    private readonly WeatherEngine _weatherEngine;
    private readonly MainViewModel _main;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isInjecting;

    [ObservableProperty]
    private bool _isPassiveMode;

    [ObservableProperty]
    private bool _isFrozen;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _aircraftPosition = "---";

    [ObservableProperty]
    private string _manualStation = string.Empty;

    [ObservableProperty]
    private string _selectedAirportName = "---";

    [ObservableProperty]
    private string _simWeatherInfo = "---";

    [ObservableProperty]
    private bool _hasSimWeatherReadback;

    [ObservableProperty]
    private string _lastUpdate = "---";

    [ObservableProperty]
    private string _skyAnchorPhaseBadge = "EN-ROUTE";

    public string ModeBadgeText => IsFrozen ? "FROZEN" : IsInjecting ? "INJECTING" : IsPassiveMode ? "PASSIVE" : "IDLE";
    public string ModeBadgeBrush => IsFrozen ? "#FFA502" : IsInjecting ? "#E94560" : IsPassiveMode ? "#00D2D3" : "#555E6B";

    public ConnectionViewModel(SimConnectManager simConnect, WeatherEngine weatherEngine, MainViewModel main)
    {
        _simConnect = simConnect;
        _weatherEngine = weatherEngine;
        _main = main;
    }

    partial void OnIsInjectingChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeBadgeText));
        OnPropertyChanged(nameof(ModeBadgeBrush));
    }

    partial void OnIsPassiveModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeBadgeText));
        OnPropertyChanged(nameof(ModeBadgeBrush));
    }

    partial void OnIsFrozenChanged(bool value)
    {
        _weatherEngine.IsFrozen = value;
        OnPropertyChanged(nameof(ModeBadgeText));
        OnPropertyChanged(nameof(ModeBadgeBrush));
    }

    [RelayCommand]
    public void ToggleFreeze()
    {
        IsFrozen = !IsFrozen;
        _weatherEngine.IsFrozen = IsFrozen;
        StatusText = IsFrozen ? "Weather frozen (hold active)" : (IsInjecting ? "Weather engine running" : "Ready");
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        await _main.ConnectAsync();
    }

    [RelayCommand]
    private void Disconnect()
    {
        _main.Disconnect();
    }

    [RelayCommand]
    private async Task StartWeatherAsync()
    {
        await _main.StartWeatherAsync();
    }

    [RelayCommand]
    private async Task StartPassiveAsync()
    {
        await _main.StartPassiveAsync();
    }

    [RelayCommand]
    private void StopWeather()
    {
        _main.StopWeather();
    }

    [RelayCommand]
    private async Task RefreshNowAsync()
    {
        await _main.RefreshNowAsync();
    }

    [RelayCommand]
    private async Task SelectAirportAsync(string? icaoId)
    {
        await _main.SelectAirportAsync(icaoId);
    }

    [RelayCommand]
    private async Task UseManualStationAsync()
    {
        await _main.UseManualStationAsync();
    }
}
