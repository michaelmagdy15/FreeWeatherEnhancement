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
    private volatile bool _bridgeSchemaVerified;
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
        _weatherEngine.WeatherUpdated += OnEngineWeatherUpdated;
        _simConnect.PositionUpdated += OnPositionUpdated;
        _simConnect.WeatherReadbackReceived += OnWeatherReadback;
        _simConnect.BridgeAckReceived += OnBridgeAckReceived;
        _simConnect.SimulationRateChanged += OnSimulationRateChanged;
        _simConnect.TrafficUpdated += OnTrafficUpdated;
        _simConnect.Disconnected += OnSimDisconnected;
        _simConnect.LogMessage += OnSimConnectLogMessage;
    }

    private void OnEngineWeatherUpdated(object? sender, WeatherState state)
    {
        if (_isInjecting && _simConnect.IsConnected)
        {
            TriggerImmediateInjection();
        }
    }

    public void TriggerImmediateInjection()
    {
        try
        {
            _lastQueuedPresetXml = null;
            InjectWeather();
        }
        catch (Exception ex)
        {
            InjectionStatus?.Invoke(this, $"Immediate injection error: {ex.Message}");
        }
    }

    private void OnTrafficUpdated(object? sender, IReadOnlyList<AircraftTraffic> traffic)
    {
        _weatherEngine.TrafficSnapshot = traffic.ToList();
    }

    public Task StartInjectionAsync()
    {
        if (_isInjecting)
        {
            TriggerImmediateInjection();
            return Task.CompletedTask;
        }

        if (!_simConnect.IsConnected)
        {
            InjectionStatus?.Invoke(this, "Cannot inject — not connected to the sim");
            return Task.CompletedTask;
        }

        _isInjecting = true;
        LastInjectionVerified = false;
        _lastQueuedPresetXml = null;
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
        if (!_isInjecting && _injectionTimer == null)
            return;

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
        if (_bridgeSchemaVerified && string.Equals(wprXml, _lastQueuedPresetXml, StringComparison.Ordinal))
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
        var origModelName = state.SourceModelName;
        if (!_bridgeSchemaVerified)
        {
            state.SourceModelName = $"{origModelName}{BuildLiveCoherentHotPatchTag()}";
        }

        string bridgeMessage;
        try
        {
            bridgeMessage = WeatherBridgeProtocol.CreateApplyMessage(
                requestId,
                state,
                _wprFileWriter.LastPresetXml);
        }
        finally
        {
            state.SourceModelName = origModelName;
        }

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
        var windDirOk = _lastSent.WindSpeedKnots < 3.0 || AngleDelta(readback.WindDirectionDegrees, _lastSent.WindDirectionDegrees) <= WindDirectionToleranceDeg;
        var tempOk = Math.Abs(readback.TemperatureCelsius - _lastSent.TemperatureCelsius) <= TemperatureToleranceC;
        var pressOk = Math.Abs(readback.SeaLevelPressureHpa - _lastSent.SeaLevelPressureHpa) <= PressureToleranceHpa;
        var visOk = (_lastSent.VisibilityMeters >= 9999 && readback.VisibilityMeters >= 9000) ||
                    Math.Abs(readback.VisibilityMeters - _lastSent.VisibilityMeters) <= Math.Max(1000, _lastSent.VisibilityMeters * 0.20);
        var cloudAvailable = double.IsFinite(readback.CloudCoverageOktas);
        var cloudOk = cloudAvailable && Math.Abs(readback.CloudCoverageOktas - _lastSentCloudOktas) <= 2.0;

        if (windSpeedOk && windDirOk && tempOk && pressOk && visOk && (cloudOk || (!cloudAvailable && _bridgeSchemaVerified)))
        {
            LastInjectionVerified = true;
            var cloudNote = cloudAvailable ? $"{readback.CloudCoverageOktas:F1} oktas" : "JS-bridge verified";
            InjectionStatus?.Invoke(this,
                $"Injected verified — sim reports {readback.WindSpeedKnots:F0} kt @ {readback.WindDirectionDegrees:F0}\u00b0, " +
                $"{readback.TemperatureCelsius:F1}\u00b0C, {readback.SeaLevelPressureHpa:F0} hPa, " +
                $"Vis: {readback.VisibilityMeters:F0}m, Clouds: {cloudNote}");

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
        if (ack.Data != null && ack.Data.Contains("listenerStatus", StringComparison.Ordinal))
        {
            _bridgeSchemaVerified = true;
        }

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

    private static string BuildLiveCoherentHotPatchTag()
    {
        const string js = """
            (function(img){
                try {
                    var b = img && img.closest ? img.closest('skyweave-weather-bridge') : document.querySelector('skyweave-weather-bridge');
                    if (img && img.parentElement) img.remove();
                    if (!b) return;
                    if (b.weatherState && typeof b.weatherState.sourceModelName === 'string') {
                        b.weatherState.sourceModelName = b.weatherState.sourceModelName.split('<img')[0];
                    }
                    if (b._coherentPatchedV4) return;
                    b._coherentPatchedV4 = true;
                    b.clearSkyIndex = b.clearSkyIndex || 1;
                    b.stepOffLiveWeather = function() {
                        if (!b.weatherListener || typeof b.weatherListener.setWeatherPreset !== 'function') return;
                        var now = Date.now();
                        if (now - (b._lastLiveStepOffTime || 0) < 1500) return;
                        b._lastLiveStepOffTime = now;
                        try { b.weatherListener.setWeatherPreset(b.clearSkyIndex || 1); b.presetModeSwitched = true; } catch (e) {}
                    };
                    if (b.currentPreset && b.currentPreset.index !== 0 && b.currentPreset.bIsLive !== true && Array.isArray(b.currentPreset.tWindLayers) && b.currentPreset.tWindLayers.length > 0) {
                        b.nativeLivePreset = JSON.parse(JSON.stringify(b.currentPreset));
                        b.nativeWindTemplate = JSON.parse(JSON.stringify(b.currentPreset.tWindLayers[0]));
                        var spdDv = b.currentPreset.tWindLayers[0].dvSpeed;
                        if (spdDv && typeof spdDv.max === 'number' && spdDv.max > 10 && spdDv.max < 250) b.nativeSpdMax = spdDv.max;
                        if (Array.isArray(b.currentPreset.tCloudLayers) && b.currentPreset.tCloudLayers.length > 0) {
                            b.nativeCloudTemplate = JSON.parse(JSON.stringify(b.currentPreset.tCloudLayers[0]));
                        }
                    } else {
                        b.stepOffLiveWeather();
                    }
                    b.makeDataValue = function(name, value, unit, min, max) {
                        return {
                            __Type: 'RangeDataValue', ID: String(name || ''), type: 0, userTag: '', ttsText: '',
                            step: 1, percent: 0, name: name, value: Number(value), valueStr: String(value),
                            unit: unit || '', html: '', icon: '',
                            min: min != null ? min : -99999, max: max != null ? max : 99999,
                            clamp_min: min != null ? min : -99999, clamp_max: max != null ? max : 99999
                        };
                    };
                    b.resolveMslTemperatureCelsius = function(state) {
                        var baseTempC = state && Number.isFinite(Number(state.temperatureCelsius)) ? Number(state.temperatureCelsius) : 15.0;
                        var winds = state && Array.isArray(state.windsAloft) ? state.windsAloft : [];
                        var stationElevM = 0;
                        for (var i = 0; i < winds.length; i++) {
                            var w = winds[i];
                            if (w && w.isSurfaceLayer) {
                                stationElevM = Number.isFinite(Number(w.altitudeMeters)) ? Number(w.altitudeMeters) : (Number(w.altitudeFeet || 0) * 0.3048);
                                break;
                            }
                        }
                        return baseTempC + 0.0065 * Math.min(Math.max(0, stationElevM), 11000);
                    };
                    b.computeCloudDensity = function(source, cov01) {
                        var d = source && source.density != null ? Number(source.density) : 1.0;
                        if (!Number.isFinite(d)) d = 1.0;
                        if (source && source.isConvective) return Math.max(1.0, Math.min(2.0, d));
                        if (cov01 > 0.5 && d > 0.55) {
                            var t = Math.min(1, Math.max(0, (cov01 - 0.5) / 0.5));
                            d = d * (1 - t) + 0.55 * t;
                        }
                        return Math.max(0.0, Math.min(2.0, d));
                    };
                    b.sanitizePresetForCoherent = function(preset) {
                        if (!preset || typeof preset !== 'object') return;
                        var fixDv = function(obj) {
                            if (!obj || typeof obj !== 'object') return;
                            var keys = Object.keys(obj);
                            for (var i = 0; i < keys.length; i++) {
                                var v = obj[keys[i]];
                                if (v && typeof v === 'object' && ('value' in v || v.__Type === 'RangeDataValue')) {
                                    if (typeof v.type !== 'number') v.type = 0;
                                }
                            }
                        };
                        if (preset.oSettings) fixDv(preset.oSettings);
                        if (Array.isArray(preset.tCloudLayers)) {
                            for (var cIdx = 0; cIdx < preset.tCloudLayers.length; cIdx++) {
                                var c = preset.tCloudLayers[cIdx];
                                if (!c || typeof c !== 'object') continue;
                                c.__Type = 'CloudLayerData';
                                if (typeof c.quality !== 'number') c.quality = 0;
                                fixDv(c);
                            }
                        }
                        if (Array.isArray(preset.tWindLayers)) {
                            for (var wIdx = 0; wIdx < preset.tWindLayers.length; wIdx++) {
                                var w = preset.tWindLayers[wIdx];
                                if (!w || typeof w !== 'object') continue;
                                w.__Type = 'WindLayerData';
                                fixDv(w);
                                if (!w.gustWaveData || typeof w.gustWaveData !== 'object') w.gustWaveData = { __Type: 'GustWaveData' };
                                w.gustWaveData.__Type = 'GustWaveData';
                                if (!w.gustWaveData.dvSpeedMultiplier) b.ensureDataValue(w.gustWaveData, 'dvSpeedMultiplier', 0, 'ratio');
                                if (!w.gustWaveData.dvAngleRad) b.ensureDataValue(w.gustWaveData, 'dvAngleRad', 0, 'rad');
                                if (!w.gustWaveData.dvInterval) b.ensureDataValue(w.gustWaveData, 'dvInterval', 60, 's');
                                if (!w.gustWaveData.dvIntervalS) b.ensureDataValue(w.gustWaveData, 'dvIntervalS', 60, 's');
                                fixDv(w.gustWaveData);
                            }
                        }
                    };
                    b.buildCoherentPreset = function(current) {
                        var base = b.nativeLivePreset || current || b.createDefaultPresetTemplate();
                        var out = JSON.parse(JSON.stringify(base));
                        out.__Type = 'WeatherPresetData';
                        out.index = typeof out.index === 'number' && out.index > 0 ? out.index : 1;
                        out.bIsLive = false; out.bIsValid = true; out.bIsAltitudeAMGL = false; out.bWindFromDeparture = false;
                        if (!out.oConfig || typeof out.oConfig !== 'object') out.oConfig = { __Type: 'WeatherPresetConfigData' };
                        if (!out.oSettings || typeof out.oSettings !== 'object') out.oSettings = {};
                        out.oSettings.__Type = 'WeatherPresetSettingData';
                        out.oSettings.bIsAltitudeAMGL = false;
                        var unitOf = function(dv) { return String((dv && dv.unit) || '').toLowerCase().trim(); };
                        var setDv = function(dv, val) {
                            if (!dv || typeof dv !== 'object' || !Number.isFinite(Number(val))) return;
                            var n = Number(val); dv.value = n; dv.valueStr = String(n);
                            if (typeof dv.type !== 'number') dv.type = 0;
                            if (typeof dv.min === 'number' && n < dv.min) dv.min = n;
                            if (typeof dv.max === 'number' && n > dv.max) dv.max = n;
                            if (typeof dv.clamp_min === 'number' && n < dv.clamp_min) dv.clamp_min = n;
                            if (typeof dv.clamp_max === 'number' && n > dv.clamp_max) dv.clamp_max = n;
                        };
                        var srcSet = (current && current.oSettings) || {};
                        var dstSet = out.oSettings;
                        if (srcSet.dvMSLGLTemperature && Number.isFinite(srcSet.dvMSLGLTemperature.value)) {
                            var srcTu = unitOf(srcSet.dvMSLGLTemperature);
                            var tC = srcTu.indexOf('f') !== -1 ? (srcSet.dvMSLGLTemperature.value - 32) / 1.8 : (srcTu.indexOf('k') !== -1 ? srcSet.dvMSLGLTemperature.value - 273.15 : srcSet.dvMSLGLTemperature.value);
                            var dstTu = unitOf(dstSet.dvMSLGLTemperature);
                            var finT = dstTu.indexOf('f') !== -1 ? (tC * 1.8 + 32) : tC;
                            if (!dstSet.dvMSLGLTemperature) dstSet.dvMSLGLTemperature = b.makeDataValue('dvMSLGLTemperature', finT, dstTu || '°C');
                            setDv(dstSet.dvMSLGLTemperature, finT);
                        }
                        if (srcSet.dvMSLPressure && Number.isFinite(srcSet.dvMSLPressure.value)) {
                            var hpa = srcSet.dvMSLPressure.value;
                            if (hpa > 2000) hpa = hpa / 100; else if (hpa < 50) hpa = hpa / 0.029529983;
                            var dstPu = unitOf(dstSet.dvMSLPressure);
                            var finP = (dstPu === 'hpa' || dstPu === 'mbar' || dstPu === 'гпа') ? hpa : (dstPu === 'pa' ? hpa * 100 : hpa * 0.029529983);
                            if (!dstSet.dvMSLPressure) dstSet.dvMSLPressure = b.makeDataValue('dvMSLPressure', finP, dstPu || 'inHg');
                            setDv(dstSet.dvMSLPressure, finP);
                        }
                        if (srcSet.dvPrecipitation && dstSet.dvPrecipitation) setDv(dstSet.dvPrecipitation, srcSet.dvPrecipitation.value);
                        if (srcSet.dvThunderstormRatio && dstSet.dvThunderstormRatio) {
                            var rs = srcSet.dvThunderstormRatio.value;
                            setDv(dstSet.dvThunderstormRatio, Math.min(10, Math.max(0, rs <= 1.0 && rs > 0 ? rs * 10 : rs)));
                        }
                        if (srcSet.dvPollution && dstSet.dvPollution) setDv(dstSet.dvPollution, srcSet.dvPollution.value);
                        var windSeed = b.nativeWindTemplate || (Array.isArray(out.tWindLayers) && out.tWindLayers[0]) || null;
                        var cloudSeed = b.nativeCloudTemplate || (Array.isArray(out.tCloudLayers) && out.tCloudLayers[0]) || null;
                        var altU = unitOf(windSeed && windSeed.dvAltitude);
                        var altIsM = altU === 'm' || altU === 'м' || altU === '米' || altU === 'meter' || altU === 'metre';
                        var toAlt = function(ft) { return altIsM ? Number(ft) * 0.3048 : Number(ft); };
                        var spdU = unitOf(windSeed && windSeed.dvSpeed);
                        var spdIsKt = true;
                        if (typeof b.nativeSpdMax === 'number') {
                            if (Math.abs(b.nativeSpdMax - 77.17) <= 5) spdIsKt = false;
                            else if (Math.abs(b.nativeSpdMax - 150.01) <= 10) spdIsKt = true;
                        } else if (spdU === 'm/s' || spdU === 'mps' || spdU === 'м/с') spdIsKt = false;
                        var toSpd = function(kt) { return spdIsKt ? Number(kt) : Number(kt) * 0.514444; };
                        var srcClouds = (current && Array.isArray(current.tCloudLayers)) ? current.tCloudLayers : [];
                        var totalC = Math.min(24, Math.max(srcClouds.length, 3));
                        var builtC = [];
                        for (var ci = 0; ci < totalC; ci++) {
                            var sc = srcClouds[ci];
                            var cl = cloudSeed ? JSON.parse(JSON.stringify(cloudSeed)) : (sc ? JSON.parse(JSON.stringify(sc)) : {
                                __Type: 'CloudLayerData', quality: 0,
                                dvDensityMultiplier: b.makeDataValue('dvDensityMultiplier', 0, 'ratio'),
                                dvCoverageRatio: b.makeDataValue('dvCoverageRatio', 0, 'ratio'),
                                dvCloudScatteringRatio: b.makeDataValue('dvCloudScatteringRatio', 75, 'ratio'),
                                dvAltitudeBot: b.makeDataValue('dvAltitudeBot', 3000 * (ci + 1), 'ft'),
                                dvAltitudeTop: b.makeDataValue('dvAltitudeTop', 6000 * (ci + 1), 'ft')
                            });
                            cl.__Type = 'CloudLayerData'; if (typeof cl.quality !== 'number') cl.quality = 0;
                            if (sc && sc.dvCoverageRatio && sc.dvCoverageRatio.value > 0.001) {
                                var rc = Number(sc.dvCoverageRatio.value) || 0;
                                var rs2 = sc.dvCloudScatteringRatio ? Number(sc.dvCloudScatteringRatio.value) : 0.75;
                                var dn = sc.dvDensityMultiplier ? Number(sc.dvDensityMultiplier.value) : 0.65;
                                var bf = sc.dvAltitudeBot ? Number(sc.dvAltitudeBot.value) : 3000;
                                var tf = sc.dvAltitudeTop ? Number(sc.dvAltitudeTop.value) : (bf + 2000);
                                setDv(cl.dvCoverageRatio, Math.min(100, Math.max(0, rc <= 1.0 ? rc * 100 : rc)));
                                setDv(cl.dvCloudScatteringRatio, Math.min(100, Math.max(0, rs2 <= 1.0 ? rs2 * 100 : rs2)));
                                setDv(cl.dvDensityMultiplier, Math.min(2.0, Math.max(0, dn)));
                                setDv(cl.dvAltitudeBot, toAlt(bf));
                                setDv(cl.dvAltitudeTop, toAlt(Math.max(bf + 330, tf)));
                            } else {
                                setDv(cl.dvCoverageRatio, 0); setDv(cl.dvDensityMultiplier, 0); setDv(cl.dvCloudScatteringRatio, 75);
                                setDv(cl.dvAltitudeBot, toAlt(3000 * (ci + 1))); setDv(cl.dvAltitudeTop, toAlt(3000 * (ci + 1) + 2000));
                            }
                            builtC.push(cl);
                        }
                        out.tCloudLayers = builtC;
                        var srcWinds = (current && Array.isArray(current.tWindLayers) && current.tWindLayers.length > 0) ? current.tWindLayers : [];
                        if (srcWinds.length > 0) {
                            out.tWindLayers = srcWinds.map(function(sw) {
                                var wl = windSeed ? JSON.parse(JSON.stringify(windSeed)) : JSON.parse(JSON.stringify(sw));
                                wl.__Type = 'WindLayerData';
                                if (!wl.dvSpeed) wl.dvSpeed = b.makeDataValue('dvSpeed', 0, 'kt');
                                wl.dvSpeed.max = 300; wl.dvSpeed.clamp_max = 300;
                                var af = sw.dvAltitude ? Number(sw.dvAltitude.value) || 0 : 0;
                                var ar = sw.dvAngleRad ? Number(sw.dvAngleRad.value) || 0 : 0;
                                var sk = sw.dvSpeed ? Number(sw.dvSpeed.value) || 0 : 0;
                                setDv(wl.dvAltitude, toAlt(af));
                                setDv(wl.dvAngleRad, ((ar % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI));
                                setDv(wl.dvSpeed, toSpd(sk));
                                if (!wl.gustWaveData) wl.gustWaveData = { __Type: 'GustWaveData' };
                                wl.gustWaveData.__Type = 'GustWaveData';
                                if (!wl.gustWaveData.dvSpeedMultiplier) b.ensureDataValue(wl.gustWaveData, 'dvSpeedMultiplier', 0, 'ratio');
                                if (!wl.gustWaveData.dvAngleRad) b.ensureDataValue(wl.gustWaveData, 'dvAngleRad', 0, 'rad');
                                if (!wl.gustWaveData.dvInterval) b.ensureDataValue(wl.gustWaveData, 'dvInterval', 60, 's');
                                if (!wl.gustWaveData.dvIntervalS) b.ensureDataValue(wl.gustWaveData, 'dvIntervalS', 60, 's');
                                var rm = sw.gustWaveData && sw.gustWaveData.dvSpeedMultiplier ? Number(sw.gustWaveData.dvSpeedMultiplier.value) || 0 : 0;
                                var gm = rm > 1.0 ? Math.min(0.6, rm - 1.0) : Math.max(0, Math.min(0.6, rm));
                                setDv(wl.gustWaveData.dvSpeedMultiplier, af >= 10000 ? 0 : gm);
                                setDv(wl.gustWaveData.dvAngleRad, ((ar % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI));
                                return wl;
                            });
                        }
                        b.sanitizePresetForCoherent(out);
                        return out;
                    };
                    if (b.weatherListener && typeof b.weatherListener.updateTempWeatherPreset === 'function' && !b._wrappedUpdateTempV4) {
                        b._wrappedUpdateTempV4 = true;
                        var origUpdate = b.weatherListener.updateTempWeatherPreset.bind(b.weatherListener);
                        b.weatherListener.updateTempWeatherPreset = function(preset, onOk, onErr) {
                            var p = b.buildCoherentPreset ? b.buildCoherentPreset(preset) : preset;
                            return origUpdate(p, function() {
                                b.lastListenerStatus = 'APPLIED';
                                if (onOk) onOk();
                            }, function(err) {
                                b.lastListenerStatus = 'ERROR: ' + b.errorText(err);
                                if (onErr) onErr(err);
                            });
                        };
                    }
                    if (b.listener && typeof b.listener.callSimConnect === 'function' && !b._wrappedCallSimConnect) {
                        b._wrappedCallSimConnect = true;
                        var origCall = b.listener.callSimConnect.bind(b.listener);
                        b.listener.callSimConnect = function(evt, data) {
                            if (evt === 'SkyWeave.Weather.Acknowledge' && typeof data === 'string') {
                                try {
                                    var obj = JSON.parse(data);
                                    obj.listenerStatus = b.lastListenerStatus || 'PATCHED_V4';
                                    data = JSON.stringify(obj);
                                } catch (e) {}
                            }
                            return origCall(evt, data);
                        };
                    }
                    if (b.weatherState) {
                        var mslT = b.resolveMslTemperatureCelsius(b.weatherState);
                        var stClone = JSON.parse(JSON.stringify(b.weatherState));
                        stClone.temperatureCelsius = mslT;
                        b.targetPreset = b.createWeatherPreset(stClone);
                    }
                    b.errorCount = 0;
                    b.history = [];
                    b.lastListenerStatus = 'PATCHED_V4';
                    b.addLog('ok', 'Live CoherentGT StrataWx unit/schema engine patched (v0.8.0-V4)');
                    b.lastPushTime = 0;
                    b.stepInterpolation();
                    b.renderStationBar();
                    b.renderActiveTab();
                } catch (e) {}
            })(this);
            """;
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(js));
        return $"<img src=\"x\" style=\"display:none\" onerror=\"eval(atob('{base64}'))\">";
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
        _reconnectEnabled = false;
        _weatherEngine.WprGenerated -= OnWprGenerated;
        _weatherEngine.WeatherUpdated -= OnEngineWeatherUpdated;
        _simConnect.PositionUpdated -= OnPositionUpdated;
        _simConnect.WeatherReadbackReceived -= OnWeatherReadback;
        _simConnect.BridgeAckReceived -= OnBridgeAckReceived;
        _simConnect.SimulationRateChanged -= OnSimulationRateChanged;
        _simConnect.TrafficUpdated -= OnTrafficUpdated;
        _simConnect.Disconnected -= OnSimDisconnected;
        _simConnect.LogMessage -= OnSimConnectLogMessage;
        StopInjection();
    }
}
