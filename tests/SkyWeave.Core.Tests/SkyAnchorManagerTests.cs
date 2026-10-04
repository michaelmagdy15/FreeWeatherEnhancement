using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class SkyAnchorManagerTests
{
    private readonly SkyAnchorManager _manager;
    private readonly StationFinder _stationFinder;

    public SkyAnchorManagerTests()
    {
        _stationFinder = new StationFinder();
        _manager = new SkyAnchorManager(_stationFinder);
        _manager.SetRoute("KJFK", "EGLL");
    }

    [Fact]
    public void Evaluate_DepartureHold_WithinCeilingAndDistance_ReturnsDepartureHold()
    {
        var jfk = _stationFinder.FindStation("KJFK")!;
        // Aircraft 2 NM from JFK, climbing through 2500 ft (JFK elevation is 13 ft -> ~2487 ft AGL)
        var state = _manager.Evaluate(jfk.Latitude + 0.03, jfk.Longitude, 2500);

        Assert.Equal(SkyAnchorPhase.DepartureHold, state.Phase);
        Assert.False(state.IsFrozen);
        Assert.Equal("KJFK", state.ActiveAnchorIcao);
    }

    [Fact]
    public void Evaluate_DepartureHold_AboveCeiling_TransitionsToEnRoute()
    {
        var jfk = _stationFinder.FindStation("KJFK")!;
        // Aircraft at 5000 ft (> 4000 ft AGL)
        var state = _manager.Evaluate(jfk.Latitude + 0.05, jfk.Longitude, 5000);

        Assert.Equal(SkyAnchorPhase.EnRoute, state.Phase);
        Assert.False(state.IsFrozen);
        Assert.Null(state.ActiveAnchorIcao);
    }

    [Fact]
    public void Evaluate_ArrivalHold_Within30Nm_ReturnsArrivalHold()
    {
        var egll = _stationFinder.FindStation("EGLL")!;
        // Aircraft ~20 NM from EGLL, descending at 8000 ft
        var state = _manager.Evaluate(egll.Latitude + 0.25, egll.Longitude, 8000);

        Assert.Equal(SkyAnchorPhase.ArrivalHold, state.Phase);
        Assert.False(state.IsFrozen);
        Assert.Equal("EGLL", state.ActiveAnchorIcao);
    }

    [Fact]
    public void Evaluate_FinalFreeze_Within5NmAnd1000FtAgl_ReturnsFinalFreeze()
    {
        var egll = _stationFinder.FindStation("EGLL")!;
        // Aircraft 3 NM from EGLL at 600 ft MSL (EGLL elev 83 ft -> 517 ft AGL)
        var state = _manager.Evaluate(egll.Latitude + 0.04, egll.Longitude, 600);

        Assert.Equal(SkyAnchorPhase.FinalFreeze, state.Phase);
        Assert.True(state.IsFrozen);
        Assert.Equal("EGLL", state.ActiveAnchorIcao);
    }

    [Fact]
    public void Evaluate_ManualFreeze_OverridesEverything()
    {
        _manager.IsManualFrozen = true;
        var state = _manager.Evaluate(0, 0, 35000);

        Assert.Equal(SkyAnchorPhase.ManualFreeze, state.Phase);
        Assert.True(state.IsFrozen);
    }

    [Fact]
    public void SetFlightPlan_ExtractsOriginAndDestination()
    {
        var plan = new SimBriefPlan
        {
            Origin = "KJFK",
            Destination = "EGLL"
        };

        _manager.SetFlightPlan(plan);

        Assert.NotNull(_manager.OriginAirport);
        Assert.Equal("KJFK", _manager.OriginAirport.IcaoId);
        Assert.NotNull(_manager.DestinationAirport);
        Assert.Equal("EGLL", _manager.DestinationAirport.IcaoId);
    }
}
