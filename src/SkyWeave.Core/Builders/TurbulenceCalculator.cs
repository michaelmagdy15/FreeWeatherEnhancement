using SkyWeave.Core.Models;

namespace SkyWeave.Core.Builders;

public class TurbulenceCalculator
{
    public List<TurbulenceLayer> CalculateTurbulenceLayers(
        List<WindLayer> windLayers,
        List<CloudLayer> cloudLayers,
        List<StormCell> stormCells,
        double aircraftAltitudeFeet)
    {
        var layers = new List<TurbulenceLayer>();

        layers.AddRange(CalculateThermalTurbulence(windLayers, cloudLayers));
        layers.AddRange(CalculateConvectiveTurbulence(stormCells));
        layers.AddRange(CalculateMechanicalTurbulence(windLayers));
        layers.AddRange(CalculateMountainWaveTurbulence(windLayers));
        layers.AddRange(CalculateJetstreamCatTurbulence(windLayers));

        return MergeOverlappingTurbulenceLayers(layers);
    }

    public double CalculateTurbulenceIndex(
        double windSpeedKnots,
        double windShear,
        bool inCloud,
        bool nearStorm,
        double? gustSpeed)
    {
        double index = 0;

        if (gustSpeed.HasValue && gustSpeed.Value > 15)
            index += Math.Min(0.3, (gustSpeed.Value - 15) / 50);

        if (windShear > 20)
            index += Math.Min(0.3, (windShear - 20) / 60);

        if (inCloud)
            index += 0.2;

        if (nearStorm)
            index += 0.3;

        return Math.Min(1.0, index);
    }

    private List<TurbulenceLayer> CalculateThermalTurbulence(
        List<WindLayer> windLayers,
        List<CloudLayer> cloudLayers)
    {
        var layers = new List<TurbulenceLayer>();

        foreach (var cloud in cloudLayers)
        {
            if (cloud.Type == CloudType.CB || cloud.Type == CloudType.TCU)
            {
                layers.Add(new TurbulenceLayer
                {
                    BaseFeet = (cloud.BaseMeters / WeatherUnits.FeetToMeters),
                    TopFeet = (cloud.TopMeters / WeatherUnits.FeetToMeters),
                    Intensity = TurbulenceIntensity.Moderate,
                    Type = TurbulenceType.Thermal
                });
            }
        }

        return layers;
    }

    private List<TurbulenceLayer> CalculateConvectiveTurbulence(List<StormCell> stormCells)
    {
        var layers = new List<TurbulenceLayer>();

        foreach (var cell in stormCells)
        {
            var intensity = cell.Intensity switch
            {
                >= 0.8 => TurbulenceIntensity.Severe,
                >= 0.5 => TurbulenceIntensity.Moderate,
                >= 0.2 => TurbulenceIntensity.Light,
                _ => TurbulenceIntensity.None
            };

            if (intensity != TurbulenceIntensity.None)
            {
                layers.Add(new TurbulenceLayer
                {
                    BaseFeet = Math.Max(0, cell.AltitudeFeet - 5000),
                    TopFeet = cell.AltitudeFeet + 10000,
                    Intensity = intensity,
                    Type = TurbulenceType.Convective
                });
            }
        }

        return layers;
    }

    private List<TurbulenceLayer> CalculateMechanicalTurbulence(List<WindLayer> windLayers)
    {
        var layers = new List<TurbulenceLayer>();

        for (int i = 1; i < windLayers.Count; i++)
        {
            var prev = windLayers[i - 1];
            var curr = windLayers[i];

            if (Math.Abs(curr.AltitudeFeet - prev.AltitudeFeet) < 1)
                continue;

            var speedDiff = Math.Abs(curr.SpeedKnots - prev.SpeedKnots);
            var dirDiff = Math.Abs(curr.DirectionDegrees - prev.DirectionDegrees);
            if (dirDiff > 180) dirDiff = 360 - dirDiff;

            var shear = speedDiff + dirDiff * 0.5;

            if (shear > 30)
            {
                var intensity = shear switch
                {
                    >= 60 => TurbulenceIntensity.Severe,
                    >= 45 => TurbulenceIntensity.Moderate,
                    >= 30 => TurbulenceIntensity.Light,
                    _ => TurbulenceIntensity.None
                };

                layers.Add(new TurbulenceLayer
                {
                    BaseFeet = prev.AltitudeFeet,
                    TopFeet = curr.AltitudeFeet,
                    Intensity = intensity,
                    Type = TurbulenceType.Mechanical
                });
            }
        }

        return layers;
    }

    private List<TurbulenceLayer> CalculateMountainWaveTurbulence(List<WindLayer> windLayers)
    {
        var layers = new List<TurbulenceLayer>();
        var added = false;

        foreach (var wind in windLayers)
        {
            if (wind.AltitudeFeet >= 12000 || wind.SpeedKnots < 40) continue;

            if (!added)
            {
                layers.Add(new TurbulenceLayer
                {
                    BaseFeet = 3000,
                    TopFeet = 20000,
                    Intensity = wind.SpeedKnots >= 55 ? TurbulenceIntensity.Moderate : TurbulenceIntensity.Light,
                    Type = TurbulenceType.MountainWave
                });
                added = true;
            }
        }

        return layers;
    }

    private List<TurbulenceLayer> CalculateJetstreamCatTurbulence(List<WindLayer> windLayers)
    {
        var layers = new List<TurbulenceLayer>();

        for (int i = 1; i < windLayers.Count; i++)
        {
            var prev = windLayers[i - 1];
            var curr = windLayers[i];

            if (curr.AltitudeFeet < 25000 || curr.SpeedKnots < 70) continue;
            if (Math.Abs(curr.AltitudeFeet - prev.AltitudeFeet) < 1) continue;

            var speedDiff = Math.Abs(curr.SpeedKnots - prev.SpeedKnots);
            var dirDiff = Math.Abs(curr.DirectionDegrees - prev.DirectionDegrees);
            if (dirDiff > 180) dirDiff = 360 - dirDiff;

            var shear = speedDiff + dirDiff * 0.5;

            if (shear < 25) continue;

            layers.Add(new TurbulenceLayer
            {
                BaseFeet = Math.Min(prev.AltitudeFeet, curr.AltitudeFeet),
                TopFeet = Math.Max(prev.AltitudeFeet, curr.AltitudeFeet),
                Intensity = shear >= 40 ? TurbulenceIntensity.Moderate : TurbulenceIntensity.Light,
                Type = TurbulenceType.Mechanical
            });
        }

        return layers;
    }

    private List<TurbulenceLayer> MergeOverlappingTurbulenceLayers(List<TurbulenceLayer> layers)
    {
        var merged = new List<TurbulenceLayer>();
        var sorted = layers.OrderBy(l => l.BaseFeet).ToList();

        foreach (var layer in sorted)
        {
            var overlapping = merged.FirstOrDefault(l =>
                layer.BaseFeet <= l.TopFeet &&
                layer.TopFeet >= l.BaseFeet &&
                l.Type == layer.Type);

            if (overlapping != null)
            {
                overlapping.BaseFeet = Math.Min(overlapping.BaseFeet, layer.BaseFeet);
                overlapping.TopFeet = Math.Max(overlapping.TopFeet, layer.TopFeet);
                overlapping.Intensity = (TurbulenceIntensity)Math.Max((int)overlapping.Intensity, (int)layer.Intensity);
            }
            else
            {
                merged.Add(layer);
            }
        }

        return merged;
    }
}
