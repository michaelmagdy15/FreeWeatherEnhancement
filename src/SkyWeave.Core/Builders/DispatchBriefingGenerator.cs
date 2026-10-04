using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.Core.Builders;

public class DispatchBriefingGenerator
{
    public DispatchBriefing BuildBriefing(
        SimBriefPlan plan,
        MetarData? depMetar = null,
        TafData? depTaf = null,
        MetarData? destMetar = null,
        TafData? destTaf = null,
        MetarData? altMetar = null,
        TafData? altTaf = null,
        RouteHazardProfile? hazardProfile = null,
        AirportData? depAirport = null,
        AirportData? destAirport = null,
        AirportData? altAirport = null)
    {
        var briefing = new DispatchBriefing
        {
            FlightNumber = !string.IsNullOrWhiteSpace(plan.FlightNumber) ? plan.FlightNumber : "SW-101",
            AircraftType = !string.IsNullOrWhiteSpace(plan.AircraftType) ? plan.AircraftType : "B738",
            OriginIcao = plan.Origin,
            DestinationIcao = plan.Destination,
            AlternateIcao = !string.IsNullOrWhiteSpace(plan.Alternate) ? plan.Alternate : null,
            CruiseAltitudeFt = plan.CruiseAltitudeFt > 0 ? plan.CruiseAltitudeFt : 34000,
            EstimatedTimeEnrouteMinutes = plan.EstimatedTimeEnrouteMinutes,
            RouteString = plan.RouteString,
            AiracCycle = !string.IsNullOrWhiteSpace(plan.AiracCycle) ? plan.AiracCycle : "CURRENT",
            ReleaseNumber = $"REL-{DateTime.UtcNow:MMdd}-01",
            GeneratedAtUtc = DateTime.UtcNow
        };

        // 1. Station Briefings
        briefing.DepartureBriefing = BuildStationBriefing(plan.Origin, StationBriefingRole.Departure, depMetar, depTaf, depAirport);
        briefing.DestinationBriefing = BuildStationBriefing(plan.Destination, StationBriefingRole.Destination, destMetar, destTaf, destAirport);
        if (!string.IsNullOrWhiteSpace(plan.Alternate))
        {
            briefing.AlternateBriefing = BuildStationBriefing(plan.Alternate, StationBriefingRole.Alternate, altMetar, altTaf, altAirport);
        }

        // 2. Navlog Waypoints
        var waypoints = plan.Waypoints ?? new List<SimBriefWaypoint>();
        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            double legCourse = 0;
            if (i < waypoints.Count - 1)
            {
                var nextWp = waypoints[i + 1];
                legCourse = RouteHazardAnalyzer.CalculateBearing(wp.Latitude, wp.Longitude, nextWp.Latitude, nextWp.Longitude);
            }
            else if (i > 0)
            {
                var prevWp = waypoints[i - 1];
                legCourse = RouteHazardAnalyzer.CalculateBearing(prevWp.Latitude, prevWp.Longitude, wp.Latitude, wp.Longitude);
            }

            var hw = RouteHazardAnalyzer.CalculateHeadwindComponent(wp.WindDirection, wp.WindSpeedKt, legCourse);
            var xw = Math.Abs(Math.Round(wp.WindSpeedKt * Math.Sin((wp.WindDirection - legCourse) * Math.PI / 180.0), 1));

            var entry = new NavlogWaypointEntry
            {
                Identifier = wp.Identifier,
                Stage = wp.Stage,
                AltitudeFt = wp.AltitudeFt,
                Latitude = wp.Latitude,
                Longitude = wp.Longitude,
                WindDirection = wp.WindDirection,
                WindSpeedKt = wp.WindSpeedKt,
                TemperatureCelsius = wp.TemperatureC,
                HeadwindComponentKt = hw,
                CrosswindComponentKt = xw,
                TurbulenceRisk = wp.AltitudeFt > 28000 && wp.WindSpeedKt > 80 ? "Moderate CAT" : "Smooth",
                IcingRisk = wp.TemperatureC >= -20 && wp.TemperatureC <= 0 && wp.AltitudeFt < 20000 ? "Trace/Light" : "None"
            };

            briefing.Navlog.Add(entry);
        }

        // 3. Hazard Summary & Critical Advisories
        var hz = briefing.HazardSummary;
        if (hazardProfile != null)
        {
            hz.TotalDistanceNm = hazardProfile.TotalDistanceNm;
            hz.AverageHeadwindKt = hazardProfile.AverageHeadwindKt;
            hz.MaxTurbulenceIndex = hazardProfile.MaxTurbulenceIndex;
            hz.IcingRiskCount = hazardProfile.IcingRisks.Count;
            hz.IntersectingStormCellsCount = hazardProfile.IntersectingStormCells.Count;
            hz.SigmetCount = hazardProfile.SeverePockets.Count;
        }
        else
        {
            hz.TotalDistanceNm = CalculateTotalRouteDistance(waypoints);
            hz.AverageHeadwindKt = briefing.Navlog.Count > 0 ? Math.Round(briefing.Navlog.Average(w => w.HeadwindComponentKt), 1) : 0;
            hz.MaxTurbulenceIndex = 0.2;
        }

        hz.MaxTurbulenceDescription = hz.MaxTurbulenceIndex switch
        {
            >= 0.7 => "Severe",
            >= 0.45 => "Moderate",
            >= 0.2 => "Light",
            _ => "Smooth"
        };

        // Critical Advisories
        if (briefing.DestinationBriefing != null && (briefing.DestinationBriefing.FlightCategory == "IFR" || briefing.DestinationBriefing.FlightCategory == "LIFR"))
        {
            hz.CriticalAdvisories.Add($"⚠️ DESTINATION ({briefing.DestinationIcao}) CURRENTLY REPORTING {briefing.DestinationBriefing.FlightCategory} CONDITIONS ({briefing.DestinationBriefing.CeilingText}, VIS {briefing.DestinationBriefing.VisibilityText}) — ENSURE DESTINATION & ALTERNATE FUEL CONTINGENCY.");
        }

        if (briefing.AlternateBriefing != null && (briefing.AlternateBriefing.FlightCategory == "IFR" || briefing.AlternateBriefing.FlightCategory == "LIFR"))
        {
            hz.CriticalAdvisories.Add($"⚠️ FILED ALTERNATE ({briefing.AlternateIcao}) HAS {briefing.AlternateBriefing.FlightCategory} CEILING/VISIBILITY — VERIFY ALTERNATE AIRPORT MINIMUMS.");
        }

        if (hz.AverageHeadwindKt > 25)
        {
            hz.CriticalAdvisories.Add($"💨 HIGH HEADWIND COMPONENT (+{hz.AverageHeadwindKt:F0} KT AVERAGE ALONG ROUTE) — MONITOR FMC FUEL PREDICTIONS.");
        }
        else if (hz.AverageHeadwindKt < -25)
        {
            hz.CriticalAdvisories.Add($"⚡ STRONG TAILWIND ADVANTAGE ({Math.Abs(hz.AverageHeadwindKt):F0} KT AVERAGE) — EXPECT REDUCED FLIGHT TIME.");
        }

        if (hz.MaxTurbulenceIndex >= 0.45)
        {
            hz.CriticalAdvisories.Add($"⚠️ {hz.MaxTurbulenceDescription.ToUpperInvariant()} TURBULENCE FORECAST ALONG CRUISE TRACK — OBSERVE RECOMMENDED TURBULENCE PENETRATION SPEED.");
        }

        if (hz.IntersectingStormCellsCount > 0)
        {
            hz.CriticalAdvisories.Add($"⚡ {hz.IntersectingStormCellsCount} CONVECTIVE STORM CELL(S) DETECTED IN FLIGHT CORRIDOR — AIRBORNE WEATHER RADAR MONITORING & LATERAL DEVIATIONS REQUIRED.");
        }

        if (hz.IcingRiskCount > 0)
        {
            hz.CriticalAdvisories.Add($"❄️ AIRFRAME ICING POCKETS DETECTED ENROUTE — VERIFY ENGINE AND WING ANTI-ICE ANTI-COLLISION SYSTEMS ARMED.");
        }

        if (hz.CriticalAdvisories.Count == 0)
        {
            hz.CriticalAdvisories.Add("✅ ROUTE CLEAR OF SEVERE ATMOSPHERIC HAZARDS. STANDARD DISPATCH FUEL RESERVES APPLIED.");
        }

        // Dispatcher Notes
        var eteFormatted = TimeSpan.FromMinutes(briefing.EstimatedTimeEnrouteMinutes).ToString(@"hh\:mm");
        hz.DispatcherNotes.Add("FLIGHT DISPATCH RELEASE PREPARED IN ACCORDANCE WITH FAA / ICAO DISPATCH RULES.");
        hz.DispatcherNotes.Add($"AIRAC NAVIGRAPH CYCLE: {briefing.AiracCycle}. PLANNED CRUISE: FL{briefing.CruiseAltitudeFt / 100:000}.");
        hz.DispatcherNotes.Add($"ESTIMATED TIME ENROUTE: {eteFormatted} | ROUTE DISTANCE: {hz.TotalDistanceNm:F0} NM.");
        hz.DispatcherNotes.Add("PILOT IN COMMAND IS RESPONSIBLE FOR COMPLIANCE WITH AIRPORT MINIMUMS AND FINAL FUEL DECISION.");

        return briefing;
    }

    private static StationWeatherBriefing BuildStationBriefing(
        string icao,
        StationBriefingRole role,
        MetarData? metar,
        TafData? taf,
        AirportData? airport)
    {
        var b = new StationWeatherBriefing
        {
            Icao = icao.ToUpperInvariant(),
            Name = airport?.Name ?? icao.ToUpperInvariant(),
            ElevationFeet = airport?.ElevationFeet ?? 0,
            Role = role
        };

        if (metar != null)
        {
            b.RawMetar = metar.RawText;
            b.ObservationTimeUtc = metar.ObservationTime;
            b.FlightCategory = !string.IsNullOrWhiteSpace(metar.FlightCategory) ? metar.FlightCategory : "VFR";
            b.SurfaceWindText = $"{metar.WindDirectionDegrees:000}° @ {metar.WindSpeedKnots:0} KT" + (metar.WindGustKnots > 0 ? $" G{metar.WindGustKnots:0} KT" : "");
            var ceilingCloud = metar.Clouds?.FirstOrDefault(c => c.Coverage == "BKN" || c.Coverage == "OVC" || c.Coverage == "VV");
            b.CeilingText = ceilingCloud != null ? $"CIG {ceilingCloud.Coverage}{ceilingCloud.BaseFeet:000} ({ceilingCloud.BaseFeet:N0} ft AGL)" : (metar.Clouds != null && metar.Clouds.Count > 0 ? string.Join(" ", metar.Clouds.Select(c => $"{c.Coverage}{c.BaseFeet:000}")) : "CLR");
            b.TemperatureCelsius = metar.TemperatureCelsius;
            b.DewpointCelsius = metar.DewpointCelsius;
            b.AltimeterHpa = metar.AltimeterHpa > 0 ? metar.AltimeterHpa : 1013.25;
            b.AltimeterInHg = Math.Round(b.AltimeterHpa * 0.02952998751, 2);

            b.RunwayWindAnalysis = $"Wind {b.SurfaceWindText}. Baro QNH {b.AltimeterHpa:F0} hPa ({b.AltimeterInHg:F2} inHg).";
        }
        else
        {
            b.RawMetar = $"{icao} (METAR not received or station offline)";
            b.FlightCategory = "VFR";
            b.SurfaceWindText = "CALM";
            b.VisibilityText = "10+ SM";
            b.CeilingText = "CLR";
            b.AltimeterHpa = 1013.25;
            b.AltimeterInHg = 29.92;
            b.RunwayWindAnalysis = "Standard altimeter 29.92 inHg (1013 hPa).";
        }

        if (taf != null)
        {
            b.RawTaf = taf.RawText;
            b.TafSummary = $"Valid from {taf.ValidFrom:ddHHmm}Z to {taf.ValidTo:ddHHmm}Z (Fcst: {taf.FlightCategory}, Wind: {taf.WindDirectionDegrees:000}° @ {taf.WindSpeedKnots:0} KT).";
        }
        else
        {
            b.RawTaf = $"{icao} (TAF not available)";
            b.TafSummary = "No terminal aerodrome forecast filed.";
        }

        return b;
    }

    private static double CalculateTotalRouteDistance(List<SimBriefWaypoint> waypoints)
    {
        if (waypoints.Count < 2) return 0;
        double sum = 0;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            sum += RouteHazardAnalyzer.CalculateDistanceNm(
                waypoints[i].Latitude, waypoints[i].Longitude,
                waypoints[i + 1].Latitude, waypoints[i + 1].Longitude);
        }
        return Math.Round(sum, 1);
    }

    public string GenerateHtml(DispatchBriefing b, bool darkMode = false)
    {
        var sb = new StringBuilder();
        var bgCol = darkMode ? "#0b0f19" : "#f8fafc";
        var cardBg = darkMode ? "#111827" : "#ffffff";
        var textCol = darkMode ? "#e2e8f0" : "#0f172a";
        var textMuted = darkMode ? "#94a3b8" : "#64748b";
        var borderColor = darkMode ? "#1e293b" : "#e2e8f0";
        var terminalBg = darkMode ? "#030712" : "#f1f5f9";

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>SkyWeave Dispatch Briefing - {b.FlightNumber} {b.OriginIcao} → {b.DestinationIcao}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine($"    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: {bgCol}; color: {textCol}; margin: 0; padding: 24px; font-size: 13px; line-height: 1.5; }}");
        sb.AppendLine("    .container { max-width: 960px; margin: 0 auto; }");
        sb.AppendLine("    .no-print { margin-bottom: 20px; display: flex; gap: 10px; align-items: center; }");
        sb.AppendLine("    .btn { background: #0284c7; color: #fff; border: none; padding: 8px 16px; border-radius: 6px; font-weight: 600; cursor: pointer; text-decoration: none; display: inline-flex; align-items: center; gap: 6px; font-size: 13px; }");
        sb.AppendLine("    .btn:hover { background: #0369a1; }");
        sb.AppendLine("    .btn-secondary { background: #475569; }");
        sb.AppendLine("    .btn-secondary:hover { background: #334155; }");
        sb.AppendLine($"    .card {{ background: {cardBg}; border: 1px solid {borderColor}; border-radius: 8px; padding: 20px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }}");
        sb.AppendLine("    .header-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; border-bottom: 1px solid #cbd5e1; padding-bottom: 16px; margin-bottom: 16px; }");
        sb.AppendLine($"    .stat-label {{ font-size: 10px; font-weight: 700; color: {textMuted}; text-transform: uppercase; letter-spacing: 0.5px; }}");
        sb.AppendLine($"    .stat-value {{ font-size: 16px; font-weight: 700; color: {textCol}; font-family: 'SF Mono', Consolas, monospace; }}");
        sb.AppendLine("    .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-weight: 700; font-size: 11px; text-transform: uppercase; }");
        sb.AppendLine("    .badge-vfr { background: #dcfce7; color: #15803d; border: 1px solid #86efac; }");
        sb.AppendLine("    .badge-mvfr { background: #dbeafe; color: #1d4ed8; border: 1px solid #93c5fd; }");
        sb.AppendLine("    .badge-ifr { background: #fee2e2; color: #b91c1c; border: 1px solid #fca5a5; }");
        sb.AppendLine("    .badge-lifr { background: #f3e8ff; color: #7e22ce; border: 1px solid #d8b4fe; }");
        sb.AppendLine("    .advisory-box { background: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; border-radius: 4px; margin-bottom: 12px; color: #92400e; font-size: 12px; font-weight: 500; }");
        sb.AppendLine("    .note-box { background: #eff6ff; border-left: 4px solid #3b82f6; padding: 10px 14px; border-radius: 4px; margin-bottom: 8px; color: #1e40af; font-size: 11px; font-family: monospace; }");
        sb.AppendLine($"    .terminal-box {{ background: {terminalBg}; border: 1px solid {borderColor}; border-radius: 6px; padding: 12px; font-family: 'SF Mono', Consolas, Monaco, monospace; font-size: 12px; line-height: 1.6; word-break: break-all; white-space: pre-wrap; margin: 8px 0 16px 0; }}");
        sb.AppendLine("    table.navlog-table { width: 100%; border-collapse: collapse; font-family: 'SF Mono', Consolas, monospace; font-size: 11px; text-align: left; }");
        sb.AppendLine($"    table.navlog-table th {{ background: {(darkMode ? "#1f2937" : "#f1f5f9")}; padding: 8px 10px; border-bottom: 2px solid {borderColor}; font-weight: 700; color: {textMuted}; }}");
        sb.AppendLine($"    table.navlog-table td {{ padding: 7px 10px; border-bottom: 1px solid {borderColor}; }}");
        sb.AppendLine($"    table.navlog-table tr:nth-child(even) {{ background: {(darkMode ? "#131c2e" : "#f8fafc")}; }}");
        sb.AppendLine("    @media print {");
        sb.AppendLine("      body { background: #ffffff !important; color: #000000 !important; padding: 0 !important; font-size: 11px !important; }");
        sb.AppendLine("      .container { max-width: 100% !important; margin: 0 !important; }");
        sb.AppendLine("      .no-print { display: none !important; }");
        sb.AppendLine("      .card { border: 1px solid #ccc !important; box-shadow: none !important; margin-bottom: 16px !important; padding: 14px !important; page-break-inside: avoid; }");
        sb.AppendLine("      .terminal-box { background: #f8f8f8 !important; border: 1px solid #ddd !important; color: #000 !important; }");
        sb.AppendLine("      .page-break { page-break-after: always; }");
        sb.AppendLine("    }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Action Toolbar (Hidden in print)
        sb.AppendLine("    <div class=\"no-print\">");
        sb.AppendLine("      <button class=\"btn\" onclick=\"window.print()\">🖨️ Print Dispatch Package / Save PDF</button>");
        sb.AppendLine($"      <a class=\"btn btn-secondary\" href=\"?dark={(darkMode ? "false" : "true")}\">{(darkMode ? "☀️ Light Mode" : "🌙 Dark Mode")}</a>");
        sb.AppendLine($"      <button class=\"btn btn-secondary\" onclick=\"navigator.clipboard.writeText('{EscapeJs(b.RouteString)}'); alert('Route copied to clipboard!')\">📋 Copy Route</button>");
        sb.AppendLine("    </div>");

        // Release Header Card
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <div style=\"display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px;\">");
        sb.AppendLine($"        <h1 style=\"margin: 0; font-size: 20px; font-weight: 800; letter-spacing: -0.5px;\">✈️ SKYWEAVE OPERATIONAL DISPATCH RELEASE</h1>");
        sb.AppendLine($"        <span style=\"font-family: monospace; font-size: 12px; font-weight: bold; background: #e0f2fe; color: #0369a1; padding: 4px 8px; border-radius: 4px;\">{b.ReleaseNumber}</span>");
        sb.AppendLine("      </div>");
        var altIcao = b.AlternateIcao ?? "NONE";
        var enrouteTime = TimeSpan.FromMinutes(b.EstimatedTimeEnrouteMinutes).ToString(@"hh\:mm");
        sb.AppendLine("      <div class=\"header-grid\">");
        sb.AppendLine($"        <div><div class=\"stat-label\">Flight No.</div><div class=\"stat-value\">{b.FlightNumber}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Aircraft</div><div class=\"stat-value\">{b.AircraftType}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Departure</div><div class=\"stat-value\">{b.OriginIcao}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Destination</div><div class=\"stat-value\">{b.DestinationIcao}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Alternate</div><div class=\"stat-value\">{altIcao}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Planned FL</div><div class=\"stat-value\">FL{b.CruiseAltitudeFt / 100:000}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Enroute Time</div><div class=\"stat-value\">{enrouteTime}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">AIRAC</div><div class=\"stat-value\">{b.AiracCycle}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Generated UTC</div><div class=\"stat-value\">{b.GeneratedAtUtc:HH:mm:ss}Z</div></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div>");
        sb.AppendLine("        <div class=\"stat-label\" style=\"margin-bottom: 4px;\">ATC FILED ROUTE</div>");
        sb.AppendLine($"        <div class=\"terminal-box\" style=\"margin-bottom: 0;\">{b.RouteString}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Critical Advisories
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <h2 style=\"margin-top: 0; font-size: 15px; font-weight: 700; border-bottom: 1px solid #cbd5e1; padding-bottom: 8px;\">⚠️ DISPATCH CRITICAL WEATHER ADVISORIES &amp; EN-ROUTE HAZARDS</h2>");
        foreach (var adv in b.HazardSummary.CriticalAdvisories)
        {
            sb.AppendLine($"      <div class=\"advisory-box\">{adv}</div>");
        }
        sb.AppendLine("      <div style=\"display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; margin-top: 14px;\">");
        sb.AppendLine($"        <div><div class=\"stat-label\">Route Distance</div><div class=\"stat-value\">{b.HazardSummary.TotalDistanceNm:F0} NM</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Avg Headwind Component</div><div class=\"stat-value\">{(b.HazardSummary.AverageHeadwindKt >= 0 ? "+" : "")}{b.HazardSummary.AverageHeadwindKt:F0} KT</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Max Turbulence</div><div class=\"stat-value\">{b.HazardSummary.MaxTurbulenceDescription} (Index {b.HazardSummary.MaxTurbulenceIndex:F2})</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Active Storm Cells</div><div class=\"stat-value\">{b.HazardSummary.IntersectingStormCellsCount} Cells</div></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Airport Weather Packages
        RenderStationHtml(sb, b.DepartureBriefing, "DEPARTURE AERODROME");
        RenderStationHtml(sb, b.DestinationBriefing, "DESTINATION AERODROME");
        if (b.AlternateBriefing != null)
        {
            RenderStationHtml(sb, b.AlternateBriefing, "FILED ALTERNATE AERODROME");
        }

        // Navlog Table
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <h2 style=\"margin-top: 0; font-size: 15px; font-weight: 700; border-bottom: 1px solid #cbd5e1; padding-bottom: 8px;\">📋 NAVIGATION &amp; WINDS ALOFT LOG (NAVLOG)</h2>");
        sb.AppendLine("      <div style=\"overflow-x: auto;\">");
        sb.AppendLine("        <table class=\"navlog-table\">");
        sb.AppendLine("          <thead>");
        sb.AppendLine("            <tr>");
        sb.AppendLine("              <th>WAYPOINT</th>");
        sb.AppendLine("              <th>STAGE</th>");
        sb.AppendLine("              <th>ALT (FT)</th>");
        sb.AppendLine("              <th>WIND DIR/SPD</th>");
        sb.AppendLine("              <th>TEMP (°C)</th>");
        sb.AppendLine("              <th>HWC (KT)</th>");
        sb.AppendLine("              <th>XWC (KT)</th>");
        sb.AppendLine("              <th>TURB</th>");
        sb.AppendLine("              <th>ICING</th>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </thead>");
        sb.AppendLine("          <tbody>");
        foreach (var wp in b.Navlog)
        {
            var hwcFormatted = wp.HeadwindComponentKt >= 0 ? $"+{wp.HeadwindComponentKt:F0}H" : $"{Math.Abs(wp.HeadwindComponentKt):F0}T";
            sb.AppendLine("            <tr>");
            sb.AppendLine($"              <td><strong>{wp.Identifier}</strong></td>");
            sb.AppendLine($"              <td>{wp.Stage}</td>");
            sb.AppendLine($"              <td>{(wp.AltitudeFt >= 18000 ? $"FL{wp.AltitudeFt / 100:000}" : $"{wp.AltitudeFt:N0}")}</td>");
            sb.AppendLine($"              <td>{wp.WindDirection:000}° / {wp.WindSpeedKt:00} KT</td>");
            sb.AppendLine($"              <td>{(wp.TemperatureCelsius > 0 ? "+" : "")}{wp.TemperatureCelsius:0}°C</td>");
            sb.AppendLine($"              <td>{hwcFormatted}</td>");
            sb.AppendLine($"              <td>{wp.CrosswindComponentKt:F0} KT</td>");
            sb.AppendLine($"              <td>{wp.TurbulenceRisk}</td>");
            sb.AppendLine($"              <td>{wp.IcingRisk}</td>");
            sb.AppendLine("            </tr>");
        }
        sb.AppendLine("          </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Dispatcher Legal Notes & Signature
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <h2 style=\"margin-top: 0; font-size: 15px; font-weight: 700; border-bottom: 1px solid #cbd5e1; padding-bottom: 8px;\">📝 DISPATCH REMARKS &amp; OPERATIONAL SIGNOFF</h2>");
        foreach (var note in b.HazardSummary.DispatcherNotes)
        {
            sb.AppendLine($"      <div class=\"note-box\">{note}</div>");
        }
        sb.AppendLine("      <div style=\"display: flex; justify-content: space-between; margin-top: 24px; padding-top: 16px; border-top: 1px dashed #cbd5e1; font-size: 11px;\">");
        sb.AppendLine("        <div>DISPATCHER ID: <strong>SW-AUTO-DISPATCH</strong></div>");
        sb.AppendLine($"        <div>DISPATCH RELEASE TIME: <strong>{b.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC</strong></div>");
        sb.AppendLine("        <div>PILOT ACCEPTANCE: __________________________</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void RenderStationHtml(StringBuilder sb, StationWeatherBriefing? s, string title)
    {
        if (s == null) return;
        var badgeClass = s.FlightCategory switch
        {
            "LIFR" => "badge badge-lifr",
            "IFR" => "badge badge-ifr",
            "MVFR" => "badge badge-mvfr",
            _ => "badge badge-vfr"
        };

        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <div style=\"display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;\">");
        sb.AppendLine($"        <h3 style=\"margin: 0; font-size: 14px; font-weight: 700;\">{title}: {s.Icao} - {s.Name} (ELEV {s.ElevationFeet:N0} FT)</h3>");
        sb.AppendLine($"        <span class=\"{badgeClass}\">{s.FlightCategory}</span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div style=\"display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; margin-bottom: 12px;\">");
        sb.AppendLine($"        <div><div class=\"stat-label\">Surface Wind</div><div class=\"stat-value\" style=\"font-size: 13px;\">{s.SurfaceWindText}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Visibility</div><div class=\"stat-value\" style=\"font-size: 13px;\">{s.VisibilityText}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Ceiling</div><div class=\"stat-value\" style=\"font-size: 13px;\">{s.CeilingText}</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Altimeter / QNH</div><div class=\"stat-value\" style=\"font-size: 13px;\">{s.AltimeterInHg:F2} inHg / {s.AltimeterHpa:F0} hPa</div></div>");
        sb.AppendLine($"        <div><div class=\"stat-label\">Temp / Dewpoint</div><div class=\"stat-value\" style=\"font-size: 13px;\">{s.TemperatureCelsius:0}°C / {s.DewpointCelsius:0}°C</div></div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"stat-label\">CURRENT METAR OBSERVATION</div>");
        sb.AppendLine($"      <div class=\"terminal-box\">{s.RawMetar}</div>");
        sb.AppendLine("      <div class=\"stat-label\">TERMINAL AERODROME FORECAST (TAF)</div>");
        sb.AppendLine($"      <div class=\"terminal-box\">{s.RawTaf}</div>");
        sb.AppendLine("    </div>");
    }

    private static string EscapeJs(string text)
    {
        return text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}

public static class WeatherStateBriefingExtensions
{
    public static MetarData? ToMetarData(this WeatherState? state)
    {
        if (state == null) return null;
        return new MetarData
        {
            StationId = state.StationId,
            RawText = state.RawMetar,
            ObservationTime = state.ObservationTime,
            WindDirectionDegrees = state.WindDirectionDegrees,
            WindSpeedKnots = state.WindSpeedKnots,
            WindGustKnots = state.WindGustKnots,
            VisibilityMeters = state.VisibilityMeters,
            TemperatureCelsius = state.TemperatureCelsius,
            DewpointCelsius = state.DewpointCelsius,
            AltimeterHpa = state.AltimeterHpa,
            FlightCategory = state.FlightCategory,
            Clouds = state.CloudLayers?.Select(c => new MetarCloud
            {
                Coverage = c.Type.ToString(),
                BaseFeet = (int)(c.BaseFeetAgl > 0 ? c.BaseFeetAgl : c.BaseMeters * 3.28084)
            }).ToList() ?? new List<MetarCloud>()
        };
    }
}
