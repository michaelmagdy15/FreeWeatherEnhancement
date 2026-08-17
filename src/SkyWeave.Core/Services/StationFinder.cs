using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class StationFinder
{
    private static readonly List<AirportData> MajorAirports = new()
    {
        new() { IcaoId = "KJFK", IataId = "JFK", Name = "John F. Kennedy Intl", Latitude = 40.6399, Longitude = -73.7787, ElevationFeet = 13, Country = "US" },
        new() { IcaoId = "KLAX", IataId = "LAX", Name = "Los Angeles Intl", Latitude = 33.9425, Longitude = -118.4081, ElevationFeet = 126, Country = "US" },
        new() { IcaoId = "KORD", IataId = "ORD", Name = "Chicago O'Hare Intl", Latitude = 41.9742, Longitude = -87.9073, ElevationFeet = 672, Country = "US" },
        new() { IcaoId = "KATL", IataId = "ATL", Name = "Hartsfield-Jackson Atlanta Intl", Latitude = 33.6367, Longitude = -84.4281, ElevationFeet = 1026, Country = "US" },
        new() { IcaoId = "KDFW", IataId = "DFW", Name = "Dallas/Fort Worth Intl", Latitude = 32.8969, Longitude = -97.0381, ElevationFeet = 607, Country = "US" },
        new() { IcaoId = "KDEN", IataId = "DEN", Name = "Denver Intl", Latitude = 39.8561, Longitude = -104.6737, ElevationFeet = 5431, Country = "US" },
        new() { IcaoId = "KSFO", IataId = "SFO", Name = "San Francisco Intl", Latitude = 37.6189, Longitude = -122.3750, ElevationFeet = 13, Country = "US" },
        new() { IcaoId = "KMIA", IataId = "MIA", Name = "Miami Intl", Latitude = 25.7959, Longitude = -80.2870, ElevationFeet = 8, Country = "US" },
        new() { IcaoId = "KBOS", IataId = "BOS", Name = "Boston Logan Intl", Latitude = 42.3643, Longitude = -71.0052, ElevationFeet = 20, Country = "US" },
        new() { IcaoId = "KSEA", IataId = "SEA", Name = "Seattle-Tacoma Intl", Latitude = 47.4502, Longitude = -122.3088, ElevationFeet = 433, Country = "US" },
        new() { IcaoId = "EGLL", IataId = "LHR", Name = "London Heathrow", Latitude = 51.4700, Longitude = -0.4543, ElevationFeet = 83, Country = "GB" },
        new() { IcaoId = "LFPG", IataId = "CDG", Name = "Paris Charles de Gaulle", Latitude = 49.0097, Longitude = 2.5479, ElevationFeet = 392, Country = "FR" },
        new() { IcaoId = "EDDF", IataId = "FRA", Name = "Frankfurt am Main", Latitude = 50.0264, Longitude = 8.5431, ElevationFeet = 364, Country = "DE" },
        new() { IcaoId = "RJTT", IataId = "HND", Name = "Tokyo Haneda", Latitude = 35.5494, Longitude = 139.7798, ElevationFeet = 20, Country = "JP" },
        new() { IcaoId = "VHHH", IataId = "HKG", Name = "Hong Kong Intl", Latitude = 22.3080, Longitude = 113.9185, ElevationFeet = 28, Country = "HK" },
        new() { IcaoId = "WSSS", IataId = "SIN", Name = "Singapore Changi", Latitude = 1.3502, Longitude = 103.9944, ElevationFeet = 22, Country = "SG" },
        new() { IcaoId = "OMDB", IataId = "DXB", Name = "Dubai Intl", Latitude = 25.2528, Longitude = 55.3644, ElevationFeet = 62, Country = "AE" },
        new() { IcaoId = "UUEE", IataId = "SVO", Name = "Sheremetyevo Intl", Latitude = 55.9726, Longitude = 37.4146, ElevationFeet = 630, Country = "RU" },
        new() { IcaoId = "SBGR", IataId = "GRU", Name = "São Paulo–Guarulhos Intl", Latitude = -23.4356, Longitude = -46.4731, ElevationFeet = 2459, Country = "BR" },
        new() { IcaoId = "YSSY", IataId = "SYD", Name = "Sydney Kingsford Smith", Latitude = -33.9461, Longitude = 151.1772, ElevationFeet = 21, Country = "AU" },
    };

    public string FindNearestStation(double latitude, double longitude)
    {
        return MajorAirports
            .OrderBy(a => CalculateDistance(latitude, longitude, a.Latitude, a.Longitude))
            .First()
            .IcaoId;
    }

    public AirportData? FindNearestAirport(double latitude, double longitude)
    {
        return MajorAirports
            .OrderBy(a => CalculateDistance(latitude, longitude, a.Latitude, a.Longitude))
            .FirstOrDefault();
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
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

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
