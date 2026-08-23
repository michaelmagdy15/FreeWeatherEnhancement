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
}
