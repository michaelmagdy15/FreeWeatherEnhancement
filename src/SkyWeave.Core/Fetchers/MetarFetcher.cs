using System.Net.Http.Json;
using System.Text.Json;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class MetarFetcher
{
    private readonly HttpClient _httpClient;
    private readonly MetarDecoder _decoder;
    private const string BaseUrl = "https://aviationweather.gov/api/data";

    public MetarFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _decoder = new MetarDecoder();
    }

    public MetarFetcher(HttpClient httpClient, MetarDecoder decoder)
    {
        _httpClient = httpClient;
        _decoder = decoder;
    }

    public Task<MetarData?> FetchMetarAsync(string icaoId)
    {
        return FetchRetry.WithRetryAsync(() => FetchInternalAsync(icaoId), maxRetries: 3);
    }

    public Task<MetarData?> FetchMetarByPositionAsync(double latitude, double longitude, double radiusNm = 50)
    {
        return FetchRetry.WithRetryAsync(() => FetchByPositionInternalAsync(latitude, longitude, radiusNm), maxRetries: 3);
    }

    private async Task<MetarData?> FetchInternalAsync(string icaoId)
    {
        try
        {
            var url = $"{BaseUrl}/metar?ids={icaoId}&format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetFromJsonAsync<JsonElement[]>(url, cts.Token);
            
            if (response == null || response.Length == 0)
                return null;

            return _decoder.Decode(response[0]);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<MetarData?> FetchByPositionInternalAsync(double latitude, double longitude, double radiusNm = 50)
    {
        try
        {
            var bbox = CalculateBoundingBox(latitude, longitude, radiusNm);
            var url = $"{BaseUrl}/metar?bbox={bbox.west},{bbox.south},{bbox.east},{bbox.north}&format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetFromJsonAsync<JsonElement[]>(url, cts.Token);
            
            if (response == null || response.Length == 0)
                return null;

            return _decoder.Decode(response[0]);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private (double north, double south, double east, double west) CalculateBoundingBox(double lat, double lon, double radiusNm)
    {
        var radiusKm = radiusNm * 1.852;
        var latDelta = radiusKm / 111.0;
        var lonDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

        return (
            north: lat + latDelta,
            south: lat - latDelta,
            east: lon + lonDelta,
            west: lon - lonDelta
        );
    }
}
