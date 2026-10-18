using System;
using System.Linq;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class RunwayWindCalculatorTests
{
    [Fact]
    public void CalculateRunway_DirectHeadwind_CalculatesZeroCrosswindAndFullHeadwind()
    {
        // Runway 36 (360°), Wind 360° @ 25 kt
        var result = RunwayWindCalculator.CalculateRunway("36", 360, 360, 25);

        Assert.Equal("36", result.Runway);
        Assert.Equal(25.0, result.HeadwindKnots, 1);
        Assert.Equal(0.0, result.CrosswindKnots, 1);
        Assert.Equal(RunwaySuitability.Favorable, result.Suitability);
        Assert.Contains("Headwind", result.Summary);
    }

    [Fact]
    public void CalculateRunway_DirectTailwind_CalculatesNegativeHeadwindAndExceedsLimit()
    {
        // Runway 18 (180°), Wind 360° @ 15 kt -> 15 kt pure tailwind
        var result = RunwayWindCalculator.CalculateRunway("18", 180, 360, 15, maxTailwindLimit: 10.0);

        Assert.Equal("18", result.Runway);
        Assert.Equal(-15.0, result.HeadwindKnots, 1);
        Assert.Equal(0.0, result.CrosswindKnots, 1);
        Assert.Equal(RunwaySuitability.LimitExceeded, result.Suitability);
        Assert.Contains("Tailwind", result.SuitabilityText);
        Assert.Contains("Tailwind", result.Summary);
    }

    [Fact]
    public void CalculateRunway_RightCrosswind_ReportsRightCrosswindSide()
    {
        // Runway 36 (360°), Wind 090° @ 20 kt
        var result = RunwayWindCalculator.CalculateRunway("36", 360, 90, 20);

        Assert.Equal(0.0, result.HeadwindKnots, 1);
        Assert.Equal(20.0, result.CrosswindKnots, 1);
        Assert.Equal("Right", result.CrosswindSide);
    }

    [Fact]
    public void CalculateRunway_LeftCrosswind_ReportsLeftCrosswindSide()
    {
        // Runway 36 (360°), Wind 270° @ 20 kt
        var result = RunwayWindCalculator.CalculateRunway("36", 360, 270, 20);

        Assert.Equal(0.0, result.HeadwindKnots, 1);
        Assert.Equal(20.0, result.CrosswindKnots, 1);
        Assert.Equal("Left", result.CrosswindSide);
    }

    [Fact]
    public void CalculateRunway_WithWindGust_ComputesGustComponents()
    {
        // Runway 04L (044°), Wind 070° @ 15G28 kt
        var result = RunwayWindCalculator.CalculateRunway("04L", 44, 70, 15, windGust: 28);

        Assert.True(result.HeadwindKnots > 0);
        Assert.True(result.CrosswindKnots > 0);
        Assert.True(result.GustHeadwindKnots > result.HeadwindKnots);
        Assert.True(result.GustCrosswindKnots > result.CrosswindKnots);
        Assert.Contains("G28", result.Summary);
    }

    [Fact]
    public void GetRunwaysForAirport_CatalogAirport_ReturnsKnownRunways()
    {
        var jfkRunways = RunwayWindCalculator.GetRunwaysForAirport("KJFK");
        Assert.NotEmpty(jfkRunways);
        Assert.Contains(jfkRunways, r => r.Identifier == "31L");
        Assert.Contains(jfkRunways, r => r.Identifier == "13R");
        Assert.Contains(jfkRunways, r => r.Identifier == "04L");
        Assert.Contains(jfkRunways, r => r.Identifier == "22R");
    }

    [Fact]
    public void GetRunwaysForAirport_NonCatalogAirportWithPlannedRunway_GeneratesReciprocalPair()
    {
        // Uncataloged station with planned runway 24R
        var runways = RunwayWindCalculator.GetRunwaysForAirport("ZZZZ", plannedRunway: "24R");

        Assert.Equal(2, runways.Count);
        Assert.Contains(runways, r => r.Identifier == "24R" && Math.Abs(r.HeadingDegrees - 240) < 0.1);
        Assert.Contains(runways, r => r.Identifier == "06L" && Math.Abs(r.HeadingDegrees - 60) < 0.1);
    }

    [Fact]
    public void AnalyzeStationRunways_JFKNorthwestWind_RecommendsRunway31LOr31R()
    {
        // JFK with 310° @ 22G30 kt
        var report = RunwayWindCalculator.AnalyzeStationRunways("KJFK", 310, 22, 30);

        Assert.NotNull(report);
        Assert.Equal("KJFK", report.StationId);
        Assert.NotNull(report.RecommendedRunway);
        Assert.StartsWith("31", report.RecommendedRunway);
        Assert.NotNull(report.BestRunway);
        Assert.Equal(RunwaySuitability.Recommended, report.BestRunway.Suitability);
        Assert.True(report.BestRunway.HeadwindKnots > 18.0);
        Assert.True(report.BestRunway.CrosswindKnots < 5.0);

        // Reciprocal runways 13L/13R should exceed tailwind limit
        var rwy13R = report.Runways.FirstOrDefault(r => r.Runway == "13R");
        Assert.NotNull(rwy13R);
        Assert.Equal(RunwaySuitability.LimitExceeded, rwy13R.Suitability);
    }

    [Fact]
    public void AnalyzeStationRunways_CalmWinds_ReturnsSafeReport()
    {
        var report = RunwayWindCalculator.AnalyzeStationRunways("EGLL", 0, 2);

        Assert.NotNull(report);
        Assert.NotNull(report.BestRunway);
        Assert.NotEmpty(report.Runways);
    }
}
