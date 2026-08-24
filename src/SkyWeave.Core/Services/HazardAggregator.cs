using System;
using System.Collections.Generic;
using System.Linq;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class HazardAggregator
{
    private static readonly Dictionary<string, double> WxPrecipRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["+TSRA"] = 30, ["TSRA"] = 25, ["+RA"] = 20, ["RA"] = 5, ["-RA"] = 1,
        ["+SN"] = 15, ["SN"] = 3, ["-SN"] = 0.5, ["RASN"] = 4,
        ["GR"] = 8, ["+SHSN"] = 12, ["SHSN"] = 5
    };

    public double CalculatePrecipitationRate(MetarData metar)
    {
        foreach (var condition in metar.WeatherConditions)
        {
            if (WxPrecipRates.TryGetValue(condition, out var rate))
                return rate;
            
            // Fallbacks for unknown combinations
            var upper = condition.ToUpper();
            if (upper.Contains("TS")) return 30.0;
            if (upper.Contains("GR")) return 8.0;
        }
        return 0.0;
    }

    public double CalculateFreezingLevel(List<WindLayer> windLayers)
    {
        for (int i = 0; i < windLayers.Count - 1; i++)
        {
            if (windLayers[i].TemperatureCelsius >= 0 &&
                windLayers[i + 1].TemperatureCelsius < 0)
            {
                var t = windLayers[i].TemperatureCelsius /
                        (windLayers[i].TemperatureCelsius - windLayers[i + 1].TemperatureCelsius);
                return windLayers[i].AltitudeFeet +
                       t * (windLayers[i + 1].AltitudeFeet - windLayers[i].AltitudeFeet);
            }
        }
        return 10000;
    }

    public double CalculateCeiling(MetarData metar)
    {
        var bknOvc = metar.Clouds
            .Where(c => c.Coverage == "BKN" || c.Coverage == "OVC" || c.Coverage == "VV")
            .OrderBy(c => c.BaseFeet)
            .FirstOrDefault();
        return bknOvc?.BaseFeet ?? 99999;
    }

    public double CalculateThunderstormIntensity(
        MetarData metar, List<WeatherHazard> sigmets,
        List<LightningStrike> lightning, List<StormCell> stormCells)
    {
        double intensity = 0;

        if (metar.WeatherConditions.Any(w => w.Contains("TS")))
            intensity = Math.Max(intensity, 0.4);

        if (sigmets.Any(h => h.Type == HazardType.ConvectiveSigmet))
            intensity = Math.Max(intensity, 0.7);

        if (lightning.Count > 0)
        {
            var strikeIntensity = Math.Min(0.8, lightning.Count / 20.0);
            intensity = Math.Max(intensity, strikeIntensity);
        }

        if (stormCells.Count > 0)
        {
            var cellIntensity = stormCells.Max(c => c.Intensity);
            intensity = Math.Max(intensity, cellIntensity);
        }

        return Math.Min(1.0, intensity);
    }

    public double CalculateIcingIndex(List<IcingLayer> icingLayers)
    {
        if (icingLayers.Count == 0) return 0;
        return Math.Min(1.0, icingLayers.Count / 5.0);
    }

    public double CalculateTurbulenceIndex(List<TurbulenceLayer> turbulenceLayers, List<LightningStrike> lightning, double altitudeFeet)
    {
        if (turbulenceLayers.Count == 0) return 0.02;

        var localLayers = turbulenceLayers.Where(l => altitudeFeet >= l.BaseFeet - 2500 && altitudeFeet <= l.TopFeet + 2500).ToList();
        if (localLayers.Count == 0) return 0.02; // Smooth air

        var maxIntensity = localLayers.Max(l => l.Intensity switch
        {
            TurbulenceIntensity.Extreme => 1.0,
            TurbulenceIntensity.Severe => 0.8,
            TurbulenceIntensity.Moderate => 0.45,
            TurbulenceIntensity.Light => 0.15,
            _ => 0.02
        });

        if (lightning.Count > 5)
            maxIntensity = Math.Max(maxIntensity, 0.6);

        return Math.Min(1.0, maxIntensity);
    }

    public double CalculateAerosolDensity(double visibilityMeters)
    {
        if (visibilityMeters > 10000) return 0.1;
        if (visibilityMeters > 5000) return 0.3;
        if (visibilityMeters > 2000) return 0.5;
        if (visibilityMeters > 1000) return 0.7;
        return 0.9;
    }
}
