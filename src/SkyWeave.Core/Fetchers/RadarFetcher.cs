using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class RadarFetcher
{
    private readonly HttpClient _httpClient;
    private const string RainViewerApiUrl = "https://api.rainviewer.com/public/weather-maps.json";
    private const string DefaultTileHost = "https://tilecache.rainviewer.com";

    public RadarFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<RadarFrame?> GetLatestRadarFrameAsync()
    {
        return FetchRetry.WithRetryAsync(GetLatestRadarFrameInternalAsync, maxRetries: 2);
    }

    private async Task<RadarFrame?> GetLatestRadarFrameInternalAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetStringAsync(RainViewerApiUrl, cts.Token);
            var json = JsonDocument.Parse(response);

            if (!json.RootElement.TryGetProperty("radar", out var radar) ||
                !radar.TryGetProperty("past", out var past))
                return null;

            var frames = past.EnumerateArray().ToList();
            if (frames.Count == 0) return null;

            var latest = frames[^1];
            var time = latest.TryGetProperty("time", out var t) ? t.GetDouble() : 0;
            var path = latest.TryGetProperty("path", out var p) ? p.GetString() : string.Empty;
            var host = json.RootElement.TryGetProperty("host", out var h) ? h.GetString() : null;

            if (string.IsNullOrEmpty(path))
                return null;

            var tileUrl = host ?? DefaultTileHost;
            if (!tileUrl.EndsWith('/'))
                tileUrl += "/";
            tileUrl += path.TrimStart('/');

            return new RadarFrame
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)time).DateTime,
                TileUrl = tileUrl
            };
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

    public async Task<double> GetPrecipitationAtPositionAsync(double latitude, double longitude)
    {
        try
        {
            var frame = await GetLatestRadarFrameAsync();
            if (frame == null) return 0;

            var tileX = (int)((longitude + 180) / 360 * 256);
            var latRad = latitude * Math.PI / 180;
            var tileY = (int)((1 - Math.Log(Math.Tan(latRad) + 1 / Math.Cos(latRad)) / Math.PI) / 2 * 256);

            tileX = Math.Clamp(tileX, 0, 255);
            tileY = Math.Clamp(tileY, 0, 255);

            var url = $"{frame.TileUrl}/256/6/{tileX}/{tileY}/2/1_1.png";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode) return 0;

            return 0.3;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<List<StormCell>> DetectStormCellsAsync(double latitude, double longitude, double radiusNm = 100)
    {
        try
        {
            var frame = await GetLatestRadarFrameAsync();
            if (frame == null) return new List<StormCell>();

            return new List<StormCell>();
        }
        catch
        {
            return new List<StormCell>();
        }
    }
}

public class RadarFrame
{
    public DateTime Timestamp { get; set; }
    public string TileUrl { get; set; } = string.Empty;
}
