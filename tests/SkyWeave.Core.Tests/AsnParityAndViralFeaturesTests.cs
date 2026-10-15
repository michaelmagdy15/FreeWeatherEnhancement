using System;
using System.Collections.Generic;
using System.Linq;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class AsnParityAndViralFeaturesTests
{
    [Fact]
    public void TurbulenceCalculator_MountainWave_SuppressedOverFlatOrOceanicTerrain()
    {
        var calc = new TurbulenceCalculator();
        var winds = new List<WindLayer>
        {
            new() { AltitudeMeters = 0, AltitudeFeet = 0, SpeedKnots = 12, DirectionDegrees = 250, TemperatureCelsius = 15, IsSurfaceLayer = true },
            new() { AltitudeMeters = 2500, AltitudeFeet = 8200, SpeedKnots = 58, DirectionDegrees = 255, TemperatureCelsius = -2 }
        };

        // Oceanic / flat station (15 ft MSL) -> NO mountain wave even with 58 kt @ 8200 ft
        var oceanicLayers = calc.CalculateTurbulenceLayers(winds, new List<CloudLayer>(), new List<StormCell>(), 8000.0, terrainElevationFeet: 15.0);
        Assert.DoesNotContain(oceanicLayers, l => l.Type == TurbulenceType.MountainWave);

        // Elevated mountain station (5,400 ft MSL, e.g. KDEN/LOWI) -> triggers MountainWave
        var mountainLayers = calc.CalculateTurbulenceLayers(winds, new List<CloudLayer>(), new List<StormCell>(), 8000.0, terrainElevationFeet: 5400.0);
        var mw = Assert.Single(mountainLayers, l => l.Type == TurbulenceType.MountainWave);
        Assert.True(mw.Intensity >= TurbulenceIntensity.Moderate);
    }

    [Fact]
    public void SmoothingPipeline_InterpolatesCloudLayersAndPreservesNewStateFields()
    {
        var pipeline = new SmoothingPipeline();
        var fromLayers = new List<CloudLayer>
        {
            new() { BaseMeters = 1000, TopMeters = 2000, CoveragePercent = 50.0, Density = 0.6, Scattering = 0.7, Type = CloudType.SCT }
        };

        var toLayers = new List<CloudLayer>
        {
            new() { BaseMeters = 2000, TopMeters = 3000, CoveragePercent = 90.0, Density = 0.8, Scattering = 0.5, Type = CloudType.CB },
            new() { BaseMeters = 5500, TopMeters = 6500, CoveragePercent = 40.0, Density = 0.4, Scattering = 0.8, Type = CloudType.FEW }
        };

        var midClouds = pipeline.InterpolateCloudLayers(fromLayers, toLayers, 0.5);
        Assert.Equal(2, midClouds.Count);

        // Layer 0 interpolated halfway between 1000..2000 and 2000..3000
        Assert.Equal(1500.0, midClouds[0].BaseMeters, 2);
        Assert.Equal(2500.0, midClouds[0].TopMeters, 2);
        Assert.Equal(70.0, midClouds[0].CoveragePercent, 2);

        // Layer 1 (>2500m away from Layer 0) faded in from 0 coverage to 40% coverage -> 20% at t=0.5
        Assert.Equal(20.0, midClouds[1].CoveragePercent, 2);

        var targetState = new WeatherState
        {
            StationId = "KEWR",
            StructuralIcePercent = 28.0,
            InterpolatedStationIds = new List<string> { "KEWR", "KJFK", "KLGA" },
            SpatialBlendSummary = "KEWR 60% + KJFK 25% + KLGA 15%",
            CloudLayers = toLayers
        };

        pipeline.SnapToState(targetState);
        var snapped = pipeline.GetCurrentState();
        Assert.Equal(28.0, snapped.StructuralIcePercent);
        Assert.Equal(3, snapped.InterpolatedStationIds.Count);
        Assert.Equal("KEWR 60% + KJFK 25% + KLGA 15%", snapped.SpatialBlendSummary);
    }

    [Fact]
    public void SpatialWeatherInterpolator_PreservesExactMetarWithin3Nm_AndBlendsEnroute()
    {
        var interpolator = new SpatialWeatherInterpolator();

        var kjfkAirport = new AirportData { IcaoId = "KJFK", Name = "New York JFK", Latitude = 40.6413, Longitude = -73.7781, ElevationFeet = 13 };
        var klgaAirport = new AirportData { IcaoId = "KLGA", Name = "New York LaGuardia", Latitude = 40.7769, Longitude = -73.8740, ElevationFeet = 21 };

        var kjfk = new MetarData
        {
            StationId = "KJFK",
            RawText = "METAR KJFK 181200Z 18010KT 10SM FEW030 20/12 A2992",
            WindDirectionDegrees = 180,
            WindSpeedKnots = 10,
            TemperatureCelsius = 20,
            DewpointCelsius = 12,
            AltimeterHpa = 1013.2,
            VisibilityMeters = 16093,
            FlightCategory = "VFR"
        };

        var klga = new MetarData
        {
            StationId = "KLGA",
            RawText = "METAR KLGA 181200Z 20020KT 5SM BKN020 16/10 A2980",
            WindDirectionDegrees = 200,
            WindSpeedKnots = 20,
            TemperatureCelsius = 16,
            DewpointCelsius = 10,
            AltimeterHpa = 1009.1,
            VisibilityMeters = 8046,
            FlightCategory = "MVFR"
        };

        // 1. At airport (distance = 0.8 NM <= 3.0 NM): NFR-A1 lock preserves KJFK 100%
        var lockedResult = interpolator.BlendObservations(
            40.6413, -73.7781, 50,
            new List<StationObservationSample>
            {
                new(kjfkAirport, kjfk, 0.8),
                new(klgaAirport, klga, 9.5)
            });

        Assert.True(lockedResult.IsAirportLocked);
        Assert.Equal(20.0, lockedResult.BlendedMetar.TemperatureCelsius);
        Assert.Equal(1013.2, lockedResult.BlendedMetar.AltimeterHpa);
        Assert.Single(lockedResult.StationIds);

        // 2. Enroute halfway between stations (each 12 NM away): IDW blend
        var enrouteResult = interpolator.BlendObservations(
            40.7100, -73.8200, 3500,
            new List<StationObservationSample>
            {
                new(kjfkAirport, kjfk, 12.0),
                new(klgaAirport, klga, 12.0)
            });

        Assert.False(enrouteResult.IsAirportLocked);
        Assert.Equal(2, enrouteResult.StationIds.Count);
        Assert.InRange(enrouteResult.BlendedMetar.TemperatureCelsius, 17.5, 18.5);
        Assert.InRange(enrouteResult.BlendedMetar.WindSpeedKnots, 14.0, 16.0);
        Assert.Contains("KJFK", enrouteResult.Summary);
        Assert.Contains("KLGA", enrouteResult.Summary);
    }

    [Fact]
    public void AirframePhysicsCalculator_ComputesWakeVortexMicroburstAndDrydenCat()
    {
        var physics = new AirframePhysicsCalculator();
        var weather = new WeatherState
        {
            StationId = "KMCO",
            Latitude = 28.4294,
            Longitude = -81.3089,
            TurbulenceIndex = 0.55,
            ThunderstormIntensity = 0.85,
            StormCells = new List<StormCell>
            {
                new()
                {
                    Latitude = 28.4294,
                    Longitude = -81.3089,
                    AltitudeFeet = 3500,
                    RadiusNm = 3.0,
                    Intensity = 0.90,
                    Type = CellType.Core
                }
            }
        };

        var traffic = new List<AircraftTraffic>
        {
            new()
            {
                Callsign = "BAW117",
                Latitude = 28.4500,
                Longitude = -81.3089,
                AltitudeFeet = 1400,
                HeadingDegrees = 0,
                SpeedKnots = 150,
                OnGround = false,
                DistanceNm = 1.2,
                RelativeBearingDegrees = 5,
                AltitudeDeltaFeet = 200,
                WeightClass = AircraftWeightClass.Heavy,
                IsInWakeZone = true
            }
        };

        var step = physics.CalculateStep(
            weather: weather,
            aircraftLatitude: 28.4294,
            aircraftLongitude: -81.3089,
            aircraftAltitudeFeet: 1200,
            aircraftHeadingDegrees: 0,
            aircraftWeightPounds: 140000,
            isOnGround: false,
            nearbyTraffic: traffic,
            intensityScale: 1.0,
            randomSample: 0.5);

        Assert.True(step.IsWindshearWarning);
        Assert.True(step.IsWakeVortexActive);
        Assert.True(Math.Abs(step.RollRateRadPerSec) > 0.01);
        Assert.True(step.VerticalVelocityFps < -5.0);
        Assert.Contains("WINDSHEAR ALERT", step.ActiveEffectSummary);
    }

    [Fact]
    public void FlightWatchRadioService_DetectsFrequenciesAndSynthesizesValidWavHeader()
    {
        var service = new FlightWatchRadioService();
        Assert.Equal(FlightWatchChannel.EnrouteFlightWatch12200, service.DetectTunedChannel(122.000, 118.500));
        Assert.Equal(FlightWatchChannel.DestinationAtis122025, service.DetectTunedChannel(118.500, 122.025));
        Assert.Equal(FlightWatchChannel.DepartureAtis122050, service.DetectTunedChannel(122.050, 121.500));
        Assert.Equal(FlightWatchChannel.None, service.DetectTunedChannel(121.500, 118.100));

        var state = new WeatherState
        {
            StationId = "KJFK",
            WindDirectionDegrees = 270,
            WindSpeedKnots = 18,
            WindGustKnots = 26,
            VisibilityMeters = 9999,
            TemperatureCelsius = 14,
            DewpointCelsius = 8,
            AltimeterHpa = 1013.25,
            TurbulenceIndex = 0.35,
            CloudLayers = new List<CloudLayer>
            {
                new() { BaseFeetAgl = 3500, CoveragePercent = 65.0, Type = CloudType.BKN }
            }
        };

        var broadcast = service.BuildTransmission(state, FlightWatchChannel.EnrouteFlightWatch12200, 5000);
        Assert.Equal("122.000 MHz", broadcast.FrequencyDisplay);
        Assert.Contains("Kilo Juliet Foxtrot Kilo", broadcast.SpokenPhoneticScript);
        Assert.Contains("Altimeter", broadcast.SpokenPhoneticScript);

        var wav = service.GenerateVhfSquelchBurstWav(durationMs: 200);
        Assert.True(wav.Length > 44);
        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'I', wav[1]);
        Assert.Equal((byte)'F', wav[2]);
        Assert.Equal((byte)'F', wav[3]);
        Assert.Equal((byte)'W', wav[8]);
        Assert.Equal((byte)'A', wav[9]);
        Assert.Equal((byte)'V', wav[10]);
        Assert.Equal((byte)'E', wav[11]);
    }

    [Fact]
    public void AirborneWeatherRadarService_ComputesTiltSweepAndDopplerTurbulence()
    {
        var wxr = new AirborneWeatherRadarService();
        var weather = new WeatherState
        {
            StationId = "KMIA",
            Latitude = 25.7959,
            Longitude = -80.2870,
            FreezingLevelFeet = 14000,
            PrecipitationRate = 12.5,
            TurbulenceIndex = 0.65,
            ThunderstormIntensity = 0.85,
            StormCells = new List<StormCell>
            {
                new()
                {
                    Latitude = 25.9500,
                    Longitude = -80.2870,
                    AltitudeFeet = 18000,
                    RadiusNm = 8.0,
                    Intensity = 0.85,
                    Type = CellType.Core
                }
            }
        };

        var scan = wxr.ComputeRadarScan(
            weather: weather,
            aircraftLatitude: 25.7959,
            aircraftLongitude: -80.2870,
            aircraftAltitudeFeet: 12000,
            aircraftHeadingDegrees: 0,
            tiltDegrees: 1.5,
            rangeNm: 40,
            gainDb: 0,
            mode: AirborneRadarMode.WxPlusTurb);

        Assert.Equal(AirborneRadarMode.WxPlusTurb, scan.Mode);
        Assert.Equal(40.0, scan.RangeNm);
        Assert.Equal(1.5, scan.TiltDegrees);
        Assert.True(scan.SweepBins.Count > 0);
        Assert.Equal(8, scan.VerticalProfile.Count);
        Assert.True(scan.MaxReturnColorCode >= 2);
        Assert.Contains(scan.SweepBins, b => b.HasDopplerTurbulence || b.ColorCode == 4);
    }

    [Fact]
    public void ExtremeWeatherHunterService_ScoresChallengesAndReturnsFamousStorms()
    {
        var hunter = new ExtremeWeatherHunterService();
        var storms = hunter.GetFamousHistoricalStorms();
        Assert.True(storms.Count >= 6);
        Assert.Contains(storms, s => s.Id == "EUNICE_EGLL_2022" && s.IcaoId == "EGLL");

        var severeMetar = new MetarData
        {
            StationId = "EGLL",
            RawText = "METAR EGLL 181220Z 25042G68KT 2000 +TSRA BKN012CB 08/06 Q0978",
            WindDirectionDegrees = 250,
            WindSpeedKnots = 42,
            WindGustKnots = 68,
            VisibilityMeters = 2000,
            TemperatureCelsius = 8,
            DewpointCelsius = 6,
            AltimeterHpa = 978,
            FlightCategory = "IFR"
        };

        var challenge = hunter.ScoreMetar(severeMetar);
        Assert.Equal("EGLL", challenge.IcaoId);
        Assert.True(challenge.DifficultyScore >= 70.0);
        Assert.NotEmpty(challenge.CategoryBadge);
    }

    [Fact]
    public void PostFlightSummaryGenerator_TracksExtremesAndBuildsShareableMarkdownCard()
    {
        var gen = new PostFlightSummaryGenerator();
        var depState = new WeatherState
        {
            StationId = "EGLL",
            WindSpeedKnots = 24,
            WindGustKnots = 36,
            VisibilityMeters = 4500,
            AltimeterHpa = 998.0,
            TurbulenceIndex = 0.45,
            IcingIndex = 0.30,
            StructuralIcePercent = 8.5,
            ThunderstormIntensity = 0.20
        };

        var arrState = new WeatherState
        {
            StationId = "LOWI",
            WindSpeedKnots = 32,
            WindGustKnots = 48,
            VisibilityMeters = 1800,
            AltimeterHpa = 1018.0,
            TurbulenceIndex = 0.72,
            IcingIndex = 0.55,
            StructuralIcePercent = 19.2,
            ThunderstormIntensity = 0.65
        };

        gen.RecordSnapshot(depState, wakeEncounterActive: true);
        gen.RecordSnapshot(arrState, wakeEncounterActive: false);

        var card = gen.GenerateReport();
        Assert.Equal("EGLL", card.DepartureStation);
        Assert.Equal("LOWI", card.ArrivalStation);
        Assert.Equal(32.0, card.PeakWindKnots);
        Assert.Equal(48.0, card.PeakGustKnots);
        Assert.Equal(1800.0, card.MinimumVisibilityMeters);
        Assert.Equal(19.2, card.PeakStructuralIcePercent);
        Assert.Equal(1, card.WakeVortexEncounters);
        Assert.Contains("SkyWeave v0.8.0", card.ShareableMarkdownCard);
        Assert.Contains("EGLL ➔ LOWI", card.ShareableMarkdownCard);
    }
}
