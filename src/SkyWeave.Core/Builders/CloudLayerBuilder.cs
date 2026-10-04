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
        var isClearAtSurface = hasExplicitClear || (metar?.Clouds != null && metar.Clouds.Count == 0 && !raw.Contains("FG") && !raw.Contains("VV"));

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

        // Surface Fog Deck Synthesis:
        // In MSFS 2024, aerosol/pollution factor alone does not reliably produce low-visibility runway fog.
        // When METAR reports fog (FG, FZFG) or visibility <= 1600m (<= 1 SM), synthesize a ground stratus deck
        // resting at station elevation to ensure genuine IMC volumetric fog in the simulator.
        var hasFogPhenomenon = (metar?.WeatherConditions != null && metar.WeatherConditions.Any(p => p.Contains("FG", StringComparison.OrdinalIgnoreCase)))
            || System.Text.RegularExpressions.Regex.IsMatch(raw, @"\b(\+|-|VC)?(FG|FZFG)\b");
        var hasLowVis = metar != null && metar.VisibilityMeters > 0 && metar.VisibilityMeters <= 1600;
        var isFog = !hasExplicitClear && (hasFogPhenomenon || hasLowVis);

        if (isFog && !layers.Any(l => l.BaseFeetAgl <= 150 && l.CoveragePercent >= 0.7))
        {
            layers.Insert(0, new CloudLayer
            {
                BaseMeters = Math.Max(0, elevationMeters),
                TopMeters = Math.Max(0, elevationMeters + 400 * FEET_TO_METERS),
                BaseFeetAgl = 0,
                TopFeetAgl = 400,
                Density = 0.95,
                Scattering = 0.03,
                Type = CloudType.ST,
                CoveragePercent = 1.0
            });
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

    /// <summary>
    /// Prioritizes cloud layers so the sim's volumetric renderer (which focuses on ~3 active slots)
    /// preserves the layer enclosing the aircraft's current altitude, the lowest surface/ceiling layer,
    /// and major convective layers.
    /// </summary>
    public static List<CloudLayer> PrioritizeCloudLayers(List<CloudLayer> layers, double? aircraftAltitudeFeet, int maxSlots = 24)
    {
        if (layers == null || layers.Count <= 3 || !aircraftAltitudeFeet.HasValue)
            return layers ?? new List<CloudLayer>();

        var altFt = aircraftAltitudeFeet.Value;

        // 1. Lowest layer (critical for surface ceiling & runway visibility)
        var lowest = layers.OrderBy(l => l.BaseMeters).First();

        // 2. Layer enclosing or closest to aircraft altitude
        var enclosingOrClosest = layers.FirstOrDefault(l =>
            (l.BaseMeters / FEET_TO_METERS) <= altFt + 500 &&
            (l.TopMeters / FEET_TO_METERS) >= altFt - 500)
            ?? layers.MinBy(l => Math.Abs(((l.BaseMeters + l.TopMeters) / (2.0 * FEET_TO_METERS)) - altFt));

        // 3. Thickest/most convective layer
        var convective = layers.Where(l => l != lowest && l != enclosingOrClosest)
            .MaxBy(l => (l.TopMeters - l.BaseMeters) * l.Density);

        var topPriority = new HashSet<CloudLayer> { lowest };
        if (enclosingOrClosest != null) topPriority.Add(enclosingOrClosest);
        if (convective != null) topPriority.Add(convective);

        var result = new List<CloudLayer>(topPriority);
        foreach (var layer in layers.OrderBy(l => l.BaseMeters))
        {
            if (result.Count >= maxSlots) break;
            if (!result.Contains(layer))
                result.Add(layer);
        }

        return result.OrderBy(l => l.BaseMeters).ToList();
    }
}
