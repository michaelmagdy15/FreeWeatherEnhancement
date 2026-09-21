using System;
using System.Collections.Generic;
using System.Linq;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class RouteHazardAnalyzer
{
    private const double EarthRadiusNm = 3440.065;

    public RouteHazardProfile AnalyzeRoute(
        SimBriefPlan plan,
        IEnumerable<WeatherHazard>? hazards = null,
        IEnumerable<StormCell>? stormCells = null,
        IEnumerable<TurbulenceLayer>? turbulenceLayers = null,
        IEnumerable<IcingLayer>? icingLayers = null,
        double corridorBufferNm = 25.0)
    {
        var profile = new RouteHazardProfile();
        if (plan == null || plan.Waypoints.Count == 0)
        {
            return profile;
        }

        var waypoints = plan.Waypoints;
        var hazardList = hazards?.ToList() ?? new List<WeatherHazard>();
        var cellList = stormCells?.ToList() ?? new List<StormCell>();
        var turbList = turbulenceLayers?.ToList() ?? new List<TurbulenceLayer>();
        var iceLayerList = icingLayers?.ToList() ?? new List<IcingLayer>();

        // 1. Calculate total distance, leg courses, and distance-weighted headwind
        double totalDistance = 0;
        double weightedHeadwindSum = 0;

        // Collect discrete interpolated corridor sample points
        var routeSamples = new List<(double Lat, double Lon, double AltFt)>();

        if (waypoints.Count == 1)
        {
            var singleWp = waypoints[0];
            routeSamples.Add((singleWp.Latitude, singleWp.Longitude, singleWp.AltitudeFt));
            var hw = CalculateHeadwindComponent(singleWp.WindDirection, singleWp.WindSpeedKt, 0);
            profile.AverageHeadwindKt = Math.Round(hw, 1);
        }
        else
        {
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var w1 = waypoints[i];
                var w2 = waypoints[i + 1];

                var legDist = CalculateDistanceNm(w1.Latitude, w1.Longitude, w2.Latitude, w2.Longitude);
                var legBearing = CalculateBearing(w1.Latitude, w1.Longitude, w2.Latitude, w2.Longitude);

                // Compute headwind at w1 and w2 relative to leg bearing
                var hw1 = CalculateHeadwindComponent(w1.WindDirection, w1.WindSpeedKt, legBearing);
                var hw2 = CalculateHeadwindComponent(w2.WindDirection, w2.WindSpeedKt, legBearing);
                var avgLegHw = (hw1 + hw2) / 2.0;

                totalDistance += legDist;
                weightedHeadwindSum += avgLegHw * legDist;

                // Interpolate corridor samples every ~15 nm
                int steps = Math.Max(1, (int)Math.Ceiling(legDist / 15.0));
                for (int s = 0; s < steps; s++)
                {
                    double frac = (double)s / steps;
                    var pt = InterpolateGreatCircle(w1.Latitude, w1.Longitude, w2.Latitude, w2.Longitude, frac);
                    double alt = w1.AltitudeFt + frac * (w2.AltitudeFt - w1.AltitudeFt);
                    routeSamples.Add((pt.Lat, pt.Lon, alt));
                }
            }

            var lastWp = waypoints[^1];
            routeSamples.Add((lastWp.Latitude, lastWp.Longitude, lastWp.AltitudeFt));

            profile.TotalDistanceNm = Math.Round(totalDistance, 1);
            profile.AverageHeadwindKt = totalDistance > 0 ? Math.Round(weightedHeadwindSum / totalDistance, 1) : 0;
        }

        // 2. Corridor intersection for Storm Cells
        foreach (var cell in cellList)
        {
            double minDist = MinDistanceToRouteSamples(cell.Latitude, cell.Longitude, routeSamples);
            double effectiveBuffer = corridorBufferNm + cell.RadiusNm;

            if (minDist <= effectiveBuffer)
            {
                profile.IntersectingStormCells.Add(cell);

                var stormHazard = new WeatherHazard
                {
                    Id = $"STORM-{cell.Type}-{cell.Latitude:F2}_{cell.Longitude:F2}",
                    Type = HazardType.ConvectiveSigmet,
                    Description = $"Thunderstorm {cell.Type} cell within corridor ({minDist:F1} nm from track, intensity {cell.Intensity:P0})",
                    Severity = Math.Clamp(cell.Intensity, 0.0, 1.0),
                    Latitude = cell.Latitude,
                    Longitude = cell.Longitude,
                    AltitudeMinFeet = 0,
                    AltitudeMaxFeet = cell.AltitudeFeet > 0 ? cell.AltitudeFeet : 45000,
                    RawText = $"Storm cell {cell.Type} radius {cell.RadiusNm:F1}nm"
                };
                profile.SeverePockets.Add(stormHazard);
            }
        }

        // 3. Corridor intersection for SIGMETs / Hazards
        foreach (var hazard in hazardList)
        {
            if (hazard.Latitude.HasValue && hazard.Longitude.HasValue)
            {
                double dist = MinDistanceToRouteSamples(hazard.Latitude.Value, hazard.Longitude.Value, routeSamples);
                if (dist <= corridorBufferNm)
                {
                    profile.SeverePockets.Add(hazard);

                    if (hazard.Type == HazardType.IcingSigmet)
                    {
                        profile.IcingRisks.Add(new RouteIcingRisk
                        {
                            Id = hazard.Id,
                            Type = HazardType.IcingSigmet,
                            Description = hazard.Description,
                            Severity = hazard.Severity,
                            Latitude = hazard.Latitude,
                            Longitude = hazard.Longitude,
                            AltitudeMinFeet = hazard.AltitudeMinFeet,
                            AltitudeMaxFeet = hazard.AltitudeMaxFeet,
                            IcingSeverity = hazard.Severity >= 0.8 ? IcingSeverity.Severe : IcingSeverity.Moderate,
                            IcingType = IcingType.Mixed,
                            RawText = hazard.RawText
                        });
                    }
                }
            }
        }

        // 4. Icing Risk Assessment along Waypoints (0 to -40 C envelope, peak -15 C)
        foreach (var wp in waypoints)
        {
            if (wp.TemperatureC <= 0 && wp.TemperatureC >= -40)
            {
                var matchingLayer = iceLayerList.FirstOrDefault(l => wp.AltitudeFt >= l.BaseFeet && wp.AltitudeFt <= l.TopFeet);

                IcingSeverity severity;
                IcingType icingType;
                if (matchingLayer != null)
                {
                    severity = matchingLayer.Severity;
                    icingType = matchingLayer.IcingType;
                }
                else
                {
                    // Temperature envelope factor peaking at -15 C
                    double tempFactor = 1.0 - Math.Abs(wp.TemperatureC + 15.0) / 15.0;
                    tempFactor = Math.Clamp(tempFactor, 0.0, 1.0);

                    severity = tempFactor switch
                    {
                        >= 0.75 => IcingSeverity.Severe,
                        >= 0.40 => IcingSeverity.Moderate,
                        >= 0.10 => IcingSeverity.Light,
                        _ => IcingSeverity.None
                    };
                    icingType = wp.TemperatureC > -10 ? IcingType.Clear : (wp.TemperatureC < -15 ? IcingType.Rime : IcingType.Mixed);
                }

                if (severity != IcingSeverity.None)
                {
                    var risk = new RouteIcingRisk
                    {
                        Id = $"ICE-{wp.Identifier}",
                        WaypointIdentifier = wp.Identifier,
                        Latitude = wp.Latitude,
                        Longitude = wp.Longitude,
                        AltitudeFt = wp.AltitudeFt,
                        TemperatureCelsius = wp.TemperatureC,
                        IcingSeverity = severity,
                        IcingType = icingType,
                        Severity = (double)severity / (double)IcingSeverity.Extreme,
                        Description = $"Icing risk ({severity}, {icingType}) at {wp.Identifier} ({wp.AltitudeFt:F0} ft, {wp.TemperatureC:F1}\u00b0C)",
                        Type = HazardType.IcingSigmet
                    };
                    profile.IcingRisks.Add(risk);

                    if (severity >= IcingSeverity.Severe && !profile.SeverePockets.Any(h => h.Id == risk.Id))
                    {
                        profile.SeverePockets.Add(risk);
                    }
                }
            }
        }

        // 5. Max Turbulence Index along Route
        double maxTurb = 0.02; // Standard smooth air baseline

        if (turbList.Count > 0)
        {
            foreach (var wp in waypoints)
            {
                var nearbyLayers = turbList.Where(l => wp.AltitudeFt >= l.BaseFeet - 2000 && wp.AltitudeFt <= l.TopFeet + 2000);
                foreach (var l in nearbyLayers)
                {
                    double idx = l.Intensity switch
                    {
                        TurbulenceIntensity.Extreme => 1.0,
                        TurbulenceIntensity.Severe => 0.8,
                        TurbulenceIntensity.Moderate => 0.45,
                        TurbulenceIntensity.Light => 0.15,
                        _ => 0.02
                    };
                    maxTurb = Math.Max(maxTurb, idx);
                }
            }
        }

        // Convective storm cells elevate turbulence index
        if (profile.IntersectingStormCells.Count > 0)
        {
            var maxStormIntensity = profile.IntersectingStormCells.Max(c => c.Intensity);
            maxTurb = Math.Max(maxTurb, Math.Min(1.0, maxStormIntensity * 0.9));
        }

        // Turbulence SIGMETs in corridor
        var turbSigmets = profile.SeverePockets.Where(h => h.Type == HazardType.TurbulenceSigmet).ToList();
        if (turbSigmets.Count > 0)
        {
            var maxSigmetSev = turbSigmets.Max(h => h.Severity);
            maxTurb = Math.Max(maxTurb, Math.Max(0.6, maxSigmetSev));
        }

        profile.MaxTurbulenceIndex = Math.Clamp(Math.Round(maxTurb, 2), 0.0, 1.0);

        return profile;
    }

    public static double CalculateHeadwindComponent(double windDirectionDeg, double windSpeedKt, double courseHeadingDeg)
    {
        var diffRad = (windDirectionDeg - courseHeadingDeg) * Math.PI / 180.0;
        var headwind = windSpeedKt * Math.Cos(diffRad);
        return Math.Round(headwind, 1);
    }

    public static double CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusNm * c;
    }

    public static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var phi1 = ToRadians(lat1);
        var phi2 = ToRadians(lat2);
        var deltaLambda = ToRadians(lon2 - lon1);

        var y = Math.Sin(deltaLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) -
                Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);
        var theta = Math.Atan2(y, x);
        return (theta * 180.0 / Math.PI + 360.0) % 360.0;
    }

    public static (double Lat, double Lon) InterpolateGreatCircle(double lat1, double lon1, double lat2, double lon2, double f)
    {
        if (f <= 0.0) return (lat1, lon1);
        if (f >= 1.0) return (lat2, lon2);

        var phi1 = ToRadians(lat1);
        var lambda1 = ToRadians(lon1);
        var phi2 = ToRadians(lat2);
        var lambda2 = ToRadians(lon2);

        var delta = 2.0 * Math.Asin(Math.Sqrt(
            Math.Sin((phi2 - phi1) / 2.0) * Math.Sin((phi2 - phi1) / 2.0) +
            Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin((lambda2 - lambda1) / 2.0) * Math.Sin((lambda2 - lambda1) / 2.0)));

        if (delta < 1e-7) return (lat1, lon1);

        var a = Math.Sin((1.0 - f) * delta) / Math.Sin(delta);
        var b = Math.Sin(f * delta) / Math.Sin(delta);

        var x = a * Math.Cos(phi1) * Math.Cos(lambda1) + b * Math.Cos(phi2) * Math.Cos(lambda2);
        var y = a * Math.Cos(phi1) * Math.Sin(lambda1) + b * Math.Cos(phi2) * Math.Sin(lambda2);
        var z = a * Math.Sin(phi1) + b * Math.Sin(phi2);

        var phi3 = Math.Atan2(z, Math.Sqrt(x * x + y * y));
        var lambda3 = Math.Atan2(y, x);

        return (phi3 * 180.0 / Math.PI, lambda3 * 180.0 / Math.PI);
    }

    private static double MinDistanceToRouteSamples(double targetLat, double targetLon, List<(double Lat, double Lon, double AltFt)> samples)
    {
        if (samples.Count == 0) return double.MaxValue;

        double min = double.MaxValue;
        for (int i = 0; i < samples.Count; i++)
        {
            var d = CalculateDistanceNm(targetLat, targetLon, samples[i].Lat, samples[i].Lon);
            if (d < min)
                min = d;
        }
        return min;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
