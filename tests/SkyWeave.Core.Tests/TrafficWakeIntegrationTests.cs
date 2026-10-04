using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class TrafficWakeIntegrationTests
{
    [Fact]
    public void BuildWeatherState_WithWakeTurbulenceLayer_InjectsWakeVortexHazard()
    {
        var cache = new WeatherCache();
        var stationFinder = new StationFinder();
        using var http = new HttpClient();
        var pipeline = new WeatherPipeline(
            cache,
            new MetarFetcher(http, stationFinder),
            new WindsAloftFetcher(http),
            new SigmetFetcher(http),
            new LightningFetcher(http),
            new RadarFetcher(http),
            new TafFetcher(http, stationFinder),
            new CloudLayerBuilder(),
            new WindLayerBuilder(),
            new IcingCalculator(),
            new TurbulenceCalculator(),
            new StormModeler(),
            new WakeTurbulenceEngine(),
            stationFinder,
            new HazardAggregator());

        var metar = new MetarData
        {
            StationId = "KJFK",
            RawText = "KJFK 041800Z 18012KT 10SM FEW050 20/12 A2992",
            ObservationTime = DateTime.UtcNow,
            TemperatureCelsius = 20,
            DewpointCelsius = 12,
            AltimeterHpa = 1013.25,
            VisibilityMeters = 16093,
            FlightCategory = "VFR",
            WindDirectionDegrees = 180,
            WindSpeedKnots = 12
        };

        var turbulenceLayers = new List<TurbulenceLayer>
        {
            new TurbulenceLayer
            {
                BaseFeet = 2000,
                TopFeet = 4000,
                Intensity = TurbulenceIntensity.Severe,
                Type = TurbulenceType.Wake
            }
        };

        var pipelineData = new PipelineData(
            metar,
            null,
            new List<CloudLayer>(),
            new List<WindLayer>(),
            new List<IcingLayer>(),
            turbulenceLayers,
            new List<StormCell>(),
            new List<WeatherHazard>(),
            new List<LightningStrike>(),
            0,
            null,
            null);

        var state = pipeline.BuildWeatherState(pipelineData, 40.64, -73.78, 3000);

        Assert.NotNull(state);
        var wakeHazard = state.Hazards.FirstOrDefault(h => h.Description.Contains("Wake Vortex Encounter"));
        Assert.NotNull(wakeHazard);
        Assert.Equal(HazardType.TurbulenceSigmet, wakeHazard.Type);
        Assert.Equal((double)TurbulenceIntensity.Severe, wakeHazard.Severity);
        Assert.Contains("Severe", wakeHazard.Description);
    }
}
