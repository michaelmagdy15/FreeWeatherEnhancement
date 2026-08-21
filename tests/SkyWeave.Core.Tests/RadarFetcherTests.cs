using System.Net;
using System.Text;
using SkyWeave.Core.Fetchers;
using Xunit;

namespace SkyWeave.Core.Tests;

public class RadarFetcherTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void EnqueueJson(string json) => _responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private const string RainViewerJson =
        """{"version":"2.0","generated":1787139600,"host":"https://tilecache.rainviewer.com","radar":{"past":[{"time":1787131800,"path":"/v2/radar/a1b2c3d4e5f6"},{"time":1787139600,"path":"/v2/radar/b177e8fda05f"}],"nowcast":[]},"satellite":{"infrared":[{"time":1787139600,"path":"/v2/satellite/ir/a1b2c3"}]}}""";

    [Fact]
    public async Task Radar_HostAndPathCombined_AbsoluteTileUrl()
    {
        var handler = new StubHandler();
        handler.EnqueueJson(RainViewerJson);
        using var client = new HttpClient(handler);
        var fetcher = new RadarFetcher(client);

        var frame = await fetcher.GetLatestRadarFrameAsync();

        Assert.NotNull(frame);
        Assert.Equal("https://tilecache.rainviewer.com/v2/radar/b177e8fda05f", frame!.TileUrl);
        Assert.Equal(new DateTime(2026, 8, 19, 11, 40, 0, DateTimeKind.Utc), frame.Timestamp);
    }

    [Fact]
    public async Task Radar_HostMissing_FallsBackToDefaultTileHost()
    {
        var handler = new StubHandler();
        handler.EnqueueJson("""{"radar":{"past":[{"time":1787139600,"path":"/v2/radar/b177e8fda05f"}]}}""");
        using var client = new HttpClient(handler);
        var fetcher = new RadarFetcher(client);

        var frame = await fetcher.GetLatestRadarFrameAsync();

        Assert.NotNull(frame);
        Assert.Equal("https://tilecache.rainviewer.com/v2/radar/b177e8fda05f", frame!.TileUrl);
    }

    [Fact]
    public async Task Radar_NoPastFrames_ReturnsNull()
    {
        var handler = new StubHandler();
        handler.EnqueueJson("""{"radar":{"past":[]}}""");
        using var client = new HttpClient(handler);
        var fetcher = new RadarFetcher(client);

        var frame = await fetcher.GetLatestRadarFrameAsync();

        Assert.Null(frame);
    }

    [Fact]
    public async Task Radar_EmptyPath_ReturnsNull()
    {
        var handler = new StubHandler();
        handler.EnqueueJson("""{"host":"https://tilecache.rainviewer.com","radar":{"past":[{"time":1787139600,"path":""}]}}""");
        using var client = new HttpClient(handler);
        var fetcher = new RadarFetcher(client);

        var frame = await fetcher.GetLatestRadarFrameAsync();

        Assert.Null(frame);
    }

    [Fact]
    public async Task Radar_MalformedJson_ReturnsNull()
    {
        var handler = new StubHandler();
        handler.EnqueueJson("""{not json""");
        using var client = new HttpClient(handler);
        var fetcher = new RadarFetcher(client);

        var frame = await fetcher.GetLatestRadarFrameAsync();

        Assert.Null(frame);
    }
}