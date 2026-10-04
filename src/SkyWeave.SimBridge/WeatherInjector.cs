using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.SimBridge;

/// <summary>
/// Sends weather to the optional in-sim HTML/JS bridge. The WPR file remains
/// available as a manual-loading fallback. Readback verification confirms
/// whether the sim accepted the weather; a queued CommBus message is not
/// treated as injection success.
/// </summary>
public class WeatherInjector : IDisposable
{
    private const double WindSpeedToleranceKt = 3.0;
    private const double WindDirectionToleranceDeg = 30.0;
    private const double TemperatureToleranceC = 3.5;
    private const double PressureToleranceHpa = 2.5;
    private static readonly TimeSpan ReadbackDelay = TimeSpan.FromSeconds(3);

    // Position forwarding throttle: full engine refresh only on meaningful
    // movement or every 15 minutes.
    private const double PositionDeltaDeg = 0.05;
    private static readonly TimeSpan PositionForwardInterval = TimeSpan.FromMinutes(15);

    private readonly SimConnectManager _simConnect;
    private readonly WeatherEngine _weatherEngine;
    private readonly WprFileWriter _wprFileWriter;
    private Timer? _injectionTimer;
    private Timer? _readbackTimer;
    private volatile bool _isInjecting;

    private AmbientWeatherData _lastSent;
    private double _lastSentCloudOktas;
    private string? _lastQueuedPresetXml;
    private bool _awaitingReadback;
    private bool _reconnectEnabled = true;
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
    public bool IsFrozen { get; set; }

    public void SetFrozen(bool frozen) => IsFrozen = frozen;

    public WeatherInjector(SimConnectManager simConnect, WeatherEngine weatherEngine)
    {
        _simConnect = simConnect;
        _weatherEngine = weatherEngine;
        _wprFileWriter = new WprFileWriter(message => InjectionStatus?.Invoke(this, message));

        _weatherEngine.WprGenerated += OnWprGenerated;
        _simConnect.PositionUpdated += OnPositionUpdated;
        _simConnect.WeatherReadbackReceived += OnWeatherReadback;
        _simConnect.BridgeAckReceived += OnBridgeAckReceived;
        _simConnect.SimulationRateChanged += OnSimulationRateChanged;
        _simConnect.TrafficUpdated += OnTrafficUpdated;
        _simConnect.Disconnected += OnSimDisconnected;
        _simConnect.LogMessage += OnSimConnectLogMessage;
    }

    private void OnTrafficUpdated(object? sender, IReadOnlyList<AircraftTraffic> traffic)
    {
        _weatherEngine.TrafficSnapshot = traffic.ToList();
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
        InjectionStatus?.Invoke(this, "Injection started (in-sim JS bridge)");

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
        // WPR XML is now written to file by WprFileWriter — nothing to stash here.
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
        if (!_isInjecting || !_simConnect.IsConnected || IsFrozen || _weatherEngine.CurrentAnchorState.IsFrozen)
            return;

        var state = _weatherEngine.CurrentState;
        if (state == null)
        {
            InjectionStatus?.Invoke(this, "Waiting for weather data...");
            return;
        }

        var maxOktas = state.CloudLayers.Any()
            ? state.CloudLayers.Max(c => c.CoveragePercent) * 8.0
            : 0.0;

        var target = new AmbientWeatherData
        {
            WindDirectionDegrees = state.WindDirectionDegrees,
            WindSpeedKnots = state.WindSpeedKnots,
            TemperatureCelsius = state.TemperatureCelsius,
            SeaLevelPressureHpa = state.AltimeterHpa,
            VisibilityMeters = state.VisibilityMeters
        };

        // Step 1: Generate the WPR before writing or sending it. This lets us
        // avoid resetting MSFS weather every five seconds when nothing changed.
        var boost = Math.Min(30, state.TurbulenceIndex * 20 +
            (state.TurbulenceLayers.Any(l => l.Type == TurbulenceType.Wake) ? 8 : 0));
        var wprXml = _wprFileWriter.GeneratePresetXml(state, boost);
        if (string.Equals(wprXml, _lastQueuedPresetXml, StringComparison.Ordinal))
        {
            return;
        }

        var wprPath = _wprFileWriter.WritePresetXml(wprXml);

        if (wprPath == null)
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this, "Injection FAILED — could not write WPR preset file");
            return;
        }

        if (_wprFileWriter.LastPresetXml == null)
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this, "Injection FAILED — WPR content unavailable");
            return;
        }

        // Step 2: send the preset to the in-sim JS bridge. The bridge calls
        // UpdateTempWeatherPreset through JS_LISTENER_WEATHER and acknowledges
        // only the listener result; ambient readback is still required for success.
        var requestId = Guid.NewGuid().ToString("N");
        var bridgeMessage = WeatherBridgeProtocol.CreateApplyMessage(
            requestId,
            state,
            _wprFileWriter.LastPresetXml);
        var bridgeSent = _simConnect.SendWeatherBridgeMessage(bridgeMessage);
        if (!bridgeSent && !_simConnect.SetWeatherTheme(_wprFileWriter.PresetName))
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this,
                "Injection FAILED — JS bridge unavailable and WPR fallback rejected");
            return;
        }

        _lastSent = target;
        _lastSentCloudOktas = maxOktas;
        _lastQueuedPresetXml = wprXml;
        _awaitingReadback = true;
        InjectionStatus?.Invoke(this,
            $"WPR preset written to {wprPath} — " +
            (bridgeSent ? $"bridge request {requestId} sent — " :
                $"WeatherSetModeTheme(\"{_wprFileWriter.PresetName}\") sent — ") +
            $"awaiting sim readback");

        _readbackTimer?.Change(ReadbackDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnWeatherReadback(object? sender, AmbientWeatherData readback)
    {
        if (!_awaitingReadback)
            return;

        _awaitingReadback = false;

        var windSpeedOk = Math.Abs(readback.WindSpeedKnots - _lastSent.WindSpeedKnots) <= WindSpeedToleranceKt;
        var windDirOk = AngleDelta(readback.WindDirectionDegrees, _lastSent.WindDirectionDegrees) <= WindDirectionToleranceDeg;
        var tempOk = Math.Abs(readback.TemperatureCelsius - _lastSent.TemperatureCelsius) <= TemperatureToleranceC;
        var pressOk = Math.Abs(readback.SeaLevelPressureHpa - _lastSent.SeaLevelPressureHpa) <= PressureToleranceHpa;
        var visOk = Math.Abs(readback.VisibilityMeters - _lastSent.VisibilityMeters) <= Math.Max(1000, _lastSent.VisibilityMeters * 0.20);
        var cloudAvailable = double.IsFinite(readback.CloudCoverageOktas);
        var cloudOk = cloudAvailable && Math.Abs(readback.CloudCoverageOktas - _lastSentCloudOktas) <= 2.0;

        if (windSpeedOk && windDirOk && tempOk && pressOk && visOk && cloudOk)
        {
            LastInjectionVerified = true;
            InjectionStatus?.Invoke(this,
                $"Injected verified — sim reports {readback.WindSpeedKnots:F0} kt @ {readback.WindDirectionDegrees:F0}\u00b0, " +
                $"{readback.TemperatureCelsius:F1}\u00b0C, {readback.SeaLevelPressureHpa:F0} hPa, " +
                $"Vis: {readback.VisibilityMeters:F0}m, Clouds: {readback.CloudCoverageOktas:F1} oktas");

            var state = _weatherEngine.CurrentState;
            if (state != null)
                WeatherInjected?.Invoke(this, state);
        }
        else if (windSpeedOk && windDirOk && tempOk && pressOk && visOk && !cloudAvailable)
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this,
                "Readback PARTIAL — wind, temperature, pressure and visibility match; " +
                "cloud coverage is unavailable through SimConnect. Full injection remains unverified.");
        }
        else
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this,
                $"Readback MISMATCH — target {_lastSent.WindSpeedKnots:F1} kt @ {_lastSent.WindDirectionDegrees:F0} deg / {_lastSent.TemperatureCelsius:F1} C / {_lastSent.SeaLevelPressureHpa:F1} hPa / {_lastSent.VisibilityMeters:F0}m; " +
                $"sim {readback.WindSpeedKnots:F1} kt @ {readback.WindDirectionDegrees:F0} deg / {readback.TemperatureCelsius:F1} C / {readback.SeaLevelPressureHpa:F1} hPa / {readback.VisibilityMeters:F0}m. " +
                "Cloud coverage readback unavailable; full injection remains unverified.");
        }
    }

    private void OnSimConnectLogMessage(object? sender, string message)
    {
        InjectionStatus?.Invoke(this, message);
    }

    private void OnSimulationRateChanged(object? sender, double rate)
    {
        _weatherEngine.SimulationRate = rate;
        InjectionStatus?.Invoke(this, $"Simulation rate changed to {rate:F1}x — smoothing scaled");
    }

    /// <summary>
    /// The in-sim bridge answered. This confirms the JS saw the command — it
    /// does NOT confirm the sim applied the weather; readback does that.
    /// </summary>
    private void OnBridgeAckReceived(object? sender, BridgeAckData ack)
    {
        if (!WeatherBridgeProtocol.TryParseAcknowledgement(ack.Data ?? string.Empty, out var acknowledgement))
        {
            InjectionStatus?.Invoke(this,
                $"Bridge ack unreadable — event {ack.EventID}: \"{ack.Data ?? "(empty)"}\"");
            return;
        }

        if (acknowledgement.Accepted)
        {
            InjectionStatus?.Invoke(this,
                $"In-sim bridge accepted request {acknowledgement.RequestId} — {ack.Data}");
        }
        else
        {
            InjectionStatus?.Invoke(this,
                $"In-sim bridge REJECTED request {acknowledgement.RequestId}: " +
                $"{acknowledgement.Error ?? "unknown error"}");
        }
    }

    private void OnSimDisconnected(object? sender, EventArgs e)
    {
        if (_isInjecting)
            StopInjection();

        if (_reconnectEnabled)
            _ = ReconnectLoopAsync();
    }

    private async Task ReconnectLoopAsync()
    {
        InjectionStatus?.Invoke(this, "Sim disconnected. Waiting 5s before reconnect loop...");
        await Task.Delay(5_000);
        
        while (!_simConnect.IsConnected)
        {
            try
            {
                _simConnect.Connect();
            }
            catch { /* Ignore connect errors */ }
            await Task.Delay(10_000);
        }
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
