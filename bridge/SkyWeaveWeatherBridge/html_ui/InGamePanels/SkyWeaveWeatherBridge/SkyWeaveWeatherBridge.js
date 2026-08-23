class SkyWeaveWeatherBridgeElement extends HTMLElement {
    connectedCallback() {
        if (this._initialized) return;
        this._initialized = true;

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

        const lerp = (a, b, factor) => a + (b - a) * factor;
        const lerpKey = (objA, objB, key, factor) => {
            if (objA[key] && objB[key] && typeof objA[key].value === "number" && typeof objB[key].value === "number") {
                const diff = objB[key].value - objA[key].value;
                if (Math.abs(diff) > 0.001) {
                    this.setValue(objA[key], lerp(objA[key].value, objB[key].value, factor));
                    modified = true;
                }
            }
        };

        const f = this.smoothingFactor || 0.2;
        if (current.oSettings && target.oSettings) {
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

        if (Array.isArray(current.tCloudLayers) && Array.isArray(target.tCloudLayers)) {
            const count = Math.min(current.tCloudLayers.length, target.tCloudLayers.length);
            for (let i = 0; i < count; i++) {
                const cLayer = current.tCloudLayers[i];
                const tLayer = target.tCloudLayers[i];
                if (cLayer && tLayer) {
                    lerpKey(cLayer, tLayer, "dvCoverageRatio", f);
                    lerpKey(cLayer, tLayer, "dvDensityMultiplier", f);
                    lerpKey(cLayer, tLayer, "dvCloudScatteringRatio", f);
                    lerpKey(cLayer, tLayer, "dvAltitudeBot", f);
                    lerpKey(cLayer, tLayer, "dvAltitudeTop", f);
                }
            }
        }

        if (Array.isArray(current.tWindLayers) && Array.isArray(target.tWindLayers)) {
            const count = Math.min(current.tWindLayers.length, target.tWindLayers.length);
            for (let i = 0; i < count; i++) {
                const cWind = current.tWindLayers[i];
                const tWind = target.tWindLayers[i];
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
            const tempC = s && s.temperatureCelsius != null ? s.temperatureCelsius.toFixed(1) + "°C" : "--°C";
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
                        <span class="hud-val">${dirStr}°/${windSpd}kt${windGust}</span>
                        <span class="hud-val">QNH ${qnhInHg} <span class="hud-sub">(${qnhHpa})</span></span>
                        <span class="flight-cat-badge ${cat}">${cat.toUpperCase()}</span>
                    </div>
                    <div class="mini-hud-right">
                        <button class="hud-btn btn-expand" id="btn-expand-panel" title="Expand Full Avionics">↗ EXPAND</button>
                    </div>
                </div>
            `;

            const expandBtn = this.querySelector("#btn-expand-panel");
            if (expandBtn) {
                expandBtn.addEventListener("click", (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    this.isMinimized = false;
                    this.renderShell();
                });
            }
            this.setupDraggable();
            return;
        }

        this.className = "expanded-mode";
        this.innerHTML = `
            <div class="bridge-header drag-handle">
                <div class="brand-section">
                    <div class="brand-logo">W</div>
                    <span class="brand-title">SkyWeave</span>
                    <span class="brand-badge">SimBridge v0.4</span>
                </div>
                <div class="header-right">
                    <span class="zulu-clock">--:--:--Z</span>
                    <div class="status-badge ${this.currentStatusType || (this.weatherState ? 'injected' : 'ready')}">
                        <span class="status-dot"></span>
                        <span class="status-text">${this.currentStatusText || (this.weatherState ? 'INJECTED' : 'READY')}</span>
                    </div>
                    <button class="hud-btn btn-minimize" id="btn-minimize-panel" title="Minimize to Floating Background HUD">— HUD</button>
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
                <button class="tab-btn ${this.activeTab === "overview" ? "active" : ""}" data-tab="overview">Overview</button>
                <button class="tab-btn ${this.activeTab === "atmosphere" ? "active" : ""}" data-tab="atmosphere">Atmosphere</button>
                <button class="tab-btn ${this.activeTab === "hazards" ? "active" : ""}" data-tab="hazards">Hazards</button>
                <button class="tab-btn ${this.activeTab === "forecast" ? "active" : ""}" data-tab="forecast">Forecast</button>
                <button class="tab-btn ${this.activeTab === "diagnostics" ? "active" : ""}" data-tab="diagnostics">Diagnostics</button>
            </div>

            <div class="bridge-body" id="tab-content-area">
                ${this.getActiveTabHtml()}
            </div>
        `;

        const minBtn = this.querySelector("#btn-minimize-panel");
        if (minBtn) {
            minBtn.addEventListener("click", (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.isMinimized = true;
                this.renderShell();
            });
        }

        this.setupTabEvents();
        this.attachTabEvents();
        this.renderStationBar();
        this.setupTabEvents();
    }

    setupDraggable() {
        // Native MSFS window manager handles InGamePanel positioning & Right-Alt Popout
    }

    setupTabEvents() {
        const tabs = this.querySelectorAll(".tab-btn");
        for (let i = 0; i < tabs.length; i++) {
            const btn = tabs[i];
            btn.addEventListener("click", (e) => {
                e.preventDefault();
                e.stopPropagation();
                const tab = btn.getAttribute("data-tab");
                if (tab) {
                    this.switchTab(tab);
                }
            });
        }
    }

    switchTab(tabName) {
        if (!tabName) return;
        this.activeTab = tabName;
        const tabs = this.querySelectorAll(".tab-btn");
        for (let i = 0; i < tabs.length; i++) {
            tabs[i].classList.toggle("active", tabs[i].getAttribute("data-tab") === tabName);
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
        container.innerHTML = this.getActiveTabHtml();
        this.attachTabEvents();
    }

    attachTabEvents() {
        if (this.activeTab === "overview") this.attachOverviewEvents();
        else if (this.activeTab === "diagnostics") this.attachDiagEvents();
    }

    renderStationBar() {
        if (!this.weatherState) return;
        const icaoEl = this.querySelector(".station-icao");
        const detailsEl = this.querySelector(".station-details");
        const catEl = this.querySelector(".flight-cat-badge");

        if (icaoEl) icaoEl.textContent = this.weatherState.stationId || "LOCAL";
        if (detailsEl) {
            const lat = this.weatherState.latitude ? this.weatherState.latitude.toFixed(2) + "&deg;" : "";
            const lon = this.weatherState.longitude ? this.weatherState.longitude.toFixed(2) + "&deg;" : "";
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
        const ceiling = s.ceilingFeet && s.ceilingFeet < 25000 ? (Math.round(s.ceilingFeet) + " ft") : "Unlimited";
        const freezeLvl = s.freezingLevelFeet ? (Math.round(s.freezingLevelFeet) + " ft") : "---";

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
                <div class="raw-metar-text">${this.rawMetar || "No raw METAR string available."}</div>
            </div>
        `;
    }

    attachOverviewEvents() {
        const copyBtn = this.querySelector("#btn-copy-metar");
        if (copyBtn) {
            copyBtn.addEventListener("click", () => {
                if (navigator.clipboard && this.rawMetar) {
                    navigator.clipboard.writeText(this.rawMetar);
                    copyBtn.textContent = "Copied!";
                    setTimeout(() => { copyBtn.textContent = "Copy METAR"; }, 1500);
                }
            });
        }
    }

    renderAtmosphereTab() {
        const s = this.weatherState;
        if (!s) return `<div class="empty-state">No atmospheric data loaded.</div>`;

        const winds = Array.isArray(s.windsAloft) ? s.windsAloft : [];
        const clouds = Array.isArray(s.cloudLayers) ? s.cloudLayers : [];

        let windsHtml = '<table class="layer-table"><thead><tr><th>Altitude</th><th>Dir / Spd</th><th>Visual</th><th>Gust</th></tr></thead><tbody>';
        for (let i = 0; i < winds.length; i++) {
            const w = winds[i];
            const alt = w.altitudeFeet === 0 ? "Surface" : (w.altitudeFeet.toLocaleString() + " ft");
            const dir = Math.round(w.directionDegrees);
            const spd = Math.round(w.speedKnots);
            const pct = Math.min(100, Math.round((spd / 120) * 100));
            const gust = w.gustSpeedKnots ? (Math.round(w.gustSpeedKnots) + " kt") : "---";
            const dirStr = (dir < 100 ? (dir < 10 ? "00" : "0") : "") + dir;

            windsHtml += `
                <tr>
                    <td>${alt}</td>
                    <td><strong>${dirStr}&deg;</strong> @ ${spd} kt</td>
                    <td>
                        <div class="progress-bar-bg" style="width:70px;">
                            <div class="progress-bar-fill" style="width:${pct}%;"></div>
                        </div>
                    </td>
                    <td>${gust}</td>
                </tr>
            `;
        }
        windsHtml += '</tbody></table>';

        let cloudsHtml = '<table class="layer-table"><thead><tr><th>Layer</th><th>Coverage</th><th>Base (AGL)</th><th>Top (AGL)</th><th>Density</th></tr></thead><tbody>';
        if (clouds.length === 0) {
            cloudsHtml += '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">Clear Skies (SKC / NSC)</td></tr>';
        } else {
            for (let i = 0; i < clouds.length; i++) {
                const c = clouds[i];
                const cov = c.coveragePercent != null ? (Math.round(c.coveragePercent) + "%") : "---";
                const base = Math.round(c.baseFeetAgl).toLocaleString() + " ft";
                const top = Math.round(c.topFeetAgl).toLocaleString() + " ft";
                const density = c.density != null ? (Math.round(c.density * 100) + "%") : "---";

                cloudsHtml += `
                    <tr>
                        <td>Layer #${i + 1}</td>
                        <td><span class="flight-cat-badge mvfr">${cov}</span></td>
                        <td>${base}</td>
                        <td>${top}</td>
                        <td>${density}</td>
                    </tr>
                `;
            }
        }
        cloudsHtml += '</tbody></table>';

        return `
            <div class="card-section">
                <div class="section-title">Winds & Temperatures Aloft Profile</div>
                ${windsHtml}
            </div>

            <div class="card-section" style="margin-top:12px;">
                <div class="section-title">Synthesized Cloud Stratification (MSFS Layers)</div>
                ${cloudsHtml}
            </div>
        `;
    }

    renderHazardsTab() {
        const s = this.weatherState;
        if (!s) return `<div class="empty-state">No weather hazard data loaded.</div>`;

        const turbVal = (s.turbulenceIndex || 0) * 100;
        const icingVal = (s.icingRiskIndex || 0) * 100;
        const stormVal = (s.thunderstormIntensity || 0) * 100;
        const catVal = (s.catRiskIndex || 0) * 100;

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
                        <span>Intensity: ${turbVal.toFixed(0)}%</span>
                        <span>Mountain / Convective / Wake</span>
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
                        <span>Risk Index: ${icingVal.toFixed(0)}%</span>
                        <span>Supercooled Liquid Range</span>
                    </div>
                </div>
            </div>

            <div class="card-grid-2" style="margin-top:10px;">
                <div class="hazard-card ${stormVal > 30 ? 'danger' : stormVal > 5 ? 'warning' : 'safe'}">
                    <div class="hazard-header">
                        <span class="hazard-title">Thunderstorm & Lightning</span>
                        <span class="hazard-badge">${stormVal > 30 ? 'ACTIVE' : stormVal > 5 ? 'VCTS' : 'NONE'}</span>
                    </div>
                    <div class="hazard-meter-container">
                        <div class="hazard-meter-fill" style="width:${stormVal}%;"></div>
                    </div>
                    <div class="hazard-footer">
                        <span>CAPE / Convective Storms: ${stormVal.toFixed(0)}%</span>
                        <span>Lightning Flash Probability</span>
                    </div>
                </div>

                <div class="hazard-card ${catVal > 40 ? 'danger' : catVal > 15 ? 'warning' : 'safe'}">
                    <div class="hazard-header">
                        <span class="hazard-title">Clear Air Turbulence (CAT)</span>
                        <span class="hazard-badge">${catVal > 40 ? 'JETSTREAM' : catVal > 15 ? 'SHEAR' : 'SMOOTH'}</span>
                    </div>
                    <div class="hazard-meter-container">
                        <div class="hazard-meter-fill" style="width:${catVal}%;"></div>
                    </div>
                    <div class="hazard-footer">
                        <span>Upper Shear Risk: ${catVal.toFixed(0)}%</span>
                        <span>Tropopause Gradient</span>
                    </div>
                </div>
            </div>
        `;
    }

    renderForecastTab() {
        const s = this.weatherState;
        if (!s) return `<div class="empty-state">No forecast loaded.</div>`;

        const changes = Array.isArray(s.tafForecast) ? s.tafForecast : [];
        if (changes.length === 0) {
            return `
                <div class="empty-state">
                    <p style="color:var(--cyan); font-weight:600;">No Terminal Area Forecast (TAF) Change Groups</p>
                    <p style="color:var(--text-secondary); margin-top:4px;">Weather is expected to remain consistent with current observations.</p>
                </div>
            `;
        }

        let html = '<div class="forecast-timeline">';
        for (let i = 0; i < changes.length; i++) {
            const ch = changes[i];
            const type = ch.changeType || "FM";
            const time = ch.timeWindow || "---";
            const wind = ch.wind || "---";
            const vis = ch.visibility || "---";
            const cwg = ch.clouds || "---";

            html += `
                <div class="timeline-item">
                    <div class="timeline-badge">${type}</div>
                    <div class="timeline-content">
                        <div class="timeline-time">${time}</div>
                        <div class="timeline-details">
                            <span>Wind: <strong>${wind}</strong></span>
                            <span>Vis: <strong>${vis}</strong></span>
                            <span>Clouds: <strong>${cwg}</strong></span>
                        </div>
                    </div>
                </div>
            `;
        }
        html += '</div>';
        return html;
    }

    renderDiagnosticsTab() {
        const filtered = this.history.filter(h => {
            if (this.logFilter === "all") return true;
            if (this.logFilter === "errors") return h.state === "error";
            if (this.logFilter === "events") return h.state === "event" || h.state === "acked" || h.state === "ok";
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

            <div class="card-grid-3" style="margin-bottom:10px;">
                <div class="metric-card">
                    <span class="metric-label">Injected Events</span>
                    <span class="metric-value">${this.eventCount}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">ACK Count</span>
                    <span class="metric-value" style="color:var(--green);">${this.ackCount}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Errors</span>
                    <span class="metric-value" style="color:${this.errorCount > 0 ? 'var(--red)' : 'var(--green)'}">${this.errorCount}</span>
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
            filterBtns[i].addEventListener("click", (e) => {
                this.logFilter = e.currentTarget.getAttribute("data-filter");
                this.renderActiveTab();
            });
        }

        const copyLogBtn = this.querySelector("#btn-copy-log");
        if (copyLogBtn) {
            copyLogBtn.addEventListener("click", () => {
                let text = "";
                for (let i = 0; i < this.history.length; i++) {
                    text += this.history[i].time + " [" + this.history[i].state.toUpperCase() + "] " + this.history[i].text + "\n";
                }
                if (navigator.clipboard) {
                    navigator.clipboard.writeText(text);
                    copyLogBtn.textContent = "Copied!";
                    setTimeout(() => { copyLogBtn.textContent = "Copy Log"; }, 1500);
                }
            });
        }

        const clearLogBtn = this.querySelector("#btn-clear-log");
        if (clearLogBtn) {
            clearLogBtn.addEventListener("click", () => {
                this.history = [];
                this.renderActiveTab();
            });
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

        const clouds = Array.isArray(state.cloudLayers) ? state.cloudLayers : [];
        if (clouds.length > 0) {
            preset.tCloudLayers = clouds.map((source, index) => {
                const layer = {
                    __Type: "CloudLayerData"
                };
                this.ensureDataValue(layer, "dvDensityMultiplier", source.density != null ? source.density : 1.0, "ratio");
                this.ensureDataValue(layer, "dvCoverageRatio", source.coveragePercent != null ? (source.coveragePercent > 1 ? source.coveragePercent / 100 : source.coveragePercent) : 0.5, "ratio");
                this.ensureDataValue(layer, "dvCloudScatteringRatio", source.scattering != null ? source.scattering : 1.0, "ratio");
                this.ensureDataValue(layer, "dvAltitudeBot", source.baseFeetAgl != null ? source.baseFeetAgl : 3000 * (index + 1), "ft");
                this.ensureDataValue(layer, "dvAltitudeTop", source.topFeetAgl != null ? source.topFeetAgl : 6000 * (index + 1), "ft");
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

customElements.define("skyweave-weather-bridge", SkyWeaveWeatherBridgeElement);

function checkAutoload() {
    if (!document.querySelector("skyweave-weather-bridge")) {
        const el = document.createElement("skyweave-weather-bridge");
        document.body.appendChild(el);
    }
}
checkAutoload();
