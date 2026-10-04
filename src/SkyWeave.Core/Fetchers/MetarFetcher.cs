using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.Core.Fetchers;

public class MetarFetcher
{
    private readonly HttpClient _httpClient;
    private readonly MetarDecoder _decoder;
    private readonly StationFinder _stationFinder;
    private const string BaseUrl = "https://aviationweather.gov/api/data";

    public MetarFetcher(HttpClient httpClient)
        : this(httpClient, new StationFinder())
    {
    }

    public MetarFetcher(HttpClient httpClient, MetarDecoder decoder)
        : this(httpClient, new StationFinder(), decoder)
    {
    }

    public MetarFetcher(HttpClient httpClient, StationFinder stationFinder)
        : this(httpClient, stationFinder, new MetarDecoder())
    {
    }

    public MetarFetcher(HttpClient httpClient, StationFinder stationFinder, MetarDecoder decoder)
    {
        _httpClient = httpClient;
        _stationFinder = stationFinder;
        _decoder = decoder;
    }

    public bool PreferVatsim { get; set; }
    public bool PreferIvao { get; set; }

    public Task<MetarData?> FetchMetarAsync(string icaoId)
    {
        return FetchRetry.WithRetryAsync(() => FetchInternalAsync(icaoId), maxRetries: 3);
    }

    public Task<MetarData?> FetchMetarByPositionAsync(double latitude, double longitude)
    {
        return FetchRetry.WithRetryAsync(() => FetchByPositionInternalAsync(latitude, longitude), maxRetries: 3);
    }

    private async Task<MetarData?> FetchInternalAsync(string icaoId)
    {
        if (PreferVatsim)
        {
            var vatsim = await FetchVatsimAsync(icaoId);
            if (vatsim != null) return vatsim;
        }

        if (PreferIvao)
        {
            var ivao = await FetchIvaoAsync(icaoId);
            if (ivao != null) return ivao;
        }

        var primary = await FetchAwcJsonAsync(icaoId);
        if (primary != null) return primary;

        var tgftp = await FetchTgftpAsync(icaoId);
        if (tgftp != null) return tgftp;

        if (!PreferVatsim)
        {
            var vatsim = await FetchVatsimAsync(icaoId);
            if (vatsim != null) return vatsim;
        }

        if (!PreferIvao)
        {
            return await FetchIvaoAsync(icaoId);
        }

        return null;
    }

    private async Task<MetarData?> FetchAwcJsonAsync(string icaoId)
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
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<MetarData?> FetchTgftpAsync(string icaoId)
    {
        try
        {
            var url = $"https://tgftp.nws.noaa.gov/data/observations/metar/stations/{icaoId}.TXT";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var body = await _httpClient.GetStringAsync(url, cts.Token);

            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 2 || !lines[1].Contains($"{icaoId} ", StringComparison.OrdinalIgnoreCase))
                return null;

            return _decoder.DecodeRaw(string.Join(" ", lines.Skip(1)));
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

    private async Task<MetarData?> FetchVatsimAsync(string icaoId)
    {
        try
        {
            var url = $"https://metar.vatsim.net/metar.php?id={icaoId}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var body = (await _httpClient.GetStringAsync(url, cts.Token)).Trim();

            if (!Regex.IsMatch(body, $@"(?:^|\b)(?:METAR\s+)?{icaoId}\s+\d{{6}}Z", RegexOptions.IgnoreCase))
                return null;

            return _decoder.DecodeRaw(body);
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

    private async Task<MetarData?> FetchIvaoAsync(string icaoId)
    {
        try
        {
            var url = $"https://api.ivao.aero/v2/airports/{icaoId}/metar";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = (await response.Content.ReadAsStringAsync(cts.Token)).Trim();
            if (body.StartsWith("{") && body.EndsWith("}"))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("metar", out var mProp))
                {
                    body = mProp.GetString()?.Trim() ?? string.Empty;
                }
                else if (doc.RootElement.TryGetProperty("raw", out var rProp))
                {
                    body = rProp.GetString()?.Trim() ?? string.Empty;
                }
            }

            if (!Regex.IsMatch(body, $@"(?:^|\b)(?:METAR\s+)?{icaoId}\s+\d{{6}}Z", RegexOptions.IgnoreCase))
                return null;

            return _decoder.DecodeRaw(body);
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

    private async Task<MetarData?> FetchByPositionInternalAsync(double latitude, double longitude)
    {
        var nearest = _stationFinder.FindNearestStation(latitude, longitude);
        return await FetchInternalAsync(nearest);
    }
}
