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
            const clockEl = this.querySelector(".zulu-clock");
            if (clockEl) {
                const now = new Date();
                const hh = (now.getUTCHours() < 10 ? "0" : "") + now.getUTCHours();
                const mm = (now.getUTCMinutes() < 10 ? "0" : "") + now.getUTCMinutes();
                const ss = (now.getUTCSeconds() < 10 ? "0" : "") + now.getUTCSeconds();
                clockEl.textContent = hh + ":" + mm + ":" + ss + "Z";
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
            const qnh = message.state.altimeterHpa ? message.state.altimeterHpa.toFixed(1) : "---";
            const temp = message.state.temperatureCelsius != null ? message.state.temperatureCelsius.toFixed(1) : "--";
            this.addLog("event", "Apply #" + this.eventCount + " (" + station + ") - QNH " + qnh + " hPa, " + temp + "°C");

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

            this.renderActiveTab();
            this.renderStationBar();
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
                if (Math.abs(diff) > 0.01) {
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

        if (modified && this.weatherListener) {
            this.weatherListener.updateTempWeatherPreset(current, () => {}, () => {});
        }
    }

    renderShell() {
        const bootstrap = document.getElementById("skyweave-bootstrap-status");
        if (bootstrap) bootstrap.style.display = "none";

        this.innerHTML = `
            <div class="bridge-header">
                <div class="brand-section">
                    <div class="brand-logo">W</div>
                    <span class="brand-title">SkyWeave</span>
                    <span class="brand-badge">SimBridge v0.4</span>
                </div>
                <div class="header-right">
                    <span class="zulu-clock">--:--:--Z</span>
                    <div class="status-badge ready">
                        <span class="status-dot"></span>
                        <span class="status-text">READY</span>
                    </div>
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
                <button class="tab-btn active" data-tab="overview">Overview</button>
                <button class="tab-btn" data-tab="atmosphere">Atmosphere</button>
                <button class="tab-btn" data-tab="hazards">Hazards</button>
                <button class="tab-btn" data-tab="forecast">Forecast</button>
                <button class="tab-btn" data-tab="diagnostics">Diagnostics</button>
            </div>

            <div class="bridge-body" id="tab-content-area">
                ${this.renderOverviewTab()}
            </div>
        `;

        this.setupTabEvents();
    }

    setupTabEvents() {
        const tabs = this.querySelectorAll(".tab-btn");
        for (let i = 0; i < tabs.length; i++) {
            const btn = tabs[i];
            const onTabActivate = (e) => {
                e.preventDefault();
                e.stopPropagation();
                const tab = btn.getAttribute("data-tab");
                if (tab) {
                    this.switchTab(tab);
                }
            };
            btn.addEventListener("click", onTabActivate);
            btn.addEventListener("mousedown", onTabActivate);
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

    renderActiveTab() {
        const container = this.querySelector("#tab-content-area");
        if (!container) return;

        if (this.activeTab === "overview") {
            container.innerHTML = this.renderOverviewTab();
            this.attachOverviewEvents();
        } else if (this.activeTab === "atmosphere") {
            container.innerHTML = this.renderAtmosphereTab();
        } else if (this.activeTab === "hazards") {
            container.innerHTML = this.renderHazardsTab();
        } else if (this.activeTab === "forecast") {
            container.innerHTML = this.renderForecastTab();
        } else if (this.activeTab === "diagnostics") {
            container.innerHTML = this.renderDiagnosticsTab();
            this.attachDiagEvents();
        }
    }

    renderStationBar() {
        if (!this.weatherState) return;
        const icaoEl = this.querySelector(".station-icao");
        const detailsEl = this.querySelector(".station-details");
        const catEl = this.querySelector(".flight-cat-badge");

        if (icaoEl) icaoEl.textContent = this.weatherState.stationId || "LOCAL";
        if (detailsEl) {
            const lat = this.weatherState.latitude ? this.weatherState.latitude.toFixed(2) + "°" : "";
            const lon = this.weatherState.longitude ? this.weatherState.longitude.toFixed(2) + "°" : "";
            const model = this.weatherState.sourceModelName || "Multi-Model";
            detailsEl.textContent = lat + " " + lon + " • " + model;
        }
        if (catEl) {
            const cat = (this.weatherState.flightCategory || "VFR").toLowerCase();
            catEl.className = "flight-cat-badge " + cat;
            catEl.textContent = cat.toUpperCase();
        }
    }

    updateStatus(type, text) {
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
        const visSm = s.visibilityMeters != null ? (s.visibilityMeters / 1609.344).toFixed(1) : "--";
        const visKm = s.visibilityMeters != null ? (s.visibilityMeters / 1000).toFixed(1) : "--";
        const humidity = s.humidityPercent != null ? (Math.round(s.humidityPercent) + "%") : "---";
        const ceiling = s.ceilingFeet ? (Math.round(s.ceilingFeet) + " ft") : "Unlimited";
        const freezeLvl = s.freezingLevelFeet ? (Math.round(s.freezingLevelFeet) + " ft") : "---";

        const dirStr = (windDir < 100 ? (windDir < 10 ? "00" : "0") : "") + windDir;
        const turbSeverity = (s.turbulenceIndex || 0) > 0.6 ? "Severe" : (s.turbulenceIndex || 0) > 0.3 ? "Moderate" : (s.turbulenceIndex || 0) > 0.1 ? "Light" : "Smooth";
        const precip = s.precipitationRate != null && s.precipitationRate > 0 ? (s.precipitationRate.toFixed(1) + " mm/h") : "None";

        return `
            <div class="hero-weather-card">
                <div class="hero-temp-section">
                    <span class="hero-temp-large">${tempC}°C</span>
                    <span class="hero-temp-sub">${tempF}°F</span>
                </div>
                <div class="hero-metrics-row">
                    <div class="hero-metric-item">
                        <span class="hero-metric-label">Dewpoint</span>
                        <span class="hero-metric-val">${dewpC}°C</span>
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
                    <span class="metric-label">Altimeter / QNH</span>
                    <span class="metric-value">${qnhInHg}</span>
                    <span class="metric-sub">${qnhHpa} hPa</span>
                </div>

                <div class="metric-card">
                    <span class="metric-label">Surface Wind</span>
                    <div class="wind-widget">
                        <div class="compass-dial">
                            <div class="compass-arrow" style="transform: rotate(${windDir}deg);"></div>
                        </div>
                        <div>
                            <span class="metric-value">${dirStr}° / ${windSpd}</span>
                            <span class="metric-sub">kts ${windGust}</span>
                        </div>
                    </div>
                </div>

                <div class="metric-card">
                    <span class="metric-label">Visibility & Ceiling</span>
                    <span class="metric-value">${visSm} SM</span>
                    <span class="metric-sub">Ceiling: ${ceiling}</span>
                </div>
            </div>

            <div class="metar-container">
                <div class="metar-header">
                    <span class="metar-title">Current METAR Observation</span>
                    <button class="btn-mini" id="btn-copy-metar">Copy METAR</button>
                </div>
                <div class="metar-text">${this.rawMetar || "No raw METAR string"}</div>
            </div>

            <div class="card-grid-4">
                <div class="metric-card">
                    <span class="metric-label">Freezing Level</span>
                    <span class="metric-value" style="font-size:12px;">${freezeLvl}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Turbulence</span>
                    <span class="metric-value" style="font-size:12px;">${turbSeverity}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Visibility (Metric)</span>
                    <span class="metric-value" style="font-size:12px;">${visKm} km</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Aerosol Factor</span>
                    <span class="metric-value" style="font-size:12px;">${(s.aerosolDensity || 0).toFixed(2)}</span>
                </div>
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
                    <td><strong>${dirStr}°</strong> @ ${spd} kt</td>
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
            <div style="font-weight:700; font-size:11px; text-transform:uppercase; color:var(--cyan); margin-bottom:4px;">Winds Aloft Profile</div>
            <div style="background:var(--bg-card); border:1px solid var(--border-subtle); border-radius:6px; overflow:hidden; margin-bottom:12px;">
                ${windsHtml}
            </div>

            <div style="font-weight:700; font-size:11px; text-transform:uppercase; color:var(--cyan); margin-bottom:4px;">Cloud Layers</div>
            <div style="background:var(--bg-card); border:1px solid var(--border-subtle); border-radius:6px; overflow:hidden;">
                ${cloudsHtml}
            </div>
        `;
    }

    renderHazardsTab() {
        const s = this.weatherState;
        if (!s) return `<div class="empty-state">No hazard data available.</div>`;

        const icing = (s.icingIndex || 0) * 100;
        const turb = (s.turbulenceIndex || 0) * 100;
        const cape = s.convectiveAvailablePotentialEnergy != null ? Math.round(s.convectiveAvailablePotentialEnergy) : 0;
        const li = s.liftedIndex != null ? s.liftedIndex.toFixed(1) : "---";
        const tstormRatio = (s.thunderstormIntensity || 0) * 100;
        const precipRate = s.precipitationRate != null ? (s.precipitationRate.toFixed(1) + " mm/h") : "None";

        let icingSeverity = "None";
        let icingColor = "var(--green)";
        if (icing > 70) { icingSeverity = "Severe"; icingColor = "var(--red)"; }
        else if (icing > 40) { icingSeverity = "Moderate"; icingColor = "var(--amber)"; }
        else if (icing > 10) { icingSeverity = "Light"; icingColor = "var(--cyan)"; }

        let turbSeverity = "Smooth";
        let turbColor = "var(--green)";
        if (turb > 75) { turbSeverity = "Severe"; turbColor = "var(--red)"; }
        else if (turb > 45) { turbSeverity = "Moderate"; turbColor = "var(--amber)"; }
        else if (turb > 15) { turbSeverity = "Light"; turbColor = "var(--cyan)"; }

        return `
            <div class="hazard-meter">
                <div>
                    <div class="hazard-label">Structural Icing Potential</div>
                    <div style="font-size:10px; color:${icingColor}; font-weight:700;">${icingSeverity} (${Math.round(icing)}%)</div>
                </div>
                <div class="hazard-bar-container">
                    <div class="progress-bar-bg">
                        <div class="progress-bar-fill" style="width:${icing}%; background:${icingColor};"></div>
                    </div>
                </div>
            </div>

            <div class="hazard-meter">
                <div>
                    <div class="hazard-label">Atmospheric Turbulence</div>
                    <div style="font-size:10px; color:${turbColor}; font-weight:700;">${turbSeverity} (${Math.round(turb)}%)</div>
                </div>
                <div class="hazard-bar-container">
                    <div class="progress-bar-bg">
                        <div class="progress-bar-fill" style="width:${turb}%; background:${turbColor};"></div>
                    </div>
                </div>
            </div>

            <div class="hazard-meter">
                <div>
                    <div class="hazard-label">Convective / Thunderstorms</div>
                    <div style="font-size:10px; color:var(--text-secondary);">CAPE: ${cape} J/kg • LI: ${li}</div>
                </div>
                <div class="hazard-bar-container">
                    <div class="progress-bar-bg">
                        <div class="progress-bar-fill" style="width:${tstormRatio}%; background:var(--magenta);"></div>
                    </div>
                </div>
            </div>

            <div class="card-grid-4" style="margin-top:6px;">
                <div class="metric-card">
                    <span class="metric-label">Precipitation</span>
                    <span class="metric-value" style="font-size:13px;">${precipRate}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Storm Cells</span>
                    <span class="metric-value" style="font-size:13px;">${(s.stormCells || []).length}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Icing Layers</span>
                    <span class="metric-value" style="font-size:13px;">${(s.icingLayers || []).length}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Turb Layers</span>
                    <span class="metric-value" style="font-size:13px;">${(s.turbulenceLayers || []).length}</span>
                </div>
            </div>
        `;
    }

    renderForecastTab() {
        const s = this.weatherState;
        const taf = s ? s.taf : null;
        if (!taf) {
            return `<div class="empty-state">No Terminal Aerodrome Forecast (TAF) available.</div>`;
        }

        const validFrom = taf.validFrom ? new Date(taf.validFrom).toUTCString().substr(17, 5) + "Z" : "---";
        const validTo = taf.validTo ? new Date(taf.validTo).toUTCString().substr(17, 5) + "Z" : "---";
        const cat = (taf.flightCategory || "VFR").toLowerCase();

        return `
            <div class="metar-container">
                <div class="metar-header">
                    <span class="metar-title">Terminal Aerodrome Forecast (TAF)</span>
                    <span class="flight-cat-badge ${cat}">${cat.toUpperCase()}</span>
                </div>
                <div style="font-size:11px; color:var(--text-secondary); margin-bottom:4px;">
                    Validity: <strong>${validFrom}</strong> → <strong>${validTo}</strong>
                </div>
                <div class="metar-text">${taf.rawText || "No raw TAF text"}</div>
            </div>

            <div class="card-grid-4" style="margin-top:8px;">
                <div class="metric-card">
                    <span class="metric-label">Forecast Wind</span>
                    <span class="metric-value" style="font-size:13px;">${Math.round(taf.windDirectionDegrees || 0)}° @ ${Math.round(taf.windSpeedKnots || 0)} kt</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Forecast Vis</span>
                    <span class="metric-value" style="font-size:13px;">${taf.visibilityMeters ? (taf.visibilityMeters / 1609.344).toFixed(1) + " SM" : "P6SM"}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Max Temp</span>
                    <span class="metric-value" style="font-size:13px;">${taf.maxTemperatureCelsius != null ? taf.maxTemperatureCelsius + "°C" : "---"}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Min Temp</span>
                    <span class="metric-value" style="font-size:13px;">${taf.minTemperatureCelsius != null ? taf.minTemperatureCelsius + "°C" : "---"}</span>
                </div>
            </div>
        `;
    }

    renderDiagnosticsTab() {
        const filtered = this.history.filter(h => {
            if (this.logFilter === "all") return true;
            return h.state === this.logFilter;
        });

        let logLines = "";
        for (let i = 0; i < filtered.length; i++) {
            const item = filtered[i];
            logLines += '<div class="log-line ' + item.state + '">' + item.time + ' [' + item.state.toUpperCase() + '] ' + item.text + '</div>';
        }

        return `
            <div class="diag-controls">
                <div class="log-filter-group">
                    <button class="log-filter-btn ${this.logFilter === 'all' ? 'active' : ''}" data-filter="all">ALL</button>
                    <button class="log-filter-btn ${this.logFilter === 'ok' ? 'active' : ''}" data-filter="ok">OK</button>
                    <button class="log-filter-btn ${this.logFilter === 'event' ? 'active' : ''}" data-filter="event">EVENT</button>
                    <button class="log-filter-btn ${this.logFilter === 'warn' ? 'active' : ''}" data-filter="warn">WARN</button>
                    <button class="log-filter-btn ${this.logFilter === 'error' ? 'active' : ''}" data-filter="error">ERR</button>
                </div>

                <div style="flex:1;"></div>

                <button class="btn-action" id="btn-copy-log">Copy Log</button>
                <button class="btn-action" id="btn-clear-log">Clear</button>
            </div>

            <div class="card-grid-4">
                <div class="metric-card">
                    <span class="metric-label">Events</span>
                    <span class="metric-value">${this.eventCount}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Acks</span>
                    <span class="metric-value">${this.ackCount}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Errors</span>
                    <span class="metric-value" style="color:${this.errorCount > 0 ? 'var(--red)' : 'var(--green)'}">${this.errorCount}</span>
                </div>
                <div class="metric-card">
                    <span class="metric-label">Smoothing</span>
                    <span class="metric-value">${this.smoothingFactor}</span>
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
                div.textContent = time + " [" + state.toUpperCase() + "] " + text;
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
                dvMSLGLTemperature: this.makeDataValue("dvMSLGLTemperature", 15, "°C", -100, 100),
                dvMSLPressure: this.makeDataValue("dvMSLPressure", 1013.25, "hPa", 800, 1200),
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

        this.ensureDataValue(settings, "dvMSLGLTemperature", state.temperatureCelsius, "°C");
        this.ensureDataValue(settings, "dvMSLTemperature", state.temperatureCelsius, "°C");
        this.ensureDataValue(settings, "dvTemperature", state.temperatureCelsius, "°C");
        this.ensureDataValue(settings, "dvMSLPressure", state.altimeterHpa, "hPa");
        this.ensureDataValue(settings, "dvPressure", state.altimeterHpa, "hPa");
        this.ensureDataValue(settings, "dvPrecipitation", state.precipitationRate, "mm/h");
        this.ensureDataValue(settings, "dvThunderstormRatio", state.thunderstormIntensity, "%");
        this.ensureDataValue(settings, "dvPollution", state.aerosolDensity, "aerosol");
        this.ensureDataValue(settings, "dvHumidityMultiplier", state.humidityPercent / 100, "ratio");

        const clouds = Array.isArray(state.cloudLayers) ? state.cloudLayers : [];
        if (!Array.isArray(preset.tCloudLayers) || preset.tCloudLayers.length === 0) {
            preset.tCloudLayers = [{}, {}, {}];
        }
        preset.tCloudLayers = preset.tCloudLayers.slice(0, 3);
        preset.tCloudLayers.forEach((layer, index) => {
            layer.__Type = "CloudLayerData";
            const source = clouds[index];
            if (!source) {
                this.ensureDataValue(layer, "dvDensityMultiplier", 0, "ratio");
                this.ensureDataValue(layer, "dvCoverageRatio", 0, "ratio");
                this.ensureDataValue(layer, "dvCloudScatteringRatio", 1, "ratio");
                this.ensureDataValue(layer, "dvAltitudeBot", 3000 * (index + 1), "ft");
                this.ensureDataValue(layer, "dvAltitudeTop", 6000 * (index + 1), "ft");
                return;
            }
            this.ensureDataValue(layer, "dvDensityMultiplier", source.density, "ratio");
            this.ensureDataValue(layer, "dvCoverageRatio", source.coveragePercent != null ? (source.coveragePercent > 1 ? source.coveragePercent / 100 : source.coveragePercent) : 0.5, "ratio");
            this.ensureDataValue(layer, "dvCloudScatteringRatio", source.scattering, "ratio");
            this.ensureDataValue(layer, "dvAltitudeBot", source.baseFeetAgl, "ft");
            this.ensureDataValue(layer, "dvAltitudeTop", source.topFeetAgl, "ft");
        });

        const winds = Array.isArray(state.windsAloft) ? state.windsAloft : [];
        if (!Array.isArray(preset.tWindLayers) || preset.tWindLayers.length === 0) {
            preset.tWindLayers = [{}];
        }
        preset.tWindLayers.forEach((layer, index) => {
            layer.__Type = "WindLayerData";
            const source = winds[index] || winds[winds.length - 1];
            if (!source) {
                return;
            }
            this.ensureDataValue(layer, "dvAltitude", source.altitudeFeet, "ft");
            this.ensureDataValue(layer, "dvAngleRad", source.directionDegrees * Math.PI / 180, "rad");
            this.ensureDataValue(layer, "dvSpeed", source.speedKnots, "knots");
            if (!layer.gustWaveData || typeof layer.gustWaveData !== "object") {
                layer.gustWaveData = {};
            }
            layer.gustWaveData.__Type = "GustWaveData";
            const gust = source.gustSpeedKnots || 0;
            this.ensureDataValue(layer.gustWaveData, "dvSpeedMultiplier",
                source.speedKnots > 0 ? gust / source.speedKnots : 0, "ratio");
            this.ensureDataValue(layer.gustWaveData, "dvAngleRad",
                (source.gustDirectionDegrees != null ? source.gustDirectionDegrees : source.directionDegrees) * Math.PI / 180, "rad");
        });

        return preset;
    }

    ensureDataValue(container, key, value, defaultUnit) {
        if (!container[key] || typeof container[key] !== "object") {
            container[key] = this.makeDataValue(key, value, defaultUnit);
        } else {
            const target = container[key];
            const u = (target.unit || defaultUnit || "").toLowerCase().trim();
            const num = Number(value);
            let finalVal = num;

            if (key.toLowerCase().indexOf("pressure") !== -1) {
                let hpa = num;
                if (hpa > 2000) hpa = hpa / 100; // Passed in Pa

                if (u.indexOf("inhg") !== -1) {
                    finalVal = hpa * 0.029529983;
                } else if (u === "pa" || u === "pascal" || u === "pascals") {
                    finalVal = hpa * 100;
                } else {
                    // Default to hPa / mbar
                    finalVal = hpa;
                }
            } else if (key.toLowerCase().indexOf("temp") !== -1) {
                if (u.indexOf("f") !== -1 || u.indexOf("°f") !== -1) {
                    finalVal = num * 9 / 5 + 32;
                } else if (u.indexOf("k") !== -1) {
                    finalVal = num + 273.15;
                } else {
                    finalVal = num;
                }
            }

            this.setValue(target, finalVal, defaultUnit);
        }
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
checkAutoload();
