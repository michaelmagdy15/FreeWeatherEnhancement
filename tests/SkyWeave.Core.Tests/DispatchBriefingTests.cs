using System;
using System.Collections.Generic;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class DispatchBriefingTests
{
    [Fact]
    public void BuildBriefing_WithCompletePlan_PopulatesAllSectionsAndAdvisories()
    {
        var plan = new SimBriefPlan
        {
            FlightNumber = "DAL882",
            AircraftType = "A321",
            Origin = "KATL",
            Destination = "KJFK",
            Alternate = "KBOS",
            CruiseAltitudeFt = 35000,
            EstimatedTimeEnrouteMinutes = 118,
            RouteString = "KATL DCT SPA J14 CREWE J174 HTO KJFK",
            AiracCycle = "2408",
            Waypoints = new List<SimBriefWaypoint>
            {
                new SimBriefWaypoint { Identifier = "KATL", Latitude = 33.64, Longitude = -84.42, AltitudeFt = 1000, WindDirection = 270, WindSpeedKt = 10, TemperatureC = 20, Stage = WaypointStage.Climb },
                new SimBriefWaypoint { Identifier = "SPA", Latitude = 34.97, Longitude = -81.96, AltitudeFt = 24000, WindDirection = 280, WindSpeedKt = 45, TemperatureC = -25, Stage = WaypointStage.Climb },
                new SimBriefWaypoint { Identifier = "CREWE", Latitude = 37.21, Longitude = -78.21, AltitudeFt = 35000, WindDirection = 300, WindSpeedKt = 85, TemperatureC = -48, Stage = WaypointStage.Cruise },
                new SimBriefWaypoint { Identifier = "HTO", Latitude = 40.92, Longitude = -72.31, AltitudeFt = 12000, WindDirection = 310, WindSpeedKt = 35, TemperatureC = -5, Stage = WaypointStage.Descent },
                new SimBriefWaypoint { Identifier = "KJFK", Latitude = 40.64, Longitude = -73.78, AltitudeFt = 50, WindDirection = 310, WindSpeedKt = 18, TemperatureC = 12, Stage = WaypointStage.Descent }
            }
        };

        var depMetar = new MetarData
        {
            StationId = "KATL",
            RawText = "KATL 041800Z 27010KT 10SM CLR 20/10 A3002",
            FlightCategory = "VFR",
            WindDirectionDegrees = 270,
            WindSpeedKnots = 10,
            VisibilityMeters = 16093,
            AltimeterHpa = 1016.6,
            TemperatureCelsius = 20,
            DewpointCelsius = 10
        };

        var destMetar = new MetarData
        {
            StationId = "KJFK",
            RawText = "KJFK 041800Z 31018G26KT 2SM -RA OVC008 12/11 A2985",
            FlightCategory = "IFR",
            WindDirectionDegrees = 310,
            WindSpeedKnots = 18,
            WindGustKnots = 26,
            Clouds = new List<MetarCloud> { new MetarCloud { Coverage = "OVC", BaseFeet = 800 } },
            VisibilityMeters = 3218,
            AltimeterHpa = 1010.8,
            TemperatureCelsius = 12,
            DewpointCelsius = 11
        };

        var altMetar = new MetarData
        {
            StationId = "KBOS",
            RawText = "KBOS 041800Z 30012KT 10SM BKN040 14/08 A2990",
            FlightCategory = "VFR",
            WindDirectionDegrees = 300,
            WindSpeedKnots = 12,
            VisibilityMeters = 16093,
            AltimeterHpa = 1012.5,
            TemperatureCelsius = 14,
            DewpointCelsius = 8
        };

        var generator = new DispatchBriefingGenerator();
        var briefing = generator.BuildBriefing(plan, depMetar: depMetar, destMetar: destMetar, altMetar: altMetar);

        Assert.NotNull(briefing);
        Assert.Equal("DAL882", briefing.FlightNumber);
        Assert.Equal("KATL", briefing.OriginIcao);
        Assert.Equal("KJFK", briefing.DestinationIcao);
        Assert.Equal("KBOS", briefing.AlternateIcao);
        Assert.Equal(5, briefing.Navlog.Count);

        // Verify destination weather detected as IFR and triggered advisory
        Assert.NotNull(briefing.DestinationBriefing);
        Assert.Equal("IFR", briefing.DestinationBriefing.FlightCategory);
        Assert.Contains(briefing.HazardSummary.CriticalAdvisories, a => a.Contains("DESTINATION") && a.Contains("IFR"));

        // Verify Navlog calculations
        var cruiseWp = briefing.Navlog[2];
        Assert.Equal("CREWE", cruiseWp.Identifier);
        Assert.Equal(35000, cruiseWp.AltitudeFt);
        Assert.Equal(85, cruiseWp.WindSpeedKt);
        Assert.True(cruiseWp.CrosswindComponentKt >= 0);

        // Verify HTML Generation
        var html = generator.GenerateHtml(briefing, darkMode: false);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("DAL882", html);
        Assert.Contains("KATL", html);
        Assert.Contains("KJFK", html);
        Assert.Contains("CREWE", html);
        Assert.Contains("window.print()", html);
        Assert.Contains("@media print", html);
    }
}
