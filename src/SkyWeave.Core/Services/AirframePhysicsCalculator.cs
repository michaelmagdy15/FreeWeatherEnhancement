using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public sealed record AirframePerturbation(
    double PitchRateRadPerSec,
    double YawRateRadPerSec,
    double RollRateRadPerSec,
    double VerticalVelocityFps,
    bool IsWindshearWarning,
    bool IsWakeVortexActive,
    string ActiveEffectSummary)
{
    public static readonly AirframePerturbation Zero = new(0, 0, 0, 0, false, false, "Nominal");
}

/// <summary>
/// Active Sky Next-grade 6-DOF airframe physics calculator.
/// Computes Burnham-Hallock twin-vortex wake roll moments, convective microburst vortex-ring
/// downdraft/windshear, and Dryden spectral continuous turbulence perturbations.
/// All outputs are strictly clamped to safe aerodynamic limits.
/// </summary>
public class AirframePhysicsCalculator
{
    private const double MaxRollRateRadPerSec = 0.38;   // ~21.7 deg/sec max perturbation
    private const double MaxPitchRateRadPerSec = 0.16;  // ~9.1 deg/sec max perturbation
    private const double MaxYawRateRadPerSec = 0.18;    // ~10.3 deg/sec max perturbation
    private const double MaxVerticalVelFps = 42.0;      // ~2,520 fpm max vertical perturbation

    private double _drydenRollState;
    private double _drydenPitchState;
    private double _drydenYawState;
    private double _drydenVerticalState;

    public AirframePerturbation CalculateStep(
        WeatherState? weather,
        double aircraftLatitude,
        double aircraftLongitude,
        double aircraftAltitudeFeet,
        double aircraftHeadingDegrees,
        double aircraftWeightPounds,
        bool isOnGround,
        IReadOnlyList<AircraftTraffic>? nearbyTraffic,
        double intensityScale = 1.0,
        double? randomSample = null)
    {
        if (weather == null || isOnGround || intensityScale <= 0.01)
        {
            ResetFilterState();
            return AirframePerturbation.Zero;
        }

        var clampedScale = Math.Clamp(intensityScale, 0.0, 2.0);
        var massDamping = ComputeMassDampingFactor(aircraftWeightPounds);

        // 1. Burnham-Hallock Wake Vortex Roll & Sink
        var (wakeRoll, wakePitch, wakeVert, wakeSummary) = CalculateWakeVortexPerturbation(
            weather,
            aircraftLatitude,
            aircraftLongitude,
            aircraftAltitudeFeet,
            aircraftHeadingDegrees,
            nearbyTraffic,
            massDamping);

        // 2. Convective Microburst / Low-Level Windshear (below 3,500 ft AGL/MSL near intense cells)
        var (mbPitch, mbVert, isWindshear, mbSummary) = CalculateMicroburstShear(
            weather,
            aircraftLatitude,
            aircraftLongitude,
            aircraftAltitudeFeet,
            aircraftHeadingDegrees,
            massDamping);

        // 3. Dryden Continuous Spectral Turbulence (CAT / Thermal / Mountain Wave)
        var (catPitch, catYaw, catRoll, catVert) = CalculateDrydenTurbulence(
            weather,
            aircraftAltitudeFeet,
            massDamping,
            randomSample);

        var totalPitch = Math.Clamp((wakePitch + mbPitch + catPitch) * clampedScale, -MaxPitchRateRadPerSec, MaxPitchRateRadPerSec);
        var totalYaw = Math.Clamp(catYaw * clampedScale, -MaxYawRateRadPerSec, MaxYawRateRadPerSec);
        var totalRoll = Math.Clamp((wakeRoll + catRoll) * clampedScale, -MaxRollRateRadPerSec, MaxRollRateRadPerSec);
        var totalVert = Math.Clamp((wakeVert + mbVert + catVert) * clampedScale, -MaxVerticalVelFps, MaxVerticalVelFps);

        var isWakeActive = Math.Abs(wakeRoll) > 0.02;
        var summary = isWindshear
            ? mbSummary
            : isWakeActive
                ? wakeSummary
                : Math.Abs(totalRoll) > 0.03 || Math.Abs(totalVert) > 2.0
                    ? $"Dryden Spectral CAT (Roll {totalRoll * 57.2958:+0.0;-0.0}°/s, Vz {totalVert * 60:+0;-0} fpm)"
                    : "Nominal";

        return new AirframePerturbation(
            PitchRateRadPerSec: totalPitch,
            YawRateRadPerSec: totalYaw,
            RollRateRadPerSec: totalRoll,
            VerticalVelocityFps: totalVert,
            IsWindshearWarning: isWindshear,
            IsWakeVortexActive: isWakeActive,
            ActiveEffectSummary: summary);
    }

    public (double RollRadPerSec, double PitchRadPerSec, double VerticalFps, string Summary) CalculateWakeVortexPerturbation(
        WeatherState weather,
        double lat,
        double lon,
        double altFeet,
        double headingDeg,
        IReadOnlyList<AircraftTraffic>? trafficList,
        double massDamping)
    {
        if (trafficList == null || trafficList.Count == 0)
            return (0, 0, 0, string.Empty);

        double bestRoll = 0;
        double bestPitch = 0;
        double bestVert = 0;
        string bestSummary = string.Empty;

        foreach (var lead in trafficList)
        {
            if (lead.OnGround || lead.SpeedKnots < 45)
                continue;

            var distNm = StationFinder.CalculateDistance(lat, lon, lead.Latitude, lead.Longitude);
            if (distNm <= 0.05 || distNm > 6.5)
                continue;

            // Check if user is roughly behind the lead aircraft's track
            var bearingFromLeadToUser = CalculateBearingDegrees(lead.Latitude, lead.Longitude, lat, lon);
            var reciprocalTrack = (lead.HeadingDegrees + 180.0) % 360.0;
            var angleFromWakeAxis = Math.Abs(NormalizeAngleSigned(bearingFromLeadToUser - reciprocalTrack));
            if (angleFromWakeAxis > 35.0)
                continue;

            // Vortex age in seconds based on distance behind lead aircraft
            var leadGroundSpeedNmPerSec = Math.Max(60.0, lead.SpeedKnots) / 3600.0;
            var vortexAgeSec = distNm / leadGroundSpeedNmPerSec;
            if (vortexAgeSec > 180.0)
                continue;

            // Vortex sinks at ~400 fpm (6.67 ft/s) down to ~900 ft below lead aircraft
            var vortexSinkFeet = Math.Min(950.0, vortexAgeSec * 6.5);
            var expectedVortexAltFeet = lead.AltitudeFeet - vortexSinkFeet;
            var altDiffFeet = Math.Abs(altFeet - expectedVortexAltFeet);
            if (altDiffFeet > 550.0)
                continue;

            // Burnham-Hallock circulation strength factor by weight class
            var circulationStrength = lead.WeightClass switch
            {
                AircraftWeightClass.Super => 1.00,
                AircraftWeightClass.Heavy => 0.78,
                AircraftWeightClass.Medium => 0.45,
                _ => 0.18
            };

            var decayFactor = Math.Exp(-vortexAgeSec / 95.0);
            var verticalProximity = 1.0 - (altDiffFeet / 550.0);
            var lateralOffsetRad = NormalizeAngleSigned(bearingFromLeadToUser - reciprocalTrack) * Math.PI / 180.0;
            var crossTrackMeters = Math.Sin(lateralOffsetRad) * (distNm * 1852.0);

            // Burnham-Hallock twin-core tangential velocity profile (core radius rc = 12 m)
            const double coreRadiusMeters = 12.0;
            var normalizedOffset = crossTrackMeters / coreRadiusMeters;
            var tangentialFactor = (2.0 * normalizedOffset) / (1.0 + normalizedOffset * normalizedOffset);
            if (Math.Abs(tangentialFactor) < 0.25)
            {
                // Directly inside core: strong roll impulse
                tangentialFactor = normalizedOffset >= 0 ? 0.65 : -0.65;
            }

            var rollMoment = circulationStrength * decayFactor * verticalProximity * tangentialFactor * 0.34 * massDamping;
            var downdraftFps = -circulationStrength * decayFactor * verticalProximity * 14.0 * massDamping;
            var pitchMoment = -Math.Abs(rollMoment) * 0.15;

            if (Math.Abs(rollMoment) > Math.Abs(bestRoll))
            {
                bestRoll = rollMoment;
                bestPitch = pitchMoment;
                bestVert = downdraftFps;
                bestSummary = $"Wake Vortex ({lead.Callsign} {lead.WeightClass}, {distNm:F1} NM ahead, Roll {rollMoment * 57.2958:+0.0;-0.0}°/s)";
            }
        }

        return (bestRoll, bestPitch, bestVert, bestSummary);
    }

    public (double PitchRadPerSec, double VerticalFps, bool IsWindshear, string Summary) CalculateMicroburstShear(
        WeatherState weather,
        double lat,
        double lon,
        double altFeet,
        double headingDeg,
        double massDamping)
    {
        if (altFeet > 4500 || weather.StormCells.Count == 0 && weather.ThunderstormIntensity < 0.45)
            return (0, 0, false, string.Empty);

        foreach (var cell in weather.StormCells)
        {
            if (cell.Intensity < 0.55)
                continue;

            var distNm = StationFinder.CalculateDistance(lat, lon, cell.Latitude, cell.Longitude);
            var coreRadiusNm = Math.Clamp(cell.RadiusNm * 0.45, 0.8, 3.0);
            if (distNm > coreRadiusNm * 1.6)
                continue;

            var ratio = distNm / coreRadiusNm;
            var bearingToCell = CalculateBearingDegrees(lat, lon, cell.Latitude, cell.Longitude);
            var relativeBearing = Math.Abs(NormalizeAngleSigned(bearingToCell - headingDeg));
            var isApproaching = relativeBearing < 90.0;

            if (ratio <= 0.85)
            {
                // Phase 2: Inside Microburst Core — severe downdraft (-18 to -38 fps = -1,080 to -2,280 fpm)
                var coreStrength = (1.0 - (ratio / 0.85)) * cell.Intensity;
                var downdraftFps = -Math.Clamp(16.0 + coreStrength * 24.0, 12.0, 38.0) * massDamping;
                var pitchDown = -0.07 * coreStrength * massDamping;
                return (pitchDown, downdraftFps, true, $"WINDSHEAR ALERT: Microburst Core ({downdraftFps * 60:F0} fpm downdraft)");
            }

            if (isApproaching)
            {
                // Phase 1: Outflow Entry — headwind surge & nose-up ballooning tendency
                var outflowStrength = (1.6 - ratio) / 0.75 * cell.Intensity;
                var updraftFps = 6.5 * outflowStrength * massDamping;
                var pitchUp = 0.05 * outflowStrength * massDamping;
                return (pitchUp, updraftFps, true, "WINDSHEAR CAUTION: Microburst Outflow Headwind Surge");
            }
            else
            {
                // Phase 3: Outflow Exit — rapid tailwind shear & sink
                var exitStrength = (1.6 - ratio) / 0.75 * cell.Intensity;
                var sinkFps = -15.0 * exitStrength * massDamping;
                var pitchDown = -0.06 * exitStrength * massDamping;
                return (pitchDown, sinkFps, true, "WINDSHEAR WARNING: Microburst Tailwind Shear & Sink");
            }
        }

        return (0, 0, false, string.Empty);
    }

    private (double PitchRadPerSec, double YawRadPerSec, double RollRadPerSec, double VerticalFps) CalculateDrydenTurbulence(
        WeatherState weather,
        double altFeet,
        double massDamping,
        double? deterministicSample)
    {
        var activeLayer = weather.TurbulenceLayers
            .Where(l => altFeet >= l.BaseFeet - 500 && altFeet <= l.TopFeet + 500)
            .OrderByDescending(l => (int)l.Intensity)
            .FirstOrDefault();

        var baseIndex = weather.TurbulenceIndex;
        if (activeLayer != null)
        {
            var layerBoost = activeLayer.Intensity switch
            {
                TurbulenceIntensity.Extreme => 1.0,
                TurbulenceIntensity.Severe => 0.8,
                TurbulenceIntensity.Moderate => 0.5,
                TurbulenceIntensity.Light => 0.25,
                _ => 0.0
            };
            baseIndex = Math.Max(baseIndex, layerBoost);
        }

        if (baseIndex <= 0.05)
        {
            _drydenRollState *= 0.7;
            _drydenPitchState *= 0.7;
            _drydenYawState *= 0.7;
            _drydenVerticalState *= 0.7;
            return (0, 0, 0, 0);
        }

        // First-order Gauss-Markov / Dryden spectral filter (alpha = 0.65 correlation)
        const double alpha = 0.65;
        var beta = Math.Sqrt(1.0 - alpha * alpha);

        double n1, n2, n3, n4;
        if (deterministicSample.HasValue)
        {
            n1 = deterministicSample.Value;
            n2 = deterministicSample.Value * 0.7;
            n3 = -deterministicSample.Value * 0.5;
            n4 = deterministicSample.Value * 0.85;
        }
        else
        {
            n1 = (Random.Shared.NextDouble() * 2.0) - 1.0;
            n2 = (Random.Shared.NextDouble() * 2.0) - 1.0;
            n3 = (Random.Shared.NextDouble() * 2.0) - 1.0;
            n4 = (Random.Shared.NextDouble() * 2.0) - 1.0;
        }

        _drydenRollState = alpha * _drydenRollState + beta * n1;
        _drydenPitchState = alpha * _drydenPitchState + beta * n2;
        _drydenYawState = alpha * _drydenYawState + beta * n3;
        _drydenVerticalState = alpha * _drydenVerticalState + beta * n4;

        var scale = baseIndex * massDamping;
        return (
            PitchRadPerSec: _drydenPitchState * 0.08 * scale,
            YawRadPerSec: _drydenYawState * 0.09 * scale,
            RollRadPerSec: _drydenRollState * 0.22 * scale,
            VerticalFps: _drydenVerticalState * 16.0 * scale);
    }

    public static double ComputeMassDampingFactor(double weightPounds)
    {
        if (weightPounds <= 0) return 0.75;
        // Light GA (~2,500 lbs) -> 1.15x response; Narrowbody (~150,000 lbs) -> 0.65x; Heavy (~500,000 lbs) -> 0.42x
        if (weightPounds < 12_500) return 1.15;
        if (weightPounds < 50_000) return 0.90;
        if (weightPounds < 220_000) return 0.65;
        return 0.42;
    }

    public void ResetFilterState()
    {
        _drydenRollState = 0;
        _drydenPitchState = 0;
        _drydenYawState = 0;
        _drydenVerticalState = 0;
    }

    private static double CalculateBearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var phi1 = lat1 * Math.PI / 180.0;
        var phi2 = lat2 * Math.PI / 180.0;
        var dLambda = (lon2 - lon1) * Math.PI / 180.0;

        var y = Math.Sin(dLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLambda);
        var brng = Math.Atan2(y, x) * 180.0 / Math.PI;
        return (brng + 360.0) % 360.0;
    }

    private static double NormalizeAngleSigned(double angle)
    {
        while (angle > 180.0) angle -= 360.0;
        while (angle < -180.0) angle += 360.0;
        return angle;
    }
}
