using SkyWeave.Core.Models;

namespace SkyWeave.Core.Builders;

public class CloudLayerBuilder
{
    private const double FEET_TO_METERS = WeatherUnits.FeetToMeters;
    private const int MAX_LAYERS = 24;

    public List<CloudLayer> BuildCloudLayers(MetarData metar, WindsAloftData? windsAloft)
        => BuildCloudLayers(metar, windsAloft, 0);

    public List<CloudLayer> BuildCloudLayers(MetarData metar, WindsAloftData? windsAloft, double stationElevationFeet)
    {
        if (!double.IsFinite(stationElevationFeet)) throw new ArgumentOutOfRangeException(nameof(stationElevationFeet));
        var elevationMeters = stationElevationFeet * FEET_TO_METERS;
        var layers = new List<CloudLayer>();

        var raw = metar?.RawText?.ToUpperInvariant() ?? string.Empty;
        var hasExplicitClear = raw.Contains("CLR") || raw.Contains("SKC") || raw.Contains("CAVOK") || raw.Contains("NCD") || raw.Contains("NSC");
        var isClearAtSurface = hasExplicitClear || (metar?.Clouds != null && metar.Clouds.Count == 0);

        if (metar?.Clouds != null && metar.Clouds.Count > 0)
        {
            foreach (var metarCloud in metar.Clouds)
            {
                var thickness = EstimateLayerThickness(metarCloud.Type);
                layers.Add(new CloudLayer
                {
                    BaseMeters = Math.Max(0, (stationElevationFeet + metarCloud.BaseFeet) * FEET_TO_METERS),
                    TopMeters = Math.Max(0, (stationElevationFeet + metarCloud.BaseFeet + thickness) * FEET_TO_METERS),
                    BaseFeetAgl = metarCloud.BaseFeet,
                    TopFeetAgl = metarCloud.BaseFeet + thickness,
                    Density = MapCoverageToDensity(metarCloud.Coverage),
                    Scattering = MapCloudTypeToScattering(metarCloud.Type),
                    Type = MapToCloudType(metarCloud.Coverage),
                    CoveragePercent = MapCoverageToPercent(metarCloud.Coverage)
                });
            }
        }

        if (windsAloft?.PressureLevels != null)
        {
            foreach (var level in windsAloft.PressureLevels)
            {
                if (level.AltitudeMeters <= elevationMeters) continue;
                // If METAR is explicitly clear at surface, do not create low-altitude clouds below 10,000 ft (3000m)
                if (isClearAtSurface && level.AltitudeMeters - elevationMeters < 3000)
                    continue;

                if (level.CloudCoverPercent > 0)
                {
                    if (level.CloudCoverPercent < 45) continue;

                    var existingLayer = FindOverlappingLayer(layers, level.AltitudeMeters);
                    if (existingLayer == null)
                    {
                        layers.Add(new CloudLayer
                        {
                            BaseMeters = Math.Max(Math.Max(0, elevationMeters), level.AltitudeMeters - 500),
                            TopMeters = level.AltitudeMeters + 500,
                            BaseFeetAgl = Math.Max(0, (level.AltitudeMeters - 500) / FEET_TO_METERS - stationElevationFeet),
                            TopFeetAgl = (level.AltitudeMeters + 500) / FEET_TO_METERS - stationElevationFeet,
                            Density = MapCloudCoverToDensity(level.CloudCoverPercent),
                            Scattering = EstimateScattering(level),
                            Type = EstimateCloudType(level),
                            CoveragePercent = Math.Min(1.0, level.CloudCoverPercent / 100.0)
                        });
                    }
                }
                else if (level.RelativeHumidity >= 85)
                {
                    var existingLayer = FindOverlappingLayer(layers, level.AltitudeMeters);
                    if (existingLayer == null)
                    {
                        layers.Add(new CloudLayer
                        {
                            BaseMeters = Math.Max(Math.Max(0, elevationMeters), level.AltitudeMeters - 500),
                            TopMeters = level.AltitudeMeters + 500,
                            BaseFeetAgl = Math.Max(0, (level.AltitudeMeters - 500) / FEET_TO_METERS - stationElevationFeet),
                            TopFeetAgl = (level.AltitudeMeters + 500) / FEET_TO_METERS - stationElevationFeet,
                            Density = MapHumidityToDensity(level.RelativeHumidity),
                            Scattering = EstimateScattering(level),
                            Type = EstimateCloudType(level),
                            CoveragePercent = Math.Min(1.0, level.RelativeHumidity / 100.0)
                        });
                    }
                }
            }
        }

        layers = MergeOverlappingLayers(layers, stationElevationFeet);
        return layers.Take(MAX_LAYERS).ToList();
    }

    private int EstimateLayerThickness(string cloudType)
    {
        return cloudType?.ToUpper() switch
        {
            "FEW" => 1000,
            "SCT" => 2000,
            "BKN" => 3000,
            "OVC" => 4000,
            "CB" => 15000,
            "TCU" => 10000,
            _ => 2000
        };
    }

    private double MapCoverageToDensity(string? coverage)
    {
        return coverage?.ToUpper() switch
        {
            "FEW" => 0.2,
            "SCT" => 0.4,
            "BKN" => 0.7,
            "OVC" => 1.0,
            "VV" => 1.0,
            _ => 0.5
        };
    }

    private double MapCoverageToPercent(string? coverage)
    {
        return coverage?.ToUpper() switch
        {
            "FEW" => 0.15,
            "SCT" => 0.35,
            "BKN" => 0.7,
            "OVC" => 1.0,
            "VV" => 1.0,
            _ => 0.5
        };
    }

    private double MapCloudTypeToScattering(string? cloudType)
    {
        return cloudType?.ToUpper() switch
        {
            "FEW" => 0.3,
            "SCT" => 0.5,
            "BKN" => 0.2,
            "OVC" => 0.0,
            "CB" => 0.9,
            "TCU" => 0.8,
            _ => 0.5
        };
    }

    private CloudType MapToCloudType(string? coverage)
    {
        return coverage?.ToUpper() switch
        {
            "FEW" => CloudType.FEW,
            "SCT" => CloudType.SCT,
            "BKN" => CloudType.BKN,
            "OVC" => CloudType.OVC,
            "VV" => CloudType.OVC,
            _ => CloudType.SCT
        };
    }

    private double MapHumidityToDensity(double humidity)
    {
        if (humidity > 90) return 0.9;
        if (humidity > 80) return 0.7;
        if (humidity > 70) return 0.5;
        return 0.3;
    }

    private double MapCloudCoverToDensity(double cloudCoverPercent)
    {
        if (cloudCoverPercent >= 95) return 1.0;
        if (cloudCoverPercent >= 85) return 0.8;
        if (cloudCoverPercent >= 70) return 0.6;
        if (cloudCoverPercent >= 55) return 0.45;
        return 0.35;
    }

    private double EstimateScattering(PressureLevelData level)
    {
        if (level.RelativeHumidity > 90) return 0.1;
        if (level.RelativeHumidity > 80) return 0.3;
        return 0.5;
    }

    private CloudType EstimateCloudType(PressureLevelData level)
    {
        if (level.AltitudeFeet < 6500) return CloudType.ST;
        if (level.AltitudeFeet < 20000) return CloudType.SCT;
        return CloudType.BKN;
    }

    private CloudLayer? FindOverlappingLayer(List<CloudLayer> layers, double altitudeMeters)
    {
        return layers.FirstOrDefault(l => 
            altitudeMeters >= l.BaseMeters - 500 && 
            altitudeMeters <= l.TopMeters + 500);
    }

    private List<CloudLayer> MergeOverlappingLayers(List<CloudLayer> layers, double stationElevationFeet)
    {
        var merged = new List<CloudLayer>();
        var sorted = layers.OrderBy(l => l.BaseMeters).ToList();

        foreach (var layer in sorted)
        {
            var overlapping = merged.FirstOrDefault(l => 
                layer.BaseMeters <= l.TopMeters + 200 && 
                layer.TopMeters >= l.BaseMeters - 200);

            if (overlapping != null)
            {
                overlapping.BaseMeters = Math.Min(overlapping.BaseMeters, layer.BaseMeters);
                overlapping.TopMeters = Math.Max(overlapping.TopMeters, layer.TopMeters);
                overlapping.BaseFeetAgl = Math.Max(0, overlapping.BaseMeters / FEET_TO_METERS - stationElevationFeet);
                overlapping.TopFeetAgl = Math.Max(0, overlapping.TopMeters / FEET_TO_METERS - stationElevationFeet);
                overlapping.Density = Math.Max(overlapping.Density, layer.Density);
            }
            else
            {
                merged.Add(layer);
            }
        }

        return merged;
    }
}
