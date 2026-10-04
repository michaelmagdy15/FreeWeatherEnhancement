using System.Globalization;
using System.Text;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class SoundingGenerator
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public SoundingProfileData Generate(
        WeatherState state,
        double? aircraftAltitudeFeet = null,
        double canvasWidth = 380.0,
        double canvasHeight = 280.0)
    {
        var data = new SoundingProfileData
        {
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight,
            FreezingLevelFeet = state.FreezingLevelFeet,
            AircraftAltitudeFeet = aircraftAltitudeFeet
        };

        const double xMin = 38.0;
        var xMax = canvasWidth - 94.0;
        const double yMin = 16.0; // FL450
        var yMax = canvasHeight - 24.0; // Surface
        const double tMin = -70.0;
        const double tMax = 40.0;

        double AltToY(double alt) => Math.Clamp(yMax - (Math.Max(0, alt) / 45000.0) * (yMax - yMin), yMin, yMax);
        double TempToX(double temp) => Math.Clamp(xMin + ((temp - tMin) / (tMax - tMin)) * (xMax - xMin), xMin, xMax);

        data.FreezingLevelY = AltToY(state.FreezingLevelFeet);
        data.FreezingLevelLabel = $"0°C FL ({state.FreezingLevelFeet:F0} ft)";

        if (aircraftAltitudeFeet.HasValue && aircraftAltitudeFeet.Value > 0)
        {
            data.AircraftY = AltToY(aircraftAltitudeFeet.Value);
        }

        // 1. Temperature & Dewpoint Curves
        var sortedWinds = state.WindsAloft
            .Select(w => new WindLayer
            {
                Id = w.Id,
                IsSurfaceLayer = w.IsSurfaceLayer,
                AltitudeMeters = w.AltitudeMeters,
                AltitudeFeet = w.AltitudeFeet > 0 ? w.AltitudeFeet : w.AltitudeMeters * 3.28084,
                DirectionDegrees = w.DirectionDegrees,
                SpeedKnots = w.SpeedKnots,
                GustSpeedKnots = w.GustSpeedKnots,
                GustDirectionDegrees = w.GustDirectionDegrees,
                TemperatureCelsius = w.TemperatureCelsius,
                TurbulenceIntensity = w.TurbulenceIntensity
            })
            .OrderBy(w => w.AltitudeFeet)
            .ToList();

        var tempPts = new List<(double x, double y)>();
        var dewPts = new List<(double x, double y)>();

        // Surface anchor point
        var sfcAlt = state.CloudLayers.Count > 0 && state.CloudLayers[0].BaseMeters > 0
            ? Math.Max(0.0, state.CloudLayers[0].BaseMeters * 3.28084 - state.CloudLayers[0].BaseFeetAgl)
            : 0.0;
        var sfcY = AltToY(sfcAlt);
        var sfcTempX = TempToX(state.TemperatureCelsius);
        var sfcDewX = TempToX(state.DewpointCelsius);

        tempPts.Add((sfcTempX, sfcY));
        dewPts.Add((sfcDewX, sfcY));

        var sfcSpread = Math.Max(0.5, state.TemperatureCelsius - state.DewpointCelsius);

        double lastAlt = sfcAlt;
        double lastTemp = state.TemperatureCelsius;
        double lastDew = state.DewpointCelsius;

        foreach (var w in sortedWinds)
        {
            if (w.AltitudeFeet <= sfcAlt) continue;

            if (w.AltitudeFeet > 45000.0)
            {
                // Smoothly terminate at 45000 ft ceiling without multiple clamped points
                var factor = (45000.0 - lastAlt) / Math.Max(1.0, w.AltitudeFeet - lastAlt);
                var interpTemp = lastTemp + factor * (w.TemperatureCelsius - lastTemp);
                var interpDew = lastDew + factor * (w.TemperatureCelsius - 30.0 - lastDew);

                tempPts.Add((TempToX(interpTemp), yMin));
                dewPts.Add((TempToX(interpDew), yMin));
                break;
            }

            var wy = AltToY(w.AltitudeFeet);
            var wxTemp = TempToX(w.TemperatureCelsius);

            // Smooth boundary layer transition from surface dewpoint spread
            var blend = Math.Clamp(w.AltitudeFeet / 10000.0, 0.0, 1.0);
            var baseSpread = (1.0 - blend) * sfcSpread + blend * Math.Min(30.0, 6.0 + (w.AltitudeFeet / 2000.0));

            var cloudAtLevel = state.CloudLayers.Any(c => w.AltitudeFeet >= c.BaseFeetAgl && w.AltitudeFeet <= c.TopFeetAgl);
            if (cloudAtLevel)
            {
                baseSpread = Math.Min(baseSpread, 1.5);
            }

            var dewC = w.TemperatureCelsius - baseSpread;
            var wxDew = TempToX(dewC);

            tempPts.Add((wxTemp, wy));
            dewPts.Add((wxDew, wy));

            lastAlt = w.AltitudeFeet;
            lastTemp = w.TemperatureCelsius;
            lastDew = dewC;
        }

        // Build SVG paths
        data.TemperaturePath = BuildPolylineSvg(tempPts);
        data.DewpointPath = BuildPolylineSvg(dewPts);

        // 2. Cloud Blocks
        foreach (var cloud in state.CloudLayers)
        {
            var topY = AltToY(cloud.TopFeetAgl);
            var baseY = AltToY(cloud.BaseFeetAgl);
            var height = Math.Max(4.0, baseY - topY);

            data.CloudBlocks.Add(new SoundingCloudBlock
            {
                BaseAltitudeFeet = cloud.BaseFeetAgl,
                TopAltitudeFeet = cloud.TopFeetAgl,
                Y = topY,
                Height = height,
                CoveragePercent = cloud.CoveragePercent,
                Opacity = Math.Clamp(cloud.CoveragePercent * 0.45 + 0.15, 0.15, 0.65),
                Type = cloud.Type.ToString(),
                Label = $"{cloud.Type} {cloud.CoveragePercent * 100:0}%"
            });
        }

        // 3. Hazard Bands (Icing & Turbulence)
        foreach (var ice in state.IcingLayers)
        {
            var topY = AltToY(ice.TopFeet);
            var baseY = AltToY(ice.BaseFeet);
            data.HazardBands.Add(new SoundingHazardBand
            {
                Type = "ICING",
                BaseAltitudeFeet = ice.BaseFeet,
                TopAltitudeFeet = ice.TopFeet,
                Y = topY,
                Height = Math.Max(4.0, baseY - topY),
                Severity = ice.Severity.ToString(),
                ColorHex = "#38BDF8",
                Label = $"ICE: {ice.Severity}"
            });
        }

        foreach (var turb in state.TurbulenceLayers)
        {
            var topY = AltToY(turb.TopFeet);
            var baseY = AltToY(turb.BaseFeet);
            data.HazardBands.Add(new SoundingHazardBand
            {
                Type = "TURB",
                BaseAltitudeFeet = turb.BaseFeet,
                TopAltitudeFeet = turb.TopFeet,
                Y = topY,
                Height = Math.Max(4.0, baseY - topY),
                Severity = turb.Intensity.ToString(),
                ColorHex = "#FFA502",
                Label = $"TURB: {turb.Intensity}"
            });
        }

        // 4. Standard Flight Levels
        var standardAlts = new[]
        {
            (0.0, "SFC"),
            (5000.0, "FL050"),
            (10000.0, "FL100"),
            (18000.0, "FL180"),
            (24000.0, "FL240"),
            (30000.0, "FL300"),
            (34000.0, "FL340"),
            (39000.0, "FL390")
        };

        foreach (var (alt, name) in standardAlts)
        {
            var closestWind = sortedWinds
                .OrderBy(w => Math.Abs(w.AltitudeFeet - alt))
                .FirstOrDefault();

            var dir = closestWind?.DirectionDegrees ?? state.WindDirectionDegrees;
            var spd = closestWind?.SpeedKnots ?? state.WindSpeedKnots;
            var temp = closestWind?.TemperatureCelsius ?? (state.TemperatureCelsius - (alt / 1000.0) * 1.98);

            data.StandardLevels.Add(new SoundingLevel
            {
                AltitudeFeet = alt,
                FlightLevelName = name,
                Y = AltToY(alt),
                WindDirectionDegrees = dir,
                WindSpeedKnots = spd,
                TemperatureCelsius = temp,
                WindText = $"{dir:000}°/{spd:0}k",
                TempText = $"{temp:+0;-0;0}°"
            });
        }

        return data;
    }

    private static string BuildPolylineSvg(IReadOnlyList<(double x, double y)> pts)
    {
        if (pts.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendFormat(Inv, "M {0:F1},{1:F1} ", pts[0].x, pts[0].y);
        for (var i = 1; i < pts.Count; i++)
        {
            sb.AppendFormat(Inv, "L {0:F1},{1:F1} ", pts[i].x, pts[i].y);
        }
        return sb.ToString();
    }
}
