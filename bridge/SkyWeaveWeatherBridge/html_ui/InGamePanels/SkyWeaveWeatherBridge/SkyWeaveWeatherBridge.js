class SkyWeaveWeatherBridgeElement extends HTMLElement {
    constructor() {
        super();
        this.history = [];
        this.eventCount = 0;
        this.ackCount = 0;
        this.errorCount = 0;
        this.activeTab = "overview";
        this.logFilter = "all";
        this.smoothingFactor = 0.2;
        this.weatherState = null;
        this.rawMetar = "";
        this.targetPreset = null;
        this.currentPreset = null;
        this.presetSeen = false;
        this.presetModeSwitched = false;
        this.isMinimized = false;
    }

    connectedCallback() {
        if (this._initialized) return;
        this._initialized = true;

        this.renderShell();
        this.startClock();

        try {
            if (typeof RegisterCommBusListener !== "function") {
                this.addLog("error", "CommBus.js not available");
                this.updateStatus("error", "NO COMMBUS");
                return;
            }
            this.listener = RegisterCommBusListener(() => {
                this.addLog("ready", "CommBus listener registered by MSFS");
                this.updateStatus("ready", "CONNECTED");
            });
            this.listener.on("SkyWeave.Weather.Apply", this.onApply.bind(this));
            this.addLog("ready", "Listening for SkyWeave.Weather.Apply");
            
            this.heartbeatInterval = setInterval(() => {
                if (this.listener && typeof this.listener.callSimConnect === 'function') {
                    this.listener.callSimConnect("SkyWeave.Weather.Heartbeat", JSON.stringify({
                        protocol: 1,
                        version: 1
                    })).catch(() => {});
                }
            }, 30000);
            
            this.loadWeatherListener();
        } catch (error) {
            this.addLog("error", "Listener registration failed: " + this.errorText(error));
            this.updateStatus("error", "INIT ERROR");
        }
    }

    startClock() {
        if (this.clockInterval) clearInterval(this.clockInterval);
        this.clockInterval = setInterval(() => {
            const clockEls = this.querySelectorAll(".zulu-clock");
            const now = new Date();
            const hh = (now.getUTCHours() < 10 ? "0" : "") + now.getUTCHours();
            const mm = (now.getUTCMinutes() < 10 ? "0" : "") + now.getUTCMinutes();
            const ss = (now.getUTCSeconds() < 10 ? "0" : "") + now.getUTCSeconds();
            const zulu = hh + ":" + mm + ":" + ss + "Z";
            for (let i = 0; i < clockEls.length; i++) {
                clockEls[i].textContent = zulu;
            }
        }, 1000);
    }

    loadWeatherListener() {
        if (typeof RegisterWeatherListener === "function") {
            this.registerWeatherListener();
            return;
        }

        if (!window.g_globalVarMgr || typeof window.g_globalVarMgr.AskGlobalValue !== "function") {
            window.g_globalVarMgr = { AskGlobalValue: function () { return "{}"; } };
        }

        const script = document.createElement("script");
        script.src = "/JS/Services/Weather.js";
        script.onload = () => this.registerWeatherListener();
        script.onerror = () => this.addLog("error", "Weather.js failed to load in this panel");
        document.head.appendChild(script);
    }

    registerWeatherListener() {
        try {
            if (typeof RegisterWeatherListener !== "function") {
                this.addLog("error", "Weather.js loaded without RegisterWeatherListener");
                return;
            }
            this.weatherListener = RegisterWeatherListener();
            this.weatherListener.updatePreset((preset) => {
                this.currentPreset = preset;
                if (!this.presetSeen) {
                    this.presetSeen = true;
                    const sKeys = preset && preset.oSettings ? Object.keys(preset.oSettings).join(",") : "none";
                    this.addLog("ready", "Snapshot: " + (preset.name || "Custom") + " | Keys: " + sKeys);
                }
            });

            if (typeof this.weatherListener.getPresets === "function") {
                this.weatherListener.getPresets((presets) => {
                    this.presets = presets;
                    this.addLog("ready", "Preset catalog: " + (presets ? presets.length : 0) + " available");
                    if (!this.currentPreset && Array.isArray(presets) && presets.length > 0) {
                        const customIdx = presets.findIndex(p => p && p.bIsLive === false);
                        const targetIdx = customIdx >= 0 ? customIdx : 1;
                        if (typeof this.weatherListener.setWeatherPreset === "function") {
                            this.weatherListener.setWeatherPreset(targetIdx);
                            this.presetModeSwitched = true;
                        }
                    }
                });
            }

            this.addLog("ready", "MSFS native weather listener registered");
        } catch (error) {
            this.addLog("error", "Weather listener registration failed: " + this.errorText(error));
        }
    }

    async onApply(rawMessage) {
        this.eventCount++;
        this.updateStatus("injecting", "INJECTING");

        const acknowledgement = {
            protocol: 1,
            type: "acknowledge",
            requestId: "",
            accepted: false,
            error: null
        };

        try {
            const text = typeof rawMessage === "string" ? rawMessage : String(rawMessage);
            const clean = text.split("\0")[0];
            const message = JSON.parse(clean);
            acknowledgement.requestId = message.requestId || "";

            if (message.protocol !== 1 || message.type !== "apply" || !message.state) {
                throw new Error("Unsupported SkyWeave weather message");
            }

            this.weatherState = message.state;
            this.rawMetar = message.state.rawMetar || "";
            const station = message.state.stationId || "LOCAL";
            const qnhHpa = message.state.altimeterHpa ? message.state.altimeterHpa.toFixed(1) : "---";
            const qnhInHg = message.state.altimeterHpa ? (message.state.altimeterHpa * 0.029529983).toFixed(2) : "--.--";
            const temp = message.state.temperatureCelsius != null ? message.state.temperatureCelsius.toFixed(1) : "--";
            this.addLog("event", "Apply #" + this.eventCount + " (" + station + ") - QNH " + qnhInHg + " inHg (" + qnhHpa + " hPa), " + temp + "°C");

            if (this.weatherListener && typeof this.weatherListener.setWeatherPreset === "function" && !this.presetModeSwitched) {
                try {
                    this.weatherListener.setWeatherPreset(1);
                    this.presetModeSwitched = true;
                } catch (e) {
                    // Best-effort
                }
            }

            const targetPreset = this.createWeatherPreset(message.state);
            this.queuePresetInterpolation(targetPreset);

            this.addLog("ok", "Preset queued for smooth interpolation");
            acknowledgement.accepted = true;

            if (targetPreset && targetPreset.oSettings) {
                acknowledgement.applied = {
                    temp: targetPreset.oSettings.dvMSLGLTemperature ? targetPreset.oSettings.dvMSLGLTemperature.value : null,
                    press: targetPreset.oSettings.dvMSLPressure ? targetPreset.oSettings.dvMSLPressure.value : null
                };
            }

            if (!this.isMinimized) {
                this.renderActiveTab();
                this.renderStationBar();
            } else {
                this.renderShell();
            }

            setTimeout(() => {
                this.updateStatus("injected", "INJECTED");
            }, 2000);
        } catch (error) {
            this.errorCount++;
            acknowledgement.error = this.errorText(error);
            this.addLog("error", "Apply failed: " + acknowledgement.error);
            this.updateStatus("error", "APPLY ERROR");
        }

        try {
            await this.listener.callSimConnect(
                "SkyWeave.Weather.Acknowledge",
                JSON.stringify(acknowledgement));
            this.ackCount++;
            this.addLog("acked", "Ack sent (" + acknowledgement.requestId + ")");
        } catch (error) {
            this.errorCount++;
            this.addLog("error", "Ack send failed: " + this.errorText(error));
        }
    }

    queuePresetInterpolation(targetPreset) {
        this.targetPreset = targetPreset;
        if (!this.interpolationInterval) {
            this.interpolationInterval = setInterval(() => {
                this.stepInterpolation();
            }, 1000);
        }
    }

    stepInterpolation() {
        if (!this.currentPreset || !this.targetPreset) return;

        const current = this.currentPreset;
        const target = this.targetPreset;
        let modified = false;

        const lerpKey = (objA, objB, key, factor) => {
            if (objA[key] && objB[key] && typeof objA[key].value === "number" && typeof objB[key].value === "number") {
                let diff = objB[key].value - objA[key].value;
                if (key === "dvAngleRad") diff = Math.atan2(Math.sin(diff), Math.cos(diff));
                if (Math.abs(diff) > 0.001) {
                    let value = objA[key].value + diff * factor;
                    if (key === "dvAngleRad") value = (value + 2 * Math.PI) % (2 * Math.PI);
                    this.setValue(objA[key], value);
                    modified = true;
                } else if (diff !== 0) {
                    this.setValue(objA[key], objB[key].value);
                    modified = true;
                }
            }
        };

        const f = this.smoothingFactor || 0.2;
        if (current.oSettings && target.oSettings) {
            if (current.oSettings.bIsAltitudeAMGL !== target.oSettings.bIsAltitudeAMGL) {
                // An AGL snapshot cannot be blended numerically with an MSL target.
                // Re-seed layers at the new datum; clouds fade in at their correct heights.
                if (current.oSettings.bIsAltitudeAMGL === true && target.oSettings.bIsAltitudeAMGL === false) {
                    current.tCloudLayers = [];
                    current.tWindLayers = [];
                }
                current.oSettings.bIsAltitudeAMGL = target.oSettings.bIsAltitudeAMGL;
                modified = true;
            }
            lerpKey(current.oSettings, target.oSettings, "dvMSLGLTemperature", f);
            lerpKey(current.oSettings, target.oSettings, "dvMSLTemperature", f);
            lerpKey(current.oSettings, target.oSettings, "dvTemperature", f);
            lerpKey(current.oSettings, target.oSettings, "dvMSLPressure", f);
            lerpKey(current.oSettings, target.oSettings, "dvPressure", f);
            lerpKey(current.oSettings, target.oSettings, "dvPrecipitation", f);
            lerpKey(current.oSettings, target.oSettings, "dvThunderstormRatio", f);
            lerpKey(current.oSettings, target.oSettings, "dvPollution", f);
            lerpKey(current.oSettings, target.oSettings, "dvHumidityMultiplier", f);
            lerpKey(current.oSettings, target.oSettings, "dvSnowCover", f);
            lerpKey(current.oSettings, target.oSettings, "dvSnowCoverMultiplier", f);
        }

        if (Array.isArray(target.tCloudLayers)) {
            const remaining = (current.tCloudLayers || []).slice();
            const targets = target.tCloudLayers.slice(0, 24).sort((a, b) => a.dvAltitudeBot.value - b.dvAltitudeBot.value);
            // Resolve closest pairs first: an inserted low deck must not steal an unchanged high deck.
            const matches = new Map();
            const pairs = [];
            targets.forEach((t, index) => remaining.forEach(layer => pairs.push({ index, layer,
                distance: Math.abs(layer.dvAltitudeBot.value - t.dvAltitudeBot.value) })));
            pairs.sort((a, b) => a.distance - b.distance);
            for (const pair of pairs) {
                const index = remaining.indexOf(pair.layer);
                if (matches.has(pair.index) || index < 0) continue;
                matches.set(pair.index, pair.layer);
                remaining.splice(index, 1);
            }
            const layers = targets.map((t, index) => {
                if (matches.has(index)) return matches.get(index);
                const added = JSON.parse(JSON.stringify(t));
                this.setValue(added.dvCoverageRatio, 0);
                this.setValue(added.dvDensityMultiplier, 0);
                modified = true;
                return added;
            });
            for (const removed of remaining) {
                const fade = JSON.parse(JSON.stringify(removed));
                this.setValue(fade.dvCoverageRatio, 0);
                this.setValue(fade.dvDensityMultiplier, 0);
                layers.push(removed);
                targets.push(fade);
            }
            const count = layers.length;
            for (let i = 0; i < count; i++) {
                const cLayer = layers[i];
                const tLayer = targets[i];
                if (cLayer && tLayer) {
                    lerpKey(cLayer, tLayer, "dvCoverageRatio", f);
                    lerpKey(cLayer, tLayer, "dvDensityMultiplier", f);
                    lerpKey(cLayer, tLayer, "dvCloudScatteringRatio", f);
                    lerpKey(cLayer, tLayer, "dvAltitudeBot", f);
                    lerpKey(cLayer, tLayer, "dvAltitudeTop", f);
                }
            }
            current.tCloudLayers = layers.filter((layer, i) => i < target.tCloudLayers.length ||
                layer.dvCoverageRatio.value > 0.001 || layer.dvDensityMultiplier.value > 0.001).slice(0, 24);
            if (current.tCloudLayers.length !== layers.length) modified = true;
        }

        if (Array.isArray(target.tWindLayers)) {
            const previous = (current.tWindLayers || []).slice().sort((a, b) => a.dvAltitude.value - b.dvAltitude.value);
            const windTargets = target.tWindLayers.slice().sort((a, b) => a.dvAltitude.value - b.dvAltitude.value);
            current.tWindLayers = windTargets.map(t => {
                const layer = JSON.parse(JSON.stringify(t));
                if (!previous.length) { modified = true; return layer; }
                const altitude = t.dvAltitude.value;
                const lower = previous.filter(w => w.dvAltitude.value <= altitude).pop() || previous[0];
                const upper = previous.find(w => w.dvAltitude.value >= altitude) || previous[previous.length - 1];
                const span = upper.dvAltitude.value - lower.dvAltitude.value;
                const fraction = span > 0 ? (altitude - lower.dvAltitude.value) / span : 0;
                const sample = (dest, low, high, key) => {
                    if (!dest || !dest[key] || !low || !low[key] || !high || !high[key]) return;
                    const a = low[key].value;
                    let delta = high[key].value - a;
                    if (key === "dvAngleRad") delta = Math.atan2(Math.sin(delta), Math.cos(delta));
                    let value = a + delta * fraction;
                    if (key === "dvAngleRad") value = (value + 2 * Math.PI) % (2 * Math.PI);
                    this.setValue(dest[key], value);
                };
                sample(layer, lower, upper, "dvSpeed");
                sample(layer, lower, upper, "dvAngleRad");
                sample(layer.gustWaveData, lower.gustWaveData, upper.gustWaveData, "dvSpeedMultiplier");
                sample(layer.gustWaveData, lower.gustWaveData, upper.gustWaveData, "dvAngleRad");
                return layer;
            });
            if (previous.length !== windTargets.length || previous.some((w, i) =>
                !windTargets[i] || w.dvAltitude.value !== windTargets[i].dvAltitude.value)) modified = true;
            const count = windTargets.length;
            for (let i = 0; i < count; i++) {
                const cWind = current.tWindLayers[i];
                const tWind = windTargets[i];
                if (cWind && tWind) {
                    lerpKey(cWind, tWind, "dvAltitude", f);
                    lerpKey(cWind, tWind, "dvAngleRad", f);
                    lerpKey(cWind, tWind, "dvSpeed", f);
                    if (cWind.gustWaveData && tWind.gustWaveData) {
                        lerpKey(cWind.gustWaveData, tWind.gustWaveData, "dvSpeedMultiplier", f);
                        lerpKey(cWind.gustWaveData, tWind.gustWaveData, "dvAngleRad", f);
                    }
                }
            }
        }

        if (modified && this.weatherListener) {
            this.weatherListener.updateTempWeatherPreset(current, () => {}, () => {});
        }
    }

    renderShell() {
        const bootstrap = document.getElementById("skyweave-bootstrap-status");
        if (bootstrap) bootstrap.style.display = "none";

        if (this.isMinimized) {
            const s = this.weatherState;
            const station = s ? (s.stationId || "LOCAL") : "STANDBY";
            const tempC = s && s.temperatureCelsius != null ? s.temperatureCelsius.toFixed(1) + "&deg;C" : "--&deg;C";
            const windDir = s && s.windDirectionDegrees != null ? Math.round(s.windDirectionDegrees) : 0;
            const windSpd = s && s.windSpeedKnots != null ? Math.round(s.windSpeedKnots) : 0;
            const windGust = s && s.windGustKnots ? (" G" + Math.round(s.windGustKnots)) : "";
            const dirStr = (windDir < 100 ? (windDir < 10 ? "00" : "0") : "") + windDir;
            const qnhInHg = s && s.altimeterHpa != null ? (s.altimeterHpa * 0.029529983).toFixed(2) : "--.--";
            const qnhHpa = s && s.altimeterHpa != null ? Math.round(s.altimeterHpa) + " hPa" : "---";
            const cat = s && s.flightCategory ? s.flightCategory.toLowerCase() : "vfr";

            this.className = "mini-hud-mode";
            this.innerHTML = `
                <div class="mini-hud-bar drag-handle">
                    <div class="mini-hud-left">
                        <div class="brand-logo mini">W</div>
                        <span class="mini-pulse-dot active"></span>
                        <span class="brand-title mini">SKYWEAVE</span>
                        <span class="zulu-clock mini-zulu">--:--:--Z</span>
                    </div>
                    <div class="mini-hud-center">
                        <span class="hud-station">${station}</span>
                        <span class="hud-val highlight">${tempC}</span>
                        <span class="hud-val">${dirStr}&deg;/${windSpd}kt${windGust}</span>
                        <span class="hud-val">QNH ${qnhInHg} <span class="hud-sub">(${qnhHpa})</span></span>
                        <span class="flight-cat-badge ${cat}">${cat.toUpperCase()}</span>
                    </div>
                    <div class="mini-hud-right">
                        <button class="hud-btn btn-expand" id="btn-expand-panel" title="Expand Full Avionics">&nearr; EXPAND</button>
                    </div>
                </div>
            `;

            const expandBtn = this.querySelector("#btn-expand-panel");
            if (expandBtn) {
                expandBtn.onclick = (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    this.isMinimized = false;
                    this.renderShell();
                };
            }
            this.setupDraggable();
            return;
        }

        this.className = "expanded-mode";
        this.innerHTML = `
            <div class="bridge-header">
                <div class="brand-section">
                    <div class="brand-logo">W</div>
                    <span class="brand-title">SkyWeave</span>
                    <span class="brand-badge">SimBridge v0.5</span>
                </div>
                <div class="header-right">
                    <span class="zulu-clock">--:--:--Z</span>
                    <div class="status-badge ${this.currentStatusType || (this.weatherState ? 'injected' : 'ready')}">
                        <span class="status-dot"></span>
                        <span class="status-text">${this.currentStatusText || (this.weatherState ? 'INJECTED' : 'READY')}</span>
                    </div>
                    <button class="hud-btn btn-popout" id="btn-popout-hint" title="Tip: Press Right-Alt + Click anywhere on this window to pop it out to a movable desktop window!">&#x29c9; POPOUT</button>
                    <button class="hud-btn btn-minimize" id="btn-minimize-panel" title="Minimize to Floating Background HUD">&mdash; HUD</button>
                </div>
            </div>

            <div class="station-subbar">
                <div class="station-info">
                    <span class="station-icao">---</span>
                    <span class="station-details">No weather loaded</span>
                </div>
                <span class="flight-cat-badge vfr">VFR</span>
            </div>

            <div class="bridge-tabs">
                <div role="button" tabindex="0" class="tab-btn ${this.activeTab === "overview" ? "active" : ""}" data-tab="overview" onclick="this.closest('skyweave-weather-bridge').switchTab('overview')">Overview</div>
                <div role="button" tabindex="0" class="tab-btn ${this.activeTab === "atmosphere" ? "active" : ""}" data-tab="atmosphere" onclick="this.closest('skyweave-weather-bridge').switchTab('atmosphere')">Atmosphere</div>
                <div role="button" tabindex="0" class="tab-btn ${this.activeTab === "hazards" ? "active" : ""}" data-tab="hazards" onclick="this.closest('skyweave-weather-bridge').switchTab('hazards')">Hazards</div>
                <div role="button" tabindex="0" class="tab-btn ${this.activeTab === "forecast" ? "active" : ""}" data-tab="forecast" onclick="this.closest('skyweave-weather-bridge').switchTab('forecast')">Forecast</div>
                <div role="button" tabindex="0" class="tab-btn ${this.activeTab === "diagnostics" ? "active" : ""}" data-tab="diagnostics" onclick="this.closest('skyweave-weather-bridge').switchTab('diagnostics')">Diagnostics</div>
            </div>

            <div class="bridge-body" id="tab-content-area">
                ${this.getActiveTabHtml()}
            </div>
        `;

        const minBtn = this.querySelector("#btn-minimize-panel");
        if (minBtn) {
            minBtn.onclick = (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.isMinimized = true;
                this.renderShell();
            };
        }

        const popoutBtn = this.querySelector("#btn-popout-hint");
        if (popoutBtn) {
            popoutBtn.onclick = (e) => {
                e.preventDefault();
                e.stopPropagation();
                popoutBtn.textContent = "Hold R-Alt + Click";
                setTimeout(() => { popoutBtn.innerHTML = "&#x29c9; POPOUT"; }, 3000);
            };
        }

        this.setupTabEvents();
        this.attachTabEvents();
        this.renderStationBar();
    }

    setupDraggable() {
        // MSFS 2024 manages InGamePanel positioning via the simulator window chrome.
        // Users can resize/move via window borders or press Right-Alt + Click to pop out to a movable multi-monitor window.
    }

    setupTabEvents() {
        const tabsBar = this.querySelector(".bridge-tabs");
        if (!tabsBar) return;

        const onTabTrigger = (tabName) => {
            if (tabName && tabName !== this.activeTab) {
                this.switchTab(tabName);
            }
        };

        const tabBtns = tabsBar.querySelectorAll(".tab-btn");
        for (let i = 0; i < tabBtns.length; i++) {
            const btn = tabBtns[i];
            const tabName = btn.getAttribute("data-tab");
            if (!tabName) continue;

            btn.onclick = (e) => {
                e.preventDefault();
                e.stopPropagation();
                onTabTrigger(tabName);
            };

            btn.onmousedown = (e) => {
                e.preventDefault();
                e.stopPropagation();
                onTabTrigger(tabName);
            };

            btn.onkeydown = (e) => {
                if (e.key === "Enter" || e.key === " " || e.keyCode === 13 || e.keyCode === 32) {
                    e.preventDefault();
                    onTabTrigger(tabName);
                }
            };
        }
    }

    switchTab(tabName) {
        if (!tabName) return;
        this.activeTab = tabName;
        const tabs = this.querySelectorAll(".tab-btn");
        for (let i = 0; i < tabs.length; i++) {
            const btn = tabs[i];
            if (btn.getAttribute("data-tab") === tabName) {
                btn.classList.add("active");
            } else {
                btn.classList.remove("active");
            }
        }
        this.renderActiveTab();
    }

    getActiveTabHtml() {
        if (this.activeTab === "atmosphere") return this.renderAtmosphereTab();
        if (this.activeTab === "hazards") return this.renderHazardsTab();
        if (this.activeTab === "forecast") return this.renderForecastTab();
        if (this.activeTab === "diagnostics") return this.renderDiagnosticsTab();
        return this.renderOverviewTab();
    }

    renderActiveTab() {
        const container = this.querySelector("#tab-content-area");
        if (!container) return;
        try {
            container.innerHTML = this.getActiveTabHtml();
            this.attachTabEvents();
        } catch (err) {
            container.innerHTML = `
                <div class="empty-state" style="color:var(--red); padding:20px;">
                    <p style="font-weight:bold; font-size:13px;">Error rendering ${this.activeTab} tab</p>
                    <p style="color:var(--text-secondary); margin-top:6px; font-family:var(--font-mono); font-size:11px;">${err && err.message ? err.message : String(err)}</p>
                </div>
            `;
        }
    }

    attachTabEvents() {
        try {
            if (this.activeTab === "overview") this.attachOverviewEvents();
            else if (this.activeTab === "forecast") this.attachForecastEvents();
            else if (this.activeTab === "diagnostics") this.attachDiagEvents();
        } catch (err) {
            this.addLog("error", "Failed attaching tab events: " + (err && err.message ? err.message : String(err)));
        }
    }

    renderStationBar() {
        if (!this.weatherState) return;
        const icaoEl = this.querySelector(".station-icao");
        const detailsEl = this.querySelector(".station-details");
        const catEl = this.querySelector(".flight-cat-badge");

        if (icaoEl) icaoEl.textContent = this.weatherState.stationId || "LOCAL";
        if (detailsEl) {
            const lat = this.weatherState.latitude != null ? this.weatherState.latitude.toFixed(2) + "&deg;" : "";
            const lon = this.weatherState.longitude != null ? this.weatherState.longitude.toFixed(2) + "&deg;" : "";
            const model = this.weatherState.sourceModelName || "Multi-Model";
            detailsEl.innerHTML = lat + " " + lon + " &bull; " + model;
        }
        if (catEl) {
            const cat = (this.weatherState.flightCategory || "VFR").toLowerCase();
            catEl.className = "flight-cat-badge " + cat;
            catEl.textContent = cat.toUpperCase();
        }
    }

    updateStatus(type, text) {
        this.currentStatusType = type;
        this.currentStatusText = text;
        const badge = this.querySelector(".status-badge");
        if (badge) {
            badge.className = "status-badge " + type;
            const label = badge.querySelector(".status-text");
            if (label) label.textContent = text;
        }
    }

    renderOverviewTab() {
        const s = this.weatherState;
        if (!s) {
            return `
                <div class="empty-state">
                    <p style="font-weight:600; font-size:14px; margin-bottom:6px; color:var(--cyan);">Waiting for SkyWeave Desktop App...</p>
                    <p style="color:var(--text-secondary); line-height:1.5;">Click <strong>Connect</strong> or <strong>Start</strong> in SkyWeave.App on your PC to inject real-world multi-model weather.</p>
                </div>
            `;
        }

        const tempC = s.temperatureCelsius != null ? s.temperatureCelsius.toFixed(1) : "--";
        const tempF = s.temperatureCelsius != null ? (s.temperatureCelsius * 9/5 + 32).toFixed(1) : "--";
        const dewpC = s.dewpointCelsius != null ? s.dewpointCelsius.toFixed(1) : "--";
        const qnhHpa = s.altimeterHpa != null ? s.altimeterHpa.toFixed(1) : "---";
        const qnhInHg = s.altimeterHpa != null ? (s.altimeterHpa * 0.029529983).toFixed(2) : "--.--";
        const windDir = s.windDirectionDegrees != null ? Math.round(s.windDirectionDegrees) : 0;
        const windSpd = s.windSpeedKnots != null ? Math.round(s.windSpeedKnots) : 0;
        const windGust = s.windGustKnots ? ("G" + Math.round(s.windGustKnots)) : "";
        const rawVisMeters = s.visibilityMeters != null ? s.visibilityMeters : 10000;
        const visSm = rawVisMeters >= 9999 ? "10+" : (rawVisMeters / 1609.344).toFixed(1);
        const visKm = rawVisMeters >= 9999 ? "10+" : (rawVisMeters / 1000).toFixed(1);
        const visNum = rawVisMeters / 1609.344;
        const humidity = s.humidityPercent != null ? (Math.round(s.humidityPercent) + "%") : "---";
        const ceiling = s.ceilingFeet && s.ceilingFeet < 25000 ? (Math.round(s.ceilingFeet).toLocaleString() + " ft") : "Unlimited";
        const freezeLvl = s.freezingLevelFeet ? (Math.round(s.freezingLevelFeet).toLocaleString() + " ft") : "---";

        const dirStr = (windDir < 100 ? (windDir < 10 ? "00" : "0") : "") + windDir;
        const turbSeverity = (s.turbulenceIndex || 0) > 0.6 ? "Severe" : (s.turbulenceIndex || 0) > 0.3 ? "Moderate" : (s.turbulenceIndex || 0) > 0.1 ? "Light" : "Smooth";
        const precip = s.precipitationRate != null && s.precipitationRate > 0 ? (s.precipitationRate.toFixed(1) + " mm/h") : "None";

        return `
            <div class="hero-weather-card">
                <div class="hero-temp-section">
                    <span class="hero-temp-large">${tempC}&deg;C</span>
                    <span class="hero-temp-sub">${tempF}&deg;F</span>
                </div>
                <div class="hero-metrics-row">
                    <div class="hero-metric-item">
                        <span class="hero-metric-label">Dewpoint</span>
                        <span class="hero-metric-val">${dewpC}&deg;C</span>
                    </div>
                    <div class="hero-metric-item">
                        <span class="hero-metric-label">Humidity</span>
                        <span class="hero-metric-val">${humidity}</span>
                    </div>
                    <div class="hero-metric-item">
                        <span class="hero-metric-label">Precipitation</span>
                        <span class="hero-metric-val" style="color:var(--cyan);">${precip}</span>
                    </div>
                </div>
            </div>

            <div class="card-grid-3">
                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Surface Wind</span>
                        <span class="metric-badge">${windSpd > 20 ? 'HIGH' : 'NORMAL'}</span>
                    </div>
                    <div class="metric-value-large">${dirStr}&deg; / ${windSpd} kt <span style="font-size:13px; color:var(--amber);">${windGust}</span></div>
                    <div class="wind-visual-gauge">
                        <div class="compass-pointer" style="transform: rotate(${windDir}deg);"></div>
                        <span class="compass-label">HEADING ${dirStr}&deg;</span>
                    </div>
                </div>

                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Barometric QNH</span>
                        <span class="metric-badge">CALIBRATED</span>
                    </div>
                    <div class="metric-value-large">${qnhInHg} <span class="metric-unit">inHg</span></div>
                    <div class="metric-sub">${qnhHpa} hPa / mbar</div>
                </div>

                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Visibility & Ceiling</span>
                        <span class="metric-badge">${visNum < 3 ? 'LOW VIS' : 'CLEAR'}</span>
                    </div>
                    <div class="metric-value-large">${visSm} <span class="metric-unit">SM</span></div>
                    <div class="metric-sub">${visKm} km &bull; CIG ${ceiling}</div>
                </div>
            </div>

            <div class="card-grid-2">
                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Freezing Level</span>
                    </div>
                    <div class="metric-value">${freezeLvl}</div>
                    <div class="metric-sub">0&deg;C Isotherm Altitude</div>
                </div>
                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Turbulence Index</span>
                        <span class="metric-badge" style="background:${turbSeverity === 'Severe' ? 'rgba(255,71,87,0.2)' : turbSeverity === 'Moderate' ? 'rgba(255,165,2,0.2)' : 'rgba(0,210,211,0.2)'}">${turbSeverity.toUpperCase()}</span>
                    </div>
                    <div class="metric-value">${((s.turbulenceIndex || 0) * 100).toFixed(0)}%</div>
                    <div class="metric-sub">${turbSeverity} Air Flow</div>
                </div>
            </div>

            <div class="raw-metar-card">
                <div class="raw-metar-header">
                    <span class="raw-metar-title">Decoded METAR Briefing</span>
                    <button class="hud-btn" id="btn-copy-metar">Copy METAR</button>
                </div>
                <div class="raw-metar-text">${this.rawMetar || (s && s.rawMetar) || "No raw METAR string available."}</div>
            </div>
        `;
    }

    attachOverviewEvents() {
        const copyBtn = this.querySelector("#btn-copy-metar");
        if (copyBtn) {
            copyBtn.onclick = (e) => {
                e.preventDefault();
                const text = this.rawMetar || (this.weatherState ? this.weatherState.rawMetar : "");
                if (navigator.clipboard && text) {
                    navigator.clipboard.writeText(text);
                    copyBtn.textContent = "Copied!";
                    setTimeout(() => { copyBtn.textContent = "Copy METAR"; }, 1500);
                }
            };
        }
    }

    renderAtmosphereTab() {
        const s = this.weatherState;
        if (!s) {
            return `
                <div class="empty-state">
                    <p style="font-weight:600; font-size:14px; margin-bottom:6px; color:var(--cyan);">No Atmospheric Data Loaded</p>
                    <p style="color:var(--text-secondary);">Waiting for weather state from SkyWeave engine.</p>
                </div>
            `;
        }

        const winds = Array.isArray(s.windsAloft) ? s.windsAloft : [];
        const clouds = Array.isArray(s.cloudLayers) ? s.cloudLayers : [];

        let windsHtml = '';
        if (winds.length === 0) {
            windsHtml = '<div style="color:var(--text-muted); text-align:center; padding:12px;">No winds aloft profile available.</div>';
        } else {
            windsHtml = `
                <table class="layer-table">
                    <thead>
                        <tr>
                            <th>Altitude</th>
                            <th>Wind</th>
                            <th>Speed</th>
                            <th>Visual</th>
                            <th>Temp</th>
                            <th>Gust</th>
                        </tr>
                    </thead>
                    <tbody>
            `;
            for (let i = 0; i < winds.length; i++) {
                const w = winds[i];
                const altFt = w.altitudeFeet != null ? Math.round(w.altitudeFeet) : (w.altitudeMeters != null ? Math.round(w.altitudeMeters / 0.3048) : 0);
                const altLabel = (w.isSurfaceLayer || altFt === 0) ? 'Surface' : (altFt.toLocaleString() + ' ft');
                const dir = Math.round(w.directionDegrees || 0);
                const spd = Math.round(w.speedKnots || 0);
                const dirStr = (dir < 100 ? (dir < 10 ? '00' : '0') : '') + dir;
                const pct = Math.min(100, Math.max(0, Math.round((spd / 120) * 100)));
                const tempC = w.temperatureCelsius != null ? ((w.temperatureCelsius > 0 ? '+' : '') + Math.round(w.temperatureCelsius) + '&deg;C') : '---';
                const gust = w.gustSpeedKnots ? (Math.round(w.gustSpeedKnots) + ' kt') : '---';

                windsHtml += `
                    <tr>
                        <td style="font-weight:600; color:#fff;">${altLabel}</td>
                        <td><strong>${dirStr}&deg;</strong></td>
                        <td>${spd} <span style="font-size:9px; color:var(--text-muted);">kt</span></td>
                        <td>
                            <div class="progress-bar-bg" style="width:65px;">
                                <div class="progress-bar-fill" style="width:${pct}%;"></div>
                            </div>
                        </td>
                        <td style="color:${w.temperatureCelsius <= 0 ? 'var(--cyan)' : 'var(--text-secondary)'};">${tempC}</td>
                        <td style="color:${w.gustSpeedKnots ? 'var(--amber)' : 'var(--text-muted)'};">${gust}</td>
                    </tr>
                `;
            }
            windsHtml += '</tbody></table>';
        }

        let cloudsHtml = '';
        if (clouds.length === 0) {
            cloudsHtml = `
                <div style="background:rgba(46,213,115,0.08); border:1px solid rgba(46,213,115,0.25); border-radius:4px; padding:12px; text-align:center; color:var(--green);">
                    <strong>SKC / CAVOK</strong> &mdash; Clear skies, no significant cloud layers detected.
                </div>
            `;
        } else {
            cloudsHtml = `
                <table class="layer-table">
                    <thead>
                        <tr>
                            <th>Deck</th>
                            <th>Coverage</th>
                            <th>Base (AGL)</th>
                            <th>Top (AGL)</th>
                            <th>MSL Band</th>
                            <th>Density</th>
                        </tr>
                    </thead>
                    <tbody>
            `;
            for (let i = 0; i < clouds.length; i++) {
                const c = clouds[i];
                const typeStr = (c.type != null && typeof c.type === 'string') ? c.type.toUpperCase() : ('Deck #' + (i + 1));
                const rawCov = c.coveragePercent != null ? c.coveragePercent : 0;
                const covPct = Math.round(rawCov > 1 ? rawCov : rawCov * 100);
                const catBadge = covPct > 80 ? 'lifr' : covPct > 50 ? 'ifr' : covPct > 25 ? 'mvfr' : 'vfr';
                const baseAgl = c.baseFeetAgl != null ? Math.round(c.baseFeetAgl).toLocaleString() + ' ft' : (c.baseMeters != null ? Math.round(c.baseMeters / 0.3048).toLocaleString() + ' ft' : '---');
                const topAgl = c.topFeetAgl != null ? Math.round(c.topFeetAgl).toLocaleString() + ' ft' : (c.topMeters != null ? Math.round(c.topMeters / 0.3048).toLocaleString() + ' ft' : '---');
                const baseMsl = c.baseMeters != null ? Math.round(c.baseMeters / 0.3048).toLocaleString() + ' ft' : '---';
                const topMsl = c.topMeters != null ? Math.round(c.topMeters / 0.3048).toLocaleString() + ' ft' : '---';
                const density = c.density != null ? Math.round(c.density * 100) + '%' : '---';

                cloudsHtml += `
                    <tr>
                        <td style="font-weight:700; color:#fff;">${typeStr}${c.isConvective ? ' <span style="color:var(--red); font-size:9px;">[CB]</span>' : ''}</td>
                        <td><span class="flight-cat-badge ${catBadge}">${covPct}%</span></td>
                        <td>${baseAgl}</td>
                        <td>${topAgl}</td>
                        <td style="color:var(--text-secondary); font-size:10px;">${baseMsl} &ndash; ${topMsl}</td>
                        <td>${density}</td>
                    </tr>
                `;
            }
            cloudsHtml += '</tbody></table>';
        }

        return `
            <div class="card-section">
                <div class="section-title">Winds & Temperatures Aloft Profile</div>
                ${windsHtml}
            </div>

            <div class="card-section" style="margin-top:10px;">
                <div class="section-title">Synthesized Cloud Stratification (MSFS Decks)</div>
                ${cloudsHtml}
            </div>
        `;
    }

    renderHazardsTab() {
        const s = this.weatherState;
        if (!s) return `<div class="empty-state">No weather hazard data loaded.</div>`;

        const turbVal = Math.min(100, Math.max(0, Math.round((s.turbulenceIndex || 0) * 100)));
        const icingVal = Math.min(100, Math.max(0, Math.round((s.icingIndex || 0) * 100)));
        const stormVal = Math.min(100, Math.max(0, Math.round((s.thunderstormIntensity || 0) * 100)));
        const cape = s.convectiveAvailablePotentialEnergy != null ? Math.round(s.convectiveAvailablePotentialEnergy) : null;
        const li = s.liftedIndex != null ? s.liftedIndex.toFixed(1) : null;

        const icingLayers = Array.isArray(s.icingLayers) ? s.icingLayers : [];
        const turbLayers = Array.isArray(s.turbulenceLayers) ? s.turbulenceLayers : [];
        const hazards = Array.isArray(s.hazards) ? s.hazards : [];

        let icingLayersHtml = '';
        if (icingLayers.length > 0) {
            icingLayersHtml = '<table class="layer-table" style="margin-top:6px;"><thead><tr><th>Altitude Band</th><th>Severity</th><th>Type</th><th>Temp</th></tr></thead><tbody>';
            for (let i = 0; i < icingLayers.length; i++) {
                const il = icingLayers[i];
                const base = Math.round(il.baseFeet || 0).toLocaleString() + ' ft';
                const top = Math.round(il.topFeet || 0).toLocaleString() + ' ft';
                const sev = typeof il.severity === 'string' ? il.severity : (il.severity === 1 ? 'Light' : il.severity === 2 ? 'Moderate' : il.severity >= 3 ? 'Severe' : 'Trace');
                const type = typeof il.icingType === 'string' ? il.icingType : (il.icingType === 0 ? 'Clear' : il.icingType === 1 ? 'Rime' : 'Mixed');
                const temp = il.temperatureCelsius != null ? Math.round(il.temperatureCelsius) + '&deg;C' : '---';
                icingLayersHtml += `<tr><td>${base} &ndash; ${top}</td><td style="color:${sev === 'Severe' ? 'var(--red)' : sev === 'Moderate' ? 'var(--amber)' : 'var(--cyan)'}; font-weight:700;">${sev}</td><td>${type}</td><td>${temp}</td></tr>`;
            }
            icingLayersHtml += '</tbody></table>';
        }

        let turbLayersHtml = '';
        if (turbLayers.length > 0) {
            turbLayersHtml = '<table class="layer-table" style="margin-top:6px;"><thead><tr><th>Altitude Band</th><th>Intensity</th><th>Type</th></tr></thead><tbody>';
            for (let i = 0; i < turbLayers.length; i++) {
                const tl = turbLayers[i];
                const base = Math.round(tl.baseFeet || 0).toLocaleString() + ' ft';
                const top = Math.round(tl.topFeet || 0).toLocaleString() + ' ft';
                const inten = typeof tl.intensity === 'string' ? tl.intensity : (tl.intensity === 1 ? 'Light' : tl.intensity === 2 ? 'Moderate' : tl.intensity >= 3 ? 'Severe' : 'Smooth');
                const type = typeof tl.type === 'string' ? tl.type : (tl.type === 0 ? 'Thermal' : tl.type === 1 ? 'Convective' : tl.type === 2 ? 'Mechanical' : tl.type === 3 ? 'MountainWave' : 'Wake');
                turbLayersHtml += `<tr><td>${base} &ndash; ${top}</td><td style="color:${inten === 'Severe' ? 'var(--red)' : inten === 'Moderate' ? 'var(--amber)' : 'var(--green)'}; font-weight:700;">${inten}</td><td>${type}</td></tr>`;
            }
            turbLayersHtml += '</tbody></table>';
        }

        let advisoriesHtml = '';
        if (hazards.length > 0) {
            advisoriesHtml = '<div class="advisories-list" style="margin-top:8px; display:flex; flex-direction:column; gap:6px;">';
            for (let i = 0; i < hazards.length; i++) {
                const h = hazards[i];
                const type = h.type != null ? String(h.type) : 'SIGMET';
                const desc = h.description || h.rawText || 'Weather hazard advisory';
                advisoriesHtml += `
                    <div style="background:rgba(255,71,87,0.1); border:1px solid rgba(255,71,87,0.3); border-radius:4px; padding:6px 10px;">
                        <span style="color:var(--red); font-weight:800; font-size:10px; text-transform:uppercase;">[${type}]</span>
                        <span style="color:var(--text-primary); font-size:11px; margin-left:6px;">${desc}</span>
                    </div>
                `;
            }
            advisoriesHtml += '</div>';
        }

        return `
            <div class="card-grid-2">
                <div class="hazard-card ${turbVal > 40 ? 'danger' : turbVal > 15 ? 'warning' : 'safe'}">
                    <div class="hazard-header">
                        <span class="hazard-title">Turbulence Activity</span>
                        <span class="hazard-badge">${turbVal > 40 ? 'HIGH' : turbVal > 15 ? 'MODERATE' : 'LIGHT'}</span>
                    </div>
                    <div class="hazard-meter-container">
                        <div class="hazard-meter-fill" style="width:${turbVal}%;"></div>
                    </div>
                    <div class="hazard-footer">
                        <span>Intensity: ${turbVal}%</span>
                        <span>${turbVal > 40 ? 'Severe Shear / Mountain Wave' : turbVal > 15 ? 'Moderate Bumps' : 'Smooth Air'}</span>
                    </div>
                </div>

                <div class="hazard-card ${icingVal > 40 ? 'danger' : icingVal > 15 ? 'warning' : 'safe'}">
                    <div class="hazard-header">
                        <span class="hazard-title">Structural Icing Risk</span>
                        <span class="hazard-badge">${icingVal > 40 ? 'HIGH' : icingVal > 15 ? 'MODERATE' : 'LOW'}</span>
                    </div>
                    <div class="hazard-meter-container">
                        <div class="hazard-meter-fill" style="width:${icingVal}%;"></div>
                    </div>
                    <div class="hazard-footer">
                        <span>Risk Index: ${icingVal}%</span>
                        <span>${icingVal > 40 ? 'Supercooled Water Drops' : icingVal > 15 ? 'Trace/Light Moisture' : 'Clear Air'}</span>
                    </div>
                </div>
            </div>

            <div class="card-grid-2" style="margin-top:8px;">
                <div class="hazard-card ${stormVal > 30 ? 'danger' : stormVal > 5 ? 'warning' : 'safe'}">
                    <div class="hazard-header">
                        <span class="hazard-title">Thunderstorm & Convection</span>
                        <span class="hazard-badge">${stormVal > 30 ? 'ACTIVE' : stormVal > 5 ? 'VCTS' : 'NONE'}</span>
                    </div>
                    <div class="hazard-meter-container">
                        <div class="hazard-meter-fill" style="width:${stormVal}%;"></div>
                    </div>
                    <div class="hazard-footer">
                        <span>CAPE: ${cape != null ? cape + ' J/kg' : '---'}</span>
                        <span>Lifted Index: ${li != null ? li : '---'}</span>
                    </div>
                </div>

                <div class="hazard-card safe">
                    <div class="hazard-header">
                        <span class="hazard-title">Freezing Level (0&deg;C)</span>
                        <span class="hazard-badge" style="color:var(--cyan); background:rgba(0,210,211,0.15);">${s.freezingLevelFeet ? Math.round(s.freezingLevelFeet).toLocaleString() + ' ft' : '---'}</span>
                    </div>
                    <div class="hazard-meter-container">
                        <div class="hazard-meter-fill" style="width:100%; background:linear-gradient(90deg, var(--green), var(--cyan));"></div>
                    </div>
                    <div class="hazard-footer">
                        <span>Aerosol / Haze Density:</span>
                        <span>${((s.aerosolDensity || 0) * 100).toFixed(0)}%</span>
                    </div>
                </div>
            </div>

            ${icingLayersHtml ? `
                <div class="card-section" style="margin-top:10px;">
                    <div class="section-title" style="color:var(--cyan);">Icing Altitude Layers</div>
                    ${icingLayersHtml}
                </div>
            ` : ''}

            ${turbLayersHtml ? `
                <div class="card-section" style="margin-top:10px;">
                    <div class="section-title" style="color:var(--amber);">Turbulence Altitude Layers</div>
                    ${turbLayersHtml}
                </div>
            ` : ''}

            ${advisoriesHtml ? `
                <div class="card-section" style="margin-top:10px;">
                    <div class="section-title" style="color:var(--red);">Active In-Flight Advisories</div>
                    ${advisoriesHtml}
                </div>
            ` : ''}
        `;
    }

    renderForecastTab() {
        const s = this.weatherState;
        if (!s) return `<div class="empty-state">No forecast loaded.</div>`;

        if (!s.taf || (!s.taf.rawText && !s.taf.windSpeedKnots && !s.taf.stationId)) {
            return `
                <div class="empty-state">
                    <p style="color:var(--cyan); font-weight:700; font-size:14px; margin-bottom:6px;">Terminal Aerodrome Forecast (TAF)</p>
                    <p style="color:var(--text-secondary); line-height:1.5;">No official TAF publication received for ${s.stationId || 'current position'}.</p>
                    <p style="color:var(--text-muted); font-size:11px; margin-top:8px;">TAFs are published by meteorological authorities every 6 hours for certified aerodromes. Observations are currently tracking live METAR.</p>
                </div>
            `;
        }

        const taf = s.taf;
        const tafStation = taf.stationId || s.stationId || 'LOCAL';
        const tafCat = (taf.flightCategory || 'vfr').toLowerCase();

        let validWindowStr = '';
        if (taf.validFrom || taf.validTo) {
            const fmt = (iso) => {
                if (!iso) return '';
                try {
                    const d = new Date(iso);
                    if (isNaN(d.getTime())) return String(iso);
                    const dd = (d.getUTCDate() < 10 ? '0' : '') + d.getUTCDate();
                    const hh = (d.getUTCHours() < 10 ? '0' : '') + d.getUTCHours();
                    return dd + 'd ' + hh + ':00Z';
                } catch (_) { return String(iso); }
            };
            validWindowStr = fmt(taf.validFrom) + (taf.validTo ? ' &rarr; ' + fmt(taf.validTo) : '');
        }

        const tafDir = Math.round(taf.windDirectionDegrees || 0);
        const tafDirStr = (tafDir < 100 ? (tafDir < 10 ? '00' : '0') : '') + tafDir;
        const tafSpd = Math.round(taf.windSpeedKnots || 0);
        const tafGust = taf.windGustKnots ? (' G' + Math.round(taf.windGustKnots) + 'kt') : '';
        const tafWindStr = taf.windSpeedKnots != null ? (tafDirStr + '&deg; / ' + tafSpd + ' kt' + tafGust) : '---';

        let tafVisStr = '---';
        if (taf.visibilityMeters != null) {
            const sm = (taf.visibilityMeters / 1609.344).toFixed(1);
            const km = (taf.visibilityMeters / 1000).toFixed(1);
            tafVisStr = taf.visibilityMeters >= 9999 ? '10+ SM (10+ km)' : (sm + ' SM (' + km + ' km)');
        }

        let tafCloudsStr = 'Clear / SKC';
        if (Array.isArray(taf.clouds) && taf.clouds.length > 0) {
            tafCloudsStr = taf.clouds.map(c => c.coverage + (c.baseFeet ? (c.baseFeet < 1000 ? '0' : '') + Math.round(c.baseFeet / 100) : '')).join(', ');
        }

        let tafWxStr = 'Nil Significant';
        if (Array.isArray(taf.weatherConditions) && taf.weatherConditions.length > 0) {
            tafWxStr = taf.weatherConditions.join(' ');
        }

        const tempStr = (taf.maxTemperatureCelsius != null || taf.minTemperatureCelsius != null)
            ? `High ${taf.maxTemperatureCelsius != null ? Math.round(taf.maxTemperatureCelsius) + '&deg;C' : '--'} / Low ${taf.minTemperatureCelsius != null ? Math.round(taf.minTemperatureCelsius) + '&deg;C' : '--'}`
            : '';

        return `
            <div class="hero-weather-card">
                <div class="hero-temp-section">
                    <div style="display:flex; align-items:center; gap:8px;">
                        <span style="font-family:var(--font-mono); font-size:18px; font-weight:800; color:#fff;">${tafStation} FORECAST</span>
                        <span class="flight-cat-badge ${tafCat}">${tafCat.toUpperCase()}</span>
                    </div>
                    <span class="hero-temp-sub">${validWindowStr ? ('Validity: ' + validWindowStr) : 'Terminal Forecast Profile'}</span>
                </div>
                ${tempStr ? `<div style="font-family:var(--font-mono); font-size:11px; color:var(--text-secondary);">${tempStr}</div>` : ''}
            </div>

            <div class="card-grid-3">
                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Forecast Wind</span>
                    </div>
                    <div class="metric-value-large">${tafWindStr}</div>
                    <div class="metric-sub">Expected Surface Flow</div>
                </div>

                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Forecast Visibility</span>
                    </div>
                    <div class="metric-value-large">${tafVisStr}</div>
                    <div class="metric-sub">Surface Sight Distance</div>
                </div>

                <div class="metric-card">
                    <div class="metric-card-header">
                        <span class="metric-label">Weather & Clouds</span>
                    </div>
                    <div class="metric-value-large" style="font-size:12px;">${tafCloudsStr}</div>
                    <div class="metric-sub">${tafWxStr}</div>
                </div>
            </div>

            <div class="raw-metar-card" style="margin-top:8px;">
                <div class="raw-metar-header">
                    <span class="raw-metar-title">Terminal Aerodrome Forecast Briefing</span>
                    <button class="hud-btn" id="btn-copy-taf">Copy TAF</button>
                </div>
                <div class="raw-metar-text">${taf.rawText || 'No raw TAF text provided.'}</div>
            </div>
        `;
    }

    attachForecastEvents() {
        const copyBtn = this.querySelector("#btn-copy-taf");
        if (copyBtn) {
            copyBtn.onclick = (e) => {
                e.preventDefault();
                const tafText = this.weatherState && this.weatherState.taf ? (this.weatherState.taf.rawText || "") : "";
                if (navigator.clipboard && tafText) {
                    navigator.clipboard.writeText(tafText);
                    copyBtn.textContent = "Copied!";
                    setTimeout(() => { copyBtn.textContent = "Copy TAF"; }, 1500);
                }
            };
        }
    }

    renderDiagnosticsTab() {
        const history = Array.isArray(this.history) ? this.history : [];
        const filter = this.logFilter || "all";
        const filtered = history.filter(h => {
            if (filter === "all") return true;
            if (filter === "errors") return h.state === "error";
            if (filter === "events") return h.state === "event" || h.state === "acked" || h.state === "ok";
            return true;
        });

        let logLines = "";
        for (let i = 0; i < filtered.length; i++) {
            const h = filtered[i];
            logLines += `<div class="log-line ${h.state}">
                <span class="log-time">${h.time}</span>
                <span class="log-tag">[${h.state.toUpperCase()}]</span>
                <span class="log-text">${h.text}</span>
            </div>`;
        }

        return `
            <div class="diag-header-actions">
                <div class="filter-group">
                    <button class="log-filter-btn ${this.logFilter === 'all' ? 'active' : ''}" data-filter="all">All</button>
                    <button class="log-filter-btn ${this.logFilter === 'events' ? 'active' : ''}" data-filter="events">Events</button>
                    <button class="log-filter-btn ${this.logFilter === 'errors' ? 'active' : ''}" data-filter="errors">Errors</button>
                </div>
                <div class="action-group">
                    <button class="hud-btn" id="btn-copy-log">Copy Log</button>
                    <button class="hud-btn" id="btn-clear-log">Clear</button>
                </div>
            </div>

            <div class="card-grid-3" style="margin-bottom:8px;">
                <div class="metric-card">
                    <span class="metric-label">Injected Events</span>
                    <span class="metric-value-large">${this.eventCount}</span>
                    <span class="metric-sub">Weather Packets</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">ACK Count</span>
                    <span class="metric-value-large" style="color:var(--green);">${this.ackCount}</span>
                    <span class="metric-sub">Sim Handshakes</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Faults / Errors</span>
                    <span class="metric-value-large" style="color:${this.errorCount > 0 ? 'var(--red)' : 'var(--green)'};">${this.errorCount}</span>
                    <span class="metric-sub">${this.errorCount === 0 ? 'Nominal' : 'Requires Attention'}</span>
                </div>
            </div>

            <div class="card-section" style="margin-bottom:8px; padding:6px 10px;">
                <div style="display:flex; justify-content:space-between; font-size:10px; color:var(--text-secondary); font-family:var(--font-mono);">
                    <span>Bridge: CommBus v1</span>
                    <span>Preset: ${this.currentPreset ? (this.currentPreset.name || 'Active') : 'Waiting'}</span>
                    <span>Smoothing: ${this.smoothingFactor || 0.2}</span>
                </div>
            </div>

            <div class="log-console" id="bridge-log-console">
                ${logLines || '<div style="color:var(--text-muted); text-align:center; padding:10px;">Log empty</div>'}
            </div>
        `;
    }

    attachDiagEvents() {
        const filterBtns = this.querySelectorAll(".log-filter-btn");
        for (let i = 0; i < filterBtns.length; i++) {
            filterBtns[i].onclick = (e) => {
                e.preventDefault();
                this.logFilter = e.currentTarget.getAttribute("data-filter") || "all";
                this.renderActiveTab();
            };
        }

        const copyLogBtn = this.querySelector("#btn-copy-log");
        if (copyLogBtn) {
            copyLogBtn.onclick = (e) => {
                e.preventDefault();
                let text = "";
                for (let i = 0; i < this.history.length; i++) {
                    text += this.history[i].time + " [" + this.history[i].state.toUpperCase() + "] " + this.history[i].text + "\n";
                }
                if (navigator.clipboard) {
                    navigator.clipboard.writeText(text);
                    copyLogBtn.textContent = "Copied!";
                    setTimeout(() => { copyLogBtn.textContent = "Copy Log"; }, 1500);
                }
            };
        }

        const clearLogBtn = this.querySelector("#btn-clear-log");
        if (clearLogBtn) {
            clearLogBtn.onclick = (e) => {
                e.preventDefault();
                this.history = [];
                this.renderActiveTab();
            };
        }
    }

    addLog(state, text) {
        const time = new Date().toISOString().substr(11, 8);
        this.history.unshift({ time: time, state: state, text: text });
        if (this.history.length > 50) this.history.pop();

        if (this.activeTab === "diagnostics") {
            const consoleEl = this.querySelector("#bridge-log-console");
            if (consoleEl) {
                const div = document.createElement("div");
                div.className = "log-line " + state;
                div.innerHTML = `<span class="log-time">${time}</span> <span class="log-tag">[${state.toUpperCase()}]</span> <span class="log-text">${text}</span>`;
                consoleEl.insertBefore(div, consoleEl.firstChild);
            }
        }
    }

    errorText(error) {
        return error && error.message ? error.message : String(error);
    }

    makeDataValue(name, value, unit, min, max) {
        return {
            __Type: "RangeDataValue",
            ID: String(name || ""),
            type: "range",
            userTag: "",
            ttsText: "",
            step: 1,
            percent: 0,
            name: name,
            value: Number(value),
            valueStr: String(value),
            unit: unit || "",
            html: "",
            icon: "",
            min: min != null ? min : -99999,
            max: max != null ? max : 99999,
            clamp_min: min != null ? min : -99999,
            clamp_max: max != null ? max : 99999
        };
    }

    createDefaultPresetTemplate() {
        return {
            __Type: "WeatherPresetData",
            index: 1,
            sPresetName: "SkyWeave Custom",
            name: "SkyWeave Custom",
            bIsLive: false,
            bIsValid: true,
            bIsRemovable: false,
            bIsAltitudeAMGL: false,
            bWindFromDeparture: false,
            oConfig: {
                __Type: "WeatherPresetConfigData"
            },
            oSettings: {
                __Type: "WeatherPresetSettingData",
                dvMSLGLTemperature: this.makeDataValue("dvMSLGLTemperature", 59, "°F", -100, 150),
                dvMSLTemperature: this.makeDataValue("dvMSLTemperature", 59, "°F", -100, 150),
                dvTemperature: this.makeDataValue("dvTemperature", 59, "°F", -100, 150),
                dvMSLPressure: this.makeDataValue("dvMSLPressure", 29.92, "inHg", 26, 32),
                dvPressure: this.makeDataValue("dvPressure", 29.92, "inHg", 26, 32),
                dvPrecipitation: this.makeDataValue("dvPrecipitation", 0, "mm/h", 0, 100),
                dvThunderstormRatio: this.makeDataValue("dvThunderstormRatio", 0, "%", 0, 1),
                dvPollution: this.makeDataValue("dvPollution", 0, "aerosol", 0, 1),
                dvHumidityMultiplier: this.makeDataValue("dvHumidityMultiplier", 0.5, "ratio", 0, 1),
                dvSnowCover: this.makeDataValue("dvSnowCover", 0, "m", 0, 100),
                dvSnowCoverMultiplier: this.makeDataValue("dvSnowCoverMultiplier", 0, "m", 0, 100)
            },
            tCloudLayers: [
                {
                    __Type: "CloudLayerData",
                    dvDensityMultiplier: this.makeDataValue("dvDensityMultiplier", 0, "ratio"),
                    dvCoverageRatio: this.makeDataValue("dvCoverageRatio", 0, "ratio"),
                    dvCloudScatteringRatio: this.makeDataValue("dvCloudScatteringRatio", 1, "ratio"),
                    dvAltitudeBot: this.makeDataValue("dvAltitudeBot", 3000, "ft"),
                    dvAltitudeTop: this.makeDataValue("dvAltitudeTop", 6000, "ft")
                },
                {
                    __Type: "CloudLayerData",
                    dvDensityMultiplier: this.makeDataValue("dvDensityMultiplier", 0, "ratio"),
                    dvCoverageRatio: this.makeDataValue("dvCoverageRatio", 0, "ratio"),
                    dvCloudScatteringRatio: this.makeDataValue("dvCloudScatteringRatio", 1, "ratio"),
                    dvAltitudeBot: this.makeDataValue("dvAltitudeBot", 8000, "ft"),
                    dvAltitudeTop: this.makeDataValue("dvAltitudeTop", 12000, "ft")
                },
                {
                    __Type: "CloudLayerData",
                    dvDensityMultiplier: this.makeDataValue("dvDensityMultiplier", 0, "ratio"),
                    dvCoverageRatio: this.makeDataValue("dvCoverageRatio", 0, "ratio"),
                    dvCloudScatteringRatio: this.makeDataValue("dvCloudScatteringRatio", 1, "ratio"),
                    dvAltitudeBot: this.makeDataValue("dvAltitudeBot", 18000, "ft"),
                    dvAltitudeTop: this.makeDataValue("dvAltitudeTop", 24000, "ft")
                }
            ],
            tWindLayers: [
                {
                    __Type: "WindLayerData",
                    dvAltitude: this.makeDataValue("dvAltitude", 0, "ft"),
                    dvAngleRad: this.makeDataValue("dvAngleRad", 0, "rad"),
                    dvSpeed: this.makeDataValue("dvSpeed", 0, "knots"),
                    gustWaveData: {
                        __Type: "GustWaveData",
                        dvSpeedMultiplier: this.makeDataValue("dvSpeedMultiplier", 0, "ratio"),
                        dvAngleRad: this.makeDataValue("dvAngleRad", 0, "rad"),
                        dvInterval: this.makeDataValue("dvInterval", 60, "s"),
                        dvIntervalS: this.makeDataValue("dvIntervalS", 60, "s")
                    }
                }
            ]
        };
    }

    createWeatherPreset(state) {
        const base = this.currentPreset || this.createDefaultPresetTemplate();
        const preset = JSON.parse(JSON.stringify(base));
        preset.__Type = "WeatherPresetData";
        preset.index = typeof preset.index === "number" ? preset.index : 1;
        preset.sPresetName = preset.sPresetName || preset.name || "SkyWeave Custom";
        preset.bIsLive = false;
        preset.bIsValid = true;
        preset.bIsRemovable = false;
        preset.bIsAltitudeAMGL = false;
        preset.bWindFromDeparture = false;
        if (!preset.oConfig || typeof preset.oConfig !== "object") {
            preset.oConfig = { __Type: "WeatherPresetConfigData" };
        }
        if (!preset.oSettings || typeof preset.oSettings !== "object") {
            preset.oSettings = {};
        }
        preset.oSettings.__Type = "WeatherPresetSettingData";
        const settings = preset.oSettings;

        // Ensure proper units for MSFS native preset engine (Fahrenheit & inHg)
        this.ensureDataValue(settings, "dvMSLGLTemperature", state.temperatureCelsius, "°F");
        this.ensureDataValue(settings, "dvMSLTemperature", state.temperatureCelsius, "°F");
        this.ensureDataValue(settings, "dvTemperature", state.temperatureCelsius, "°F");
        this.ensureDataValue(settings, "dvMSLPressure", state.altimeterHpa, "inHg");
        this.ensureDataValue(settings, "dvPressure", state.altimeterHpa, "inHg");
        this.ensureDataValue(settings, "dvPrecipitation", state.precipitationRate, "mm/h");
        this.ensureDataValue(settings, "dvThunderstormRatio", state.thunderstormIntensity, "%");
        this.ensureDataValue(settings, "dvPollution", state.aerosolDensity, "aerosol");
        this.ensureDataValue(settings, "dvHumidityMultiplier", state.humidityPercent / 100, "ratio");

        settings.bIsAltitudeAMGL = false;
        const feetToMeters = 0.3048;
        const clouds = Array.isArray(state.cloudLayers) ? state.cloudLayers : [];
        if (clouds.length > 0) {
            preset.tCloudLayers = clouds.map((source, index) => {
                if (!Number.isFinite(source.baseMeters) || !Number.isFinite(source.topMeters) ||
                    source.baseMeters < 0 || source.topMeters <= source.baseMeters) {
                    throw new Error('Cloud layer requires valid MSL baseMeters/topMeters');
                }
                const layer = {
                    __Type: "CloudLayerData"
                };
                this.ensureDataValue(layer, "dvDensityMultiplier", source.density != null ? source.density : 1.0, "ratio");
                this.ensureDataValue(layer, "dvCoverageRatio", source.coveragePercent != null ? (source.coveragePercent > 1 ? source.coveragePercent / 100 : source.coveragePercent) : 0.5, "ratio");
                this.ensureDataValue(layer, "dvCloudScatteringRatio", source.scattering != null ? source.scattering : 1.0, "ratio");
                this.ensureDataValue(layer, "dvAltitudeBot", source.baseMeters / feetToMeters, "ft");
                this.ensureDataValue(layer, "dvAltitudeTop", source.topMeters / feetToMeters, "ft");
                return layer;
            });
        } else {
            preset.tCloudLayers = [{}, {}, {}].map((layer, index) => {
                layer.__Type = "CloudLayerData";
                this.ensureDataValue(layer, "dvDensityMultiplier", 0, "ratio");
                this.ensureDataValue(layer, "dvCoverageRatio", 0, "ratio");
                this.ensureDataValue(layer, "dvCloudScatteringRatio", 1, "ratio");
                this.ensureDataValue(layer, "dvAltitudeBot", 3000 * (index + 1), "ft");
                this.ensureDataValue(layer, "dvAltitudeTop", 6000 * (index + 1), "ft");
                return layer;
            });
        }

        const winds = Array.isArray(state.windsAloft) ? state.windsAloft : [];
        if (winds.length > 0) {
            preset.tWindLayers = winds.map((source, index) => {
                const layer = {
                    __Type: "WindLayerData"
                };
                this.ensureDataValue(layer, "dvAltitude", source.altitudeFeet != null ? source.altitudeFeet : 0, "ft");
                this.ensureDataValue(layer, "dvAngleRad", (source.directionDegrees || 0) * Math.PI / 180, "rad");
                this.ensureDataValue(layer, "dvSpeed", source.speedKnots || 0, "knots");
                layer.gustWaveData = { __Type: "GustWaveData" };
                const gust = source.gustSpeedKnots || 0;
                this.ensureDataValue(layer.gustWaveData, "dvSpeedMultiplier",
                    source.speedKnots > 0 ? gust / source.speedKnots : 0, "ratio");
                this.ensureDataValue(layer.gustWaveData, "dvAngleRad",
                    (source.gustDirectionDegrees != null ? source.gustDirectionDegrees : source.directionDegrees || 0) * Math.PI / 180, "rad");
                return layer;
            });
        } else {
            preset.tWindLayers = [{}].map(layer => {
                layer.__Type = "WindLayerData";
                this.ensureDataValue(layer, "dvAltitude", 0, "ft");
                this.ensureDataValue(layer, "dvAngleRad", 0, "rad");
                this.ensureDataValue(layer, "dvSpeed", 0, "knots");
                return layer;
            });
        }

        return preset;
    }

    ensureDataValue(container, key, value, defaultUnit) {
        if (!container[key] || typeof container[key] !== "object") {
            container[key] = this.makeDataValue(key, value, defaultUnit);
        }
        const target = container[key];
        const u = (target.unit || defaultUnit || "").toLowerCase().trim();
        const num = Number(value);
        let finalVal = num;

        if (key.toLowerCase().indexOf("pressure") !== -1) {
            let hpa = num;
            if (hpa > 2000) hpa = hpa / 100; // Passed in Pa
            if (hpa < 50) hpa = hpa / 0.029529983; // Passed in inHg

            if (u === "pa" || u === "pascal" || u === "pascals") {
                finalVal = hpa * 100;
                target.unit = "Pa";
            } else if (u === "hpa" || u === "mbar" || u === "millibar" || u === "millibars") {
                finalVal = hpa;
                target.unit = "hPa";
            } else {
                // MSFS native preset settings default to inHg (29.92 inHg = 1013.25 hPa)
                // Passing hPa directly causes MSFS to clamp to 32.00 inHg (= 1084 hPa)
                finalVal = hpa * 0.029529983;
                target.unit = "inHg";
            }
        } else if (key.toLowerCase().indexOf("temp") !== -1) {
            let c = num;
            if (u.indexOf("f") !== -1 || u.indexOf("°f") !== -1) {
                finalVal = c * 9 / 5 + 32;
                target.unit = "°F";
            } else if (u.indexOf("k") !== -1) {
                finalVal = c + 273.15;
                target.unit = "K";
            } else {
                finalVal = c;
                target.unit = "°C";
            }
        }

        this.setValue(target, finalVal, target.unit || defaultUnit);
    }

    setValue(target, value, unit) {
        if (target && Number.isFinite(Number(value))) {
            const num = Number(value);
            target.value = num;
            target.valueStr = String(num);
            if (target.min !== undefined && num < target.min) target.min = num;
            if (target.max !== undefined && num > target.max) target.max = num;
            if (target.clamp_min !== undefined && num < target.clamp_min) target.clamp_min = num;
            if (target.clamp_max !== undefined && num > target.clamp_max) target.clamp_max = num;
            if (unit && !target.unit) target.unit = unit;
        }
    }
}

class IngamePanelSkyWeave extends (typeof TemplateElement !== "undefined" ? TemplateElement : HTMLElement) {
    constructor() {
        super(...arguments);
        this.ingameUi = null;
    }

    connectedCallback() {
        if (typeof super.connectedCallback === "function") {
            try {
                super.connectedCallback();
            } catch (e) {
                console.error("[SkyWeave] TemplateElement connectedCallback error:", e);
            }
        }
        this.ingameUi = this.querySelector("ingame-ui");
        if (this.ingameUi) {
            this.ingameUi.classList.remove("panelInvisible");
            this.ingameUi.addEventListener("panelActive", () => {
                if (this.ingameUi) this.ingameUi.classList.remove("panelInvisible");
            });
            this.ingameUi.addEventListener("panelInactive", () => {
                // panel closed or hidden
            });
        }
    }
}

if (typeof window !== "undefined" && window.customElements) {
    window.customElements.define("ingamepanel-skyweave", IngamePanelSkyWeave);
}

customElements.define("skyweave-weather-bridge", SkyWeaveWeatherBridgeElement);

function ensureBridgeMounted() {
    if (!document.querySelector("skyweave-weather-bridge")) {
        const parent = document.querySelector(".ingameUiContent") || document.querySelector("ingame-ui") || document.body;
        const el = document.createElement("skyweave-weather-bridge");
        parent.appendChild(el);
    }
}

if (typeof checkAutoload === "function") {
    try { checkAutoload(); } catch (e) {}
}
ensureBridgeMounted();
