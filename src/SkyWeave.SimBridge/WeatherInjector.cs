using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.SimBridge;

/// <summary>
/// Injects weather into MSFS via SimConnect and VERIFIES every injection by
/// reading the ambient sim vars back. "Injected" is only ever reported after
/// the sim's readback matches what we sent (audit C3: no fake-success logs).
/// </summary>
public class WeatherInjector : IDisposable
{
    private const double WindSpeedToleranceKt = 2.0;
    private const double WindDirectionToleranceDeg = 15.0;
    private const double TemperatureToleranceC = 1.5;
    private const double PressureToleranceHpa = 1.5;
    private static readonly TimeSpan ReadbackDelay = TimeSpan.FromSeconds(2);

    // Position forwarding throttle: full engine refresh only on meaningful
    // movement or every 30 s (part of audit C7 fix — was a 2 s firehose).
    private const double PositionDeltaDeg = 0.05;
    private static readonly TimeSpan PositionForwardInterval = TimeSpan.FromSeconds(30);

    private readonly SimConnectManager _simConnect;
    private readonly WeatherEngine _weatherEngine;
    private readonly WprGenerator _wprGenerator;
    private Timer? _injectionTimer;
    private Timer? _readbackTimer;
    private volatile bool _isInjecting;
    private string? _lastWpr;

    private AmbientWeatherData _lastSent;
    private bool _awaitingReadback;
    private DateTime _lastReadbackVerifyUtc;
    private DateTime _lastForwardUtc;
    private double _lastForwardedLat = double.NaN;
    private double _lastForwardedLon = double.NaN;

    public event EventHandler<string>? InjectionStatus;

    /// <summary>Raised only when the sim's readback VERIFIES an injection.</summary>
    public event EventHandler<WeatherState>? WeatherInjected;

    public bool IsInjecting => _isInjecting;
    /// <summary>True when the last injection was verified by sim readback.</summary>
    public bool LastInjectionVerified { get; private set; }
    public TimeSpan InjectionInterval { get; set; } = TimeSpan.FromSeconds(5);

    public WeatherInjector(SimConnectManager simConnect, WeatherEngine weatherEngine)
    {
        _simConnect = simConnect;
        _weatherEngine = weatherEngine;
        _wprGenerator = new WprGenerator();

        _weatherEngine.WprGenerated += OnWprGenerated;
        _simConnect.PositionUpdated += OnPositionUpdated;
        _simConnect.WeatherReadbackReceived += OnWeatherReadback;
        _simConnect.Disconnected += OnSimDisconnected;
    }

    public Task StartInjectionAsync()
    {
        if (_isInjecting)
            return Task.CompletedTask;

        if (!_simConnect.IsConnected)
        {
            InjectionStatus?.Invoke(this, "Cannot inject — not connected to the sim");
            return Task.CompletedTask;
        }

        _isInjecting = true;
        LastInjectionVerified = false;
        InjectionStatus?.Invoke(this, "Injection started");

        _readbackTimer = new Timer(
            _ => { try { _simConnect.RequestWeatherReadback(); } catch { /* readback is best-effort */ } },
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        _injectionTimer = new Timer(
            _ => { try { InjectWeather(); } catch (Exception ex) { InjectionStatus?.Invoke(this, $"Injection error: {ex.Message}"); } },
            null,
            TimeSpan.Zero,
            InjectionInterval);

        return Task.CompletedTask;
    }

    public void StopInjection()
    {
        _isInjecting = false;
        _injectionTimer?.Dispose();
        _injectionTimer = null;
        _readbackTimer?.Dispose();
        _readbackTimer = null;
        InjectionStatus?.Invoke(this, "Injection stopped");
    }

    private void OnWprGenerated(object? sender, string wpr)
    {
        // Stashed for diagnostics and the future WPR-preset injection mode.
        _lastWpr = wpr;
    }

    private void OnPositionUpdated(object? sender, AircraftPositionData pos)
    {
        if (!_isInjecting)
            return;

        var now = DateTime.UtcNow;
        var movedFar = double.IsNaN(_lastForwardedLat) ||
                       Math.Abs(pos.Latitude - _lastForwardedLat) > PositionDeltaDeg ||
                       Math.Abs(pos.Longitude - _lastForwardedLon) > PositionDeltaDeg;

        if (!movedFar && now - _lastForwardUtc < PositionForwardInterval)
            return;

        _lastForwardedLat = pos.Latitude;
        _lastForwardedLon = pos.Longitude;
        _lastForwardUtc = now;

        _ = ForwardPositionAsync(pos);
    }

    private async Task ForwardPositionAsync(AircraftPositionData pos)
    {
        try
        {
            await _weatherEngine.UpdatePositionAsync(pos.Latitude, pos.Longitude, pos.AltitudeFeet);
        }
        catch (Exception ex)
        {
            InjectionStatus?.Invoke(this, $"Position refresh failed: {ex.Message}");
        }
    }

    private void InjectWeather()
    {
        if (!_isInjecting || !_simConnect.IsConnected)
            return;

        var state = _weatherEngine.CurrentState;
        if (state == null)
        {
            InjectionStatus?.Invoke(this, "Waiting for weather data...");
            return;
        }

        var target = new AmbientWeatherData
        {
            WindDirectionDegrees = state.WindDirectionDegrees,
            WindSpeedKnots = state.WindSpeedKnots,
            TemperatureCelsius = state.TemperatureCelsius,
            SeaLevelPressureHpa = state.AltimeterHpa
        };

        var boost = Math.Min(30, state.TurbulenceIndex * 20 +
            (state.TurbulenceLayers.Any(l => l.Type == TurbulenceType.Wake) ? 8 : 0));
        _lastWpr = _wprGenerator.GenerateWprXml(state, boost);

        if (!_simConnect.SetAmbientWeather(target))
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this, "Injection FAILED — SimConnect write rejected");
            return;
        }

        _lastSent = target;
        _awaitingReadback = true;
        InjectionStatus?.Invoke(this,
            $"Weather sent (wind {target.WindSpeedKnots:F0} kt @ {target.WindDirectionDegrees:F0}\u00b0, " +
            $"{target.TemperatureCelsius:F1}\u00b0C, {target.SeaLevelPressureHpa:F0} hPa) — awaiting sim readback");

        _readbackTimer?.Change(ReadbackDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnWeatherReadback(object? sender, AmbientWeatherData readback)
    {
        if (!_awaitingReadback)
            return;

        _awaitingReadback = false;
        _lastReadbackVerifyUtc = DateTime.UtcNow;

        var windSpeedOk = Math.Abs(readback.WindSpeedKnots - _lastSent.WindSpeedKnots) <= WindSpeedToleranceKt;
        var windDirOk = AngleDelta(readback.WindDirectionDegrees, _lastSent.WindDirectionDegrees) <= WindDirectionToleranceDeg;
        var tempOk = Math.Abs(readback.TemperatureCelsius - _lastSent.TemperatureCelsius) <= TemperatureToleranceC;
        var pressOk = Math.Abs(readback.SeaLevelPressureHpa - _lastSent.SeaLevelPressureHpa) <= PressureToleranceHpa;

        if (windSpeedOk && windDirOk && tempOk && pressOk)
        {
            LastInjectionVerified = true;
            InjectionStatus?.Invoke(this,
                $"Injected \u2713 verified — sim reports {readback.WindSpeedKnots:F0} kt @ {readback.WindDirectionDegrees:F0}\u00b0, " +
                $"{readback.TemperatureCelsius:F1}\u00b0C, {readback.SeaLevelPressureHpa:F0} hPa");

            var state = _weatherEngine.CurrentState;
            if (state != null)
                WeatherInjected?.Invoke(this, state);
        }
        else
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this,
                $"Readback MISMATCH — sent {_lastSent.WindSpeedKnots:F0} kt / {_lastSent.TemperatureCelsius:F1}\u00b0C / {_lastSent.SeaLevelPressureHpa:F0} hPa, " +
                $"sim reports {readback.WindSpeedKnots:F0} kt / {readback.TemperatureCelsius:F1}\u00b0C / {readback.SeaLevelPressureHpa:F0} hPa. " +
                "Check that MSFS weather mode allows custom injection.");
        }
    }

    private void OnSimDisconnected(object? sender, EventArgs e)
    {
        if (_isInjecting)
            StopInjection();
    }

    private static double AngleDelta(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    public void Dispose()
    {
        StopInjection();
    }
}
