using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class SmoothingPipelineTests
{
    private readonly SmoothingPipeline _pipeline = new();

    private WeatherState CreateState(double temp, double windSpeed = 10.0, double windDir = 270.0)
    {
        return new WeatherState
        {
            TemperatureCelsius = temp,
            WindSpeedKnots = windSpeed,
            WindDirectionDegrees = windDir,
            DewpointCelsius = 5.0,
            PressureHpa = 1013.0,
            AltimeterHpa = 1013.0,
            VisibilityMeters = 10000,
            HumidityPercent = 65.0,
        };
    }

    [Fact]
    public void SetTarget_WithNullCurrent_InitializesImmediately()
    {
        var state = CreateState(20.0);
        _pipeline.SetTarget(state);

        var result = _pipeline.GetCurrentState();
        Assert.Equal(20.0, result.TemperatureCelsius);
    }

    [Fact]
    public void GetCurrentState_AtT0_ReturnsFromState()
    {
        var from = CreateState(20.0);
        var to = CreateState(30.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);

        var result = _pipeline.GetCurrentState();
        Assert.True(Math.Abs(result.TemperatureCelsius - 20.0) < 0.01);
    }

    [Fact]
    public void GetCurrentState_InterpolatesCorrectly()
    {
        var from = CreateState(10.0);
        var to = CreateState(30.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var result = _pipeline.GetCurrentState();
        Assert.True(result.TemperatureCelsius > 10.0);
        Assert.True(result.TemperatureCelsius < 30.0);
    }

    [Fact]
    public void IsTransitioning_ReturnsTrueDuringTransition()
    {
        var from = CreateState(10.0);
        var to = CreateState(20.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);

        Assert.True(_pipeline.IsTransitioning);
    }

    [Fact]
    public void ForceImmediate_SetsCurrentToTarget()
    {
        var from = CreateState(10.0);
        var to = CreateState(30.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.ForceImmediate();

        var result = _pipeline.GetCurrentState();
        Assert.Equal(30.0, result.TemperatureCelsius);
    }

    [Fact]
    public void GetCurrentState_AtT0_UsesSmoothStep()
    {
        var from = CreateState(0.0);
        var to = CreateState(100.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var result = _pipeline.GetCurrentState();
        Assert.True(result.TemperatureCelsius > 0.0);
    }

    [Fact]
    public void SetTarget_WithNewTarget_UpdatesTransition()
    {
        var from = CreateState(10.0);
        var to = CreateState(20.0);
        var to2 = CreateState(30.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.SetTarget(to2);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var result = _pipeline.GetCurrentState();
        // At t=0, result should be close to _current (10.0), not the new target
        Assert.True(Math.Abs(result.TemperatureCelsius - 10.0) < 0.1);
        Assert.True(_pipeline.IsTransitioning);
    }

    [Fact]
    public void GetCurrentState_WhenNoTarget_ReturnsEmpty()
    {
        var result = _pipeline.GetCurrentState();
        Assert.NotNull(result);
    }

    [Fact]
    public void SetTarget_FirstState_PreservesRawMetar()
    {
        var state = CreateState(20.0);
        state.RawMetar = "METAR HECA 180700Z 05007KT 9999 FEW025 28/18 Q1011 NOSIG";
        _pipeline.SetTarget(state);

        var result = _pipeline.GetCurrentState();
        Assert.Equal(state.RawMetar, result.RawMetar);
    }

    [Fact]
    public void GetCurrentState_DuringTransition_PreservesTargetRawMetar()
    {
        var from = CreateState(10.0);
        from.RawMetar = "METAR HECA 171000Z 05007KT 9999 FEW025 27/17 Q1011 NOSIG";
        var to = CreateState(30.0);
        to.RawMetar = "METAR HECA 180700Z 05007KT 9999 FEW025 28/18 Q1011 NOSIG";

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var result = _pipeline.GetCurrentState();
        Assert.Equal(to.RawMetar, result.RawMetar);
    }

    [Fact]
    public void LerpAngle_Handles360Wraparound()
    {
        var from = CreateState(15.0, windDir: 350.0);
        var to = CreateState(25.0, windDir: 10.0);

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var result = _pipeline.GetCurrentState();
        // At t≈0, should be close to 350 (from state)
        Assert.True(result.WindDirectionDegrees >= 340.0);
    }

    [Fact]
    public void SetTarget_MidTransition_AdvancesCurrentStateWithoutSnapback()
    {
        var s1 = CreateState(10.0);
        var s2 = CreateState(20.0);
        var s3 = CreateState(30.0);

        _pipeline.TransitionDuration = TimeSpan.FromSeconds(100);
        _pipeline.SetTarget(s1);
        _pipeline.SetTarget(s2);

        // When setting target s3 midway, starting point for s3 must not reset to s1
        _pipeline.SetTarget(s3);
        var current = _pipeline.GetCurrentState();

        // At t=0 of new transition, temperature should be at or above s1 (10.0) and below s3 (30.0)
        Assert.True(current.TemperatureCelsius >= 10.0);
        Assert.True(current.TemperatureCelsius <= 30.0);
    }

    [Fact]
    public void GetCurrentState_DuringTransition_PreservesPassThroughFields()
    {
        var from = CreateState(10.0);
        from.SourceModelName = "OLD_MODEL";
        from.DataAgeMinutes = 15;
        from.ConvectiveAvailablePotentialEnergy = 1000;
        from.LiftedIndex = -2;

        var to = CreateState(20.0);
        to.SourceModelName = "NEW_MODEL";
        to.DataAgeMinutes = 5;
        to.ConvectiveAvailablePotentialEnergy = 2000;
        to.LiftedIndex = -4;

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var result = _pipeline.GetCurrentState();
        
        Assert.Equal("NEW_MODEL", result.SourceModelName);
        Assert.Equal(5, result.DataAgeMinutes);
        Assert.Equal(2000, result.ConvectiveAvailablePotentialEnergy);
        Assert.Equal(-4, result.LiftedIndex);
    }

    [Fact]
    public void SlewClampSpeed_LimitsAcceleration()
    {
        _pipeline.MaxWindSpeedRateKtPerSec = 5.0;
        // From 10kt to 60kt (50kt diff). At t=1.0 after 2 seconds: max change is 10kt -> 20kt
        var result = _pipeline.SlewClampSpeed(10.0, 60.0, 1.0, 2.0);
        Assert.Equal(20.0, result, 2);

        // Deceleration: from 80kt to 20kt. At t=1.0 after 3 seconds: max reduction is 15kt -> 65kt
        var decel = _pipeline.SlewClampSpeed(80.0, 20.0, 1.0, 3.0);
        Assert.Equal(65.0, decel, 2);
    }

    [Fact]
    public void SlewClampAngle_LimitsHeadingSwingAcrossNorth()
    {
        _pipeline.MaxWindDirRateDegPerSec = 7.5;
        // From 350 to 30 deg (shortest turn is +40 deg clockwise across 360).
        // After 2 seconds, max turn is 15 deg -> 350 + 15 = 365 = 5 deg.
        var result = _pipeline.SlewClampAngle(350.0, 30.0, 1.0, 2.0);
        Assert.Equal(5.0, result, 1);

        // Counter-clockwise turn: from 20 deg to 340 deg (-40 deg).
        // After 2 seconds, max turn is -15 deg -> 20 - 15 = 5 deg.
        var ccw = _pipeline.SlewClampAngle(20.0, 340.0, 1.0, 2.0);
        Assert.Equal(5.0, ccw, 1);
    }

    [Fact]
    public void InterpolateWindsAloft_SmoothlyInterpolatesAloftLayers()
    {
        var from = CreateState(15.0);
        from.WindsAloft = new List<WindLayer>
        {
            new WindLayer
            {
                AltitudeFeet = 30000,
                AltitudeMeters = 30000 * 0.3048,
                DirectionDegrees = 270,
                SpeedKnots = 80,
                TemperatureCelsius = -45
            }
        };

        var to = CreateState(15.0);
        to.WindsAloft = new List<WindLayer>
        {
            new WindLayer
            {
                AltitudeFeet = 30000,
                AltitudeMeters = 30000 * 0.3048,
                DirectionDegrees = 290,
                SpeedKnots = 60,
                TemperatureCelsius = -40
            }
        };

        _pipeline.SetTarget(from);
        _pipeline.SetTarget(to);
        _pipeline.TransitionDuration = TimeSpan.FromMinutes(10);

        var current = _pipeline.GetCurrentState();
        Assert.NotNull(current.WindsAloft);
        var layer = Assert.Single(current.WindsAloft);
        Assert.Equal(30000, layer.AltitudeFeet);
        // At t≈0, should be close to initial 270 deg and 80 kt
        Assert.InRange(layer.SpeedKnots, 75, 80);
        Assert.InRange(layer.DirectionDegrees, 269, 275);
    }
}
