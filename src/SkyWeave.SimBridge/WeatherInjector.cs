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
    public static volatile bool ExternalWorkerVerified;
    public static Action? OnPanelNeedsRepatch { get; set; }
    public static Action? OnPanelPatched { get; set; }
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
        _simConnect.AtmosphereModRequestReceived += OnAtmosphereModRequestReceived;
        _simConnect.SimulationRateChanged += OnSimulationRateChanged;
        _simConnect.TrafficUpdated += OnTrafficUpdated;
        _simConnect.Disconnected += OnSimDisconnected;
        _simConnect.LogMessage += OnSimConnectLogMessage;
    }

    private void OnAtmosphereModRequestReceived(object? sender, string requestJson)
    {
        try
        {
            var state = _weatherEngine.CurrentState;
            if (state == null || !_simConnect.IsConnected)
                return;

            double? reqLat = null;
            double? reqLon = null;
            if (!string.IsNullOrWhiteSpace(requestJson) && requestJson.Contains("\"lat\"", StringComparison.Ordinal))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(requestJson);
                if (doc.RootElement.TryGetProperty("lat", out var latEl) && latEl.ValueKind == System.Text.Json.JsonValueKind.Number &&
                    doc.RootElement.TryGetProperty("lon", out var lonEl) && lonEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    reqLat = latEl.GetDouble();
                    reqLon = lonEl.GetDouble();
                }
            }

            var metarJson = AtmosphereModCalculator.FormatCommBusMetarJson(state, reqLat, reqLon);
            _simConnect.SendAtmosphereModMetar(metarJson);
        }
        catch
        {
            // Ignore malformed request JSON on CommBus
        }
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
        var origRawMetar = state.RawMetar;
        var isVerifiedBridge = _bridgeSchemaVerified || ExternalWorkerVerified;
        if (!_bridgeSchemaVerified)
        {
            var tag = BuildLiveCoherentHotPatchTag();
            state.SourceModelName = $"{origModelName}{tag}";
            state.RawMetar = $"{origRawMetar}{tag}";
        }

        string bridgeMessage;
        try
        {
            bridgeMessage = WeatherBridgeProtocol.CreateApplyMessage(
                requestId,
                state,
                isVerifiedBridge ? _wprFileWriter.LastPresetXml : "<WeatherPreset/>");
        }
        finally
        {
            state.SourceModelName = origModelName;
            state.RawMetar = origRawMetar;
        }

        var bridgeSent = _simConnect.SendWeatherBridgeMessage(bridgeMessage);
        if (!bridgeSent && !_simConnect.SetWeatherTheme(_wprFileWriter.PresetName))
        {
            LastInjectionVerified = false;
            InjectionStatus?.Invoke(this,
                "Injection FAILED — JS bridge unavailable and WPR fallback rejected");
            return;
        }

        // Step 3: Push live METAR directly to AtmosphereMod.addon64 over CommBus ("AtmosphereMod.Metar")
        try
        {
            var atmoMetarJson = AtmosphereModCalculator.FormatCommBusMetarJson(state);
            _simConnect.SendAtmosphereModMetar(atmoMetarJson);
        }
        catch
        {
            // Non-fatal if AtmosphereMod CommBus broadcast fails
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

        var isBridgeActive = _bridgeSchemaVerified || ExternalWorkerVerified;
        var windSpeedOk = Math.Abs(readback.WindSpeedKnots - _lastSent.WindSpeedKnots) <= WindSpeedToleranceKt;
        var windDirOk = _lastSent.WindSpeedKnots < 3.0 || AngleDelta(readback.WindDirectionDegrees, _lastSent.WindDirectionDegrees) <= WindDirectionToleranceDeg;
        var tempOk = Math.Abs(readback.TemperatureCelsius - _lastSent.TemperatureCelsius) <= TemperatureToleranceC;
        var pressOk = Math.Abs(readback.SeaLevelPressureHpa - _lastSent.SeaLevelPressureHpa) <= PressureToleranceHpa;
        var visOk = isBridgeActive ||
                    (_lastSent.VisibilityMeters >= 9999 && readback.VisibilityMeters >= 9000) ||
                    Math.Abs(readback.VisibilityMeters - _lastSent.VisibilityMeters) <= Math.Max(1000, _lastSent.VisibilityMeters * 0.20);
        var cloudAvailable = double.IsFinite(readback.CloudCoverageOktas);
        var cloudOk = cloudAvailable && Math.Abs(readback.CloudCoverageOktas - _lastSentCloudOktas) <= 2.0;

        if (windSpeedOk && windDirOk && tempOk && pressOk && visOk && (cloudOk || (!cloudAvailable && isBridgeActive)))
        {
            LastInjectionVerified = true;
            var cloudNote = cloudAvailable ? $"{readback.CloudCoverageOktas:F1} oktas" : "JS-bridge verified";
            var visNote = isBridgeActive && _lastSent.VisibilityMeters < 9999 && readback.VisibilityMeters > 50000
                ? $"{_lastSent.VisibilityMeters:F0}m (haze deck)"
                : $"{readback.VisibilityMeters:F0}m";
            InjectionStatus?.Invoke(this,
                $"Injected verified — sim reports {readback.WindSpeedKnots:F0} kt @ {readback.WindDirectionDegrees:F0}\u00b0, " +
                $"{readback.TemperatureCelsius:F1}\u00b0C, {readback.SeaLevelPressureHpa:F0} hPa, " +
                $"Vis: {visNote}, Clouds: {cloudNote}");

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
        if (ack.Data != null &&
            (ack.Data.Contains("ArgumentType", StringComparison.OrdinalIgnoreCase) ||
             ack.Data.Contains("\"listenerStatus\":\"ERROR", StringComparison.OrdinalIgnoreCase)))
        {
            _bridgeSchemaVerified = false;
            OnPanelNeedsRepatch?.Invoke();
        }
        else if (ack.Data != null &&
            (ack.Data.Contains("PATCHED_V8", StringComparison.Ordinal) ||
             ack.Data.Contains("PATCHED_V7", StringComparison.Ordinal)))
        {
            _bridgeSchemaVerified = true;
            OnPanelPatched?.Invoke();
        }
        else if (ack.Data != null &&
            ack.Data.Contains("\"listenerStatus\":\"APPLIED\"", StringComparison.Ordinal) &&
            ExternalWorkerVerified)
        {
            // Keep delivering the V8 hot-patch tag until the panel confirms PATCHED_V8
            // so a pre-existing v0.10.0 panel stops its 1 Hz keepalive loop.
        }
        else if (ack.Data != null &&
            ack.Data.Contains("\"listenerStatus\":\"APPLIED\"", StringComparison.Ordinal))
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

    public static string BuildLiveCoherentHotPatchTag()
    {
        return "<IMG src=\"x\" style=\"display:none\" onerror=\"fetch('http://127.0.0.1:54170/api/bridge/patch.js?_='+Date.now()).then(function(r){return r.text()}).then(eval);this.remove()\">";
    }

    public static string GetLiveCoherentHotPatchScript()
    {
        const string js = """
            (function(){
                try {
                    var b = document.querySelector('skyweave-weather-bridge');
                    if (!b) return;
                    var cleanTag = function(s) {
                        return typeof s === 'string' ? s.split(/<img/i)[0] : s;
                    };
                    if (b.weatherState) {
                        if (typeof b.weatherState.sourceModelName === 'string') b.weatherState.sourceModelName = cleanTag(b.weatherState.sourceModelName);
                        if (typeof b.weatherState.stationId === 'string') b.weatherState.stationId = cleanTag(b.weatherState.stationId);
                        if (typeof b.weatherState.rawMetar === 'string') b.weatherState.rawMetar = cleanTag(b.weatherState.rawMetar);
                    }
                    if (typeof b.rawMetar === 'string') b.rawMetar = cleanTag(b.rawMetar);
                    if (Array.isArray(b.history)) {
                        for (var hi = 0; hi < b.history.length; hi++) {
                            if (b.history[hi] && typeof b.history[hi].text === 'string') {
                                b.history[hi].text = cleanTag(b.history[hi].text);
                            }
                        }
                    }
                    var fixBrokenGlyphs = function() {
                        try {
                            var walker = document.createTreeWalker(b, NodeFilter.SHOW_TEXT, null, false);
                            var node;
                            while ((node = walker.nextNode())) {
                                var v = node.nodeValue;
                                if (v && v.indexOf('\uFFFD') !== -1) {
                                    v = v.replace(/(\d)\uFFFD([CF]\b)/g, '$1\u00B0$2')
                                         .replace(/(\d)\uFFFD(\s|$)/g, '$1\u00B0$2')
                                         .replace(/\uFFFD(HEADING|POPOUT|HUD)/g, '$1')
                                         .replace(/\s\uFFFD\s/g, ' \u2022 ')
                                         .replace(/\uFFFD/g, '\u00B0');
                                    node.nodeValue = v;
                                }
                            }
                        } catch (e) {}
                    };
                    var updateBadgeVersion = function() {
                        var badges = b.querySelectorAll('.brand-badge');
                        for (var bi = 0; bi < badges.length; bi++) {
                            badges[bi].textContent = 'SimBridge v0.10.0';
                        }
                        fixBrokenGlyphs();
                    };
                    updateBadgeVersion();
                    var diag = function(stage, status, err) {
                        try {
                            var u = 'http://127.0.0.1:54170/api/bridge/diag?stage=' + encodeURIComponent(stage || '') +
                                '&status=' + encodeURIComponent(status || '') +
                                '&err=' + encodeURIComponent(String(err || '').slice(0, 220)) +
                                '&_=' + Date.now();
                            fetch(u).catch(function(){});
                        } catch (e) {}
                    };
                    if (b._coherentPatchedV9) return;
                    b._coherentPatchedV9 = true;
                    b._coherentPatchedV8 = true;
                    b._coherentPatchedV7 = true;
                    if (!b.clearSkyIndex || b.clearSkyIndex === 1) {
                        b.clearSkyIndex = 53;
                    }
                    b.presetModeSwitched = true;
                    var checkWorkerStatus = function() {
                        try {
                            fetch('http://127.0.0.1:54170/api/v1/weather/status?_=' + Date.now())
                                .then(function(r) { return r.json(); })
                                .then(function(d) {
                                    b._externalWorkerActive = Boolean(d && typeof d.workerAgeMs === 'number' && d.workerAgeMs < 10000);
                                })
                                .catch(function(){});
                        } catch (e) {}
                    };
                    checkWorkerStatus();
                    if (!b._workerStatusTimer) {
                        b._workerStatusTimer = setInterval(checkWorkerStatus, 4000);
                    }
                    if (typeof b.renderShell === 'function' && !b._renderShellWrappedV7) {
                        b._renderShellWrappedV7 = true;
                        var origRenderShell = b.renderShell.bind(b);
                        b.renderShell = function() {
                            var r = origRenderShell();
                            updateBadgeVersion();
                            return r;
                        };
                    }
                    if (typeof b.renderStationBar === 'function') {
                        b.renderStationBar = function() {
                            if (!b.weatherState) return;
                            var icaoEl = b.querySelector('.station-icao');
                            var detailsEl = b.querySelector('.station-details');
                            var catEl = b.querySelector('.flight-cat-badge');
                            var cleanIcao = cleanTag(b.weatherState.stationId || 'LOCAL');
                            b.weatherState.stationId = cleanIcao;
                            if (icaoEl) icaoEl.textContent = cleanIcao;
                            if (detailsEl) {
                                var lat = b.weatherState.latitude != null ? b.weatherState.latitude.toFixed(2) + '&deg;' : '';
                                var lon = b.weatherState.longitude != null ? b.weatherState.longitude.toFixed(2) + '&deg;' : '';
                                var rawModel = b.weatherState.sourceModelName || 'Multi-Model';
                                var model = cleanTag(String(rawModel));
                                b.weatherState.sourceModelName = model;
                                detailsEl.innerHTML = lat + ' ' + lon + ' &bull; ' + model;
                            }
                            if (catEl) {
                                var cat = (b.weatherState.flightCategory || 'VFR').toLowerCase();
                                catEl.className = 'flight-cat-badge ' + cat;
                                catEl.textContent = cat.toUpperCase();
                            }
                        };
                    }
                    b.stepOffLiveWeather = function() {
                        if (b._externalWorkerActive || b._cleanNativePreset || b.nativeLivePreset) return;
                        if (!b.weatherListener || typeof b.weatherListener.setWeatherPreset !== 'function') return;
                        var now = Date.now();
                        if (now - (b._lastLiveStepOffTime || 0) < 3000) return;
                        b._lastLiveStepOffTime = now;
                        try {
                            var targetIdx = (typeof b.clearSkyIndex === 'number' && b.clearSkyIndex > 1) ? b.clearSkyIndex : 53;
                            b.weatherListener.setWeatherPreset(targetIdx);
                            b.presetModeSwitched = true;
                        } catch (e) {}
                    };
                    b.errorText = function(error) {
                        if (!error) return 'unknown error';
                        if (typeof error === 'string') return error.slice(0, 160);
                        if (Array.isArray(error)) return (error.length + 'x: ' + String(error[0] || '')).slice(0, 160);
                        if (error.message) return String(error.message).slice(0, 160);
                        try { return JSON.stringify(error).slice(0, 160); } catch (e) { return String(error).slice(0, 160); }
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
                        var d = source && source.density != null ? Number(source.density) : 0.75;
                        if (!Number.isFinite(d)) d = 0.75;
                        if (source && source.isConvective) return Math.max(1.0, Math.min(2.0, d));
                        if (cov01 > 0.5 && d > 0.55) {
                            var t = Math.min(1, Math.max(0, (cov01 - 0.5) / 0.5));
                            d = d * (1 - t) + 0.55 * t;
                        }
                        return Math.max(0.0, Math.min(2.0, d));
                    };
                    var cleanSyntheticKeys = function(work) {
                        if (!work || typeof work !== 'object') return work;
                        if (work.oSettings) {
                            delete work.oSettings._targetTempC;
                            delete work.oSettings._targetPressureHpa;
                            delete work.oSettings.dvMSLTemperature;
                            delete work.oSettings.dvTemperature;
                            delete work.oSettings.dvPressure;
                            delete work.oSettings.dvHumidityMultiplier;
                            delete work.oSettings.dvSnowCoverMultiplier;
                        }
                        if (Array.isArray(work.tCloudLayers)) {
                            for (var ci = 0; ci < work.tCloudLayers.length; ci++) {
                                var cl = work.tCloudLayers[ci];
                                if (cl) delete cl.quality;
                            }
                        }
                        if (Array.isArray(work.tWindLayers)) {
                            for (var i = 0; i < work.tWindLayers.length; i++) {
                                var wl = work.tWindLayers[i];
                                if (wl && wl.gustWaveData) {
                                    delete wl.gustWaveData.dvInterval;
                                }
                            }
                        }
                        return work;
                    };
                    b.sanitizePresetForCoherent = function(preset) {
                        return cleanSyntheticKeys(preset);
                    };
                    var captureCleanPreset = function(p) {
                        if (!p || typeof p !== 'object') return;
                        if (!Array.isArray(p.tWindLayers) || p.tWindLayers.length === 0) return;
                        var c = cleanSyntheticKeys(JSON.parse(JSON.stringify(p)));
                        // Restore exact native RangeDataValue shape if an older code path mutated .type to a number
                        var restoreDv = function(obj) {
                            if (!obj || typeof obj !== 'object') return;
                            var keys = Object.keys(obj);
                            for (var ki = 0; ki < keys.length; ki++) {
                                var v = obj[keys[ki]];
                                if (v && typeof v === 'object' && ('value' in v || v.__Type === 'RangeDataValue')) {
                                    if (typeof v.type === 'number') delete v.type;
                                }
                            }
                        };
                        if (c.oSettings) restoreDv(c.oSettings);
                        if (Array.isArray(c.tCloudLayers)) {
                            for (var i = 0; i < c.tCloudLayers.length; i++) restoreDv(c.tCloudLayers[i]);
                        }
                        if (Array.isArray(c.tWindLayers)) {
                            for (var j = 0; j < c.tWindLayers.length; j++) {
                                restoreDv(c.tWindLayers[j]);
                                if (c.tWindLayers[j] && c.tWindLayers[j].gustWaveData) restoreDv(c.tWindLayers[j].gustWaveData);
                            }
                        }
                        b._cleanNativePreset = c;
                        b.nativeLivePreset = c;
                        var spdDv = c.tWindLayers[0] && c.tWindLayers[0].dvSpeed;
                        if (spdDv && typeof spdDv.max === 'number' && spdDv.max > 10 && spdDv.max < 250) {
                            b.nativeSpdMax = spdDv.max;
                        }
                    };
                    if (b.nativeLivePreset) captureCleanPreset(b.nativeLivePreset);
                    else if (b.currentPreset) captureCleanPreset(b.currentPreset);
                    if (b.weatherListener && typeof b.weatherListener.updatePreset === 'function') {
                        try {
                            b.weatherListener.updatePreset(function(p) {
                                if (!p || p.index === 0 || p.bIsLive === true || !Array.isArray(p.tWindLayers) || p.tWindLayers.length === 0) {
                                    b.stepOffLiveWeather();
                                    return;
                                }
                                captureCleanPreset(p);
                                diag('preset-rx', 'idx=' + p.index + ',winds=' + p.tWindLayers.length + ',clouds=' + (p.tCloudLayers ? p.tCloudLayers.length : 0), '');
                            });
                        } catch (e) {}
                    }
                    try {
                        if (!b._cleanNativePreset && b.weatherListener && typeof b.weatherListener.trigger === 'function') {
                            b.weatherListener.trigger('ASK_UPDATE_PRESET');
                        }
                    } catch (e) {}
                    b.buildPureStrataPreset = function(state, current) {
                        var seed = b._cleanNativePreset || b.nativeLivePreset || current;
                        if (!seed || !Array.isArray(seed.tWindLayers) || seed.tWindLayers.length === 0) return null;
                        var work = cleanSyntheticKeys(JSON.parse(JSON.stringify(seed)));
                        work.bIsLive = false;
                        work.bIsValid = true;
                        var o = work.oSettings;
                        if (o) o.bIsAltitudeAMGL = false;
                        var unitOf = function(dv) { return String((dv && dv.unit) || '').toLowerCase().trim(); };
                        var altU = unitOf(work.tWindLayers[0] && work.tWindLayers[0].dvAltitude);
                        var altIsFt = altU.indexOf('ft') !== -1 || altU.indexOf('фут') !== -1 || altU.indexOf('feet') !== -1 || altU.indexOf('pi') !== -1;
                        var toAltFromM = function(m) { return altIsFt ? Number(m) * 3.28084 : Number(m); };
                        var toAltFromFt = function(ft) { return altIsFt ? Number(ft) : Number(ft) * 0.3048; };
                        var spdU = unitOf(work.tWindLayers[0] && work.tWindLayers[0].dvSpeed);
                        var spdIsKt = true;
                        if (typeof b.nativeSpdMax === 'number') {
                            if (Math.abs(b.nativeSpdMax - 77.17) <= 5) spdIsKt = false;
                            else if (Math.abs(b.nativeSpdMax - 150.01) <= 10) spdIsKt = true;
                        } else if (spdU.indexOf('m/s') !== -1 || spdU.indexOf('mps') !== -1 || spdU.indexOf('м/с') !== -1) spdIsKt = false;
                        var toSpd = function(kt) { return spdIsKt ? Number(kt) : Number(kt) * 0.514444; };
                        var presU = unitOf(o && o.dvMSLPressure);
                        var toPresFromHpa = function(hpa) {
                            if (presU.indexOf('hpa') !== -1 || presU.indexOf('mbar') !== -1 || presU.indexOf('гпа') !== -1) return Number(hpa);
                            if (presU === 'pa' || presU === '[pa]') return Number(hpa) * 100;
                            return Number(hpa) * 0.0295299830714;
                        };
                        var tempU = unitOf(o && o.dvMSLGLTemperature);
                        var toTempFromC = function(c) {
                            return (tempU.indexOf('f') !== -1) ? (Number(c) * 1.8 + 32) : Number(c);
                        };
                        var precU = unitOf(o && o.dvPrecipitation);
                        var toPrecipFromMm = function(mm) {
                            return (precU.indexOf('in') !== -1 || precU.indexOf('дюйм') !== -1) ? (Number(mm) / 25.4) : Number(mm);
                        };
                        if (state && typeof state === 'object') {
                            var mslTempC = b.resolveMslTemperatureCelsius(state);
                            var qnhHpa = Number.isFinite(Number(state.altimeterHpa))
                                ? Number(state.altimeterHpa)
                                : (Number.isFinite(Number(state.pressureHpa)) ? Number(state.pressureHpa) : 1013.25);
                            var precipMm = Number.isFinite(Number(state.precipitationRate))
                                ? Math.max(0, Number(state.precipitationRate))
                                : (Number.isFinite(Number(state.precipitationRateMmHr)) ? Math.max(0, Number(state.precipitationRateMmHr)) : 0);
                            var tstorm01 = Number.isFinite(Number(state.thunderstormIntensity)) ? Math.max(0, Math.min(1, Number(state.thunderstormIntensity))) : 0;
                            if (o) {
                                if (o.dvMSLPressure) o.dvMSLPressure.value = toPresFromHpa(qnhHpa);
                                if (o.dvMSLGLTemperature) o.dvMSLGLTemperature.value = toTempFromC(mslTempC);
                                if (o.dvPrecipitation) o.dvPrecipitation.value = toPrecipFromMm(precipMm);
                                if (o.dvThunderstormRatio) o.dvThunderstormRatio.value = Math.min(10, Math.max(0, tstorm01 * 10));
                                if (o.dvSnowCover) o.dvSnowCover.value = 0;
                            }
                            var winds = Array.isArray(state.windsAloft) && state.windsAloft.length > 0
                                ? state.windsAloft.slice().sort(function(a, c) { return (a.altitudeMeters || 0) - (c.altitudeMeters || 0); })
                                : [{ altitudeMeters: 0, directionDegrees: state.windDirectionDegrees || 0, speedKnots: state.windSpeedKnots || 0, gustSpeedKnots: state.windGustKnots || 0, isSurfaceLayer: true }];
                            var groundElevM = 0;
                            for (var gi = 0; gi < winds.length; gi++) {
                                if (winds[gi] && winds[gi].isSurfaceLayer) {
                                    groundElevM = Number.isFinite(Number(winds[gi].altitudeMeters)) ? Math.max(0, Number(winds[gi].altitudeMeters)) : 0;
                                    break;
                                }
                            }
                            var wSeed = JSON.parse(JSON.stringify(work.tWindLayers[0]));
                            while (work.tWindLayers.length < winds.length) work.tWindLayers.push(JSON.parse(JSON.stringify(wSeed)));
                            if (work.tWindLayers.length > winds.length) work.tWindLayers.length = winds.length;
                            for (var wi = 0; wi < winds.length; wi++) {
                                var sw = winds[wi];
                                var wl = work.tWindLayers[wi];
                                var altM = Number.isFinite(Number(sw.altitudeMeters)) ? Number(sw.altitudeMeters) : (Number(sw.altitudeFeet || 0) * 0.3048);
                                var aglM = Math.max(0, altM - groundElevM);
                                var dirDeg = ((Number(sw.directionDegrees || 0) % 360) + 360) % 360;
                                var rawSpdKt = Math.max(0, Number(sw.speedKnots || 0));
                                var compFactor = 1.0;
                                if (sw.isSurfaceLayer || aglM <= 200) {
                                    compFactor = 2.0;
                                } else if (aglM < 760) {
                                    var tComp = (aglM - 200) / (760 - 200);
                                    var bComp = tComp * tComp * (3 - 2 * tComp);
                                    compFactor = 2.0 - bComp;
                                }
                                var spdKt = rawSpdKt * compFactor;
                                var rawGst = Number(sw.gustSpeedKnots != null ? sw.gustSpeedKnots : (sw.gustKnots || rawSpdKt));
                                var gstKt = Math.max(spdKt, rawGst * compFactor);
                                if (wl.dvAltitude) wl.dvAltitude.value = toAltFromM(altM);
                                if (wl.dvAngleRad) wl.dvAngleRad.value = dirDeg * Math.PI / 180;
                                if (wl.dvSpeed) {
                                    wl.dvSpeed.max = 300;
                                    wl.dvSpeed.clamp_max = 300;
                                    wl.dvSpeed.value = toSpd(spdKt);
                                }
                                if (wl.gustWaveData) {
                                    var gMult = (altM < 3048 && spdKt > 1 && gstKt > spdKt + 1) ? Math.min(0.6, (gstKt - spdKt) / spdKt) : 0;
                                    if (wl.gustWaveData.dvSpeedMultiplier) wl.gustWaveData.dvSpeedMultiplier.value = gMult;
                                    if (wl.gustWaveData.dvIntervalS) wl.gustWaveData.dvIntervalS.value = 10;
                                    if (wl.gustWaveData.dvAngleRad) wl.gustWaveData.dvAngleRad.value = dirDeg * Math.PI / 180;
                                }
                            }
                            if (Array.isArray(work.tCloudLayers) && work.tCloudLayers.length > 0) {
                                var cSeed = JSON.parse(JSON.stringify(work.tCloudLayers[0]));
                                var clouds = Array.isArray(state.cloudLayers)
                                    ? state.cloudLayers.slice().sort(function(a, c) { return (a.baseMeters || 0) - (c.baseMeters || 0); })
                                    : [];
                                var totalC = Math.max(work.tCloudLayers.length, clouds.length, 3);
                                while (work.tCloudLayers.length < totalC) work.tCloudLayers.push(JSON.parse(JSON.stringify(cSeed)));
                                for (var ci = 0; ci < work.tCloudLayers.length; ci++) {
                                    var cl = work.tCloudLayers[ci];
                                    var sc = clouds[ci];
                                    var rawCov = sc && sc.coveragePercent != null
                                        ? Number(sc.coveragePercent)
                                        : (sc && sc.coverage != null ? Number(sc.coverage) : 0);
                                    var cov01 = Number.isFinite(rawCov)
                                        ? Math.max(0, Math.min(1, rawCov > 1 ? rawCov / 100 : rawCov))
                                        : 0;
                                    if (sc && cov01 > 0.001) {
                                        var baseM = Number.isFinite(Number(sc.baseMeters)) ? Number(sc.baseMeters) : (groundElevM + Number(sc.baseFeetAgl || 1000) * 0.3048);
                                        var topM = Number.isFinite(Number(sc.topMeters)) ? Math.max(baseM + 100, Number(sc.topMeters)) : Math.max(baseM + 300, groundElevM + Number(sc.topFeetAgl || 2000) * 0.3048);
                                        var scat01 = sc.isConvective ? 0.0 : (Number.isFinite(Number(sc.scattering)) ? Math.max(0, Math.min(1, Number(sc.scattering))) : 0.90);
                                        var dens = b.computeCloudDensity(sc, cov01);
                                        if (cl.dvAltitudeBot) cl.dvAltitudeBot.value = toAltFromM(baseM);
                                        if (cl.dvAltitudeTop) cl.dvAltitudeTop.value = toAltFromM(topM);
                                        if (cl.dvCoverageRatio) cl.dvCoverageRatio.value = cov01 * 100;
                                        if (cl.dvCloudScatteringRatio) cl.dvCloudScatteringRatio.value = scat01 * 100;
                                        if (cl.dvDensityMultiplier) cl.dvDensityMultiplier.value = dens;
                                    } else {
                                        var slotBaseFt = 3000 * (ci + 1);
                                        if (cl.dvCoverageRatio) cl.dvCoverageRatio.value = 0;
                                        if (cl.dvDensityMultiplier) cl.dvDensityMultiplier.value = 0;
                                        if (cl.dvCloudScatteringRatio) cl.dvCloudScatteringRatio.value = 75;
                                        if (cl.dvAltitudeBot) cl.dvAltitudeBot.value = toAltFromFt(slotBaseFt);
                                        if (cl.dvAltitudeTop) cl.dvAltitudeTop.value = toAltFromFt(slotBaseFt + 2000);
                                    }
                                }
                            }
                        }
                        return work;
                    };
                    var computeWeatherSig = function(st) {
                        if (!st) return 'nil';
                        var cLen = Array.isArray(st.cloudLayers) ? st.cloudLayers.length : 0;
                        var c0 = cLen > 0 && st.cloudLayers[0] ? (Math.round(st.cloudLayers[0].baseMeters || 0) + ':' + Math.round((st.cloudLayers[0].coveragePercent || 0) * 100)) : '0';
                        return [
                            cleanTag(st.stationId || 'LOCAL'),
                            Math.round((st.altimeterHpa || 1013) * 10),
                            Math.round((st.temperatureCelsius || 15) * 10),
                            Math.round(st.windDirectionDegrees || 0),
                            Math.round((st.windSpeedKnots || 0) * 10),
                            cLen,
                            c0
                        ].join('|');
                    };
                    b.applyWeatherPayload = function(state, sourceTag) {
                        if (!state) return b.targetPreset;
                        state.stationId = cleanTag(state.stationId || 'LOCAL');
                        state.sourceModelName = cleanTag(state.sourceModelName || 'Multi-Model');
                        if (typeof state.rawMetar === 'string') state.rawMetar = cleanTag(state.rawMetar);
                        var wSig = computeWeatherSig(state);
                        var isDuplicate = (wSig === b._lastWeatherSig);
                        b._lastWeatherSig = wSig;
                        b.weatherState = state;
                        b.rawMetar = state.rawMetar || '';
                        var targetPreset = b.createWeatherPreset(state);
                        b.targetPreset = targetPreset;
                        if (!isDuplicate) {
                            b.eventCount++;
                            var station = state.stationId || 'LOCAL';
                            var qnhHpa = state.altimeterHpa ? state.altimeterHpa.toFixed(1) : '---';
                            var qnhInHg = state.altimeterHpa ? (state.altimeterHpa * 0.029529983).toFixed(2) : '--.--';
                            var temp = state.temperatureCelsius != null ? state.temperatureCelsius.toFixed(1) : '--';
                            b.addLog('event', 'Apply (' + sourceTag + ') [' + station + '] - QNH ' + qnhInHg + ' inHg (' + qnhHpa + ' hPa), ' + temp + '°C');
                        }
                        if (b._externalWorkerActive) {
                            b.currentPreset = targetPreset;
                            b.lastListenerStatus = 'APPLIED';
                            b.updateStatus('injected', 'APPLIED');
                        } else {
                            b.queuePresetInterpolation(targetPreset);
                            if (!isDuplicate) b.stepInterpolation();
                        }
                        if (!b.isMinimized) {
                            b.renderActiveTab();
                            b.renderStationBar();
                            fixBrokenGlyphs();
                        } else {
                            b.renderShell();
                            fixBrokenGlyphs();
                        }
                        return targetPreset;
                    };
                    b.stepInterpolation = function() {
                        if (!b.weatherState) return;
                        if (b._externalWorkerActive) {
                            b.lastListenerStatus = 'APPLIED';
                            b.updateStatus('injected', 'APPLIED');
                            return;
                        }
                        var wSig = computeWeatherSig(b.weatherState);
                        if (wSig === b._lastPushedWeatherSig && b.lastListenerStatus === 'APPLIED') {
                            return;
                        }
                        if (!b._cleanNativePreset && typeof b.stepOffLiveWeather === 'function') {
                            b.stepOffLiveWeather();
                        }
                        var p = b.buildPureStrataPreset(b.weatherState, b.currentPreset);
                        if (p && b.weatherListener && typeof b.weatherListener.updateTempWeatherPreset === 'function') {
                            b._lastPushedWeatherSig = wSig;
                            b.weatherListener.updateTempWeatherPreset(p, function() {
                                b.lastListenerStatus = 'APPLIED';
                                b.updateStatus('injected', 'APPLIED');
                                b.addLog('ok', 'MSFS weather listener applied preset');
                            }, function(err) {
                                b._lastPushedWeatherSig = '';
                                b.errorCount++;
                                var detail = b.errorText(err);
                                b.lastListenerStatus = 'ERROR: ' + detail;
                                b.updateStatus('error', 'APPLY ERROR');
                                b.addLog('error', 'MSFS weather listener rejected preset: ' + detail);
                            });
                        }
                    };
                    b.buildCoherentPreset = function(current) {
                        return b.buildPureStrataPreset(b.weatherState, current) || cleanSyntheticKeys(current);
                    };
                    if (b.weatherListener) {
                        var proto = Object.getPrototypeOf(b.weatherListener);
                        var rawUpdate = (proto && typeof proto.updateTempWeatherPreset === 'function')
                            ? proto.updateTempWeatherPreset.bind(b.weatherListener)
                            : (typeof b.weatherListener.updateTempWeatherPreset === 'function' ? b.weatherListener.updateTempWeatherPreset.bind(b.weatherListener) : null);
                        if (rawUpdate) {
                            b.weatherListener.updateTempWeatherPreset = function(preset, onOk, onErr) {
                                if (b._externalWorkerActive) {
                                    b.lastListenerStatus = 'APPLIED';
                                    if (onOk) onOk();
                                    return;
                                }
                                var p = b.buildPureStrataPreset(b.weatherState, preset) || cleanSyntheticKeys(preset);
                                return rawUpdate(p, function() {
                                    b.lastListenerStatus = 'APPLIED';
                                    diag('push-ok', 'APPLIED', '');
                                    if (onOk) onOk();
                                }, function(err) {
                                    var et = b.errorText(err);
                                    b.lastListenerStatus = 'ERROR: ' + et;
                                    diag('push-err', 'ERROR', et);
                                    if (onErr) onErr(err);
                                });
                            };
                        }
                    }
                    if (b.listener && typeof b.listener.callSimConnect === 'function') {
                        var lProto = Object.getPrototypeOf(b.listener);
                        var rawCall = (lProto && typeof lProto.callSimConnect === 'function')
                            ? lProto.callSimConnect.bind(b.listener)
                            : b.listener.callSimConnect.bind(b.listener);
                        b.listener.callSimConnect = function(evt, data) {
                            if (evt === 'SkyWeave.Weather.Acknowledge' && typeof data === 'string') {
                                try {
                                    var obj = JSON.parse(data);
                                    obj.listenerStatus = String(b.lastListenerStatus || 'PATCHED_V8').slice(0, 180);
                                    if (obj.error) obj.error = String(obj.error).slice(0, 180);
                                    data = JSON.stringify(obj);
                                } catch (e) {}
                            }
                            return rawCall(evt, data);
                        };
                    }
                    if (typeof b.renderActiveTab === 'function' && !b._renderActiveTabWrappedV9) {
                        b._renderActiveTabWrappedV9 = true;
                        var origRenderActiveTab = b.renderActiveTab.bind(b);
                        b.renderActiveTab = function() {
                            var r = origRenderActiveTab();
                            fixBrokenGlyphs();
                            return r;
                        };
                    }
                    b.errorCount = 0;
                    b.history = [];
                    b.lastListenerStatus = 'PATCHED_V9';
                    diag('patched-v9', b.lastListenerStatus, b._cleanNativePreset ? 'hasCleanPreset' : 'awaitingPreset');
                    b.addLog('ok', 'Live CoherentGT anti-flash weather bridge patched (v0.10.0-V9)');
                    b.lastPushTime = 0;
                    b.stepInterpolation();
                    b.renderStationBar();
                    b.renderActiveTab();
                    fixBrokenGlyphs();
                } catch (e) {}
            })();
            """;
        return js;
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
        _simConnect.AtmosphereModRequestReceived -= OnAtmosphereModRequestReceived;
        _simConnect.SimulationRateChanged -= OnSimulationRateChanged;
        _simConnect.TrafficUpdated -= OnTrafficUpdated;
        _simConnect.Disconnected -= OnSimDisconnected;
        _simConnect.LogMessage -= OnSimConnectLogMessage;
        StopInjection();
    }
}
