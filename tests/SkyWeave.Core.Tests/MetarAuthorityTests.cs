using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using SkyWeave.Core.Injectors;
using System.Xml.Linq;
using Xunit;

namespace SkyWeave.Core.Tests;

public class MetarAuthorityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(25.0)]
    public void WprTurbulenceBoost_DoesNotChangeSurfaceGust(double? gust)
    {
        var state = new WeatherState
        {
            WindsAloft = new() { new() { AltitudeFeet = 0, SpeedKnots = 15,
                DirectionDegrees = 210, GustSpeedKnots = gust } }
        };
        var xml = XDocument.Parse(new WprGenerator().GenerateWprXml(state, 20));
        var wind = Assert.Single(xml.Descendants("WindLayer"));
        if (gust.HasValue)
        {
            Assert.Equal("25", wind.Element("GustWave")?.Element("GustWaveSpeed")?.Attribute("Value")?.Value);
            Assert.Equal("210", wind.Element("GustWave")?.Element("GustAngle")?.Attribute("Value")?.Value);
        }
        else
        {
            Assert.Null(wind.Element("GustWave"));
        }
    }

    [Theory]
    [InlineData("BECMG")]
    [InlineData("TEMPO")]
    public void ActiveForecast_DoesNotOverrideObservedConditions(string changeType)
    {
        var now = DateTime.UtcNow;
        var from = now.AddHours(-1);
        var to = now.AddHours(1);
        var taf = new TafData
        {
            RawText = $"TAF KSEA {now:ddHHmm}Z {from:ddHH}/{to:ddHH} 21015KT 9999 BKN030 {changeType} {from:ddHH}/{to:ddHH} 09030KT 1SM OVC002"
        };
        // Ensure this fixture exercises an active conflicting forecast, including month rollover.
        var group = Assert.Single(new TafDecoder().DecodeChangeGroups(taf.RawText));
        Assert.True(group.ValidFrom <= now && group.ValidTo > now);
        Assert.NotNull(group.VisibilityMeters);
        Assert.True(group.VisibilityMeters < 2000);

        var metar = new MetarData
        {
            StationId = "KSEA", ObservationTime = now, RawText = "KSEA observed",
            TemperatureCelsius = 10, DewpointCelsius = 8, AltimeterHpa = 1013.2,
            WindDirectionDegrees = 210, WindSpeedKnots = 15, WindGustKnots = 25,
            VisibilityMeters = 10000, FlightCategory = "MVFR"
        };
        var clouds = new List<CloudLayer> { new() { BaseFeetAgl = 3000, CoveragePercent = 0.7 } };
        var winds = new WindLayerBuilder().BuildWindLayers(metar, null);
        var data = new PipelineData(metar, null, clouds, winds, new(), new(), new(), new(), new(), 0, null, taf);
        var pipeline = new WeatherPipeline(null!, null!, null!, null!, null!, null!, null!,
            new CloudLayerBuilder(), new WindLayerBuilder(), new IcingCalculator(),
            new TurbulenceCalculator(), new StormModeler(), new WakeTurbulenceEngine(),
            null!, new HazardAggregator());

        var state = pipeline.BuildWeatherState(data, 47.45, -122.31, 0);

        Assert.Equal(metar.VisibilityMeters, state.VisibilityMeters);
        Assert.Equal(metar.WindDirectionDegrees, state.WindDirectionDegrees);
        Assert.Equal(metar.WindSpeedKnots, state.WindSpeedKnots);
        Assert.Equal(metar.WindGustKnots, state.WindGustKnots);
        Assert.Equal(metar.FlightCategory, state.FlightCategory);
        Assert.Same(clouds, state.CloudLayers);
        Assert.Same(taf, state.Taf);
        Assert.Equal(metar.RawText, state.RawMetar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(25.0)]
    public void AtmosphericModeling_PreservesObservedSurfaceIncludingAbsentGust(double? gust)
    {
        // Position the fixture at local solar noon regardless of the test runner's UTC time.
        var longitude = (14 - DateTime.UtcNow.TimeOfDay.TotalHours) * 15;
        var state = new WeatherState
        {
            TemperatureCelsius = 10, PressureHpa = 1013.2, AltimeterHpa = 1013.2,
            WindSpeedKnots = 15, WindDirectionDegrees = 210, WindGustKnots = gust,
            WindsAloft = new() { new() { AltitudeFeet = 0, SpeedKnots = 15, GustSpeedKnots = gust } }
        };

        new AtmosphericModeler().ApplySpatialGridAndThermals(state, 47.45, longitude);

        Assert.Equal(10, state.TemperatureCelsius);
        Assert.Equal(1013.2, state.PressureHpa);
        Assert.Equal(1013.2, state.AltimeterHpa);
        Assert.Equal(15, state.WindSpeedKnots);
        Assert.Equal(210, state.WindDirectionDegrees);
        Assert.Equal(gust, state.WindGustKnots);
        Assert.Equal(gust, Assert.Single(state.WindsAloft).GustSpeedKnots);
    }
}
