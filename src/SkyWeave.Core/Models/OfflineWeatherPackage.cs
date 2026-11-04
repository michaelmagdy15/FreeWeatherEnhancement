using System;
using System.Collections.Generic;
using System.Linq;
using SkyWeave.Core.Services;

namespace SkyWeave.Core.Models;

public class OfflineWeatherPackage
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string IcaoPrefix { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public double MinLatitude { get; set; }
    public double MaxLatitude { get; set; }
    public double MinLongitude { get; set; }
    public double MaxLongitude { get; set; }
    public Dictionary<string, MetarData> Stations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Decoders.TafData> Tafs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<WindsAloftData> WindsGrid { get; set; } = new();
    public List<WeatherHazard> Hazards { get; set; } = new();

    public int StationCount => Stations.Count;
    public int WindsGridCount => WindsGrid.Count;

    public OfflinePackageSummary ToSummary(long fileSizeBytes = 0, bool isActive = false)
    {
        return new OfflinePackageSummary
        {
            Id = Id,
            Name = Name,
            Description = Description,
            RegionCode = RegionCode,
            IcaoPrefix = IcaoPrefix,
            CreatedUtc = CreatedUtc,
            StationCount = Stations.Count,
            WindsGridCount = WindsGrid.Count,
            MinLatitude = MinLatitude,
            MaxLatitude = MaxLatitude,
            MinLongitude = MinLongitude,
            MaxLongitude = MaxLongitude,
            FileSizeBytes = fileSizeBytes,
            IsActive = isActive
        };
    }

    public MetarData? FindNearestMetar(double latitude, double longitude, StationFinder stationFinder)
    {
        if (Stations.Count == 0) return null;

        // 1. Direct match with nearest station in finder
        var nearestAirport = stationFinder.FindNearestAirport(latitude, longitude);
        if (nearestAirport != null && Stations.TryGetValue(nearestAirport.IcaoId, out var directMetar))
        {
            return directMetar;
        }

        // 2. Find closest station among all package stations
        MetarData? best = null;
        double bestDist = double.MaxValue;

        foreach (var (icao, metar) in Stations)
        {
            var ap = stationFinder.FindStation(icao);
            double dist;
            if (ap != null)
            {
                dist = StationFinder.CalculateDistance(latitude, longitude, ap.Latitude, ap.Longitude);
            }
            else
            {
                dist = Math.Sqrt(Math.Pow(latitude - 0, 2) + Math.Pow(longitude - 0, 2));
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                best = metar;
            }
        }

        return best ?? Stations.Values.FirstOrDefault();
    }

    public WindsAloftData? FindNearestWinds(double latitude, double longitude)
    {
        if (WindsGrid.Count == 0) return null;

        WindsAloftData? best = null;
        double bestDistSq = double.MaxValue;

        foreach (var w in WindsGrid)
        {
            var dLat = latitude - w.Latitude;
            var dLon = (longitude - w.Longitude) * Math.Cos(latitude * Math.PI / 180.0);
            var distSq = dLat * dLat + dLon * dLon;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = w;
            }
        }

        return best ?? WindsGrid[0];
    }
}

public class OfflinePackageSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string IcaoPrefix { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public int StationCount { get; set; }
    public int WindsGridCount { get; set; }
    public double MinLatitude { get; set; }
    public double MaxLatitude { get; set; }
    public double MinLongitude { get; set; }
    public double MaxLongitude { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsActive { get; set; }
}

public class OfflinePackageCreateRequest
{
    public string? Preset { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? RegionCode { get; set; }
    public string? IcaoPrefix { get; set; }
    public List<string>? IcaoList { get; set; }
    public double? MinLatitude { get; set; }
    public double? MaxLatitude { get; set; }
    public double? MinLongitude { get; set; }
    public double? MaxLongitude { get; set; }
    public bool ActivateImmediately { get; set; } = true;
}
