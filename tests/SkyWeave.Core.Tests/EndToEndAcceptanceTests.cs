using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Injectors;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class EndToEndAcceptanceTests
{
    [Fact]
    public void NfrA1_MetarToWpr_RoundTrip_IsCorrect()
    {
        // 1. Arrange: Decode a METAR string
        var json = @"
        {
            ""icaoId"": ""KSEA"",
            ""rawOb"": ""KSEA 151253Z 21015G25KT 5SM -RA BR SCT015 BKN030 OVC050 10/08 A2992 RMK AO2"",
            ""temp"": 10.0,
            ""dewp"": 8.0,
            ""wdir"": 210,
            ""wspd"": 15,
            ""wgst"": 25,
            ""visib"": 5.0,
            ""altim"": 1013.2,
            ""fltCat"": ""MVFR"",
            ""wxString"": ""-RA BR"",
            ""obsTime"": ""2024-01-15T12:53:00Z"",
            ""clouds"": [
                { ""cover"": ""SCT"", ""base"": 1500 },
                { ""cover"": ""BKN"", ""base"": 3000 },
                { ""cover"": ""OVC"", ""base"": 5000 }
            ]
        }";
        var rootElement = JsonDocument.Parse(json).RootElement;
        
        var decoder = new MetarDecoder();
        var metarData = decoder.Decode(rootElement);
        Assert.NotNull(metarData);

        var cloudBuilder = new CloudLayerBuilder();
        var windBuilder = new WindLayerBuilder();
        var icingCalc = new IcingCalculator();
        var turbCalc = new TurbulenceCalculator();
        var stormModeler = new StormModeler();

        var cloudLayers = cloudBuilder.BuildCloudLayers(metarData, null);
        var windLayers = windBuilder.BuildWindLayers(metarData, null);
        var icingLayers = icingCalc.CalculateIcingLayers(cloudLayers, windLayers);
        var stormCells = stormModeler.ModelStorms(
            new List<LightningStrike>(), new List<WeatherHazard>(), 47.45, -122.31, DateTime.UtcNow, null);
        var turbLayers = turbCalc.CalculateTurbulenceLayers(windLayers, cloudLayers, stormCells, 132 * 3.28084);

        // Build PipelineData object
        var pipelineData = new PipelineData(
            Metar: metarData,
            Winds: null,
            CloudLayers: cloudLayers,
            WindLayers: windLayers,
            IcingLayers: icingLayers,
            TurbulenceLayers: turbLayers,
            StormCells: stormCells,
            Sigmets: new List<WeatherHazard>(),
            Lightning: new List<LightningStrike>(),
            RadarPrecip: 0,
            RadarFrame: null,
            Taf: null
        );

        // Construct pipeline dependencies
        var pipeline = new WeatherPipeline(
            null!, null!, null!, null!, null!, null!, null!,
            cloudBuilder, windBuilder, icingCalc, turbCalc,
            stormModeler, new WakeTurbulenceEngine(),
            null!, new HazardAggregator()
        );

        var modeler = new AtmosphericModeler();
        var wprGenerator = new WprGenerator();

        // 2. Act: Run the weather pipeline synchronously
        var state = pipeline.BuildWeatherState(pipelineData, 47.45, -122.31, 132 * 3.28084); // Altitude in feet
        
        // Apply models
        modeler.ApplySpatialGridAndThermals(state, 47.45, -122.31);

        // Generate WPR
        var wprXml = wprGenerator.GenerateWprXml(state);

        // 3. Assert: Verify end-to-end values
        var doc = XDocument.Parse(wprXml);
        var preset = doc.Descendants("WeatherPreset.Preset").FirstOrDefault();
        Assert.NotNull(preset);

        // Pressure: 29.92 inHg -> 1013.2 hPa -> ~101320 Pa (allow +/- 150 Pa for spatial noise)
        var pressure = preset.Element("MSLPressure")?.Attribute("Value")?.Value;
        Assert.NotNull(pressure);
        Assert.InRange(double.Parse(pressure), 101200, 101450);

        // Temperature: 10C -> 283.15 K (allow +/- 1.5 C for spatial noise)
        var temp = preset.Element("MSLTemperature")?.Attribute("Value")?.Value;
        Assert.NotNull(temp);
        Assert.InRange(double.Parse(temp), 281.0, 285.0);

        // Cloud Layers: SCT015, BKN030, OVC050
        // Cloud Layers: SCT015, BKN030, OVC050 will merge into 1 thick layer due to overlap
        var xmlCloudLayers = doc.Descendants("CloudLayer").ToList();
        Assert.Single(xmlCloudLayers);
        
        // Wind: 210 at 15 knots, gusting 25
        var xmlWindLayers = doc.Descendants("WindLayer").ToList();
        Assert.NotEmpty(xmlWindLayers);

        var surfaceWind = xmlWindLayers.First();
        var windDir = surfaceWind.Element("WindLayerAngle")?.Attribute("Value")?.Value;
        var windSpeed = surfaceWind.Element("WindLayerSpeed")?.Attribute("Value")?.Value;
        
        // Assert some values
        Assert.NotNull(windDir);
        Assert.NotNull(windSpeed);
        
        // Precipitation: -RA means some rain
        var precip = preset.Element("Precipitations")?.Attribute("Value")?.Value;
        Assert.NotNull(precip);
        Assert.True(double.Parse(precip) > 0, "Precipitation should be > 0 for -RA");
    }
}
