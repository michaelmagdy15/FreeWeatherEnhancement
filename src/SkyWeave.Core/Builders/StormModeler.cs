using System.Text.RegularExpressions;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Builders;

public class StormModeler
{
    private const double EarthRadiusNm = 3440.065;
    private const double ClusterRadiusNm = 15.0;
    private const double MinStrikesForCluster = 3;
    private const double IntensityCap = 1.0;
    private const double StrikeIntensityDivisor = 10.0;
    private const double BaseRadiusNm = 5.0;
    private const double RadiusPerStrike = 0.5;
    private const double MultiCellThreshold = 10;

    public List<StormCell> ModelStorms(
        List<LightningStrike> lightning,
        List<WeatherHazard> sigmets,
        double latitude,
        double longitude,
        DateTime lastModelTime,
        WindsAloftData? windsAloft = null)
    {
        var stormCells = new List<StormCell>();
        var capeBoost = CalculateCapeBoost(windsAloft?.ConvectiveAvailablePotentialEnergy);

        // Steering winds are typically found at the 500hPa level (~18,000 ft).
        var steeringWind = windsAloft?.PressureLevels.OrderBy(p => Math.Abs(p.PressureHpa - 500)).FirstOrDefault();
        double baseMotionDir = steeringWind?.WindDirectionDegrees ?? 0;
        double baseMotionSpeed = steeringWind?.WindSpeedKnots ?? 0;

        var clusters = ClusterLightningStrikes(lightning);

        foreach (var cluster in clusters)
        {
            var avgLat = cluster.Average(s => s.Latitude);
            var avgLon = cluster.Average(s => s.Longitude);
            var intensity = Math.Min(IntensityCap, cluster.Count / StrikeIntensityDivisor);
            var radiusNm = BaseRadiusNm + (cluster.Count * RadiusPerStrike);
            var type = cluster.Count >= MultiCellThreshold ? CellType.MultiCell : CellType.Core;

            var cell = new StormCell
            {
                Latitude = avgLat,
                Longitude = avgLon,
                AltitudeFeet = 15000,
                MotionDirectionDegrees = baseMotionDir,
                MotionSpeedKnots = baseMotionSpeed,
                Intensity = intensity,
                RadiusNm = radiusNm,
                Type = type,
                NearbyStrikes = cluster
            };

            AddCloudLayers(cell);
            stormCells.Add(cell);
        }

        foreach (var sigmet in sigmets)
        {
            if (sigmet.Type == HazardType.ConvectiveSigmet &&
                sigmet.Latitude.HasValue && sigmet.Longitude.HasValue)
            {
                var motion = ParseMotionFromRawText(sigmet.RawText);
                var sigmetDir = motion?.direction ?? baseMotionDir;
                var sigmetSpeed = motion?.speed ?? baseMotionSpeed;

                var existingCell = stormCells
                    .OrderBy(c => CalculateDistanceNm(c.Latitude, c.Longitude, sigmet.Latitude!.Value, sigmet.Longitude!.Value))
                    .FirstOrDefault();

                if (existingCell != null)
                {
                    var dist = CalculateDistanceNm(existingCell.Latitude, existingCell.Longitude, sigmet.Latitude!.Value, sigmet.Longitude!.Value);
                    if (dist < existingCell.RadiusNm + 20)
                    {
                        existingCell.Intensity = Math.Max(existingCell.Intensity, 0.7);
                        existingCell.Type = existingCell.Intensity >= 0.9 ? CellType.MultiCell : existingCell.Type;
                        existingCell.MotionDirectionDegrees = sigmetDir;
                        existingCell.MotionSpeedKnots = sigmetSpeed;
                    }
                    else
                    {
                        var newCell = new StormCell
                        {
                            Latitude = sigmet.Latitude!.Value,
                            Longitude = sigmet.Longitude!.Value,
                            AltitudeFeet = 15000,
                            MotionDirectionDegrees = sigmetDir,
                            MotionSpeedKnots = sigmetSpeed,
                            Intensity = 0.75,
                            RadiusNm = 10,
                            Type = CellType.MultiCell
                        };
                        AddCloudLayers(newCell);
                        stormCells.Add(newCell);
                    }
                }
                else
                {
                    var newCell = new StormCell
                    {
                        Latitude = sigmet.Latitude!.Value,
                        Longitude = sigmet.Longitude!.Value,
                        AltitudeFeet = 15000,
                        MotionDirectionDegrees = sigmetDir,
                        MotionSpeedKnots = sigmetSpeed,
                        Intensity = 0.75,
                        RadiusNm = 10,
                        Type = CellType.MultiCell
                    };
                    AddCloudLayers(newCell);
                    stormCells.Add(newCell);
                }
            }
        }

        if (lastModelTime != default)
        {
            var elapsedHours = (DateTime.UtcNow - lastModelTime).TotalHours;
            if (elapsedHours > 0)
            {
                foreach (var cell in stormCells)
                {
                    if (cell.MotionSpeedKnots > 0)
                    {
                        var displacementNm = cell.MotionSpeedKnots * elapsedHours;
                        cell.Latitude = cell.Latitude + displacementNm * Math.Cos(ToRadians(cell.MotionDirectionDegrees)) / EarthRadiusNm * (180 / Math.PI);
                        cell.Longitude = cell.Longitude + displacementNm * Math.Sin(ToRadians(cell.MotionDirectionDegrees)) / (EarthRadiusNm * Math.Cos(ToRadians(cell.Latitude))) * (180 / Math.PI);
                    }
                }
            }
        }

        if (capeBoost > 0)
        {
            foreach (var cell in stormCells)
            {
                cell.Intensity = Math.Min(IntensityCap, cell.Intensity + capeBoost);
            }
        }

        return stormCells;
    }

    public double CalculateCapeBoost(double? cape)
    {
        if (!cape.HasValue) return 0;

        return cape.Value switch
        {
            >= 4000 => 0.25,
            >= 2500 => 0.20,
            >= 1500 => 0.15,
            >= 1000 => 0.10,
            >= 500 => 0.05,
            _ => 0
        };
    }

    private List<List<LightningStrike>> ClusterLightningStrikes(List<LightningStrike> strikes)
    {
        var clusters = new List<List<LightningStrike>>();
        var used = new HashSet<int>();

        for (int i = 0; i < strikes.Count; i++)
        {
            if (used.Contains(i)) continue;

            var cluster = new List<LightningStrike> { strikes[i] };
            used.Add(i);

            for (int j = i + 1; j < strikes.Count; j++)
            {
                if (used.Contains(j)) continue;

                var dist = CalculateDistanceNm(
                    strikes[i].Latitude, strikes[i].Longitude,
                    strikes[j].Latitude, strikes[j].Longitude);

                if (dist < ClusterRadiusNm)
                {
                    cluster.Add(strikes[j]);
                    used.Add(j);
                }
            }

            if (cluster.Count >= MinStrikesForCluster)
                clusters.Add(cluster);
        }

        return clusters;
    }

    private void AddCloudLayers(StormCell cell)
    {
        cell.CloudLayers.Add(new CloudLayer
        {
            Type = CloudType.CB,
            Density = 0.95,
            Scattering = 0.9,
            BaseFeetAgl = 10000,
            TopFeetAgl = 40000,
            IsConvective = true,
            ThunderstormIntensity = cell.Intensity
        });

        cell.CloudLayers.Add(new CloudLayer
        {
            Type = CloudType.OVC,
            Density = 0.3,
            Scattering = 0.1,
            BaseFeetAgl = 40000,
            TopFeetAgl = 60000
        });

        cell.CloudLayers.Add(new CloudLayer
        {
            Type = CloudType.CB,
            Density = 0.8,
            Scattering = 0.7,
            BaseFeetAgl = 5000,
            TopFeetAgl = 15000,
            IsConvective = true,
            ThunderstormIntensity = cell.Intensity
        });
    }

    private (double direction, double speed)? ParseMotionFromRawText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        var upper = rawText.ToUpperInvariant();

        var dirMatch = Regex.Match(upper, @"MOV\s+(?:FROM\s+)?(\d{2,3})");
        if (!dirMatch.Success)
            return null;

        var dir = double.Parse(dirMatch.Groups[1].Value);

        var speedMatch = Regex.Match(upper, @"(\d{1,3})\s*KT");
        if (!speedMatch.Success)
            return null;

        var speed = double.Parse(speedMatch.Groups[1].Value);

        return (dir, speed);
    }

    private double CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusNm * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
}
