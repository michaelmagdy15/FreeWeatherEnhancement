using System.Text.Json;
using SkyWeave.Api;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Api.Tests;

public class WeatherDataProviderTests
{
    private sealed class FakeProvider : IWeatherDataProvider
    {
        public int Calls { get; private set; }

        public Task<WeatherState?> GetStateAsync(double latitude, double longitude)
        {
            Calls++;
            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = "KJFK",
                Latitude = latitude,
                Longitude = longitude,
                TemperatureCelsius = 27.2,
                DewpointCelsius = 21.1,
                WindDirectionDegrees = 40,
                WindSpeedKnots = 9,
                FlightCategory = "VFR",
                VisibilityMeters = 16093.44,
                AltimeterHpa = 1013.25,
                SourceModelName = "HRRR CONUS 3 km",
                Taf = new SkyWeave.Core.Decoders.TafData { StationId = "KJFK", RawText = "TAF KJFK ..." }
            });
        }

        public Task<MetarData?> GetMetarAsync(double latitude, double longitude)
        {
            Calls++;
            return Task.FromResult<MetarData?>(new MetarData
            {
                StationId = "KJFK",
                TemperatureCelsius = 27.2,
                FlightCategory = "VFR"
            });
        }

        public Task<List<WeatherHazard>> GetHazardsAsync(double latitude, double longitude)
        {
            Calls++;
            return Task.FromResult(new List<WeatherHazard>
            {
                new() { Type = HazardType.ConvectiveSigmet, Description = "Area of embedded thunderstorms", Severity = 0.7 }
            });
        }
    }

    [Fact]
    public async Task GetStateAsync_ReturnsStateWithPositionAndTaf()
    {
        var provider = new FakeProvider();
        var state = await provider.GetStateAsync(40.6399, -73.7787);

        Assert.NotNull(state);
        Assert.Equal("KJFK", state!.StationId);
        Assert.Equal(40.6399, state.Latitude);
        Assert.Equal("VFR", state.FlightCategory);
        Assert.NotNull(state.Taf);
    }

    [Fact]
    public async Task GetStateAsync_SerializesToJsonWithPascalCaseFields()
    {
        var provider = new FakeProvider();
        var state = await provider.GetStateAsync(40.6399, -73.7787);
        var json = JsonSerializer.Serialize(state);

        Assert.Contains("\"StationId\"", json);
        Assert.Contains("\"FlightCategory\"", json);
        Assert.Contains("\"Taf\"", json);
    }

    [Fact]
    public async Task GetHazardsAsync_ReturnsList()
    {
        var provider = new FakeProvider();
        var hazards = await provider.GetHazardsAsync(40.6399, -73.7787);

        Assert.Single(hazards);
        Assert.Equal(HazardType.ConvectiveSigmet, hazards[0].Type);
    }
}