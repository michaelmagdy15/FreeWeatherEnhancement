using System.Net.Http.Json;
using System.Text.Json;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class TafFetcher
{
    private readonly HttpClient _httpClient;
    private readonly TafDecoder _decoder;
    private const string BaseUrl = "https://aviationweather.gov/api/data";

    public TafFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _decoder = new TafDecoder();
    }

    public TafFetcher(HttpClient httpClient, TafDecoder decoder)
    {
        _httpClient = httpClient;
        _decoder = decoder;
    }

    public Task<TafData?> FetchTafAsync(string icaoId)
    {
        return FetchRetry.WithRetryAsync(() => FetchInternalAsync(icaoId), maxRetries: 3);
    }

    public Task<TafData?> FetchTafByPositionAsync(double latitude, double longitude, double radiusNm = 50)
    {
        return FetchRetry.WithRetryAsync(() => FetchByPositionInternalAsync(latitude, longitude, radiusNm), maxRetries: 3);
    }

    private async Task<TafData?> FetchInternalAsync(string icaoId)
    {
        try
        {
            var url = $"{BaseUrl}/taf?ids={icaoId}&format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetFromJsonAsync<JsonElement[]>(url, cts.Token);
            
            if (response == null || response.Length == 0)
                return null;

            var rawTaf = response[0].TryGetProperty("rawTAF", out var rawProp)
                ? rawProp.GetString() ?? string.Empty
                : response[0].TryGetProperty("rawOb", out var rawProp2)
                    ? rawProp2.GetString() ?? string.Empty
                    : string.Empty;

            if (string.IsNullOrEmpty(rawTaf))
                return null;

            return _decoder.Decode(rawTaf);
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

    private async Task<TafData?> FetchByPositionInternalAsync(double latitude, double longitude, double radiusNm = 50)
    {
        try
        {
            var bbox = CalculateBoundingBox(latitude, longitude, radiusNm);
            var url = $"{BaseUrl}/taf?bbox={bbox.west},{bbox.south},{bbox.east},{bbox.north}&format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetFromJsonAsync<JsonElement[]>(url, cts.Token);
            
            if (response == null || response.Length == 0)
                return null;

            var rawTaf = response[0].TryGetProperty("rawTAF", out var rawProp)
                ? rawProp.GetString() ?? string.Empty
                : response[0].TryGetProperty("rawOb", out var rawProp2)
                    ? rawProp2.GetString() ?? string.Empty
                    : string.Empty;

            if (string.IsNullOrEmpty(rawTaf))
                return null;

            return _decoder.Decode(rawTaf);
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
