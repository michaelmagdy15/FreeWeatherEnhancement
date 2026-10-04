using System.Collections.Generic;
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

public class OnlineTrafficTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;

        public MockHttpMessageHandler(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task OnlineTrafficFetcher_ParsesVatsimPayload_Correctly()
    {
        var vatsimJson = """
        {
          "pilots": [
            {
              "callsign": "MSR123",
              "latitude": 30.15,
              "longitude": 31.45,
              "altitude": 24000,
              "groundspeed": 430,
              "heading": 270,
              "flight_plan": {
                "aircraft_short": "B738",
                "departure": "HECA",
                "arrival": "LTFM"
              }
            },
            {
              "callsign": "BAW456",
              "latitude": 51.47,
              "longitude": -0.45,
              "altitude": 36000,
              "groundspeed": 480,
              "heading": 120,
              "flight_plan": {
                "aircraft_short": "A359",
                "departure": "EGLL",
                "arrival": "OMDB"
              }
            }
          ]
        }
        """;

        var client = new HttpClient(new MockHttpMessageHandler(vatsimJson));
        using var fetcher = new OnlineTrafficFetcher(client);

        // Center near Cairo (HECA: 30.11, 31.41)
        var traffic = await fetcher.GetNearbyTrafficAsync(30.11, 31.41, maxDistanceNm: 100.0, includeVatsim: true, includeIvao: false);

        Assert.Single(traffic);
        var flight = traffic[0];
        Assert.Equal("MSR123", flight.Callsign);
        Assert.Equal(OnlineNetwork.Vatsim, flight.Network);
        Assert.Equal(24000, flight.AltitudeFeet);
        Assert.Equal(430, flight.GroundSpeedKnots);
        Assert.Equal(270, flight.HeadingDegrees);
        Assert.Equal("HECA", flight.Departure);
        Assert.Equal("LTFM", flight.Arrival);
        Assert.Equal("B738", flight.AircraftType);
        Assert.True(flight.DistanceNm < 100.0);
    }

    [Fact]
    public async Task OnlineTrafficFetcher_ParsesIvaoWhazzupPayload_Correctly()
    {
        var ivaoJson = """
        {
          "clients": {
            "pilots": [
              {
                "callsign": "AFR789",
                "lastTrack": {
                  "latitude": 30.20,
                  "longitude": 31.50,
                  "altitude": 12000,
                  "groundSpeed": 250,
                  "heading": 180,
                  "onGround": false
                },
                "flightPlan": {
                  "aircraftId": "A320",
                  "departureId": "LFPG",
                  "arrivalId": "HECA"
                }
              }
            ]
          }
        }
        """;

        var client = new HttpClient(new MockHttpMessageHandler(ivaoJson));
        using var fetcher = new OnlineTrafficFetcher(client);

        var traffic = await fetcher.GetNearbyTrafficAsync(30.11, 31.41, maxDistanceNm: 100.0, includeVatsim: false, includeIvao: true);

        Assert.Single(traffic);
        var flight = traffic[0];
        Assert.Equal("AFR789", flight.Callsign);
        Assert.Equal(OnlineNetwork.Ivao, flight.Network);
        Assert.Equal(12000, flight.AltitudeFeet);
        Assert.Equal(250, flight.GroundSpeedKnots);
        Assert.Equal(180, flight.HeadingDegrees);
        Assert.Equal("LFPG", flight.Departure);
        Assert.Equal("HECA", flight.Arrival);
    }

    [Fact]
    public void RadarTileCalculator_PositionToCanvas_CentersAircraftAccurately()
    {
        double centerLat = 30.111;
        double centerLon = 31.414;

        // Position of the center itself must map to exactly (384, 384) on a 768x768 canvas
        var (x, y) = RadarTileCalculator.PositionToCanvas(centerLat, centerLon, centerLat, centerLon, zoom: 6, canvasSizePx: 768.0);

        Assert.InRange(x, 383.99, 384.01);
        Assert.InRange(y, 383.99, 384.01);
    }
}
