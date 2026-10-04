using System;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

/// <summary>
/// Computes FAA / EASA Holdover Time (HOT) guidelines and ramp weather safety thresholds
/// based on live meteorological observations and GSX Pro ground telemetry.
/// </summary>
public class HoldoverTimeCalculator
{
    /// <summary>
    /// Evaluates if deicing is required and computes the recommended fluid type and estimated HOT.
    /// </summary>
    public (bool IsRequired, string Fluid, double EstimatedMinutes, string Advisory) EvaluateDeicingNeed(
        double temperatureCelsius,
        double dewpointCelsius,
        PrecipitationType precipType,
        double precipRateMmPerHour,
        double visibilityMeters)
    {
        // Aviation rule of thumb: OAT <= +3°C with visible moisture requires deicing consideration
        if (temperatureCelsius > 3.0)
        {
            return (false, "None", 0, "OAT above +3°C — Deicing not required");
        }

        var isFreezingTemp = temperatureCelsius <= 0.0;
        var hasVisibleMoisture = precipType != PrecipitationType.None || visibilityMeters < 1000 || (temperatureCelsius - dewpointCelsius) <= 1.0;

        if (!hasVisibleMoisture)
        {
            return (false, "None", 0, $"OAT {temperatureCelsius:F1}°C with no active precipitation or fog — Inspection recommended");
        }

        // Determine specific precipitation condition
        double estimatedHotMin;
        string fluid;
        string advisory;

        if (precipType == PrecipitationType.Snow)
        {
            fluid = "Type I + Type IV (Two-Step)";
            if (precipRateMmPerHour > 2.5)
            {
                estimatedHotMin = 25.0; // Moderate/Heavy snow
                advisory = $"Moderate Snow at {temperatureCelsius:F1}°C — Type IV HOT approx 20–30 min. Prompt departure required.";
            }
            else
            {
                estimatedHotMin = 45.0; // Light snow
                advisory = $"Light Snow at {temperatureCelsius:F1}°C — Type IV estimated HOT 40–55 min.";
            }
        }
        else if (precipType == PrecipitationType.FreezingRain)
        {
            fluid = "Type I + Type IV (Two-Step)";
            if (precipRateMmPerHour > 1.5)
            {
                estimatedHotMin = 10.0;
                advisory = $"Moderate/Heavy Freezing Rain at {temperatureCelsius:F1}°C — EXTREME HAZARD. HOT severely degraded (<15 min). Takeoff caution.";
            }
            else
            {
                estimatedHotMin = 20.0;
                advisory = $"Light Freezing Rain at {temperatureCelsius:F1}°C — Type IV HOT approx 15–25 min.";
            }
        }
        else if (visibilityMeters < 1000 && isFreezingTemp)
        {
            fluid = "Type I + Type IV (Two-Step)";
            estimatedHotMin = 40.0;
            advisory = $"Freezing Fog (Vis {visibilityMeters}m at {temperatureCelsius:F1}°C) — Type IV HOT approx 35–45 min.";
        }
        else if (isFreezingTemp)
        {
            fluid = "Type I (One-Step)";
            estimatedHotMin = 30.0;
            advisory = $"Active Frost / Cold-soak at {temperatureCelsius:F1}°C — Type I deicing clears contamination (approx 30 min protection).";
        }
        else
        {
            // OAT between 0°C and 3°C with rain/drizzle
            fluid = "Type I";
            estimatedHotMin = 45.0;
            advisory = $"Wet Snow/Near-freezing at {temperatureCelsius:F1}°C — Deicing recommended if wing temperatures are below freezing.";
        }

        return (true, fluid, estimatedHotMin, advisory);
    }

    /// <summary>
    /// Computes ramp weather safety alerts for ground handling operations (catering, pushback, baggage loading).
    /// </summary>
    public (GsxRampSafetyLevel Level, string Message) EvaluateRampSafety(
        double windSpeedKnots,
        double windGustKnots,
        double? closestLightningDistanceNm)
    {
        // 1. Lightning Ramp Freeze (within 5 NM)
        if (closestLightningDistanceNm.HasValue && closestLightningDistanceNm.Value <= 5.0)
        {
            return (GsxRampSafetyLevel.Warning,
                $"RAMP FREEZE ALERT: Lightning detected {closestLightningDistanceNm.Value:F1} NM away. Ground personnel evacuation mandatory.");
        }

        // 2. Severe High Wind / Door Operating Limits (gusts >= 45 kt)
        if (windGustKnots >= 45.0 || windSpeedKnots >= 40.0)
        {
            return (GsxRampSafetyLevel.Warning,
                $"SEVERE WIND WARNING: Gusts {Math.Max(windSpeedKnots, windGustKnots):F0} kt exceed aircraft door opening and ramp vehicle operating limits.");
        }

        // 3. Catering / High-Loader Wind Caution (gusts >= 35 kt)
        if (windGustKnots >= 35.0 || windSpeedKnots >= 30.0)
        {
            return (GsxRampSafetyLevel.Caution,
                $"WIND CAUTION: Gusts {Math.Max(windSpeedKnots, windGustKnots):F0} kt. High-lift catering trucks and baggage belt loaders operate at reduced safety margins.");
        }

        // 4. Normal
        return (GsxRampSafetyLevel.Normal, "NORMAL — Ramp weather conditions nominal for all ground servicing.");
    }
}
