using System.Net.Http.Json;
using System.Text.Json;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.Core.Fetchers;

public class TafFetcher
{
    private readonly HttpClient _httpClient;
    private readonly TafDecoder _decoder;
    private readonly StationFinder _stationFinder;
    private const string BaseUrl = "https://aviationweather.gov/api/data";

    public TafFetcher(HttpClient httpClient)
        : this(httpClient, new StationFinder())
    {
    }

    public TafFetcher(HttpClient httpClient, TafDecoder decoder)
        : this(httpClient, new StationFinder(), decoder)
    {
    }

    public TafFetcher(HttpClient httpClient, StationFinder stationFinder)
        : this(httpClient, stationFinder, new TafDecoder())
    {
    }

    public TafFetcher(HttpClient httpClient, StationFinder stationFinder, TafDecoder decoder)
    {
        _httpClient = httpClient;
        _stationFinder = stationFinder;
        _decoder = decoder;
    }

    public Task<TafData?> FetchTafAsync(string icaoId)
    {
        return FetchRetry.WithRetryAsync(() => FetchInternalAsync(icaoId), maxRetries: 3);
    }

    public Task<TafData?> FetchTafByPositionAsync(double latitude, double longitude)
    {
        return FetchRetry.WithRetryAsync(() => FetchByPositionInternalAsync(latitude, longitude), maxRetries: 3);
    }

    private async Task<TafData?> FetchInternalAsync(string icaoId)
    {
        var primary = await FetchAwcJsonAsync(icaoId);
        if (primary != null) return primary;

        return await FetchTgftpAsync(icaoId);
    }

    private async Task<TafData?> FetchAwcJsonAsync(string icaoId)
    {
        try
        {
            var url = $"{BaseUrl}/taf?ids={icaoId}&format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetFromJsonAsync<JsonElement[]>(url, cts.Token);

            if (response == null || response.Length == 0)
                return null;

            return DecodeJsonElement(response[0]);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<TafData?> FetchTgftpAsync(string icaoId)
    {
        try
        {
            var url = $"https://tgftp.nws.noaa.gov/data/forecasts/taf/stations/{icaoId}.TXT";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var body = await _httpClient.GetStringAsync(url, cts.Token);

            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 2 || !body.Contains(icaoId, StringComparison.OrdinalIgnoreCase))
                return null;

            var raw = string.Join(" ", lines.Skip(1));
            var taf = _decoder.Decode(raw);
            return taf?.StationId == icaoId ? taf : null;
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

    private TafData? DecodeJsonElement(JsonElement element)
    {
        var rawTaf = element.TryGetProperty("rawTAF", out var rawProp)
            ? rawProp.GetString() ?? string.Empty
            : element.TryGetProperty("rawOb", out var rawProp2)
                ? rawProp2.GetString() ?? string.Empty
                : string.Empty;

        if (string.IsNullOrEmpty(rawTaf))
            return null;

        return _decoder.Decode(rawTaf);
    }

    private async Task<TafData?> FetchByPositionInternalAsync(double latitude, double longitude)
    {
        var nearest = _stationFinder.FindNearestStation(latitude, longitude);
        return await FetchInternalAsync(nearest);
    }
}
