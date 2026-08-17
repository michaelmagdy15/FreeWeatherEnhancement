using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.SimBridge;

public class WeatherInjector : IDisposable
{
    private readonly SimConnectManager _simConnect;
    private readonly WeatherEngine _weatherEngine;
    private readonly WprGenerator _wprGenerator;
    private Timer? _injectionTimer;
    private bool _isInjecting;
    private string? _lastWpr;

    public event EventHandler<string>? InjectionStatus;
    public event EventHandler<WeatherState>? WeatherInjected;

    public bool IsInjecting => _isInjecting;
    public TimeSpan InjectionInterval { get; set; } = TimeSpan.FromSeconds(5);

    public WeatherInjector(SimConnectManager simConnect, WeatherEngine weatherEngine)
    {
        _simConnect = simConnect;
        _weatherEngine = weatherEngine;
        _wprGenerator = new WprGenerator();

        _weatherEngine.WprGenerated += OnWprGenerated;
        _simConnect.PositionUpdated += OnPositionUpdated;
    }

    public async Task StartInjectionAsync()
    {
        if (_isInjecting) return;
        if (!_simConnect.IsConnected)
        {
            InjectionStatus?.Invoke(this, "Not connected to sim");
            return;
        }

        _isInjecting = true;
        InjectionStatus?.Invoke(this, "Injection started");

        _injectionTimer = new Timer(async _ => await InjectWeather(), null, InjectionInterval, InjectionInterval);

        await Task.CompletedTask;
    }

    public void StopInjection()
    {
        _isInjecting = false;
        _injectionTimer?.Dispose();
        _injectionTimer = null;
        InjectionStatus?.Invoke(this, "Injection stopped");
    }

    private void OnWprGenerated(object? sender, string wpr)
    {
        _lastWpr = wpr;
    }

    private async void OnPositionUpdated(object? sender, (double Latitude, double Longitude, double AltitudeFeet) pos)
    {
        if (_isInjecting)
        {
            await _weatherEngine.UpdatePositionAsync(pos.Latitude, pos.Longitude, pos.AltitudeFeet);
        }
    }

    private async Task InjectWeather()
    {
        if (!_isInjecting || !_simConnect.IsConnected)
            return;

        var state = _weatherEngine.CurrentState;
        var wpr = _lastWpr;

        if (state != null)
        {
            var boost = Math.Min(30, state.TurbulenceIndex * 20 +
                (state.TurbulenceLayers.Any(l => l.Type == TurbulenceType.Wake) ? 8 : 0));
            wpr = _wprGenerator.GenerateWprXml(state, boost);
        }

        if (string.IsNullOrEmpty(wpr))
            return;

        try
        {
            _simConnect.SetWeather(wpr);

            if (state != null)
            {
                WeatherInjected?.Invoke(this, state);
            }

            InjectionStatus?.Invoke(this, $"Injected at {DateTime.Now:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            InjectionStatus?.Invoke(this, $"Injection error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        StopInjection();
    }
}
