using System.Net;
using System.Text;
using System.Text.Json;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class FetcherFallbackTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<string> RequestedUrls { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        public void EnqueueJson(string json) => Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        public void EnqueueText(string text) => Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(text, Encoding.UTF8, "text/plain")
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.ToString());
            var response = _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private const string KjfkMetarJson =
        """[{"icaoId":"KJFK","rawOb":"KJFK 172151Z 04009KT 10SM FEW020 FEW160 BKN250 27/21 A2981 RMK AO2 SLP093 T02720211 $","temp":27.2,"dewp":21.1,"wdir":40,"wspd":9,"visib":10.0,"altim":29.81,"fltCat":"VFR","obsTime":1787003460,"clouds":[{"cover":"FEW","base":2000,"type":""},{"cover":"BKN","base":25000,"type":""}]}]""";

    private const string KjfkMetarTgftp =
        "2026/08/17 21:51\nKJFK 172151Z 04009KT 10SM FEW020 FEW160 BKN250 27/21 A2981 RMK AO2 SLP093 T02720211 $";

    private const string KjfkTafTgftp =
        "2026/08/17 20:36\nTAF \n      AMD KJFK 171933Z 1720/1824 01008KT P6SM BKN025 \n     FM172200 02005KT P6SM VCSH SCT020 BKN035 \n     FM180400 04005KT P6SM SCT020 BKN025";

    [Fact]
    public async Task Metar_PrimaryAwcSucceeds_NoFallback()
    {
        var handler = new StubHandler();
        handler.EnqueueJson(KjfkMetarJson);
        using var client = new HttpClient(handler);
        var fetcher = new MetarFetcher(client, new StationFinder());

        var metar = await fetcher.FetchMetarByPositionAsync(40.6399, -73.7787);

        Assert.NotNull(metar);
        Assert.Equal("KJFK", metar!.StationId);
        Assert.Equal(27.2, metar.TemperatureCelsius);
        Assert.Single(handler.RequestedUrls);
        Assert.Contains("ids=KJFK", handler.RequestedUrls[0]);
    }

    [Fact]
    public async Task Metar_AwcDead_FallsBackToTgftp()
    {
        var handler = new StubHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.EnqueueText(KjfkMetarTgftp);
        using var client = new HttpClient(handler);
        var fetcher = new MetarFetcher(client, new StationFinder());

        var metar = await fetcher.FetchMetarByPositionAsync(40.6399, -73.7787);

        Assert.NotNull(metar);
        Assert.Equal("KJFK", metar!.StationId);
        Assert.Equal(27, metar.TemperatureCelsius);
        Assert.Equal(2, handler.RequestedUrls.Count);
        Assert.Contains("tgftp.nws.noaa.gov", handler.RequestedUrls[1]);
    }

    [Fact]
    public async Task Metar_AwcAndTgftpDead_FallsBackToVatsim()
    {
        var handler = new StubHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        handler.EnqueueText("KJFK 172151Z 04009KT 10SM FEW020 FEW160 BKN250 27/21 A2981 RMK AO2 SLP093 T02720211 $");
        using var client = new HttpClient(handler);
        var fetcher = new MetarFetcher(client, new StationFinder());

        var metar = await fetcher.FetchMetarByPositionAsync(40.6399, -73.7787);

        Assert.NotNull(metar);
        Assert.Equal("KJFK", metar!.StationId);
        Assert.Equal(3, handler.RequestedUrls.Count);
        Assert.Contains("metar.vatsim.net", handler.RequestedUrls[2]);
    }

    [Fact]
    public async Task Metar_AllSourcesDead_ReturnsNull()
    {
        var handler = new StubHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));
        handler.EnqueueText("No METAR available for KJFK");
        using var client = new HttpClient(handler);
        var fetcher = new MetarFetcher(client, new StationFinder());

        var metar = await fetcher.FetchMetarByPositionAsync(40.6399, -73.7787);

        Assert.Null(metar);
    }

    [Fact]
    public async Task Taf_AwcDead_FallsBackToTgftp()
    {
        var handler = new StubHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.EnqueueText(KjfkTafTgftp);
        using var client = new HttpClient(handler);
        var fetcher = new TafFetcher(client, new StationFinder());

        var taf = await fetcher.FetchTafByPositionAsync(40.6399, -73.7787);

        Assert.NotNull(taf);
        Assert.Equal("KJFK", taf!.StationId);
        Assert.Equal(0, taf.ValidTo.Hour);
        Assert.Equal(0, taf.ValidTo.Minute);
        Assert.True(taf.ValidTo > taf.ValidFrom);
        Assert.Equal(8, taf.WindSpeedKnots);
    }

    [Fact]
    public async Task Taf_PrimaryAwcSucceeds_NoFallback()
    {
        var handler = new StubHandler();
        handler.EnqueueJson(
            """[{"icaoId":"KJFK","rawTAF":"TAF KJFK 171730Z 1718/1824 04009KT P6SM SCT020 BKN250"}]""");
        using var client = new HttpClient(handler);
        var fetcher = new TafFetcher(client, new StationFinder());

        var taf = await fetcher.FetchTafByPositionAsync(40.6399, -73.7787);

        Assert.NotNull(taf);
        Assert.Equal("KJFK", taf!.StationId);
        Assert.Single(handler.RequestedUrls);
        Assert.Contains("ids=KJFK", handler.RequestedUrls[0]);
    }

    [Fact]
    public async Task Taf_NearestStationIsChosenByPosition()
    {
        var handler = new StubHandler();
        handler.EnqueueJson(
            """[{"icaoId":"KLAX","rawTAF":"TAF KLAX 171730Z 1718/1824 25010KT P6SM SCT020"}]""");
        using var client = new HttpClient(handler);
        var fetcher = new TafFetcher(client, new StationFinder());

        var taf = await fetcher.FetchTafByPositionAsync(33.9425, -118.4081);

        Assert.NotNull(taf);
        Assert.Equal("KLAX", taf!.StationId);
        Assert.Contains("ids=KLAX", handler.RequestedUrls[0]);
    }

    [Fact]
    public async Task Metar_PreferIvao_UsesIvaoEndpointFirst()
    {
        var handler = new StubHandler();
        handler.EnqueueJson("""{"metar":"LFPG 151200Z 24012KT 9999 SCT030 18/11 Q1018"}""");
        using var client = new HttpClient(handler);
        var fetcher = new MetarFetcher(client, new StationFinder())
        {
            PreferIvao = true
        };

        var metar = await fetcher.FetchMetarAsync("LFPG");

        Assert.NotNull(metar);
        Assert.Equal("LFPG", metar!.StationId);
        Assert.Equal(18.0, metar.TemperatureCelsius);
        Assert.Equal(11.0, metar.DewpointCelsius);
        Assert.Equal(1018.0, metar.AltimeterHpa);
        Assert.Single(handler.RequestedUrls);
        Assert.Contains("api.ivao.aero", handler.RequestedUrls[0]);
    }
}