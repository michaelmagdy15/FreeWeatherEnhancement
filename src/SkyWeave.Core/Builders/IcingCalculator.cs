using SkyWeave.Core.Models;

namespace SkyWeave.Core.Builders;

public class IcingCalculator
{
    public List<IcingLayer> CalculateIcingLayers(
        List<CloudLayer> cloudLayers, 
        List<WindLayer> windLayers)
    {
        var icingLayers = new List<IcingLayer>();

        foreach (var cloud in cloudLayers)
        {
            var baseFeetMsl = cloud.BaseMeters / WeatherUnits.FeetToMeters;
            var topFeetMsl = cloud.TopMeters / WeatherUnits.FeetToMeters;
            var baseTemp = GetTemperatureAtAltitude(windLayers, baseFeetMsl);
            var topTemp = GetTemperatureAtAltitude(windLayers, topFeetMsl);

            if (baseTemp > 0 && topTemp > 0)
                continue;

            if (baseTemp < -40 && topTemp < -40)
                continue;

            var icingBase = baseFeetMsl;
            var icingTop = topFeetMsl;

            if (baseTemp > 0)
                icingBase = FindFreezingLevel(windLayers, baseFeetMsl, topFeetMsl);

            if (topTemp < -40)
                icingTop = FindMaxIcingAltitude(windLayers, baseFeetMsl, topFeetMsl);

            if (icingBase >= icingTop)
                continue;

            var avgTemp = (baseTemp + topTemp) / 2;
            var severity = CalculateSeverity(baseTemp, topTemp, cloud.Density);
            var icingType = avgTemp > -10 ? IcingType.Clear : (avgTemp < -15 ? IcingType.Rime : IcingType.Mixed);

            icingLayers.Add(new IcingLayer
            {
                BaseFeet = icingBase,
                TopFeet = icingTop,
                Severity = severity,
                TemperatureCelsius = avgTemp,
                CloudDensity = cloud.Density,
                IcingType = icingType
            });
        }

        return MergeOverlappingIcingLayers(icingLayers);
    }

    public double CalculateIcingIndex(double tempC, bool inCloud, double cloudDensity)
    {
        if (!inCloud || tempC > 0 || tempC < -40)
            return 0.0;

        double tempFactor = 1.0 - Math.Abs(tempC + 15.0) / 15.0;
        tempFactor = Math.Max(0, tempFactor);

        double moistureFactor = cloudDensity;

        return tempFactor * moistureFactor;
    }

    private double GetTemperatureAtAltitude(List<WindLayer> windLayers, double altitudeFeet)
    {
        if (windLayers.Count == 0)
            return -15;

        var lower = windLayers.Where(w => w.AltitudeFeet <= altitudeFeet).OrderByDescending(w => w.AltitudeFeet).FirstOrDefault();
        var upper = windLayers.Where(w => w.AltitudeFeet >= altitudeFeet).OrderBy(w => w.AltitudeFeet).FirstOrDefault();

        if (lower == null && upper == null)
            return -15;

        if (lower == null)
            return upper!.TemperatureCelsius;

        if (upper == null)
            return lower.TemperatureCelsius;

        if (upper.AltitudeFeet == lower.AltitudeFeet)
            return lower.TemperatureCelsius;

        var t = (altitudeFeet - lower.AltitudeFeet) / (upper.AltitudeFeet - lower.AltitudeFeet);
        return lower.TemperatureCelsius + t * (upper.TemperatureCelsius - lower.TemperatureCelsius);
    }

    private double FindFreezingLevel(List<WindLayer> windLayers, double minAlt, double maxAlt)
    {
        for (double alt = minAlt; alt <= maxAlt; alt += 100)
        {
            var temp = GetTemperatureAtAltitude(windLayers, alt);
            if (temp <= 0)
                return alt;
        }
        return maxAlt;
    }

    private double FindMaxIcingAltitude(List<WindLayer> windLayers, double minAlt, double maxAlt)
    {
        for (double alt = maxAlt; alt >= minAlt; alt -= 100)
        {
            var temp = GetTemperatureAtAltitude(windLayers, alt);
            if (temp >= -40)
                return alt;
        }
        return minAlt;
    }

    private IcingSeverity CalculateSeverity(double baseTemp, double topTemp, double cloudDensity)
    {
        var avgTemp = (baseTemp + topTemp) / 2;
        var tempSeverity = Math.Abs(avgTemp + 15) < 10 ? 1.0 : 0.5;
        var cloudSeverity = cloudDensity;

        var combined = tempSeverity * cloudSeverity;

        return combined switch
        {
            >= 0.8 => IcingSeverity.Severe,
            >= 0.5 => IcingSeverity.Moderate,
            >= 0.2 => IcingSeverity.Light,
            _ => IcingSeverity.None
        };
    }

    private List<IcingLayer> MergeOverlappingIcingLayers(List<IcingLayer> layers)
    {
        var merged = new List<IcingLayer>();
        var sorted = layers.OrderBy(l => l.BaseFeet).ToList();

        foreach (var layer in sorted)
        {
            var overlapping = merged.FirstOrDefault(l =>
                layer.BaseFeet <= l.TopFeet &&
                layer.TopFeet >= l.BaseFeet);

            if (overlapping != null)
            {
                overlapping.BaseFeet = Math.Min(overlapping.BaseFeet, layer.BaseFeet);
                overlapping.TopFeet = Math.Max(overlapping.TopFeet, layer.TopFeet);
                overlapping.Severity = (IcingSeverity)Math.Max((int)overlapping.Severity, (int)layer.Severity);
                overlapping.CloudDensity = Math.Max(overlapping.CloudDensity, layer.CloudDensity);
                
                // If severity is updated, also update icing type or mix it
                if ((int)layer.Severity > (int)overlapping.Severity)
                {
                    overlapping.IcingType = layer.IcingType;
                }
                else if (overlapping.IcingType != layer.IcingType)
                {
                    overlapping.IcingType = IcingType.Mixed;
                }
            }
            else
            {
                merged.Add(layer);
            }
        }

        return merged;
    }
}
