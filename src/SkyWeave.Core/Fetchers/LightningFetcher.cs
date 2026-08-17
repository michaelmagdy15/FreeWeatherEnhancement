using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class LightningFetcher
{
    private readonly HttpClient _httpClient;
    private const string BlitzortungApiUrl = "https://map.blitzortung.org/GEOjson/1/10/strange.json";
    private const string FallbackApiUrl = "https://data.blitzortung.org/Data/Protected/last_strikes.php";

    public LightningFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<LightningStrike>?> FetchNearbyStrikesAsync(
        double latitude, double longitude, double radiusNm = 100, int maxStrikes = 50)
    {
        return FetchRetry.WithRetryAsync(() => FetchInternalAsync(latitude, longitude, radiusNm, maxStrikes), maxRetries: 2);
    }

    private async Task<List<LightningStrike>?> FetchInternalAsync(
        double latitude, double longitude, double radiusNm = 100, int maxStrikes = 50)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetStringAsync(BlitzortungApiUrl, cts.Token);
            var json = JsonDocument.Parse(response);

            if (!json.RootElement.TryGetProperty("features", out var features))
                return new List<LightningStrike>();

            var strikes = new List<LightningStrike>();
            var radiusKm = radiusNm * 1.852;

            foreach (var feature in features.EnumerateArray())
            {
                if (strikes.Count >= maxStrikes)
                    break;

                if (!feature.TryGetProperty("geometry", out var geometry) ||
                    !geometry.TryGetProperty("coordinates", out var coords))
                    continue;

                var coordsArray = coords.EnumerateArray().ToList();
                if (coordsArray.Count < 2) continue;

                var strikeLon = coordsArray[0].GetDouble();
                var strikeLat = coordsArray[1].GetDouble();

                var distance = CalculateDistanceKm(latitude, longitude, strikeLat, strikeLon);
                if (distance > radiusKm) continue;

                var strike = new LightningStrike
                {
                    Latitude = strikeLat,
                    Longitude = strikeLon,
                    DistanceNm = distance / 1.852,
                    Timestamp = DateTime.UtcNow,
                    Polarity = 0
                };

                if (feature.TryGetProperty("properties", out var props))
                {
                    if (props.TryGetProperty("time", out var timeProp))
                    {
                        var epoch = timeProp.GetDouble();
                        strike.Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)epoch).DateTime;
                    }
                }

                strikes.Add(strike);
            }

            return strikes.OrderBy(s => s.DistanceNm).ToList();
        }
        catch (OperationCanceledException)
        {
            return new List<LightningStrike>();
        }
        catch (HttpRequestException)
        {
            return new List<LightningStrike>();
        }
        catch (JsonException)
        {
            return new List<LightningStrike>();
        }
    }

    public int GetStrikeCountNearby(List<LightningStrike> strikes, double radiusNm = 30)
    {
        return strikes.Count(s => s.DistanceNm <= radiusNm);
    }

    public double GetClosestStrikeDistance(List<LightningStrike> strikes)
    {
        return strikes.Count > 0 ? strikes.Min(s => s.DistanceNm) : double.MaxValue;
    }

    private double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
}
