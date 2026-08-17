using System.Text.Json;
using SkyWeave.Core.Fetchers;
using Xunit;

namespace SkyWeave.Core.Tests;

public class WindsAloftFetcherTests
{
    private static readonly string NowTime = DateTime.UtcNow.ToString("o");

    private readonly WindsAloftFetcher _fetcher = new(new HttpClient());

    private static JsonElement BuildResponse(bool includeOptional = true, double geopotentialHeight = 1400.0)
    {
        var hourly = new Dictionary<string, object>
        {
            ["time"] = new[] { NowTime },
            ["temperature_850hPa"] = new[] { 5.0 },
            ["wind_speed_850hPa"] = new[] { 30.0 },
            ["wind_direction_850hPa"] = new[] { 270.0 },
            ["relative_humidity_850hPa"] = new[] { 60.0 },
            ["cloud_cover_850hPa"] = new[] { 75.0 },
            ["geopotential_height_850hPa"] = new[] { geopotentialHeight }
        };

        if (includeOptional)
        {
            hourly["cape"] = new[] { 2500.0 };
            hourly["lifted_index"] = new[] { -3.0 };
            hourly["freezing_level_height"] = new[] { 3000.0 };
        }

        var root = new Dictionary<string, object>
        {
            ["hourly"] = hourly,
            ["pressure_levels"] = new[] { 850 },
            ["model_run"] = DateTime.UtcNow.AddMinutes(-30).ToString("o")
        };

        return JsonSerializer.SerializeToElement(root);
    }

    [Theory]
    [InlineData(41.98, -87.90, "HRRR")]
    [InlineData(51.47, -0.45, "ICON")]
    [InlineData(-33.95, 151.18, "GFS")]
    [InlineData(39.86, -104.67, "HRRR")]
    public void SelectRegionalModel_ReturnsExpectedModel(double latitude, double longitude, string expectedPrefix)
    {
        var model = _fetcher.SelectRegionalModel(latitude, longitude);
        Assert.StartsWith(expectedPrefix, model);
    }

    [Fact]
    public void ParseWindsAloftResponse_ParsesPressureLevelFields()
    {
        var data = WindsAloftFetcher.ParseWindsAloftResponse(BuildResponse(), 40.0, -74.0, "HRRR CONUS 3 km");

        Assert.Single(data.PressureLevels);
        var level = data.PressureLevels[0];
        Assert.Equal(850, level.PressureHpa);
        Assert.Equal(1400.0 * 3.28084, level.AltitudeFeet, 1);
        Assert.Equal(5.0, level.TemperatureCelsius);
        Assert.Equal(30.0, level.WindSpeedKnots);
        Assert.Equal(75.0, level.CloudCoverPercent);
        Assert.Equal(1400.0, level.GeopotentialHeightMeters);
    }

    [Fact]
    public void ParseWindsAloftResponse_ParsesConvectiveFields()
    {
        var data = WindsAloftFetcher.ParseWindsAloftResponse(BuildResponse(), 40.0, -74.0, "HRRR CONUS 3 km");

        Assert.Equal(2500.0, data.ConvectiveAvailablePotentialEnergy);
        Assert.Equal(-3.0, data.LiftedIndex);
        Assert.Equal(3000.0, data.FreezingLevelHeightMeters);
        Assert.Equal("HRRR CONUS 3 km", data.SourceModel);
    }

    [Fact]
    public void ParseWindsAloftResponse_ComputesDataAgeFromModelRun()
    {
        var data = WindsAloftFetcher.ParseWindsAloftResponse(BuildResponse(), 40.0, -74.0, "HRRR CONUS 3 km");

        Assert.InRange(data.DataAgeMinutes, 20, 45);
    }

    [Fact]
    public void ParseWindsAloftResponse_NullArrayElements_DoNotThrow()
    {
        var hourly = new Dictionary<string, object>
        {
            ["time"] = new[] { NowTime },
            ["temperature_850hPa"] = new object[] { null! },
            ["wind_speed_850hPa"] = new[] { 30.0 },
            ["wind_direction_850hPa"] = new[] { 270.0 }
        };
        var root = new Dictionary<string, object>
        {
            ["hourly"] = hourly,
            ["pressure_levels"] = new[] { 850 }
        };

        var data = WindsAloftFetcher.ParseWindsAloftResponse(JsonSerializer.SerializeToElement(root), 40.0, -74.0, "HRRR CONUS 3 km");

        Assert.Single(data.PressureLevels);
        Assert.Equal(0.0, data.PressureLevels[0].TemperatureCelsius);
        Assert.Equal(270.0, data.PressureLevels[0].WindDirectionDegrees);
        Assert.Equal(30.0, data.PressureLevels[0].WindSpeedKnots);
    }

    [Fact]
    public void ParseWindsAloftResponse_MissingOptionalFields_ReturnsNullsWithoutException()
    {
        var data = WindsAloftFetcher.ParseWindsAloftResponse(BuildResponse(includeOptional: false), 40.0, -74.0, "HRRR CONUS 3 km");

        Assert.Null(data.ConvectiveAvailablePotentialEnergy);
        Assert.Null(data.LiftedIndex);
        Assert.Null(data.FreezingLevelHeightMeters);
    }

    [Fact]
    public void ParseWindsAloftResponse_ZeroGeopotentialHeight_FallsBackToApproximateAltitude()
    {
        var data = WindsAloftFetcher.ParseWindsAloftResponse(BuildResponse(geopotentialHeight: 0.0), 40.0, -74.0, "HRRR CONUS 3 km");

        Assert.Single(data.PressureLevels);
        Assert.Equal(850, data.PressureLevels[0].PressureHpa);
        Assert.Equal(1500.0, data.PressureLevels[0].AltitudeMeters);
    }
}