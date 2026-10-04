using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class SimBriefTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<string> RequestedUrls { get; } = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        public void EnqueueJson(string json) => Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.ToString());
            var response = _responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private const string RealisticSimBriefJson = """
    {
      "fetch": {
        "status": "Success"
      },
      "params": {
        "request_id": "12345678",
        "user_id": "987654"
      },
      "general": {
        "flight_number": "112",
        "icao_airline": "BAW",
        "route": "KJFK MERIT PUT BOS EGLL",
        "cruise_altitude": "35000",
        "air_time": "21600",
        "total_ete": "21600"
      },
      "origin": {
        "icao_code": "KJFK",
        "iata_code": "JFK",
        "name": "John F Kennedy Intl",
        "pos_lat": "40.639751",
        "pos_long": "-73.778925"
      },
      "destination": {
        "icao_code": "EGLL",
        "iata_code": "LHR",
        "name": "London Heathrow",
        "pos_lat": "51.4706",
        "pos_long": "-0.461941"
      },
      "alternate": {
        "icao_code": "EGCC",
        "iata_code": "MAN",
        "name": "Manchester",
        "pos_lat": "53.3537",
        "pos_long": "-2.27495"
      },
      "aircraft": {
        "icaocode": "B77W",
        "name": "Boeing 777-300ER"
      },
      "times": {
        "est_time_enroute": "21600"
      },
      "navlog": {
        "fix": [
          {
            "ident": "KJFK",
            "name": "KENNEDY",
            "type": "apt",
            "pos_lat": "40.6398",
            "pos_long": "-73.7789",
            "stage": "CLB",
            "altitude_feet": "13",
            "wind_dir": "250",
            "wind_spd": "15",
            "oat": "15"
          },
          {
            "ident": "MERIT",
            "name": "MERIT",
            "type": "fix",
            "pos_lat": "41.3817",
            "pos_long": "-72.8833",
            "stage": "CLB",
            "altitude_feet": "18000",
            "wind_dir": "260",
            "wind_spd": "35",
            "oat": "-20"
          },
          {
            "ident": "PUT",
            "name": "PUTNAM",
            "type": "vor",
            "pos_lat": "41.9550",
            "pos_long": "-71.8447",
            "stage": "CRZ",
            "altitude_feet": "35000",
            "wind_dir": "270",
            "wind_spd": "55",
            "oat": "-50"
          },
          {
            "ident": "BOS",
            "name": "BOSTON",
            "type": "vor",
            "pos_lat": "42.3550",
            "pos_long": "-70.9983",
            "stage": "DSC",
            "altitude_feet": "24000",
            "wind_dir": "270",
            "wind_spd": "40",
            "oat": "-30"
          },
          {
            "ident": "EGLL",
            "name": "HEATHROW",
            "type": "apt",
            "pos_lat": "51.4706",
            "pos_long": "-0.4619",
            "stage": "DSC",
            "altitude_feet": "83",
            "wind_dir": "240",
            "wind_spd": "10",
            "oat": "10"
          }
        ]
      }
    }
    """;

    [Fact]
    public void DecodePlanJson_WithRealisticSimBriefJson_DecodesAllPlanPropertiesCorrectly()
    {
        var fetcher = new SimBriefFetcher();
        var plan = fetcher.DecodePlanJson(RealisticSimBriefJson);

        Assert.NotNull(plan);
        Assert.Equal("KJFK", plan.Origin);
        Assert.Equal("EGLL", plan.Destination);
        Assert.Equal("EGCC", plan.Alternate);
        Assert.Equal(35000, plan.CruiseAltitudeFt);
        Assert.Equal("BAW112", plan.FlightNumber);
        Assert.Equal("B77W", plan.AircraftType);
        Assert.Equal("KJFK MERIT PUT BOS EGLL", plan.RouteString);
        Assert.Equal(360, plan.EstimatedTimeEnrouteMinutes);
        Assert.Equal(5, plan.Waypoints.Count);

        // Check waypoints
        var originWp = plan.Waypoints[0];
        Assert.Equal("KJFK", originWp.Identifier);
        Assert.Equal(40.6398, originWp.Latitude);
        Assert.Equal(-73.7789, originWp.Longitude);
        Assert.Equal(13, originWp.AltitudeFt);
        Assert.Equal(250, originWp.WindDirection);
        Assert.Equal(15, originWp.WindSpeedKt);
        Assert.Equal(15, originWp.TemperatureC);
        Assert.Equal(WaypointStage.Climb, originWp.Stage);

        var cruiseWp = plan.Waypoints[2];
        Assert.Equal("PUT", cruiseWp.Identifier);
        Assert.Equal(35000, cruiseWp.AltitudeFt);
        Assert.Equal(270, cruiseWp.WindDirection);
        Assert.Equal(55, cruiseWp.WindSpeedKt);
        Assert.Equal(-50, cruiseWp.TemperatureC);
        Assert.Equal(WaypointStage.Cruise, cruiseWp.Stage);

        var descentWp = plan.Waypoints[3];
        Assert.Equal("BOS", descentWp.Identifier);
        Assert.Equal(WaypointStage.Descent, descentWp.Stage);
    }

    [Fact]
    public void DecodePlanJson_AlternativeFieldRepresentations_HandlesGracefully()
    {
        const string json = """
        {
          "origin": "KORD",
          "destination": "KLAX",
          "general": {
            "flight_number": "UAL500",
            "initial_altitude": "FL380",
            "route_string": "KORD JOT IOW KLAX"
          },
          "aircraft": {
            "type": "A320"
          },
          "times": {
            "est_time_enroute": "04:15"
          },
          "navlog": {
            "fix": {
              "ident": "JOT",
              "pos_lat": 41.547,
              "pos_long": -88.318,
              "altitude": 38000,
              "wind_direction": 280,
              "wind_speed": 60,
              "temperature": -52,
              "stage": "CRUISE"
            }
          }
        }
        """;

        var fetcher = new SimBriefFetcher();
        var plan = fetcher.DecodePlanJson(json);

        Assert.Equal("KORD", plan.Origin);
        Assert.Equal("KLAX", plan.Destination);
        Assert.Equal("UAL500", plan.FlightNumber);
        Assert.Equal(38000, plan.CruiseAltitudeFt);
        Assert.Equal("A320", plan.AircraftType);
        Assert.Equal("KORD JOT IOW KLAX", plan.RouteString);
        Assert.Equal(255, plan.EstimatedTimeEnrouteMinutes); // 4h 15m = 255 mins
        Assert.Single(plan.Waypoints);

        var fix = plan.Waypoints[0];
        Assert.Equal("JOT", fix.Identifier);
        Assert.Equal(41.547, fix.Latitude);
        Assert.Equal(-88.318, fix.Longitude);
        Assert.Equal(38000, fix.AltitudeFt);
        Assert.Equal(280, fix.WindDirection);
        Assert.Equal(60, fix.WindSpeedKt);
        Assert.Equal(-52, fix.TemperatureC);
        Assert.Equal(WaypointStage.Cruise, fix.Stage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{ invalid json }")]
    [InlineData("{\"fetch\": {\"status\": \"Error: Pilot ID not found\"}}")]
    public void DecodePlanJson_MissingOrMalformedJson_DoesNotCrashAndProvidesGracefulBounds(string? json)
    {
        var fetcher = new SimBriefFetcher();
        var plan = fetcher.DecodePlanJson(json!);

        Assert.NotNull(plan);
        Assert.NotNull(plan.Waypoints);
        Assert.InRange(plan.CruiseAltitudeFt, 0, 60000);
        Assert.InRange(plan.EstimatedTimeEnrouteMinutes, 0, double.MaxValue);
    }

    [Fact]
    public void DecodePlanJson_OutOfBoundsValues_ClampedToPlausiblePhysicalLimits()
    {
        const string json = """
        {
          "origin": { "icao_code": "KJFK" },
          "destination": { "icao_code": "EGLL" },
          "general": { "cruise_altitude": "85000" },
          "navlog": {
            "fix": [
              {
                "ident": "TEST",
                "pos_lat": 120.0,
                "pos_long": -250.0,
                "altitude_feet": "99000",
                "wind_dir": "-45",
                "wind_spd": "500",
                "oat": "-150"
              }
            ]
          }
        }
        """;

        var fetcher = new SimBriefFetcher();
        var plan = fetcher.DecodePlanJson(json);

        Assert.Equal(60000, plan.CruiseAltitudeFt);
        var wp = Assert.Single(plan.Waypoints);
        Assert.Equal(90.0, wp.Latitude);
        Assert.Equal(-180.0, wp.Longitude);
        Assert.Equal(60000, wp.AltitudeFt);
        Assert.Equal(315, wp.WindDirection);
        Assert.Equal(300, wp.WindSpeedKt);
        Assert.Equal(-100, wp.TemperatureC);
    }

    [Fact]
    public async Task FetchPlanAsync_HttpSuccess_FetchesAndDecodesPlan()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(RealisticSimBriefJson);

        using var client = new HttpClient(handler);
        using var fetcher = new SimBriefFetcher(client);

        var plan = await fetcher.FetchPlanAsync("123456");

        Assert.NotNull(plan);
        Assert.Equal("KJFK", plan!.Origin);
        Assert.Equal("EGLL", plan.Destination);
        Assert.Single(handler.RequestedUrls);
        Assert.Contains("username=123456", handler.RequestedUrls[0]);
        Assert.Contains("json=1", handler.RequestedUrls[0]);
    }

    [Fact]
    public async Task FetchPlanAsync_Http404_ReturnsNullGracefullyWithLoggedError()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        using var client = new HttpClient(handler);
        string? logged = null;
        using var fetcher = new SimBriefFetcher(client, msg => logged = msg);

        var plan = await fetcher.FetchPlanAsync("999999");

        Assert.Null(plan);
        Assert.NotNull(logged);
        Assert.NotNull(fetcher.LastError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task FetchPlanAsync_EmptyPilotId_ReturnsNullWithoutCallingNetwork(string? pilotId)
    {
        var handler = new StubHttpMessageHandler();
        using var client = new HttpClient(handler);
        using var fetcher = new SimBriefFetcher(client);

        var plan = await fetcher.FetchPlanAsync(pilotId!);

        Assert.Null(plan);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public void RouteHazardAnalyzer_CalculateHeadwindComponent_AccurateAcrossAllQuadrants()
    {
        // Aircraft flying course 090 (East)
        // 1. Pure headwind: wind from 090 at 30 kt
        var hwPure = RouteHazardAnalyzer.CalculateHeadwindComponent(90, 30, 90);
        Assert.Equal(30.0, hwPure);

        // 2. Pure tailwind: wind from 270 at 30 kt
        var twPure = RouteHazardAnalyzer.CalculateHeadwindComponent(270, 30, 90);
        Assert.Equal(-30.0, twPure);

        // 3. Pure crosswind: wind from 360 at 30 kt
        var xwPure = RouteHazardAnalyzer.CalculateHeadwindComponent(360, 30, 90);
        Assert.Equal(0.0, xwPure);

        // 4. Quartering headwind: aircraft course 360 (North), wind from 045 at 50 kt
        // cos(45) = 0.7071 -> 50 * 0.7071 = 35.4 kt
        var hwQuarter = RouteHazardAnalyzer.CalculateHeadwindComponent(45, 50, 360);
        Assert.Equal(35.4, hwQuarter);
    }

    [Fact]
    public void RouteHazardAnalyzer_CalculatesTotalDistanceAndWeightedHeadwind()
    {
        var plan = new SimBriefPlan
        {
            Origin = "W1",
            Destination = "W3",
            Waypoints = new List<SimBriefWaypoint>
            {
                new() { Identifier = "W1", Latitude = 40.0, Longitude = -74.0, AltitudeFt = 35000, WindDirection = 90, WindSpeedKt = 20 },
                new() { Identifier = "W2", Latitude = 40.0, Longitude = -72.0, AltitudeFt = 35000, WindDirection = 90, WindSpeedKt = 20 },
                new() { Identifier = "W3", Latitude = 40.0, Longitude = -70.0, AltitudeFt = 35000, WindDirection = 90, WindSpeedKt = 20 }
            }
        };

        var analyzer = new RouteHazardAnalyzer();
        var profile = analyzer.AnalyzeRoute(plan);

        Assert.True(profile.TotalDistanceNm > 150 && profile.TotalDistanceNm < 250);
        // Eastward flight against 090 wind -> pure headwind ~20 kt
        Assert.True(profile.AverageHeadwindKt > 18.0 && profile.AverageHeadwindKt <= 20.0);
    }

    [Fact]
    public void RouteHazardAnalyzer_StormCellCorridorIntersection_DetectedWhenWithinBuffer()
    {
        // Route from (40.0, -74.0) to (40.0, -70.0)
        var plan = new SimBriefPlan
        {
            Waypoints = new List<SimBriefWaypoint>
            {
                new() { Identifier = "A", Latitude = 40.0, Longitude = -74.0, AltitudeFt = 35000 },
                new() { Identifier = "B", Latitude = 40.0, Longitude = -70.0, AltitudeFt = 35000 }
            }
        };

        // Storm cell 1 is 6 nm north of midpoint (40.1, -72.0) -> inside 25 nm buffer
        var nearCell = new StormCell
        {
            Latitude = 40.1,
            Longitude = -72.0,
            RadiusNm = 10,
            Intensity = 0.85,
            Type = CellType.Core,
            AltitudeFeet = 40000
        };

        // Storm cell 2 is ~150 nm away (42.5, -72.0) -> outside buffer
        var farCell = new StormCell
        {
            Latitude = 42.5,
            Longitude = -72.0,
            RadiusNm = 5,
            Intensity = 0.9,
            Type = CellType.Core,
            AltitudeFeet = 38000
        };

        var analyzer = new RouteHazardAnalyzer();
        var profile = analyzer.AnalyzeRoute(plan, stormCells: new[] { nearCell, farCell }, corridorBufferNm: 25.0);

        Assert.Single(profile.IntersectingStormCells);
        Assert.Equal(nearCell.Latitude, profile.IntersectingStormCells[0].Latitude);
        Assert.Contains(profile.SeverePockets, h => h.Type == HazardType.ConvectiveSigmet && h.Severity >= 0.8);
    }

    [Fact]
    public void RouteHazardAnalyzer_SigmetCorridorIntersection_DetectedWhenWithinBuffer()
    {
        var plan = new SimBriefPlan
        {
            Waypoints = new List<SimBriefWaypoint>
            {
                new() { Identifier = "A", Latitude = 40.0, Longitude = -74.0, AltitudeFt = 35000 },
                new() { Identifier = "B", Latitude = 40.0, Longitude = -70.0, AltitudeFt = 35000 }
            }
        };

        var nearSigmet = new WeatherHazard
        {
            Id = "SIGMET-1",
            Type = HazardType.TurbulenceSigmet,
            Latitude = 40.05,
            Longitude = -72.0,
            Severity = 0.8,
            Description = "Severe turbulence reported"
        };

        var farSigmet = new WeatherHazard
        {
            Id = "SIGMET-2",
            Type = HazardType.IcingSigmet,
            Latitude = 30.0,
            Longitude = -80.0,
            Severity = 0.9,
            Description = "Severe icing Florida"
        };

        var analyzer = new RouteHazardAnalyzer();
        var profile = analyzer.AnalyzeRoute(plan, hazards: new[] { nearSigmet, farSigmet }, corridorBufferNm: 25.0);

        Assert.Single(profile.SeverePockets);
        Assert.Equal("SIGMET-1", profile.SeverePockets[0].Id);
        Assert.True(profile.MaxTurbulenceIndex >= 0.8);
    }

    [Fact]
    public void RouteHazardAnalyzer_IcingRiskAssessment_EvaluatesEnvelopeAndLayers()
    {
        var plan = new SimBriefPlan
        {
            Waypoints = new List<SimBriefWaypoint>
            {
                // Cold waypoint in icing envelope (-15 C is peak severity)
                new() { Identifier = "COLD", Latitude = 40.0, Longitude = -74.0, AltitudeFt = 10000, TemperatureC = -15 },
                // Warm waypoint (above freezing) -> no icing risk
                new() { Identifier = "WARM", Latitude = 40.0, Longitude = -72.0, AltitudeFt = 2000, TemperatureC = 20 },
                // Very cold waypoint (< -40 C) -> outside icing envelope
                new() { Identifier = "FREEZING", Latitude = 40.0, Longitude = -70.0, AltitudeFt = 39000, TemperatureC = -55 }
            }
        };

        var analyzer = new RouteHazardAnalyzer();
        var profile = analyzer.AnalyzeRoute(plan);

        // Only COLD waypoint should produce icing risk
        Assert.Single(profile.IcingRisks);
        var risk = profile.IcingRisks[0];
        Assert.Equal("COLD", risk.WaypointIdentifier);
        Assert.Equal(IcingSeverity.Severe, risk.IcingSeverity);
        Assert.Equal(-15, risk.TemperatureCelsius);
    }

    [Fact]
    public void RouteHazardAnalyzer_EmptyOrSingleWaypointPlan_HandledGracefully()
    {
        var analyzer = new RouteHazardAnalyzer();

        // Empty plan
        var emptyProfile = analyzer.AnalyzeRoute(new SimBriefPlan());
        Assert.Equal(0, emptyProfile.TotalDistanceNm);
        Assert.Equal(0, emptyProfile.AverageHeadwindKt);
        Assert.Empty(emptyProfile.SeverePockets);

        // Single waypoint plan
        var singleWpPlan = new SimBriefPlan
        {
            Waypoints = new List<SimBriefWaypoint>
            {
                new() { Identifier = "KJFK", Latitude = 40.6398, Longitude = -73.7789, AltitudeFt = 13, WindDirection = 360, WindSpeedKt = 10 }
            }
        };
        var singleProfile = analyzer.AnalyzeRoute(singleWpPlan);
        Assert.Equal(0, singleProfile.TotalDistanceNm);
        Assert.Equal(10.0, singleProfile.AverageHeadwindKt);
    }

    [Fact]
    public async Task FetchPlanAsync_ParsesNavigraphAiracCycleCorrectly()
    {
        const string jsonWithAirac = """
        {
          "fetch": { "status": "Success" },
          "params": {
            "airac": "2409"
          },
          "general": {
            "flight_number": "101",
            "icao_airline": "DLH",
            "route": "EDDF DCT EGLL",
            "cruise_altitude": "32000"
          },
          "origin": { "icao_code": "EDDF" },
          "destination": { "icao_code": "EGLL" }
        }
        """;

        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(jsonWithAirac);
        using var client = new HttpClient(handler);
        var fetcher = new SimBriefFetcher(client);

        var plan = await fetcher.FetchPlanAsync("testpilot");

        Assert.NotNull(plan);
        Assert.Equal("2409", plan.AiracCycle);
        Assert.Equal("Navigraph AIRAC 2409", plan.NavigraphAirac);
    }
}
