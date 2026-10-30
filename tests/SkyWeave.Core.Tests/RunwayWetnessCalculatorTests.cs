using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class RunwayWetnessCalculatorTests
{
    private static WindsAloftData WindsWithHistory(params (double PrecipMm, double TempC, double WindKt, double SolarWm2)[] samples)
    {
        var now = DateTime.UtcNow;
        var data = new WindsAloftData { Latitude = 40.6, Longitude = -73.8 };
        for (var i = 0; i < samples.Length; i++)
        {
            var s = samples[i];
            data.Past12HourSamples.Add(new HourlySurfaceSample(
                now.AddHours(-samples.Length + i),
                s.PrecipMm,
                s.TempC,
                s.WindKt,
                s.SolarWm2));
            data.Past12HourPrecipitationMm += s.PrecipMm;
        }
        return data;
    }

    [Fact]
    public void CalculateSurfaceState_NoRain_ReturnsDry()
    {
        var metar = new MetarData { RawText = "KJFK 301151Z 21010KT 10SM CLR 18/10 A3001" };

        var report = RunwayWetnessCalculator.CalculateSurfaceState(metar, null, 0, 18, 10, 10);

        Assert.Equal(RunwaySurfaceCondition.Dry, report.Condition);
        Assert.Equal(6, report.FaaConditionCode);
        Assert.Equal(0, report.WetnessRatio);
        Assert.Equal(0, report.EffectivePrecipitationRateMmHr);
    }

    [Fact]
    public void CalculateSurfaceState_HeavyRainInLast12Hours_KeepsSurfaceWetAfterRainStops()
    {
        // 6 mm fell 8 hours ago; nothing since. Clear sky, cool, light wind.
        var history = new List<(double, double, double, double)>();
        for (var i = 0; i < 12; i++)
            history.Add(i == 4 ? (6.0, 9.0, 5.0, 0.0) : (0.0, 9.0, 5.0, 0.0));
        var winds = WindsWithHistory(history.ToArray());
        var metar = new MetarData { RawText = "KJFK 301151Z 21005KT 10SM CLR 09/07 A3001" };

        var report = RunwayWetnessCalculator.CalculateSurfaceState(metar, winds, 0, 9, 7, 5);

        Assert.True(report.WetnessRatio > 0.05, $"expected residual wetness, got {report.WetnessRatio}");
        Assert.NotEqual(RunwaySurfaceCondition.Dry, report.Condition);
        Assert.True(report.EstimatedDryingTimeHours > 0);
        // Residual wetness must survive with no active precipitation.
        Assert.Equal(0, report.ActivePrecipitationRateMmHr);
        Assert.True(report.EffectivePrecipitationRateMmHr > 0);
        Assert.True(report.EffectivePrecipitationRateMmHr < 0.5, "residual floor must not render as a rain shaft");
    }

    [Fact]
    public void CalculateSurfaceState_SnowBelowFreezing_ReturnsIcyOrSlush()
    {
        var winds = WindsWithHistory((1.0, -6.0, 12.0, 0.0));
        var metar = new MetarData { RawText = "KJFK 301151Z 33012KT 1SM SN M06/M09 A2985" };

        var report = RunwayWetnessCalculator.CalculateSurfaceState(metar, winds, 1.0, -6, -9, 12);

        Assert.True(report.Condition is RunwaySurfaceCondition.Icy or RunwaySurfaceCondition.SnowOrSlush);
        Assert.True(report.FaaConditionCode <= 2);
        Assert.True(report.WetnessRatio >= 0.75);
    }

    [Fact]
    public void CalculateSurfaceState_HotSunnyWindy_DriesFasterThanColdCalm()
    {
        var history = WindsWithHistory((2.0, 15.0, 5.0, 200.0));
        var metar = new MetarData { RawText = "KJFK 301151Z 21005KT 10SM CLR 20/05 A3001" };

        var hotDry = RunwayWetnessCalculator.CalculateSurfaceState(metar, history, 0, 32, 8, 25);
        var coldCalm = RunwayWetnessCalculator.CalculateSurfaceState(metar, history, 0, 2, 1, 2);

        Assert.True(hotDry.EvaporationRateMmHr > coldCalm.EvaporationRateMmHr);
        Assert.True(hotDry.EstimatedDryingTimeHours <= coldCalm.EstimatedDryingTimeHours);
        Assert.True(hotDry.WetnessRatio <= coldCalm.WetnessRatio);
    }

    [Fact]
    public void CalculateSurfaceState_ActiveHeavyRain_ReturnsWetOrStandingWater()
    {
        var metar = new MetarData { RawText = "KJFK 301151Z 12015KT 2SM +RA BR OVC008 16/15 A2990" };

        var report = RunwayWetnessCalculator.CalculateSurfaceState(metar, null, 9.0, 16, 15, 15);

        Assert.True(report.Condition is RunwaySurfaceCondition.Wet or RunwaySurfaceCondition.StandingWater);
        Assert.Equal(report.ActivePrecipitationRateMmHr, report.EffectivePrecipitationRateMmHr, 3);
        Assert.InRange(report.WetnessRatio, 0.0, 1.0);
    }

    [Theory]
    [InlineData(-40, -40, 0, 0)]
    [InlineData(55, -30, 200, 2000)]
    [InlineData(20, 5, 20, 500)]
    public void CalculateEvaporationRate_AlwaysWithinPhysicalBounds(double temp, double dew, double wind, double solar)
    {
        var evap = RunwayWetnessCalculator.CalculateEvaporationRateMmHr(temp, dew, wind, solar);

        Assert.InRange(evap, 0.03, 1.80);
    }

    [Fact]
    public void BuildWeatherState_PopulatesRunwayWetnessFromHistory()
    {
        var history = WindsWithHistory((5.0, 8.0, 6.0, 0.0));
        var metar = new MetarData
        {
            StationId = "KSEA", ObservationTime = DateTime.UtcNow, RawText = "KSEA 301153Z 21006KT 10SM CLR 08/06 A3001",
            TemperatureCelsius = 8, DewpointCelsius = 6, AltimeterHpa = 1013.2,
            WindDirectionDegrees = 210, WindSpeedKnots = 6, VisibilityMeters = 10000, FlightCategory = "VFR"
        };
        var winds = new WindLayerBuilder().BuildWindLayers(metar, null);
        var data = new PipelineData(metar, history, new List<CloudLayer>(), winds, new(), new(), new(), new(), new(), 0, null, null);
        var pipeline = new WeatherPipeline(null!, null!, null!, null!, null!, null!, null!,
            new CloudLayerBuilder(), new WindLayerBuilder(), new IcingCalculator(),
            new TurbulenceCalculator(), new StormModeler(), new WakeTurbulenceEngine(),
            null!, new HazardAggregator());

        var state = pipeline.BuildWeatherState(data, 47.45, -122.31, 0);

        Assert.True(state.Past12HourRainfallMm > 0);
        Assert.True(state.RunwayWetnessRatio > 0);
        Assert.False(string.IsNullOrWhiteSpace(state.RunwaySurfaceSummary));
        Assert.NotNull(state.RunwayReport?.SurfaceCondition);
    }

    [Fact]
    public void SmoothingPipeline_PreservesRunwayWetnessFields()
    {
        var from = new WeatherState
        {
            RunwayWetnessRatio = 0.6, Past12HourRainfallMm = 4, EstimatedDryingTimeHours = 6, RunwaySurfaceSummary = "WET (CC 5)"
        };
        var to = new WeatherState
        {
            RunwayWetnessRatio = 0.2, Past12HourRainfallMm = 12.4,
            EstimatedDryingTimeHours = 3.5, RunwaySurfaceSummary = "DAMP (CC 5)"
        };
        var smoothing = new SmoothingPipeline();
        smoothing.SnapToState(from);
        smoothing.SetTarget(to);

        var midTransition = smoothing.GetCurrentState();
        smoothing.ForceImmediate();
        var settled = smoothing.GetCurrentState();

        // Interpolate must carry the fields instead of dropping them to 0.
        Assert.InRange(midTransition.RunwayWetnessRatio, 0.2, 0.6);
        Assert.InRange(midTransition.EstimatedDryingTimeHours, 3.5, 6.0);
        Assert.Equal(0.2, settled.RunwayWetnessRatio, 3);
        Assert.Equal(12.4, settled.Past12HourRainfallMm);
        Assert.Equal("DAMP (CC 5)", settled.RunwaySurfaceSummary);
    }
}
