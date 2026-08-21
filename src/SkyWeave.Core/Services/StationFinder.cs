using System.Reflection;
using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class StationFinder
{
    private static readonly List<AirportData> _allAirports;

    static StationFinder()
    {
        _allAirports = LoadEmbeddedAirports();
    }

    public IReadOnlyList<AirportData> AllAirports => _allAirports;

    public string FindNearestStation(double latitude, double longitude)
    {
        return FindNearestAirport(latitude, longitude)?.IcaoId
            ?? "KJFK";
    }

    public AirportData? FindNearestAirport(double latitude, double longitude)
    {
        AirportData? best = null;
        var bestDist = double.MaxValue;

        foreach (var airport in _allAirports)
        {
            var dist = CalculateDistance(latitude, longitude, airport.Latitude, airport.Longitude);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = airport;
            }
        }

        return best;
    }

    public List<AirportData> FindNearbyAirports(double latitude, double longitude, int maxResults = 12, double maxDistanceNm = 500)
    {
        return _allAirports
            .Select(a => new { Airport = a, Dist = CalculateDistance(latitude, longitude, a.Latitude, a.Longitude) })
            .Where(x => x.Dist <= maxDistanceNm)
            .OrderBy(x => x.Dist)
            .Take(maxResults)
            .Select(x => x.Airport)
            .ToList();
    }

    private static List<AirportData> LoadEmbeddedAirports()
    {
        var airports = new List<AirportData>();

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("airports.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                return GetFallbackAirports();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return GetFallbackAirports();

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                airports.Add(new AirportData
                {
                    IcaoId = item.GetProperty("i").GetString() ?? "",
                    Latitude = item.GetProperty("a").GetDouble(),
                    Longitude = item.GetProperty("o").GetDouble(),
                    ElevationFeet = (int)item.GetProperty("e").GetDouble(),
                    Name = "",
                    IataId = "",
                    Country = ""
                });
            }
        }
        catch
        {
            return GetFallbackAirports();
        }

        return airports.Count > 0 ? airports : GetFallbackAirports();
    }

    private static List<AirportData> GetFallbackAirports()
    {
        return new List<AirportData>
        {
            new() { IcaoId = "KJFK", Latitude = 40.6399, Longitude = -73.7787 },
            new() { IcaoId = "KLAX", Latitude = 33.9425, Longitude = -118.4081 },
            new() { IcaoId = "EGLL", Latitude = 51.4700, Longitude = -0.4543 },
            new() { IcaoId = "OMDB", Latitude = 25.2528, Longitude = 55.3644 },
        };
    }

    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 3440.065;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
