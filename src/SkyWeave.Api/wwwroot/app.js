/**
 * SkyWeave Cockpit Web EFB Companion (Tablet PWA)
 * Ultra-fast Vanilla JS Avionics Engine
 */

(() => {
  'use strict';

  // State
  // Empty means follow the desktop app's current aircraft weather. A station is
  // set only after the pilot deliberately selects one in the EFB.
  let currentStation = '';
  let currentRangeNm = 40;
  let sweepAngle = 0;
  let sweepEnabled = true;
  let currentEfbData = null;
  let isPolling = false;
  let pollIntervalId = null;

  let isFrozen = false;
  let showRadarLayer = true;
  let showIsobarsLayer = true;
  let showBarbsLayer = true;
  let currentSynopticData = null;
  let currentSoundingData = null;
  let currentFlightPlan = null;

  // DOM Elements
  const elSimPulse = document.getElementById('simPulse');
  const elSimStatusText = document.getElementById('simStatusText');
  const elModeBadge = document.getElementById('modeBadge');
  const elBtnFreeze = document.getElementById('btnFreeze');
  const elAnchorBadge = document.getElementById('anchorBadge');
  const elNetworkBadge = document.getElementById('networkBadge');
  const elUtcClock = document.getElementById('utcClock');
  const elStationSelect = document.getElementById('stationSelect');
  const elStationInput = document.getElementById('stationInput');
  const elBtnStationGo = document.getElementById('btnStationGo');
  const elBtnRefresh = document.getElementById('btnRefresh');

  const elBtnLayerRadar = document.getElementById('btnLayerRadar');
  const elBtnLayerIsobars = document.getElementById('btnLayerIsobars');
  const elBtnLayerBarbs = document.getElementById('btnLayerBarbs');

  const soundingCanvas = document.getElementById('soundingCanvas');
  const elSoundingStationTag = document.getElementById('soundingStationTag');

  const elSimbriefPilotId = document.getElementById('simbriefPilotId');
  const elBtnFetchSimBrief = document.getElementById('btnFetchSimBrief');
  const elSimbriefBody = document.getElementById('simbriefBody');
  const elSimbriefEmpty = document.getElementById('simbriefEmpty');
  const elSimbriefCorridorTag = document.getElementById('simbriefCorridorTag');
  const elOfpFlightNum = document.getElementById('ofpFlightNum');
  const elOfpAircraft = document.getElementById('ofpAircraft');
  const elOfpRoute = document.getElementById('ofpRoute');
  const elOfpAlt = document.getElementById('ofpAlt');
  const elOfpCruise = document.getElementById('ofpCruise');
  const elOfpEte = document.getElementById('ofpEte');
  const elSimbriefTableBody = document.getElementById('simbriefTableBody');
  const elBtnExportPmdg = document.getElementById('btnExportPmdg');
  const elBtnExportFenix = document.getElementById('btnExportFenix');
  const elBtnExportCsv = document.getElementById('btnExportCsv');
  const elFmcStatusText = document.getElementById('fmcStatusText');

  const elFlightCategoryChip = document.getElementById('flightCategoryChip');
  const elCatTitle = document.getElementById('catTitle');
  const elCatDesc = document.getElementById('catDesc');
  const elMetaStation = document.getElementById('metaStation');
  const elMetaTime = document.getElementById('metaTime');
  const elMetaAge = document.getElementById('metaAge');
  const elMetaSource = document.getElementById('metaSource');
  const elRawMetarText = document.getElementById('rawMetarText');
  const elBtnCopyMetar = document.getElementById('btnCopyMetar');
  const elCopyText = document.getElementById('copyText');
  const elMetarDecoded = document.getElementById('metarDecoded');

  const elAtisBanner = document.getElementById('atisBanner');
  const elAtisBadgePill = document.getElementById('atisBadgePill');
  const elAtisPhonetic = document.getElementById('atisPhonetic');
  const elAtisTime = document.getElementById('atisTime');
  const elAtisQnh = document.getElementById('atisQnh');
  const elAtisRwy = document.getElementById('atisRwy');
  const elBtnToggleAtisRaw = document.getElementById('btnToggleAtisRaw');
  const elAtisRawText = document.getElementById('atisRawText');

  const elWindDirVal = document.getElementById('windDirVal');
  const elWindSpdVal = document.getElementById('windSpdVal');
  const elWindGustVal = document.getElementById('windGustVal');
  const elWindGustRow = document.getElementById('windGustRow');
  const elWindGustBadge = document.getElementById('windGustBadge');
  const elWindComponent = document.getElementById('windComponent');
  const compassCanvas = document.getElementById('compassCanvas');

  const elQnhHpa = document.getElementById('qnhHpa');
  const elQnhInHg = document.getElementById('qnhInHg');
  const elAltimeterTrend = document.getElementById('altimeterTrend');
  const elPressureAltVal = document.getElementById('pressureAltVal');

  const elTempCVal = document.getElementById('tempCVal');
  const elTempFVal = document.getElementById('tempFVal');
  const elDewCVal = document.getElementById('dewCVal');
  const elDewFVal = document.getElementById('dewFVal');
  const elSpreadBadge = document.getElementById('spreadBadge');
  const elHumidityText = document.getElementById('humidityText');
  const elHumidityBar = document.getElementById('humidityBar');
  const elFreezingLevelVal = document.getElementById('freezingLevelVal');

  const elVisSmVal = document.getElementById('visSmVal');
  const elVisKmVal = document.getElementById('visKmVal');
  const elCeilingVal = document.getElementById('ceilingVal');
  const elCeilingUnit = document.getElementById('ceilingUnit');
  const elVisCondition = document.getElementById('visCondition');
  const elCloudChips = document.getElementById('cloudChips');

  const radarCanvas = document.getElementById('radarCanvas');
  const radarViewport = document.getElementById('radarViewport');
  const elRadarRangeBadge = document.getElementById('radarRangeBadge');
  const elRadarTimeBadge = document.getElementById('radarTimeBadge');
  const elRadarCellCount = document.getElementById('radarCellCount');
  const elBtnZoomIn = document.getElementById('btnZoomIn');
  const elBtnZoomOut = document.getElementById('btnZoomOut');
  const elBtnRadarSweep = document.getElementById('btnRadarSweep');
  const rangeBtns = document.querySelectorAll('.range-btn');

  const elWindsAloftBody = document.getElementById('windsAloftBody');
  const elAloftSourceModel = document.getElementById('aloftSourceModel');
  const elHazardList = document.getElementById('hazardList');
  const elHazardCountBadge = document.getElementById('hazardCountBadge');
  const elLastUpdatedFooter = document.getElementById('lastUpdatedFooter');

  // =========================================================================
  // CLOCK & STATUS POLLING
  // =========================================================================
  function updateClock() {
    const now = new Date();
    const h = String(now.getUTCHours()).padStart(2, '0');
    const m = String(now.getUTCMinutes()).padStart(2, '0');
    const s = String(now.getUTCSeconds()).padStart(2, '0');
    elUtcClock.textContent = `${h}:${m}:${s} Z`;
  }
  setInterval(updateClock, 1000);
  updateClock();

  async function pollStatus() {
    try {
      const res = await fetch('/api/status');
      if (res.ok) {
        const data = await res.json();
        updateStatusDisplay(data);
      }
    } catch {
      // Server unreachable
      elSimPulse.className = 'pulse-dot';
      elSimStatusText.textContent = 'OFFLINE';
    }
  }

  function updateStatusDisplay(status) {
    if (status.simConnected) {
      elSimPulse.className = 'pulse-dot connected';
      elSimStatusText.textContent = 'SIM CONNECTED';
    } else {
      elSimPulse.className = 'pulse-dot';
      elSimStatusText.textContent = status.isRunning ? 'WAITING FOR SIM' : 'ENGINE OFFLINE';
    }

    if (status.isInjecting) {
      elModeBadge.className = 'mode-badge badge-injecting';
      elModeBadge.textContent = 'INJECTING';
    } else {
      elModeBadge.className = 'mode-badge badge-passive';
      elModeBadge.textContent = 'PASSIVE';
    }

    if (elNetworkBadge) {
      if (status.isOnlineNetworkActive) {
        elNetworkBadge.style.display = 'inline-block';
        elNetworkBadge.textContent = `🌐 ${status.onlineNetworkName || 'ONLINE ATC'}`;
      } else {
        elNetworkBadge.style.display = 'none';
      }
    }
  }

  async function pollFreeze() {
    try {
      const res = await fetch('/api/weather/freeze');
      if (res.ok) {
        const data = await res.json();
        isFrozen = !!data.isFrozen;
        if (elBtnFreeze) {
          elBtnFreeze.classList.toggle('frozen', isFrozen);
          elBtnFreeze.textContent = isFrozen ? 'FROZEN' : 'FREEZE';
        }
        if (elAnchorBadge) {
          const anchor = data.anchorState;
          if (anchor && anchor.phase !== 0 && anchor.phase !== 'EnRoute') {
            elAnchorBadge.style.display = 'inline-block';
            let phaseName = 'EN-ROUTE';
            if (anchor.phase === 1 || anchor.phase === 'DepartureHold') phaseName = `DEP HOLD: ${anchor.activeAnchorIcao || ''}`;
            else if (anchor.phase === 2 || anchor.phase === 'ArrivalHold') phaseName = `ARR HOLD: ${anchor.activeAnchorIcao || ''}`;
            else if (anchor.phase === 3 || anchor.phase === 'FinalFreeze') phaseName = 'FINAL FREEZE';
            else if (anchor.phase === 4 || anchor.phase === 'ManualFreeze') phaseName = 'MANUAL FREEZE';
            elAnchorBadge.textContent = phaseName;
          } else {
            elAnchorBadge.style.display = 'none';
          }
        }
      }
    } catch { }
  }

  async function toggleFreeze() {
    try {
      const res = await fetch('/api/weather/freeze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ frozen: !isFrozen })
      });
      if (res.ok) {
        const data = await res.json();
        isFrozen = !!data.isFrozen;
        if (elBtnFreeze) {
          elBtnFreeze.classList.toggle('frozen', isFrozen);
          elBtnFreeze.textContent = isFrozen ? 'FROZEN' : 'FREEZE';
        }
      }
    } catch (err) {
      console.error('Toggle freeze error:', err);
    }
  }

  async function fetchSynopticData() {
    try {
      const res = await fetch(`/api/synoptic?range=${currentRangeNm}`);
      if (res.ok) {
        currentSynopticData = await res.json();
      }
    } catch (err) {
      console.warn('Synoptic fetch error:', err);
    }
  }

  async function fetchSoundingData() {
    try {
      const res = await fetch('/api/sounding');
      if (res.ok) {
        currentSoundingData = await res.json();
        if (elSoundingStationTag) {
          elSoundingStationTag.textContent = `STATION: ${currentStation || (currentEfbData && currentEfbData.stationId) || 'LOCAL'}`;
        }
        drawSounding();
      }
    } catch (err) {
      console.warn('Sounding fetch error:', err);
    }
  }

  function drawSounding() {
    if (!soundingCanvas || !currentSoundingData) return;
    const ctx = soundingCanvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    const rect = soundingCanvas.getBoundingClientRect();
    const w = rect.width || 760;
    const h = rect.height || 260;

    soundingCanvas.width = w * dpr;
    soundingCanvas.height = h * dpr;

    ctx.save();
    ctx.scale(dpr, dpr);
    ctx.clearRect(0, 0, w, h);

    ctx.fillStyle = '#0B1120';
    ctx.fillRect(0, 0, w, h);

    const dataW = currentSoundingData.canvasWidth || 360;
    const dataH = currentSoundingData.canvasHeight || 240;
    const scaleX = w / dataW;
    const scaleY = h / dataH;

    // Draw Flight Level Grid Lines & Wind/Temp
    if (currentSoundingData.standardLevels) {
      ctx.lineWidth = 1;
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
      ctx.setLineDash([3, 4]);

      for (const lvl of currentSoundingData.standardLevels) {
        const y = lvl.y * scaleY;
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(w, y);
        ctx.stroke();

        ctx.fillStyle = '#64748B';
        ctx.font = '9px monospace';
        ctx.textAlign = 'left';
        ctx.fillText(lvl.flightLevelName || '', 8, y - 4);

        ctx.fillStyle = '#00F0FF';
        ctx.textAlign = 'right';
        ctx.fillText(`${lvl.windText || ''}  ${lvl.tempText || ''}`, w - 8, y - 4);
      }
      ctx.setLineDash([]);
    }

    // Draw Cloud Blocks
    if (currentSoundingData.cloudBlocks) {
      for (const cb of currentSoundingData.cloudBlocks) {
        const y = cb.y * scaleY;
        const boxH = Math.max(8, cb.height * scaleY);
        ctx.fillStyle = `rgba(56, 189, 248, ${cb.opacity || 0.4})`;
        ctx.strokeStyle = 'rgba(0, 240, 255, 0.6)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        if (ctx.roundRect) ctx.roundRect(70 * scaleX, y, 180 * scaleX, boxH, 4);
        else ctx.rect(70 * scaleX, y, 180 * scaleX, boxH);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = '#FFFFFF';
        ctx.font = 'bold 9px sans-serif';
        ctx.textAlign = 'left';
        ctx.fillText(cb.label || 'CLOUD LAYER', 75 * scaleX, y + Math.min(boxH - 4, 12));
      }
    }

    // Draw Hazard Bands (Icing / Turbulence)
    if (currentSoundingData.hazardBands) {
      for (const hb of currentSoundingData.hazardBands) {
        const y = hb.y * scaleY;
        const boxH = Math.max(8, hb.height * scaleY);
        ctx.fillStyle = hb.colorHex ? `${hb.colorHex}44` : 'rgba(239, 68, 68, 0.25)';
        ctx.strokeStyle = hb.colorHex || '#EF4444';
        ctx.lineWidth = 1;
        ctx.beginPath();
        if (ctx.roundRect) ctx.roundRect(260 * scaleX, y, 90 * scaleX, boxH, 3);
        else ctx.rect(260 * scaleX, y, 90 * scaleX, boxH);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = hb.colorHex || '#EF4444';
        ctx.font = 'bold 8px monospace';
        ctx.textAlign = 'left';
        ctx.fillText(hb.label || '', 264 * scaleX, y + Math.min(boxH - 3, 10));
      }
    }

    // Draw Freezing Level (0°C) Line
    if (currentSoundingData.freezingLevelY) {
      const fy = currentSoundingData.freezingLevelY * scaleY;
      ctx.strokeStyle = '#00F0FF';
      ctx.lineWidth = 1.5;
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.moveTo(0, fy);
      ctx.lineTo(w, fy);
      ctx.stroke();
      ctx.setLineDash([]);

      ctx.fillStyle = 'rgba(3, 7, 18, 0.85)';
      ctx.fillRect(8, fy - 16, 120, 14);
      ctx.strokeStyle = '#00F0FF';
      ctx.lineWidth = 1;
      ctx.strokeRect(8, fy - 16, 120, 14);

      ctx.fillStyle = '#00F0FF';
      ctx.font = 'bold 9px monospace';
      ctx.textAlign = 'left';
      ctx.fillText(currentSoundingData.freezingLevelLabel || '0°C FREEZING LEVEL', 12, fy - 5);
    }

    // Draw Temperature curve
    if (currentSoundingData.temperaturePath && typeof Path2D !== 'undefined') {
      ctx.save();
      ctx.scale(scaleX, scaleY);
      ctx.strokeStyle = '#F97316';
      ctx.lineWidth = 2.5 / Math.min(scaleX, scaleY);
      ctx.stroke(new Path2D(currentSoundingData.temperaturePath));
      ctx.restore();
    }

    // Draw Dewpoint curve
    if (currentSoundingData.dewpointPath && typeof Path2D !== 'undefined') {
      ctx.save();
      ctx.scale(scaleX, scaleY);
      ctx.strokeStyle = '#00F0FF';
      ctx.lineWidth = 2.5 / Math.min(scaleX, scaleY);
      ctx.stroke(new Path2D(currentSoundingData.dewpointPath));
      ctx.restore();
    }

    ctx.restore();
  }

  async function fetchSimBriefOfp() {
    const pilotId = (elSimbriefPilotId.value || '').trim();
    if (!pilotId) return;

    elBtnFetchSimBrief.textContent = 'FETCHING...';
    elBtnFetchSimBrief.disabled = true;

    try {
      const res = await fetch('/api/simbrief/fetch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ pilotId })
      });
      if (res.ok) {
        const data = await res.json();
        currentFlightPlan = data.plan;
        renderSimBrief(data.plan, data.anchorState);
      } else {
        const err = await res.json().catch(() => ({}));
        alert(err.error || 'Failed to fetch flight plan from SimBrief');
      }
    } catch (e) {
      alert('Network error connecting to SimBrief: ' + e.message);
    } finally {
      elBtnFetchSimBrief.textContent = 'FETCH OFP';
      elBtnFetchSimBrief.disabled = false;
    }
  }

  async function pollSimBrief() {
    try {
      const res = await fetch('/api/simbrief');
      if (res.ok) {
        const data = await res.json();
        if (data.hasPlan && data.plan) {
          currentFlightPlan = data.plan;
          renderSimBrief(data.plan, data.anchorState);
        }
      }
    } catch { }
  }

  function renderSimBrief(plan, anchorState) {
    if (!plan) return;
    elSimbriefBody.style.display = 'block';
    elSimbriefEmpty.style.display = 'none';

    elOfpFlightNum.textContent = plan.flightNumber || '--';
    elOfpAircraft.textContent = plan.aircraftType || '--';
    elOfpRoute.textContent = `${plan.origin} ➔ ${plan.destination}`;
    elOfpAlt.textContent = plan.alternate || '--';
    elOfpCruise.textContent = plan.cruiseAltitudeFt ? `FL${Math.round(plan.cruiseAltitudeFt / 100)}` : '--';
    const hrs = Math.floor((plan.estimatedTimeEnrouteMinutes || 0) / 60);
    const mins = Math.round((plan.estimatedTimeEnrouteMinutes || 0) % 60);
    elOfpEte.textContent = `${hrs}h ${mins}m`;

    if (anchorState) {
      let phaseText = 'EN-ROUTE TRACKING';
      if (anchorState.phase === 1 || anchorState.phase === 'DepartureHold') phaseText = `DEP HOLD: ${anchorState.activeAnchorIcao || plan.origin}`;
      else if (anchorState.phase === 2 || anchorState.phase === 'ArrivalHold') phaseText = `ARR HOLD: ${anchorState.activeAnchorIcao || plan.destination}`;
      else if (anchorState.phase === 3 || anchorState.phase === 'FinalFreeze') phaseText = 'FINAL FREEZE ON SHORT FINAL';
      else if (anchorState.phase === 4 || anchorState.phase === 'ManualFreeze') phaseText = 'MANUAL WEATHER FREEZE';
      elSimbriefCorridorTag.textContent = phaseText;
    }

    elSimbriefTableBody.innerHTML = '';
    if (plan.waypoints && plan.waypoints.length > 0) {
      plan.waypoints.forEach(wp => {
        const tr = document.createElement('tr');
        const alt = wp.altitudeFt > 0 ? `FL${Math.round(wp.altitudeFt / 100)}` : (elOfpCruise.textContent || '--');
        const stageStr = wp.stage === 0 || wp.stage === 'Climb' ? 'CLB' : wp.stage === 2 || wp.stage === 'Descent' ? 'DES' : 'CRZ';
        tr.innerHTML = `
          <td><strong>${wp.identifier}</strong></td>
          <td><span class="badge" style="background:#334155;color:#E2E8F0;">${stageStr}</span></td>
          <td>${alt}</td>
          <td style="color:#00D2D3;">${Math.round(wp.windDirection || 0)}° / ${Math.round(wp.windSpeedKt || 0)} KT</td>
          <td style="color:${(wp.temperatureC || 0) <= 0 ? '#38BDF8' : '#F97316'};">${wp.temperatureC > 0 ? '+' : ''}${Math.round(wp.temperatureC || 0)}°C</td>
        `;
        elSimbriefTableBody.appendChild(tr);
      });
    }
  }

  function triggerExport(format) {
    if (!currentFlightPlan) {
      alert('Please fetch a SimBrief flight plan first.');
      return;
    }
    window.location.href = `/api/fmc/export?format=${encodeURIComponent(format)}`;
    if (elFmcStatusText) {
      elFmcStatusText.textContent = `Exported ${format.toUpperCase()} successfully!`;
      setTimeout(() => { elFmcStatusText.textContent = ''; }, 3500);
    }
  }

  // =========================================================================
  // EFB DATA FETCH & RENDER
  // =========================================================================
  async function fetchEfbData(showSpinner = false) {
    if (isPolling && !showSpinner) return;
    isPolling = true;

    if (showSpinner) {
      elBtnRefresh.classList.add('spinning');
    }

    try {
      const url = currentStation
        ? `/api/efb?station=${encodeURIComponent(currentStation)}`
        : '/api/efb';
      const res = await fetch(url);
      if (res.ok) {
        const data = await res.json();
        currentEfbData = data;
        renderEfbData(data);
        const now = new Date();
        elLastUpdatedFooter.textContent = `Last update: ${now.toLocaleTimeString()}`;
      } else if (res.status === 503) {
        const err = await res.json().catch(() => null);
        if (err && err.reason === 'awaiting_sim_position') {
          elLastUpdatedFooter.textContent = 'Awaiting simulator aircraft position fix...';
          if (!currentStation) {
            elMetaStation.textContent = 'NO FIX';
            elRawMetarText.textContent = 'Awaiting aircraft position fix from MSFS...';
            elMetarDecoded.textContent = 'Connect MSFS 2024 or enter an airport ICAO code above to view weather.';
          }
        } else {
          console.warn('EFB data unavailable:', err);
        }
      } else {
        console.warn('EFB data unavailable; waiting for the desktop weather engine.');
      }
    } catch (err) {
      console.error('Fetch error:', err);
    } finally {
      isPolling = false;
      if (showSpinner) {
        setTimeout(() => elBtnRefresh.classList.remove('spinning'), 500);
      }
    }
  }

  function renderEfbData(data) {
    if (!data) return;

    // Station & Meta
    const station = data.stationId || currentStation;
    elMetaStation.textContent = station;
    if (elStationSelect.value !== station) {
      // Check if exists in dropdown
      const opt = Array.from(elStationSelect.options).find(o => o.value === station);
      if (opt) elStationSelect.value = station;
    }

    // Observation Time & Age
    if (data.observationTime) {
      const obsDate = new Date(data.observationTime);
      const hours = String(obsDate.getUTCHours()).padStart(2, '0');
      const mins = String(obsDate.getUTCMinutes()).padStart(2, '0');
      elMetaTime.textContent = `${hours}:${mins}Z`;

      const diffMins = Math.max(0, Math.floor((Date.now() - obsDate.getTime()) / 60000));
      elMetaAge.textContent = diffMins > 0 ? `${diffMins}m ago` : 'Just now';
    }
    elMetaSource.textContent = data.sourceModelName || 'HRRR CONUS 3km';

    // Raw METAR & Decoded
    const raw = data.rawMetar || `${station} METAR NOT AVAILABLE`;
    elRawMetarText.textContent = raw;
    renderDecodedMetar(data);
    renderAtis(data.atis);

    // Flight Category
    const cat = (data.flightCategory || 'VFR').toUpperCase();
    renderFlightCategory(cat);

    // Surface Wind
    renderWind(data);

    // QNH / Altimeter
    renderAltimeter(data);

    // Temperature & Dewpoint
    renderTemperature(data);

    // Visibility & Clouds
    renderVisibility(data);

    // Winds Aloft Table
    renderWindsAloft(data.windsAloft, data.sourceModelName);

    // Hazards
    renderHazards(data.hazards);

    // Radar Overlay Info
    renderRadarInfo(data);
  }

  function renderFlightCategory(cat) {
    elFlightCategoryChip.className = 'category-chip';
    elCatTitle.textContent = cat;

    if (cat === 'LIFR') {
      elFlightCategoryChip.classList.add('cat-lifr');
      elCatDesc.textContent = 'CEILING < 500 FT OR VIS < 1 SM';
    } else if (cat === 'IFR') {
      elFlightCategoryChip.classList.add('cat-ifr');
      elCatDesc.textContent = 'CEILING 500-999 FT OR VIS 1-3 SM';
    } else if (cat === 'MVFR') {
      elFlightCategoryChip.classList.add('cat-mvfr');
      elCatDesc.textContent = 'CEILING 1000-3000 FT OR VIS 3-5 SM';
    } else {
      elFlightCategoryChip.classList.add('cat-vfr');
      elCatDesc.textContent = 'VIS \u2265 5 SM \u2022 CEILING \u2265 3000 FT';
    }
  }

  function renderDecodedMetar(data) {
    const parts = [];
    const windDir = Math.round(data.windDirectionDegrees || 0);
    const windSpd = Math.round(data.windSpeedKnots || 0);
    const gust = data.windGustKnots ? Math.round(data.windGustKnots) : null;

    parts.push(`Wind ${String(windDir).padStart(3, '0')}\u00B0 at ${windSpd} kt${gust ? ` gusting to ${gust} kt` : ''}`);

    const visM = data.visibilityMeters || 16093;
    const visSm = (visM / 1609.344).toFixed(visM < 3200 ? 1 : 0);
    parts.push(`Visibility ${visSm >= 10 ? '10+' : visSm} SM`);

    if (data.cloudLayers && data.cloudLayers.length > 0) {
      const cloudDesc = data.cloudLayers.map(c => `${c.coverage || 'SCT'} at ${Math.round(c.baseAltitudeFeet || 0)} ft`).join(', ');
      parts.push(`Clouds: ${cloudDesc}`);
    } else if (data.ceilingFeet && data.ceilingFeet > 0 && data.ceilingFeet < 20000) {
      parts.push(`Ceiling ${Math.round(data.ceilingFeet)} ft`);
    } else {
      parts.push('Clear skies');
    }

    const temp = (data.temperatureCelsius || 0).toFixed(0);
    const dew = (data.dewpointCelsius || 0).toFixed(0);
    parts.push(`Temp ${temp}\u00B0C / Dew ${dew}\u00B0C`);

    const qnh = data.altimeterHpa || 1013.25;
    const inHg = (qnh * 0.02952998751).toFixed(2);
    parts.push(`QNH ${Math.round(qnh)} hPa (A${inHg.replace('.', '')})`);

    elMetarDecoded.textContent = parts.join('. ') + '.';
  }

  function renderAtis(atis) {
    if (!elAtisBanner) return;
    if (atis && (atis.atisLetter || atis.altimeterHpa || atis.runwayInUse || atis.rawText)) {
      elAtisBanner.style.display = 'block';
      elAtisBadgePill.textContent = atis.atisLetter ? `ATIS ${atis.atisLetter}` : 'ATIS';
      elAtisPhonetic.textContent = atis.atisPhonetic || atis.atisLetter || '';
      if (atis.timestamp) {
        const d = new Date(atis.timestamp);
        const hh = String(d.getUTCHours()).padStart(2, '0');
        const mm = String(d.getUTCMinutes()).padStart(2, '0');
        elAtisTime.textContent = `${hh}:${mm}Z`;
      } else {
        elAtisTime.textContent = '';
      }
      elAtisQnh.textContent = atis.altimeterHpa ? `QNH ${Math.round(atis.altimeterHpa)}` : (atis.altimeterInHg ? `A${atis.altimeterInHg.toFixed(2)}` : '');
      elAtisQnh.style.display = (atis.altimeterHpa || atis.altimeterInHg) ? 'inline-block' : 'none';

      elAtisRwy.textContent = atis.runwayInUse ? `RWY ${atis.runwayInUse}` : '';
      elAtisRwy.style.display = atis.runwayInUse ? 'inline-block' : 'none';

      if (elAtisRawText) {
        elAtisRawText.textContent = atis.rawText || '';
      }
    } else {
      elAtisBanner.style.display = 'none';
      if (elAtisRawText) elAtisRawText.style.display = 'none';
      if (elBtnToggleAtisRaw) elBtnToggleAtisRaw.textContent = 'EXPAND';
    }
  }

  function renderWind(data) {
    const dir = Math.round(data.windDirectionDegrees || 0);
    const spd = Math.round(data.windSpeedKnots || 0);
    const gust = data.windGustKnots ? Math.round(data.windGustKnots) : null;

    elWindDirVal.textContent = String(dir).padStart(3, '0');
    elWindSpdVal.textContent = String(spd).padStart(2, '0');

    if (gust && gust > spd) {
      elWindGustRow.style.display = 'flex';
      elWindGustVal.textContent = `${gust} KT`;
      elWindGustBadge.textContent = `GUST ${gust} KT`;
      elWindGustBadge.style.color = 'var(--data-caution)';
    } else {
      elWindGustRow.style.display = 'none';
      elWindGustBadge.textContent = spd < 3 ? 'CALM' : 'STEADY';
      elWindGustBadge.style.color = 'var(--accent-teal)';
    }

    // Crosswind approximation against assumed runway heading (e.g. rounded to nearest 10)
    const crosswind = Math.abs(Math.sin(dir * Math.PI / 180) * spd).toFixed(0);
    elWindComponent.textContent = `CROSSWIND COMPONENT: \u00B1${crosswind} KT (REF RWY 36)`;

    drawCompass(dir, spd);
  }

  function renderAltimeter(data) {
    const hpa = data.altimeterHpa || 1013.25;
    elQnhHpa.textContent = Math.round(hpa);

    const inHg = (hpa * 0.02952998751).toFixed(2);
    elQnhInHg.textContent = inHg;

    const diff = hpa - 1013.25;
    if (Math.abs(diff) < 2) {
      elAltimeterTrend.textContent = 'STANDARD';
      elAltimeterTrend.style.color = 'var(--data-good)';
    } else if (diff > 0) {
      elAltimeterTrend.textContent = 'HIGH PRESSURE';
      elAltimeterTrend.style.color = 'var(--accent-teal)';
    } else {
      elAltimeterTrend.textContent = 'LOW PRESSURE';
      elAltimeterTrend.style.color = 'var(--data-warning)';
    }

    // Estimated Pressure Altitude deviation
    const pAltOffset = Math.round((1013.25 - hpa) * 27);
    elPressureAltVal.textContent = `${pAltOffset >= 0 ? '+' : ''}${pAltOffset} FT`;
  }

  function renderTemperature(data) {
    const c = data.temperatureCelsius || 0;
    const dew = data.dewpointCelsius || 0;
    const f = Math.round((c * 9 / 5) + 32);
    const dewF = Math.round((dew * 9 / 5) + 32);

    elTempCVal.textContent = c.toFixed(1);
    elTempFVal.textContent = `(${f}\u00B0F)`;

    elDewCVal.textContent = dew.toFixed(1);
    elDewFVal.textContent = `(${dewF}\u00B0F)`;

    const spread = (c - dew).toFixed(1);
    elSpreadBadge.textContent = `SPREAD ${spread}\u00B0C`;

    // Humidity
    let rh = data.humidityPercent;
    if (!rh) {
      // Magnus formula approximation
      rh = Math.min(100, Math.max(10, Math.round(100 * Math.exp((17.625 * dew) / (243.04 + dew)) / Math.exp((17.625 * c) / (243.04 + c)))));
    }
    elHumidityText.textContent = `${Math.round(rh)}%`;
    elHumidityBar.style.width = `${Math.min(100, Math.max(0, rh))}%`;

    // Freezing Level
    const fz = data.freezingLevelFeet;
    if (fz && fz > 0) {
      elFreezingLevelVal.textContent = `${Math.round(fz).toLocaleString()} FT`;
    } else {
      // 2 deg C per 1000 ft lapse rate approximation if oat > 0
      const approxFz = Math.max(0, Math.round(c / 2 * 1000));
      elFreezingLevelVal.textContent = c <= 0 ? 'SURFACE (0 FT)' : `${approxFz.toLocaleString()} FT`;
    }
  }

  function renderVisibility(data) {
    const meters = data.visibilityMeters || 16093;
    const sm = meters / 1609.344;

    if (sm >= 10) {
      elVisSmVal.textContent = '10+';
      elVisKmVal.textContent = '(> 9999m)';
      elVisCondition.textContent = 'UNRESTRICTED';
      elVisCondition.style.color = 'var(--data-good)';
    } else {
      elVisSmVal.textContent = sm.toFixed(1);
      elVisKmVal.textContent = `(${Math.round(meters)}m)`;
      elVisCondition.textContent = sm < 3 ? 'POOR' : 'MARGINAL';
      elVisCondition.style.color = sm < 3 ? 'var(--data-critical)' : 'var(--data-caution)';
    }

    // Ceiling
    const ceil = data.ceilingFeet;
    if (ceil && ceil > 0 && ceil < 25000) {
      elCeilingVal.textContent = Math.round(ceil).toLocaleString();
      elCeilingUnit.textContent = 'FT';
    } else {
      elCeilingVal.textContent = 'CLR';
      elCeilingUnit.textContent = 'SKY';
    }

    // Cloud Chips
    elCloudChips.innerHTML = '';
    if (data.cloudLayers && data.cloudLayers.length > 0) {
      data.cloudLayers.forEach(l => {
        const chip = document.createElement('span');
        chip.className = 'cloud-chip';
        const cov = l.coverage || 'FEW';
        const alt = Math.round(l.baseAltitudeFeet || 0).toLocaleString();
        chip.textContent = `${cov} @ ${alt} FT`;
        elCloudChips.appendChild(chip);
      });
    } else {
      const chip = document.createElement('span');
      chip.className = 'cloud-chip';
      chip.textContent = 'SKY CLEAR / CAVOK';
      elCloudChips.appendChild(chip);
    }
  }

  function renderWindsAloft(aloft, modelName) {
    if (modelName) elAloftSourceModel.textContent = `MODEL: ${modelName}`;
    elWindsAloftBody.innerHTML = '';

    const levels = aloft && aloft.length > 0 ? aloft : getDefaultAloft();

    levels.forEach(layer => {
      const tr = document.createElement('tr');
      const alt = Math.round(layer.altitudeFeet || 0);
      const fl = alt >= 18000 ? `FL${Math.round(alt / 100)}` : `${Math.round(alt / 1000)}k FT`;
      const dir = Math.round(layer.directionDegrees || 0);
      const spd = Math.round(layer.speedKnots || 0);
      const temp = layer.temperatureCelsius !== undefined ? `${Math.round(layer.temperatureCelsius)}\u00B0C` : '--';

      tr.innerHTML = `
        <td style="font-weight:700; color:var(--accent-teal);">${fl}</td>
        <td>${alt.toLocaleString()} FT</td>
        <td>${String(dir).padStart(3, '0')}&deg;</td>
        <td><strong style="color:${spd > 35 ? 'var(--data-warning)' : '#FFF'}">${spd} KT</strong></td>
        <td>${temp}</td>
        <td>
          <span class="wind-arrow-icon" style="transform: rotate(${dir + 180}deg); color: ${spd > 40 ? 'var(--accent-brand)' : 'var(--accent-teal)'};">
            &uarr;
          </span>
        </td>
      `;
      elWindsAloftBody.appendChild(tr);
    });
  }

  function getDefaultAloft() {
    return [
      { altitudeFeet: 3000, directionDegrees: 40, speedKnots: 14, temperatureCelsius: 18 },
      { altitudeFeet: 6000, directionDegrees: 50, speedKnots: 18, temperatureCelsius: 12 },
      { altitudeFeet: 9000, directionDegrees: 60, speedKnots: 22, temperatureCelsius: 6 },
      { altitudeFeet: 12000, directionDegrees: 70, speedKnots: 28, temperatureCelsius: 0 },
      { altitudeFeet: 18000, directionDegrees: 80, speedKnots: 35, temperatureCelsius: -12 },
      { altitudeFeet: 24000, directionDegrees: 90, speedKnots: 45, temperatureCelsius: -24 },
      { altitudeFeet: 30000, directionDegrees: 95, speedKnots: 60, temperatureCelsius: -38 },
      { altitudeFeet: 34000, directionDegrees: 100, speedKnots: 75, temperatureCelsius: -46 },
      { altitudeFeet: 39000, directionDegrees: 105, speedKnots: 85, temperatureCelsius: -54 }
    ];
  }

  function renderHazards(hazards) {
    elHazardList.innerHTML = '';
    const items = hazards && hazards.length > 0 ? hazards : [];

    if (items.length === 0) {
      elHazardCountBadge.textContent = '0 ACTIVE';
      elHazardCountBadge.className = 'hazard-count-badge';
      elHazardList.innerHTML = `
        <div class="hazard-empty-state">
          <svg class="check-icon" viewBox="0 0 24 24" fill="none" stroke="#2ECC71" stroke-width="2">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
            <polyline points="22 4 12 14.01 9 11.01"></polyline>
          </svg>
          <div class="empty-title">NO SEVERE HAZARDS REPORTED</div>
          <div class="empty-desc">Convective SIGMETs, severe icing, and extreme turbulence clear within 100 NM radius.</div>
        </div>
      `;
      return;
    }

    elHazardCountBadge.textContent = `${items.length} ACTIVE`;
    elHazardCountBadge.className = 'hazard-count-badge has-hazards';

    items.forEach(h => {
      const card = document.createElement('div');
      const sev = (h.severity || 0) >= 0.7 ? 'sev-critical' : (h.severity || 0) >= 0.4 ? 'sev-warning' : '';
      card.className = `hazard-card ${sev}`;

      const sevText = (h.severity || 0) >= 0.7 ? 'CRITICAL' : (h.severity || 0) >= 0.4 ? 'WARNING' : 'ADVISORY';
      const typeName = h.type !== undefined ? formatHazardType(h.type) : 'WEATHER HAZARD';

      card.innerHTML = `
        <div class="hazard-card-head">
          <span class="hazard-type">${typeName}</span>
          <span class="hazard-badge">${sevText}</span>
        </div>
        <div class="hazard-desc">${h.description || 'Active hazard advisory.'}</div>
      `;
      elHazardList.appendChild(card);
    });
  }

  function formatHazardType(t) {
    if (typeof t === 'string') return t;
    const map = ['CONVECTIVE SIGMET', 'TURBULENCE', 'ICING', 'IFR / CEILING', 'MOUNTAIN OBSCURATION', 'VOLCANIC ASH'];
    return map[t] || 'WEATHER HAZARD';
  }

  function renderRadarInfo(data) {
    elRadarRangeBadge.textContent = `RANGE: ${currentRangeNm} NM`;
    if (data.radarTimestamp) {
      const rDate = new Date(data.radarTimestamp);
      const h = String(rDate.getUTCHours()).padStart(2, '0');
      const m = String(rDate.getUTCMinutes()).padStart(2, '0');
      elRadarTimeBadge.textContent = `RADAR: ${h}:${m}Z`;
    } else {
      elRadarTimeBadge.textContent = 'RADAR: LIVE';
    }

    const cellCount = data.stormCells ? data.stormCells.length : 0;
    elRadarCellCount.textContent = `STORM CELLS: ${cellCount}`;
  }

  // =========================================================================
  // SURFACE WIND COMPASS RENDERING
  // =========================================================================
  function drawCompass(directionDegrees, speedKnots) {
    if (!compassCanvas) return;
    const ctx = compassCanvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;

    const w = 120;
    const h = 120;
    compassCanvas.width = w * dpr;
    compassCanvas.height = h * dpr;
    ctx.scale(dpr, dpr);

    const cx = w / 2;
    const cy = h / 2;
    const r = (w / 2) - 8;

    ctx.clearRect(0, 0, w, h);

    // Background circle
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, 2 * Math.PI);
    ctx.fillStyle = '#0E131B';
    ctx.fill();
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.12)';
    ctx.stroke();

    // Ticks & Cardinals
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    for (let deg = 0; deg < 360; deg += 30) {
      const rad = (deg - 90) * Math.PI / 180;
      const isCard = deg % 90 === 0;
      const innerR = isCard ? r - 8 : r - 4;

      const x1 = cx + Math.cos(rad) * r;
      const y1 = cy + Math.sin(rad) * r;
      const x2 = cx + Math.cos(rad) * innerR;
      const y2 = cy + Math.sin(rad) * innerR;

      ctx.beginPath();
      ctx.moveTo(x1, y1);
      ctx.lineTo(x2, y2);
      ctx.lineWidth = isCard ? 1.5 : 1;
      ctx.strokeStyle = isCard ? '#EAEAEA' : 'rgba(255, 255, 255, 0.2)';
      ctx.stroke();

      if (isCard) {
        const textR = r - 16;
        const tx = cx + Math.cos(rad) * textR;
        const ty = cy + Math.sin(rad) * textR;
        ctx.font = 'bold 9px sans-serif';
        if (deg === 0) {
          ctx.fillStyle = '#E94560'; // N in brand red
          ctx.fillText('N', tx, ty);
        } else if (deg === 90) {
          ctx.fillStyle = '#9AA3B2';
          ctx.fillText('E', tx, ty);
        } else if (deg === 180) {
          ctx.fillStyle = '#9AA3B2';
          ctx.fillText('S', tx, ty);
        } else if (deg === 270) {
          ctx.fillStyle = '#9AA3B2';
          ctx.fillText('W', tx, ty);
        }
      }
    }

    // Wind Arrow
    if (speedKnots > 0) {
      const windRad = (directionDegrees - 90) * Math.PI / 180;
      const arrowLen = r - 12;

      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(windRad);

      // Arrow stem
      ctx.beginPath();
      ctx.moveTo(0, arrowLen);
      ctx.lineTo(0, -arrowLen + 10);
      ctx.lineWidth = 2.5;
      ctx.strokeStyle = '#00D2D3';
      ctx.stroke();

      // Arrow head pointing to center
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.lineTo(-5, 12);
      ctx.lineTo(5, 12);
      ctx.closePath();
      ctx.fillStyle = '#00D2D3';
      ctx.fill();

      // Tail feather
      ctx.beginPath();
      ctx.arc(0, arrowLen, 3, 0, 2 * Math.PI);
      ctx.fillStyle = '#E94560';
      ctx.fill();

      ctx.restore();
    }

    // Center pivot
    ctx.beginPath();
    ctx.arc(cx, cy, 3, 0, 2 * Math.PI);
    ctx.fillStyle = '#FFFFFF';
    ctx.fill();
  }

  // =========================================================================
  // LIVE TACTICAL RADAR RENDERING
  // =========================================================================
  function initRadar() {
    resizeRadar();
    window.addEventListener('resize', resizeRadar);
    requestAnimationFrame(renderRadarLoop);
  }

  function resizeRadar() {
    if (!radarCanvas || !radarViewport) return;
    const rect = radarViewport.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;

    radarCanvas.width = rect.width * dpr;
    radarCanvas.height = rect.height * dpr;
  }

  function renderRadarLoop() {
    if (sweepEnabled) {
      sweepAngle = (sweepAngle + 1.2) % 360;
    }
    drawRadar();
    requestAnimationFrame(renderRadarLoop);
  }

  function drawRadar() {
    if (!radarCanvas) return;
    const ctx = radarCanvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    const w = radarCanvas.width / dpr;
    const h = radarCanvas.height / dpr;

    ctx.save();
    ctx.scale(dpr, dpr);
    ctx.clearRect(0, 0, w, h);

    const cx = w / 2;
    const cy = h / 2;
    const maxR = Math.min(w, h) * 0.44;

    // Pitch-black tactical background
    ctx.fillStyle = '#06090E';
    ctx.fillRect(0, 0, w, h);

    // Range Rings (4 concentric rings)
    const ringCount = 4;
    const ringStepNm = currentRangeNm / ringCount;
    ctx.lineWidth = 1;
    ctx.strokeStyle = 'rgba(0, 210, 211, 0.15)';
    ctx.fillStyle = 'rgba(0, 210, 211, 0.6)';
    ctx.font = '10px monospace';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'bottom';

    for (let i = 1; i <= ringCount; i++) {
      const ringR = (maxR / ringCount) * i;
      ctx.beginPath();
      ctx.arc(cx, cy, ringR, 0, 2 * Math.PI);
      ctx.stroke();

      const ringDist = Math.round(ringStepNm * i);
      ctx.fillText(`${ringDist}NM`, cx + ringR + 3, cy - 2);
    }

    // Crosshairs & Azimuth lines (every 45 degrees)
    ctx.strokeStyle = 'rgba(0, 210, 211, 0.08)';
    for (let deg = 0; deg < 360; deg += 45) {
      const rad = (deg - 90) * Math.PI / 180;
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.lineTo(cx + Math.cos(rad) * maxR, cy + Math.sin(rad) * maxR);
      ctx.stroke();
    }

    // Draw Storm Cells / Weather Echoes
    if (showRadarLayer) {
      drawStormCells(ctx, cx, cy, maxR);
    }

    // Draw Synoptic Isobars
    if (showIsobarsLayer && currentSynopticData) {
      drawSynopticIsobars(ctx, cx, cy, maxR, w, h);
    }

    // Draw Synoptic Wind Barbs
    if (showBarbsLayer && currentSynopticData) {
      drawSynopticBarbs(ctx, cx, cy, maxR, w, h);
    }

    // Rotating Radar Sweep Beam
    if (sweepEnabled) {
      const rad = (sweepAngle - 90) * Math.PI / 180;
      const grad = ctx.createConicGradient(rad, cx, cy);
      grad.addColorStop(0, 'rgba(0, 210, 211, 0.25)');
      grad.addColorStop(0.1, 'rgba(0, 210, 211, 0.02)');
      grad.addColorStop(0.3, 'rgba(0, 210, 211, 0)');
      grad.addColorStop(1, 'rgba(0, 210, 211, 0)');

      ctx.save();
      ctx.beginPath();
      ctx.arc(cx, cy, maxR, 0, 2 * Math.PI);
      ctx.fillStyle = grad;
      ctx.fill();

      // Lead line
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.lineTo(cx + Math.cos(rad) * maxR, cy + Math.sin(rad) * maxR);
      ctx.lineWidth = 1.5;
      ctx.strokeStyle = 'rgba(0, 210, 211, 0.7)';
      ctx.stroke();
      ctx.restore();
    }

    // Aircraft / Station Center Reticle
    ctx.beginPath();
    ctx.arc(cx, cy, 4, 0, 2 * Math.PI);
    ctx.fillStyle = '#E94560';
    ctx.fill();

    // Aircraft miniature symbol
    ctx.strokeStyle = '#FFFFFF';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(cx, cy - 8);
    ctx.lineTo(cx, cy + 8);
    ctx.moveTo(cx - 10, cy - 1);
    ctx.lineTo(cx + 10, cy - 1);
    ctx.moveTo(cx - 4, cy + 6);
    ctx.lineTo(cx + 4, cy + 6);
    ctx.stroke();

    ctx.restore();
  }

  function drawSynopticIsobars(ctx, cx, cy, maxR, w, h) {
    if (!currentSynopticData || !currentSynopticData.isobars) return;
    const synScale = (maxR * 2) / 768.0;

    ctx.save();
    ctx.translate(cx - 384 * synScale, cy - 384 * synScale);
    ctx.scale(synScale, synScale);

    // Isobar lines
    for (const iso of currentSynopticData.isobars) {
      if (iso.svgPath && typeof Path2D !== 'undefined') {
        ctx.strokeStyle = iso.strokeColor || '#00F0FF';
        ctx.lineWidth = 1.8 / synScale;
        ctx.stroke(new Path2D(iso.svgPath));

        // Isobar label badge
        if (iso.label) {
          ctx.fillStyle = 'rgba(3, 7, 18, 0.85)';
          ctx.fillRect(iso.labelX, iso.labelY, 32, 14);
          ctx.strokeStyle = 'rgba(0, 240, 255, 0.4)';
          ctx.lineWidth = 1 / synScale;
          ctx.strokeRect(iso.labelX, iso.labelY, 32, 14);

          ctx.fillStyle = '#99D8EE';
          ctx.font = 'bold 9px monospace';
          ctx.textAlign = 'left';
          ctx.fillText(iso.label, iso.labelX + 3, iso.labelY + 10);
        }
      }
    }

    // Pressure Centers (High / Low badges)
    if (currentSynopticData.pressureCenters) {
      for (const pc of currentSynopticData.pressureCenters) {
        ctx.fillStyle = 'rgba(3, 7, 18, 0.85)';
        ctx.beginPath();
        ctx.arc(pc.x, pc.y, 16, 0, 2 * Math.PI);
        ctx.fill();
        ctx.strokeStyle = pc.colorHex || '#00F0FF';
        ctx.lineWidth = 2 / synScale;
        ctx.stroke();

        ctx.fillStyle = pc.colorHex || '#00F0FF';
        ctx.font = 'bold 11px sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(pc.label || '', pc.x, pc.y);
      }
    }

    ctx.restore();
  }

  function drawSynopticBarbs(ctx, cx, cy, maxR, w, h) {
    if (!currentSynopticData || !currentSynopticData.barbs) return;
    const synScale = (maxR * 2) / 768.0;

    ctx.save();
    ctx.translate(cx - 384 * synScale, cy - 384 * synScale);
    ctx.scale(synScale, synScale);

    ctx.strokeStyle = '#E0F2FE';
    ctx.lineWidth = 1.5 / synScale;

    for (const barb of currentSynopticData.barbs) {
      if (barb.svgPath && typeof Path2D !== 'undefined') {
        ctx.stroke(new Path2D(barb.svgPath));
      }
    }

    ctx.restore();
  }

  function drawStormCells(ctx, cx, cy, maxR) {
    const cells = currentEfbData && currentEfbData.stormCells ? currentEfbData.stormCells : [];

    if (cells.length === 0) {
      // Procedural light precip echo demonstration if precipitation is active
      if (currentEfbData && currentEfbData.rawMetar && (currentEfbData.rawMetar.includes('RA') || currentEfbData.rawMetar.includes('DZ') || currentEfbData.rawMetar.includes('SN'))) {
        drawSimulatedEcho(ctx, cx + 40, cy - 30, 28, '#00BB00');
        drawSimulatedEcho(ctx, cx - 60, cy + 25, 34, '#00FF00');
      }
      return;
    }

    // Render real storm cells from engine
    const pxPerNm = maxR / currentRangeNm;
    cells.forEach(cell => {
      const distNm = cell.distanceNm || 15;
      if (distNm > currentRangeNm * 1.1) return;

      const bearing = (cell.bearingDegrees || 0) - 90;
      const rad = bearing * Math.PI / 180;
      const r = distNm * pxPerNm;
      const cellX = cx + Math.cos(rad) * r;
      const cellY = cy + Math.sin(rad) * r;
      const radiusPx = Math.max(12, (cell.radiusNm || 8) * pxPerNm);

      const dbz = cell.maxReflectivityDbz || 45;
      const color = getDbzColor(dbz);

      drawSimulatedEcho(ctx, cellX, cellY, radiusPx, color);

      // Label
      ctx.fillStyle = '#FFFFFF';
      ctx.font = 'bold 9px monospace';
      ctx.textAlign = 'center';
      ctx.fillText(`${Math.round(dbz)} dBZ`, cellX, cellY - radiusPx - 3);
    });
  }

  function drawSimulatedEcho(ctx, x, y, radius, color) {
    const grad = ctx.createRadialGradient(x, y, 2, x, y, radius);
    grad.addColorStop(0, color);
    grad.addColorStop(0.7, color + '99');
    grad.addColorStop(1, 'rgba(0, 0, 0, 0)');

    ctx.beginPath();
    ctx.arc(x, y, radius, 0, 2 * Math.PI);
    ctx.fillStyle = grad;
    ctx.fill();
  }

  function getDbzColor(dbz) {
    if (dbz >= 60) return '#CC00FF'; // Hail
    if (dbz >= 50) return '#FF0000'; // Severe
    if (dbz >= 40) return '#FF9900'; // Heavy
    if (dbz >= 30) return '#FFFF00'; // Moderate
    return '#00FF00'; // Light
  }

  // =========================================================================
  // EVENT LISTENERS & USER CONTROLS
  // =========================================================================
  function setupEvents() {
    // Station Dropdown
    elStationSelect.addEventListener('change', () => {
      currentStation = elStationSelect.value;
      fetchEfbData(true);
    });

    // Custom Station ICAO Input
    elBtnStationGo.addEventListener('click', handleCustomStation);
    elStationInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') handleCustomStation();
    });

    // Refresh Button
    elBtnRefresh.addEventListener('click', () => {
      fetchEfbData(true);
      pollStatus();
    });

    // Copy METAR Button
    elBtnCopyMetar.addEventListener('click', async () => {
      const text = elRawMetarText.textContent;
      try {
        await navigator.clipboard.writeText(text);
        elCopyText.textContent = 'COPIED!';
        setTimeout(() => { elCopyText.textContent = 'COPY'; }, 2000);
      } catch {
        // Fallback
        elCopyText.textContent = 'COPIED!';
        setTimeout(() => { elCopyText.textContent = 'COPY'; }, 2000);
      }
    });

    // Toggle ATIS Raw Details
    if (elBtnToggleAtisRaw) {
      elBtnToggleAtisRaw.addEventListener('click', () => {
        if (!elAtisRawText) return;
        const isExpanded = elAtisRawText.style.display !== 'none';
        elAtisRawText.style.display = isExpanded ? 'none' : 'block';
        elBtnToggleAtisRaw.textContent = isExpanded ? 'EXPAND' : 'COLLAPSE';
      });
    }

    // Radar Range Buttons
    rangeBtns.forEach(btn => {
      btn.addEventListener('click', () => {
        rangeBtns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        currentRangeNm = parseInt(btn.dataset.range, 10) || 40;
        elRadarRangeBadge.textContent = `RANGE: ${currentRangeNm} NM`;
      });
    });

    // Zoom Controls
    elBtnZoomIn.addEventListener('click', () => {
      if (currentRangeNm > 20) {
        currentRangeNm = currentRangeNm === 160 ? 80 : currentRangeNm === 80 ? 40 : 20;
        updateActiveRangeBtn();
      }
    });

    elBtnZoomOut.addEventListener('click', () => {
      if (currentRangeNm < 160) {
        currentRangeNm = currentRangeNm === 20 ? 40 : currentRangeNm === 40 ? 80 : 160;
        updateActiveRangeBtn();
      }
    });

    // Radar Sweep Toggle
    elBtnRadarSweep.addEventListener('click', () => {
      sweepEnabled = !sweepEnabled;
      elBtnRadarSweep.classList.toggle('active', sweepEnabled);
    });

    // Freeze Button
    if (elBtnFreeze) {
      elBtnFreeze.addEventListener('click', toggleFreeze);
    }

    // Layer Buttons
    if (elBtnLayerRadar) {
      elBtnLayerRadar.addEventListener('click', () => {
        showRadarLayer = !showRadarLayer;
        elBtnLayerRadar.classList.toggle('active', showRadarLayer);
      });
    }
    if (elBtnLayerIsobars) {
      elBtnLayerIsobars.addEventListener('click', () => {
        showIsobarsLayer = !showIsobarsLayer;
        elBtnLayerIsobars.classList.toggle('active', showIsobarsLayer);
      });
    }
    if (elBtnLayerBarbs) {
      elBtnLayerBarbs.addEventListener('click', () => {
        showBarbsLayer = !showBarbsLayer;
        elBtnLayerBarbs.classList.toggle('active', showBarbsLayer);
      });
    }

    // SimBrief Fetch & FMC Exports
    if (elBtnFetchSimBrief) {
      elBtnFetchSimBrief.addEventListener('click', fetchSimBriefOfp);
    }
    if (elSimbriefPilotId) {
      elSimbriefPilotId.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') fetchSimBriefOfp();
      });
    }
    if (elBtnExportPmdg) {
      elBtnExportPmdg.addEventListener('click', () => triggerExport('pmdg'));
    }
    if (elBtnExportFenix) {
      elBtnExportFenix.addEventListener('click', () => triggerExport('fenix'));
    }
    if (elBtnExportCsv) {
      elBtnExportCsv.addEventListener('click', () => triggerExport('csv'));
    }
  }

  function updateActiveRangeBtn() {
    rangeBtns.forEach(btn => {
      if (parseInt(btn.dataset.range, 10) === currentRangeNm) {
        btn.classList.add('active');
      } else {
        btn.classList.remove('active');
      }
    });
    elRadarRangeBadge.textContent = `RANGE: ${currentRangeNm} NM`;
  }

  function handleCustomStation() {
    const val = elStationInput.value.trim().toUpperCase();
    if (val.length >= 3) {
      currentStation = val;
      fetchEfbData(true);
      fetchSynopticData();
      fetchSoundingData();
      elStationInput.value = '';
    }
  }

  // =========================================================================
  // INITIALIZATION
  // =========================================================================
  function init() {
    setupEvents();
    initRadar();
    pollStatus();
    pollFreeze();
    fetchEfbData(true);
    fetchSynopticData();
    fetchSoundingData();
    pollSimBrief();

    // Poll status and EFB data periodically every 5 seconds
    pollIntervalId = setInterval(() => {
      pollStatus();
      pollFreeze();
      fetchEfbData(false);
      fetchSynopticData();
      fetchSoundingData();
      pollSimBrief();
    }, 5000);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
