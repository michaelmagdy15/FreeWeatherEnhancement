using System.Text.Json;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class MetarDecoderTests
{
    private readonly MetarDecoder _decoder = new();

    private JsonElement CreateMetarJson(string icao = "KJFK", double temp = 20.0, double dewp = 10.0,
        int wdir = 270, double wspd = 12.0, string flightCat = "VFR", string visib = "10")
    {
        var json = $@"
        {{
            ""icaoId"": ""{icao}"",
            ""rawOb"": ""{icao} 141751Z {wdir:000}{wspd:000}KT {visib} {flightCat}"",
            ""temp"": {temp},
            ""dewp"": {dewp},
            ""wdir"": {wdir},
            ""wspd"": {wspd},
            ""visib"": {visib},
            ""altim"": 1013.25,
            ""fltCat"": ""{flightCat}"",
            ""obsTime"": ""2024-01-15T17:51:00Z"",
            ""clouds"": [
                {{ ""cover"": ""FEW"", ""base"": 50 }},
                {{ ""cover"": ""SCT"", ""base"": 1500 }}
            ]
        }}";
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void Decode_ReturnsCorrectStationId()
    {
        var metar = _decoder.Decode(CreateMetarJson(icao: "EGLL"));
        Assert.Equal("EGLL", metar.StationId);
    }

    [Fact]
    public void Decode_ReturnsCorrectTemperature()
    {
        var metar = _decoder.Decode(CreateMetarJson(temp: 25.5));
        Assert.Equal(25.5, metar.TemperatureCelsius);
    }

    [Fact]
    public void Decode_ReturnsCorrectDewpoint()
    {
        var metar = _decoder.Decode(CreateMetarJson(dewp: 8.3));
        Assert.Equal(8.3, metar.DewpointCelsius);
    }

    [Fact]
    public void Decode_ReturnsCorrectWindDirection()
    {
        var metar = _decoder.Decode(CreateMetarJson(wdir: 180));
        Assert.Equal(180, metar.WindDirectionDegrees);
    }

    [Fact]
    public void Decode_ReturnsCorrectWindSpeed()
    {
        var metar = _decoder.Decode(CreateMetarJson(wspd: 25.0));
        Assert.Equal(25.0, metar.WindSpeedKnots);
    }

    [Fact]
    public void Decode_ReturnsFlightCategory()
    {
        var metar = _decoder.Decode(CreateMetarJson(flightCat: "IFR"));
        Assert.Equal("IFR", metar.FlightCategory);
    }

    [Fact]
    public void Decode_ParsesCloudLayers()
    {
        var metar = _decoder.Decode(CreateMetarJson());
        Assert.Equal(2, metar.Clouds.Count);
        Assert.Equal("FEW", metar.Clouds[0].Coverage);
        Assert.Equal(50, metar.Clouds[0].BaseFeet);
        Assert.Equal("SCT", metar.Clouds[1].Coverage);
        Assert.Equal(1500, metar.Clouds[1].BaseFeet);
    }

    [Fact]
    public void Decode_SetsAltimeterPressure()
    {
        var metar = _decoder.Decode(CreateMetarJson());
        Assert.Equal(1013.25, metar.AltimeterHpa);
    }

    [Fact]
    public void Decode_HandlesMissingClouds()
    {
        var json = @"{ ""icaoId"": ""KJFK"", ""rawOb"": ""test"", ""temp"": 20, ""dewp"": 10, ""wdir"": 270, ""wspd"": 12, ""visib"": ""10"", ""altim"": 1013.25, ""fltCat"": ""VFR"" }";
        var element = JsonDocument.Parse(json).RootElement;
        var metar = _decoder.Decode(element);
        Assert.Empty(metar.Clouds);
    }

    [Fact]
    public void Decode_HandlesNullWindGust()
    {
        var json = @"{ ""icaoId"": ""KJFK"", ""rawOb"": ""test"", ""temp"": 20, ""dewp"": 10, ""wdir"": 270, ""wspd"": 12, ""visib"": ""10"", ""altim"": 1013.25, ""fltCat"": ""VFR"", ""wgst"": null }";
        var element = JsonDocument.Parse(json).RootElement;
        var metar = _decoder.Decode(element);
        Assert.Null(metar.WindGustKnots);
    }

    [Fact]
    public void Decode_HandlesUnixEpochObservationTime()
    {
        var json = @"{ ""icaoId"": ""KJFK"", ""rawOb"": ""test"", ""temp"": 20, ""dewp"": 10, ""wdir"": 270, ""wspd"": 12, ""visib"": ""10"", ""altim"": 1013.25, ""fltCat"": ""VFR"", ""obsTime"": 1705333860 }";
        var element = JsonDocument.Parse(json).RootElement;
        var metar = _decoder.Decode(element);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1705333860).UtcDateTime, metar.ObservationTime);
    }

    [Fact]
    public void Decode_SetsObservationTime()
    {
        var metar = _decoder.Decode(CreateMetarJson());
        Assert.Equal(DateTimeOffset.Parse("2024-01-15T17:51:00Z").UtcDateTime, metar.ObservationTime);
    }

    [Fact]
    public void Decode_CalculatesVisibilityInMeters()
    {
        var metar = _decoder.Decode(CreateMetarJson(visib: "10"));
        Assert.Equal(16093.44, metar.VisibilityMeters, precision: 2);
    }

    [Fact]
    public void Decode_ParsesWeatherConditionsFromJson()
    {
        var json = @"{ ""icaoId"": ""EGLL"", ""rawOb"": ""EGLL 231200Z -RA BKN015"", ""wxString"": ""-RA BR"", ""temp"": 15, ""dewp"": 12, ""wdir"": 210, ""wspd"": 15, ""visib"": ""6"", ""altim"": 1012, ""fltCat"": ""MVFR"" }";
        var element = JsonDocument.Parse(json).RootElement;
        var metar = _decoder.Decode(element);

        Assert.Contains("-RA", metar.WeatherConditions);
        Assert.Contains("BR", metar.WeatherConditions);
    }

    [Fact]
    public void DecodeRaw_ParsesWeatherPhenomenaAndFractionalVisibility()
    {
        var raw = "KJFK 231451Z 04015G25KT 1/2SM +TSRA FG BKN008 OVC015CB 18/17 A2980";
        var metar = _decoder.DecodeRaw(raw);

        Assert.Equal("KJFK", metar.StationId);
        Assert.Equal(18, metar.TemperatureCelsius);
        Assert.Equal(17, metar.DewpointCelsius);
        Assert.Equal(40, metar.WindDirectionDegrees);
        Assert.Equal(15, metar.WindSpeedKnots);
        Assert.Equal(25, metar.WindGustKnots);
        Assert.Equal(0.5 * 1609.344, metar.VisibilityMeters, precision: 1);
        Assert.Contains("+TSRA", metar.WeatherConditions);
        Assert.Contains("FG", metar.WeatherConditions);
    }
}
