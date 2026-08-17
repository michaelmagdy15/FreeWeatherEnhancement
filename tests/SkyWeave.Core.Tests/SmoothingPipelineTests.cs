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
}
