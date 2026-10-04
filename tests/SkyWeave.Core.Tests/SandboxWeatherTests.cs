using System;
using System.Linq;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class SandboxWeatherTests
{
    [Fact]
    public void CreatePresets_AllPresetsHaveValidParametersAndNames()
    {
        var presets = SandboxWeatherScenario.GetDefaultPresets();
        Assert.NotEmpty(presets);
        Assert.True(presets.Count >= 6);

        foreach (var p in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Id));
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.InRange(p.SurfaceWindDirection, 0, 360);
            Assert.InRange(p.SurfaceWindSpeedKnots, 0, 150);
            Assert.InRange(p.PressureHpa, 900, 1080);
            Assert.InRange(p.VisibilityMeters, 50, 100000);
        }
    }

    [Fact]
    public void BuildWeatherState_Cat3Fog_ProducesDenseFogAndZeroCeiling()
    {
        var scenario = SandboxWeatherScenario.CreateCat3Fog();
        var state = SandboxWeatherBuilder.BuildWeatherState(scenario, 40.6399, -73.7787, 50, 10, "KJFK");

        Assert.True(state.IsSandbox);
        Assert.Equal("CAT III ILS 0/0 Fog", state.SandboxScenarioName);
        Assert.Equal("LIFR", state.FlightCategory);
        Assert.InRange(state.VisibilityMeters, 50, 300);
        Assert.Single(state.CloudLayers);
        Assert.Equal(0, state.CloudLayers[0].BaseFeetAgl);
        Assert.Equal(100, state.CloudLayers[0].CoveragePercent);
        Assert.Contains("00002KT", state.RawMetar);
        Assert.Contains("Q1020", state.RawMetar);
    }

    [Fact]
    public void BuildWeatherState_Crosswind_ProducesHighWindAndMechanicalTurbulence()
    {
        var scenario = SandboxWeatherScenario.CreateCrosswindLanding();
        var state = SandboxWeatherBuilder.BuildWeatherState(scenario, 39.8561, -104.6737, 5400, 1655, "KDEN");

        Assert.True(state.IsSandbox);
        Assert.Equal(35, state.WindSpeedKnots);
        Assert.Equal(50, state.WindGustKnots);
        Assert.True(state.TurbulenceIndex >= 0.7);
        Assert.Contains("09035G50KT", state.RawMetar);
        Assert.Contains(state.Hazards, h => h.Type == HazardType.TurbulenceSigmet);
    }

    [Fact]
    public void BuildWeatherState_Supercell_ProducesStormCellAndConvectiveHazards()
    {
        var scenario = SandboxWeatherScenario.CreateSupercellThunderstorm();
        var state = SandboxWeatherBuilder.BuildWeatherState(scenario, 32.8998, -97.0403, 600, 180, "KDFW");

        Assert.True(state.IsSandbox);
        Assert.True(state.ThunderstormIntensity > 0.5);
        Assert.NotEmpty(state.StormCells);
        Assert.True(state.ConvectiveAvailablePotentialEnergy > 3000);
        Assert.Contains("TSRA", state.RawMetar);
        Assert.Contains(state.Hazards, h => h.Type == HazardType.ConvectiveSigmet);
    }

    [Fact]
    public void BuildWeatherState_ElevationOffset_AnchorsMSLLayersCorrectly()
    {
        var scenario = new SandboxWeatherScenario
        {
            Name = "Mountain Approach",
            CloudDecks = new()
            {
                new SandboxCloudDeck { BaseFeetAgl = 1000, TopFeetAgl = 4000, CoveragePercent = 80, Type = CloudType.BKN }
            }
        };

        double stationElevMeters = 1000.0; // ~3281 ft MSL
        var state = SandboxWeatherBuilder.BuildWeatherState(scenario, 46.0, 8.0, 3500, stationElevMeters, "LSGS");

        Assert.Single(state.CloudLayers);
        var layer = state.CloudLayers[0];
        Assert.Equal(1000, layer.BaseFeetAgl);
        // BaseMeters = 1000m + (1000ft * 0.3048) = 1304.8m
        Assert.Equal(1304.8, layer.BaseMeters, 1);
    }

    [Fact]
    public void SmoothingPipeline_PreservesSandboxProperties()
    {
        var pipeline = new SmoothingPipeline();
        var scenario = SandboxWeatherScenario.CreateMountainWaveCat();
        var state = SandboxWeatherBuilder.BuildWeatherState(scenario, 47.0, 11.0, 10000, 500, "LOWI");

        pipeline.SetTarget(state);
        pipeline.ForceImmediate();

        var current = pipeline.GetCurrentState();
        Assert.True(current.IsSandbox);
        Assert.Equal("Mountain Wave & Clear Air Turb", current.SandboxScenarioName);
    }
}
