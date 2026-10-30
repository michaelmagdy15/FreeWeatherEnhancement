using System;
using System.Collections.Generic;
using System.Linq;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public enum RunwaySurfaceCondition
{
    Dry,
    Damp,
    Wet,
    StandingWater,
    SnowOrSlush,
    Icy
}

public sealed class RunwaySurfaceReport
{
    public RunwaySurfaceCondition Condition { get; set; } = RunwaySurfaceCondition.Dry;
    public string ConditionLabel { get; set; } = "DRY (CC 6)";
    public int FaaConditionCode { get; set; } = 6;
    public double WetnessRatio { get; set; }
    public double Past12HourRainfallMm { get; set; }
    public double ActivePrecipitationRateMmHr { get; set; }
    public double EvaporationRateMmHr { get; set; }
    public double EstimatedDryingTimeHours { get; set; }
    public double EffectivePrecipitationRateMmHr { get; set; }
    public string Summary { get; set; } = "Runway surface dry (RCAM 6)";
}

/// <summary>
/// Models 12-hour historical precipitation accumulation and physical surface evaporation
/// (Penman-Monteith / aerodynamic drying approximation using temperature, dewpoint spread,
/// wind speed, and solar shortwave radiation) to keep runways realistically wet for hours
/// after rainfall stops.
/// </summary>
public static class RunwayWetnessCalculator
{
    // A standard grooved asphalt/concrete runway retains ~3.0 mm of surface water film before runoff
    private const double MaxSurfaceFilmCapacityMm = 3.5;

    public static RunwaySurfaceReport CalculateSurfaceState(
        MetarData? metar,
        WindsAloftData? windsAloft,
        double activePrecipitationRateMmHr,
        double temperatureCelsius,
        double dewpointCelsius,
        double windSpeedKnots)
    {
        var raw = metar?.RawText?.ToUpperInvariant() ?? string.Empty;
        var hasSnowOrIce = raw.Contains("SN") || raw.Contains("FZRA") || raw.Contains("FZDZ") || raw.Contains("PL") || raw.Contains("GR") || raw.Contains("IC");

        var solarRadiationWm2 = windsAloft?.ShortwaveRadiationWm2 ?? EstimateSolarRadiationFromTime(DateTime.UtcNow, windsAloft?.Latitude ?? 0, windsAloft?.Longitude ?? 0);
        var currentEvapMmHr = CalculateEvaporationRateMmHr(temperatureCelsius, dewpointCelsius, windSpeedKnots, solarRadiationWm2);

        double retainedWaterMm = 0;
        double past12hTotalMm = windsAloft?.Past12HourPrecipitationMm ?? 0;

        if (windsAloft?.Past12HourSamples != null && windsAloft.Past12HourSamples.Count > 0)
        {
            foreach (var sample in windsAloft.Past12HourSamples)
            {
                var sampleDew = Math.Min(sample.TemperatureCelsius, sample.TemperatureCelsius - Math.Max(1.0, temperatureCelsius - dewpointCelsius));
                var sampleEvap = CalculateEvaporationRateMmHr(
                    sample.TemperatureCelsius,
                    sampleDew,
                    sample.WindSpeedKnots > 0 ? sample.WindSpeedKnots : windSpeedKnots,
                    sample.ShortwaveRadiationWm2);

                retainedWaterMm = Math.Clamp(retainedWaterMm + sample.PrecipitationMm - sampleEvap, 0.0, MaxSurfaceFilmCapacityMm);
            }
        }
        else if (past12hTotalMm > 0)
        {
            // Fallback if summary total is provided without hourly array
            retainedWaterMm = Math.Clamp(past12hTotalMm - (currentEvapMmHr * 4.0), 0.0, MaxSurfaceFilmCapacityMm);
        }

        // Factor in active METAR / radar precipitation right now
        if (activePrecipitationRateMmHr > 0)
        {
            retainedWaterMm = Math.Clamp(retainedWaterMm + Math.Min(MaxSurfaceFilmCapacityMm, activePrecipitationRateMmHr * 0.65), 0.25, MaxSurfaceFilmCapacityMm);
        }
        else if (raw.Contains("RA") || raw.Contains("DZ") || raw.Contains("SH") || raw.Contains("TS"))
        {
            retainedWaterMm = Math.Max(retainedWaterMm, 1.2);
        }

        var wetnessRatio = Math.Clamp(retainedWaterMm / 2.5, 0.0, 1.0);
        var dryingHours = retainedWaterMm > 0.02
            ? Math.Round(Math.Clamp(retainedWaterMm / Math.Max(0.08, currentEvapMmHr), 0.1, 12.0), 1)
            : 0.0;

        RunwaySurfaceCondition condition;
        int rcamCode;
        string label;

        if (hasSnowOrIce && temperatureCelsius <= 0.5 && (retainedWaterMm > 0.1 || activePrecipitationRateMmHr > 0))
        {
            condition = temperatureCelsius < -3.0 ? RunwaySurfaceCondition.Icy : RunwaySurfaceCondition.SnowOrSlush;
            rcamCode = condition == RunwaySurfaceCondition.Icy ? 1 : 2;
            label = condition == RunwaySurfaceCondition.Icy ? "ICE / FROZEN (CC 1)" : "SNOW / SLUSH (CC 2)";
            wetnessRatio = Math.Max(wetnessRatio, 0.75);
        }
        else if (wetnessRatio >= 0.80 || activePrecipitationRateMmHr >= 8.0)
        {
            condition = RunwaySurfaceCondition.StandingWater;
            rcamCode = 2;
            label = "STANDING WATER (CC 2)";
        }
        else if (wetnessRatio >= 0.30 || activePrecipitationRateMmHr >= 0.5)
        {
            condition = RunwaySurfaceCondition.Wet;
            rcamCode = 5;
            label = "WET (CC 5)";
        }
        else if (wetnessRatio >= 0.05 || activePrecipitationRateMmHr > 0)
        {
            condition = RunwaySurfaceCondition.Damp;
            rcamCode = 5;
            label = "DAMP (CC 5)";
        }
        else
        {
            condition = RunwaySurfaceCondition.Dry;
            rcamCode = 6;
            label = "DRY (CC 6)";
            wetnessRatio = 0;
            dryingHours = 0;
        }

        // Keep a tiny residual precipitation floor after rain stops so the simulator's ground
        // wetness shader stays active without rendering a rain shaft. Unverified live: confirm
        // in MSFS 2024 that this alone holds wet tarmac (GAPS.md 2026-10-30).
        double effectivePrecipRate = activePrecipitationRateMmHr;
        if (activePrecipitationRateMmHr <= 0.01 && wetnessRatio >= 0.10)
        {
            effectivePrecipRate = Math.Round(0.04 + wetnessRatio * 0.14, 3);
        }

        string summary = condition == RunwaySurfaceCondition.Dry
            ? $"Runway Dry (RCAM 6) • 12h Precip: {past12hTotalMm:F1} mm"
            : activePrecipitationRateMmHr > 0.05
                ? $"{label} • Active {activePrecipitationRateMmHr:F1} mm/h (12h: {past12hTotalMm:F1} mm, Evap: {currentEvapMmHr:F2} mm/h)"
                : $"{label} • Residual 12h Rain ({past12hTotalMm:F1} mm) • Est. Dry in {dryingHours:F1}h (Evap {currentEvapMmHr:F2} mm/h)";

        return new RunwaySurfaceReport
        {
            Condition = condition,
            ConditionLabel = label,
            FaaConditionCode = rcamCode,
            WetnessRatio = Math.Round(wetnessRatio, 2),
            Past12HourRainfallMm = Math.Round(past12hTotalMm, 1),
            ActivePrecipitationRateMmHr = Math.Round(activePrecipitationRateMmHr, 2),
            EvaporationRateMmHr = Math.Round(currentEvapMmHr, 2),
            EstimatedDryingTimeHours = dryingHours,
            EffectivePrecipitationRateMmHr = effectivePrecipRate,
            Summary = summary
        };
    }

    public static double CalculateEvaporationRateMmHr(
        double temperatureCelsius,
        double dewpointCelsius,
        double windSpeedKnots,
        double shortwaveRadiationWm2)
    {
        var spread = Math.Max(0.0, temperatureCelsius - dewpointCelsius);
        var tempFactor = Math.Max(0.05, (temperatureCelsius + 10.0) / 25.0);
        var windFactor = 1.0 + (Math.Clamp(windSpeedKnots, 0.0, 45.0) * 0.045);
        var solarFactor = Math.Clamp(shortwaveRadiationWm2, 0.0, 1000.0) / 350.0;

        // Baseline evaporation in mm/hr: combines aerodynamic vapor deficit + solar radiative heating
        var evap = (0.04 + (spread * 0.025 * tempFactor)) * windFactor + (solarFactor * 0.22);
        return Math.Clamp(evap, 0.03, 1.80);
    }

    private static double EstimateSolarRadiationFromTime(DateTime utcNow, double lat, double lon)
    {
        var solarHour = (utcNow.Hour + (utcNow.Minute / 60.0) + (lon / 15.0) + 24.0) % 24.0;
        if (solarHour < 6.0 || solarHour > 18.0)
            return 0.0;

        var elevationSine = Math.Sin((solarHour - 6.0) / 12.0 * Math.PI);
        var latFactor = Math.Max(0.25, Math.Cos(Math.Abs(lat) * Math.PI / 180.0));
        return Math.Max(0.0, 750.0 * elevationSine * latFactor);
    }
}
