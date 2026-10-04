using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class Era5HistoricalTests
{
    private const string SampleEra5Json = """
    {
      "latitude": 40.71,
      "longitude": -74.01,
      "hourly": {
        "time": ["2026-09-15T12:00"],
        "temperature_2m": [18.5],
        "dew_point_2m": [12.0],
        "surface_pressure": [1015.2],
        "wind_speed_10m": [12.4],
        "wind_direction_10m": [280.0],
        "wind_gusts_10m": [22.0],
        "precipitation": [0.5],
        "cloud_cover_low": [45.0],
        "cloud_cover_mid": [10.0],
        "cloud_cover_high": [75.0],
        "cloud_cover": [80.0],
        "geopotential_height_1000hPa": [120.0],
        "geopotential_height_850hPa": [1460.0],
        "geopotential_height_700hPa": [3010.0],
        "geopotential_height_500hPa": [5570.0],
        "geopotential_height_300hPa": [9160.0],
        "geopotential_height_250hPa": [10360.0],
        "geopotential_height_200hPa": [11780.0],
        "temperature_1000hPa": [17.0],
        "temperature_850hPa": [10.5],
        "temperature_700hPa": [2.0],
        "temperature_500hPa": [-15.0],
        "temperature_300hPa": [-42.0],
        "temperature_250hPa": [-52.0],
        "temperature_200hPa": [-58.0],
        "wind_speed_1000hPa": [12.0],
        "wind_speed_850hPa": [20.0],
        "wind_speed_700hPa": [32.0],
        "wind_speed_500hPa": [55.0],
        "wind_speed_300hPa": [85.0],
        "wind_speed_250hPa": [95.0],
        "wind_speed_200hPa": [70.0],
        "wind_direction_1000hPa": [280.0],
        "wind_direction_850hPa": [290.0],
        "wind_direction_700hPa": [305.0],
        "wind_direction_500hPa": [315.0],
        "wind_direction_300hPa": [320.0],
        "wind_direction_250hPa": [325.0],
        "wind_direction_200hPa": [330.0]
      }
    }
    """;

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _statusCode;

        public int CallCount { get; private set; }

        public MockHttpMessageHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _response = response;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void BuildHistoricalArchiveUrl_FormatsIsoDatesAndVariablesCorrectly()
    {
        var date = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var url = Era5HistoricalFetcher.BuildHistoricalArchiveUrl(40.7128, -74.0060, date);

        Assert.Contains("latitude=40.7128", url);
        Assert.Contains("longitude=-74.006", url);
        Assert.Contains("start_date=2026-09-15", url);
        Assert.Contains("end_date=2026-09-15", url);
        Assert.Contains("temperature_2m", url);
        Assert.Contains("wind_speed_10m", url);
        Assert.Contains("temperature_500hPa", url);
        Assert.Contains("geopotential_height_500hPa", url);
    }

    [Fact]
    public async Task FetchHistoricalWeatherAsync_ParsesPayloadAndSetsHistoricalFlags()
    {
        var handler = new MockHttpMessageHandler(SampleEra5Json);
        using var client = new HttpClient(handler);
        var cache = new WeatherCache();
        var fetcher = new Era5HistoricalFetcher(client, cache);

        var targetUtc = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var state = await fetcher.FetchHistoricalWeatherAsync(40.71, -74.01, targetUtc, "KJFK", 4.0);

        Assert.NotNull(state);
        Assert.True(state.IsHistorical);
        Assert.Equal(targetUtc, state.HistoricalUtc);
        Assert.Equal("KJFK", state.StationId);
        Assert.Equal(18.5, state.TemperatureCelsius);
        Assert.Equal(12.0, state.DewpointCelsius);
        Assert.Equal(1015.2, state.AltimeterHpa);
        Assert.Equal(280.0, state.WindDirectionDegrees);
        Assert.Equal(12.4, state.WindSpeedKnots);
        Assert.Equal(22.0, state.WindGustKnots);
        Assert.Contains("ERA5", state.SourceModelName);

        // Verify clouds synthesized
        Assert.NotEmpty(state.CloudLayers);
        Assert.True(state.CloudLayers.Exists(c => c.Type == CloudType.SCT || c.Type == CloudType.FEW));

        // Verify winds aloft
        Assert.NotEmpty(state.WindsAloft);
        Assert.Contains(state.WindsAloft, w => w.AltitudeFeet > 15000 && w.SpeedKnots > 40);

        // Verify synthetic METAR
        Assert.NotNull(state.RawMetar);
        Assert.StartsWith("METAR KJFK", state.RawMetar);
        Assert.Contains("28012KT", state.RawMetar);
        Assert.Contains("G22KT", state.RawMetar);
    }

    [Fact]
    public async Task FetchHistoricalWeatherAsync_CachesResult_SecondCallDoesNotHitNetwork()
    {
        var handler = new MockHttpMessageHandler(SampleEra5Json);
        using var client = new HttpClient(handler);
        var cache = new WeatherCache();
        var fetcher = new Era5HistoricalFetcher(client, cache);

        var targetUtc = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var firstState = await fetcher.FetchHistoricalWeatherAsync(40.71, -74.01, targetUtc, "KJFK");
        var secondState = await fetcher.FetchHistoricalWeatherAsync(40.71, -74.01, targetUtc, "KJFK");

        Assert.NotNull(firstState);
        Assert.NotNull(secondState);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task FetchHistoricalWeatherAsync_WithMissingOrNullArrayElements_DoesNotCrash()
    {
        const string incompleteJson = """
        {
          "latitude": 40.71,
          "longitude": -74.01,
          "hourly": {
            "time": ["2026-09-15T12:00"],
            "temperature_2m": [null],
            "surface_pressure": [1013.25],
            "wind_speed_10m": [5.0]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(incompleteJson);
        using var client = new HttpClient(handler);
        var cache = new WeatherCache();
        var fetcher = new Era5HistoricalFetcher(client, cache);

        var targetUtc = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var state = await fetcher.FetchHistoricalWeatherAsync(40.71, -74.01, targetUtc, "TEST");

        Assert.NotNull(state);
        Assert.Equal(15.0, state.TemperatureCelsius); // Falls back gracefully
        Assert.Equal(1013.25, state.AltimeterHpa);
        Assert.True(state.IsHistorical);
    }
}
