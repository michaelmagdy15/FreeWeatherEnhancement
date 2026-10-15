using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public sealed record StationObservationSample(
    AirportData Airport,
    MetarData Metar,
    double DistanceNm);

public sealed record SpatialBlendResult(
    MetarData BlendedMetar,
    IReadOnlyList<string> StationIds,
    string Summary,
    bool IsAirportLocked);

/// <summary>
/// Active Sky Next-grade Multi-Station Inverse Distance Squared (IDW) spatial weather interpolator.
/// Locks 100% to the primary station within 3.0 NM of an airport (preserving NFR-A1 METAR authority),
/// and smoothly blends temperature, dewpoint, QNH, visibility, and wind vectors across up to 4 surrounding
/// stations when enroute.
/// </summary>
public class SpatialWeatherInterpolator
{
    public const double DefaultAirportLockRadiusNm = 3.0;
    public const double DefaultMaxBlendRadiusNm = 85.0;

    public SpatialBlendResult BlendObservations(
        double aircraftLatitude,
        double aircraftLongitude,
        double aircraftAltitudeFeet,
        IReadOnlyList<StationObservationSample> samples,
        double airportLockRadiusNm = DefaultAirportLockRadiusNm)
    {
        if (samples == null || samples.Count == 0)
        {
            throw new ArgumentException("At least one station observation sample is required.", nameof(samples));
        }

        var ordered = samples
            .Where(s => s.Metar != null && !string.IsNullOrWhiteSpace(s.Metar.StationId))
            .OrderBy(s => s.DistanceNm)
            .ToList();

        if (ordered.Count == 0)
        {
            throw new ArgumentException("No valid METAR samples provided.", nameof(samples));
        }

        var primary = ordered[0];

        // NFR-A1: Within airportLockRadiusNm (default 3.0 NM) or when only 1 station is available,
        // lock 100% to the nearest reporting station so surface METAR values are exact.
        if (ordered.Count == 1 || primary.DistanceNm <= airportLockRadiusNm)
        {
            var singleIds = new List<string> { primary.Metar.StationId };
            var lockSummary = primary.DistanceNm <= airportLockRadiusNm
                ? $"Station-locked {primary.Metar.StationId} ({primary.DistanceNm:F1} NM)"
                : $"Single-station {primary.Metar.StationId} ({primary.DistanceNm:F1} NM)";

            return new SpatialBlendResult(
                CloneMetar(primary.Metar),
                singleIds,
                lockSummary,
                IsAirportLocked: primary.DistanceNm <= airportLockRadiusNm);
        }

        // Compute Inverse Distance Squared weights with orographic elevation-similarity penalty
        var referenceElevation = primary.Airport.ElevationFeet;
        var rawWeights = new double[ordered.Count];
        var weightSum = 0.0;

        for (int i = 0; i < ordered.Count; i++)
        {
            var sample = ordered[i];
            var distSq = Math.Max(0.25, sample.DistanceNm * sample.DistanceNm);
            var elevDiffFeet = Math.Abs(sample.Airport.ElevationFeet - referenceElevation);
            var elevPenalty = 1.0 + Math.Pow(elevDiffFeet / 3000.0, 2.0);
            var w = 1.0 / (distSq * elevPenalty);
            rawWeights[i] = w;
            weightSum += w;
        }

        if (weightSum <= 1e-9)
        {
            return new SpatialBlendResult(
                CloneMetar(primary.Metar),
                new List<string> { primary.Metar.StationId },
                $"Single-station {primary.Metar.StationId}",
                IsAirportLocked: false);
        }

        double tempC = 0;
        double dewpC = 0;
        double altimHpa = 0;
        double visLinear = 0;
        double visHarmonicDenom = 0;
        double windX = 0;
        double windY = 0;
        double scalarWindSpeed = 0;
        double? maxWeightedGust = null;
        double gustAccumulator = 0;
        double gustWeightSum = 0;

        var summaryParts = new List<string>(ordered.Count);
        var stationIds = new List<string>(ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            var sample = ordered[i];
            var m = sample.Metar;
            var nw = rawWeights[i] / weightSum;

            stationIds.Add(m.StationId);
            summaryParts.Add($"{m.StationId} {nw * 100.0:F0}%");

            tempC += m.TemperatureCelsius * nw;
            dewpC += m.DewpointCelsius * nw;
            altimHpa += m.AltimeterHpa * nw;

            var clampedVis = Math.Clamp(m.VisibilityMeters, 50.0, 20000.0);
            visLinear += clampedVis * nw;
            visHarmonicDenom += nw / clampedVis;

            var rad = m.WindDirectionDegrees * Math.PI / 180.0;
            windX += Math.Cos(rad) * m.WindSpeedKnots * nw;
            windY += Math.Sin(rad) * m.WindSpeedKnots * nw;
            scalarWindSpeed += m.WindSpeedKnots * nw;

            if (m.WindGustKnots.HasValue && m.WindGustKnots.Value > 0)
            {
                gustAccumulator += m.WindGustKnots.Value * nw;
                gustWeightSum += nw;
            }
        }

        var blendedDir = Math.Atan2(windY, windX) * 180.0 / Math.PI;
        if (blendedDir < 0) blendedDir += 360.0;
        if (blendedDir >= 360.0) blendedDir -= 360.0;

        // Blend 50% harmonic + 50% linear visibility so fog banks are respected realistically
        var visHarmonic = visHarmonicDenom > 1e-9 ? 1.0 / visHarmonicDenom : visLinear;
        var blendedVis = Math.Round((visLinear * 0.5) + (visHarmonic * 0.5), 0);

        if (gustWeightSum >= 0.25)
        {
            var avgGust = gustAccumulator / gustWeightSum;
            if (avgGust > scalarWindSpeed + 2.0)
            {
                maxWeightedGust = Math.Round(avgGust, 1);
            }
        }

        // Smooth ramp between AirportLockRadiusNm (3 NM) and 8 NM so exiting the lock zone is seamless
        if (primary.DistanceNm < airportLockRadiusNm + 5.0)
        {
            var ramp = Math.Clamp((primary.DistanceNm - airportLockRadiusNm) / 5.0, 0.0, 1.0);
            tempC = Lerp(primary.Metar.TemperatureCelsius, tempC, ramp);
            dewpC = Lerp(primary.Metar.DewpointCelsius, dewpC, ramp);
            altimHpa = Lerp(primary.Metar.AltimeterHpa, altimHpa, ramp);
            blendedVis = Lerp(primary.Metar.VisibilityMeters, blendedVis, ramp);
            scalarWindSpeed = Lerp(primary.Metar.WindSpeedKnots, scalarWindSpeed, ramp);
            blendedDir = LerpAngle(primary.Metar.WindDirectionDegrees, blendedDir, ramp);
        }

        var blended = CloneMetar(primary.Metar);
        blended.TemperatureCelsius = Math.Round(tempC, 1);
        blended.DewpointCelsius = Math.Round(Math.Min(tempC, dewpC), 1);
        blended.AltimeterHpa = Math.Round(altimHpa, 2);
        blended.VisibilityMeters = Math.Max(50.0, Math.Round(blendedVis, 0));
        blended.WindDirectionDegrees = Math.Round(blendedDir, 0) % 360;
        blended.WindSpeedKnots = Math.Round(scalarWindSpeed, 1);
        blended.WindGustKnots = maxWeightedGust ?? primary.Metar.WindGustKnots;
        blended.FlightCategory = ComputeFlightCategory(blended.VisibilityMeters, blended.Clouds);

        var summary = $"IDW {ordered.Count}-Station Blend: {string.Join(", ", summaryParts)}";
        return new SpatialBlendResult(blended, stationIds, summary, IsAirportLocked: false);
    }

    private static string ComputeFlightCategory(double visibilityMeters, List<MetarCloud> clouds)
    {
        var ceilingFeet = clouds
            .Where(c => c.Coverage is "BKN" or "OVC" or "VV")
            .Select(c => (double?)c.BaseFeet)
            .Min() ?? 99999.0;

        var visSm = visibilityMeters / 1609.344;
        if (ceilingFeet < 500 || visSm < 1.0) return "LIFR";
        if (ceilingFeet < 1000 || visSm < 3.0) return "IFR";
        if (ceilingFeet <= 3000 || visSm <= 5.0) return "MVFR";
        return "VFR";
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double LerpAngle(double a, double b, double t)
    {
        var diff = b - a;
        while (diff > 180) diff -= 360;
        while (diff < -180) diff += 360;
        var res = a + diff * t;
        while (res < 0) res += 360;
        while (res >= 360) res -= 360;
        return res;
    }

    private static MetarData CloneMetar(MetarData source)
    {
        return new MetarData
        {
            StationId = source.StationId,
            ObservationTime = source.ObservationTime,
            WindDirectionDegrees = source.WindDirectionDegrees,
            WindSpeedKnots = source.WindSpeedKnots,
            WindGustKnots = source.WindGustKnots,
            VisibilityMeters = source.VisibilityMeters,
            TemperatureCelsius = source.TemperatureCelsius,
            DewpointCelsius = source.DewpointCelsius,
            AltimeterHpa = source.AltimeterHpa,
            FlightCategory = source.FlightCategory,
            Clouds = source.Clouds.Select(c => new MetarCloud
            {
                Coverage = c.Coverage,
                BaseFeet = c.BaseFeet,
                Type = c.Type
            }).ToList(),
            WeatherConditions = new List<string>(source.WeatherConditions),
            RawText = source.RawText
        };
    }
}
