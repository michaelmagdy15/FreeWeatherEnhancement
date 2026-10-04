using System.Text.Json;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class FmcWindExporterTests
{
    private static SimBriefPlan CreateSamplePlan()
    {
        return new SimBriefPlan
        {
            Origin = "EGLL",
            Destination = "KJFK",
            FlightNumber = "BAW177",
            AircraftType = "B772",
            CruiseAltitudeFt = 36000,
            Waypoints = new List<SimBriefWaypoint>
            {
                new()
                {
                    Identifier = "CPT",
                    Latitude = 51.49,
                    Longitude = -1.22,
                    AltitudeFt = 10000,
                    WindDirection = 260,
                    WindSpeedKt = 25,
                    TemperatureC = -5,
                    Stage = WaypointStage.Climb
                },
                new()
                {
                    Identifier = "KENET",
                    Latitude = 51.52,
                    Longitude = -1.82,
                    AltitudeFt = 36000,
                    WindDirection = 275,
                    WindSpeedKt = 65,
                    TemperatureC = -52,
                    Stage = WaypointStage.Cruise
                },
                new()
                {
                    Identifier = "BOMBI",
                    Latitude = 51.60,
                    Longitude = -5.00,
                    AltitudeFt = 36000,
                    WindDirection = 280,
                    WindSpeedKt = 75,
                    TemperatureC = -54,
                    Stage = WaypointStage.Cruise
                },
                new()
                {
                    Identifier = "ROBER",
                    Latitude = 40.75,
                    Longitude = -73.10,
                    AltitudeFt = 10000,
                    WindDirection = 240,
                    WindSpeedKt = 30,
                    TemperatureC = -2,
                    Stage = WaypointStage.Descent
                },
                new()
                {
                    Identifier = "CAMRN",
                    Latitude = 40.60,
                    Longitude = -73.60,
                    AltitudeFt = 4000,
                    WindDirection = 230,
                    WindSpeedKt = 18,
                    TemperatureC = 5,
                    Stage = WaypointStage.Descent
                }
            }
        };
    }

    [Fact]
    public void GeneratePmdgWindFile_GeneratesValidFormat()
    {
        var plan = CreateSamplePlan();
        var wx = FmcWindExporter.GeneratePmdgWindFile(plan);

        Assert.Contains("WXR", wx);
        Assert.Contains("EGLLKJFK01", wx);
        Assert.Contains("KENET", wx);
        Assert.Contains("360", wx);
        Assert.Contains("275/065", wx);
        Assert.Contains("-52", wx);
        Assert.Contains("DESCENT", wx);
        Assert.Contains("FL100 240/030", wx);
    }

    [Fact]
    public void GenerateFenixJson_ProducesValidJson()
    {
        var plan = CreateSamplePlan();
        var json = FmcWindExporter.GenerateFenixJson(plan);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("EGLL", root.GetProperty("origin").GetString());
        Assert.Equal("KJFK", root.GetProperty("destination").GetString());
        Assert.Equal(36000, root.GetProperty("cruiseAltitudeFt").GetDouble());

        var waypoints = root.GetProperty("waypoints");
        Assert.Equal(5, waypoints.GetArrayLength());
        Assert.Equal("KENET", waypoints[1].GetProperty("ident").GetString());
        Assert.Equal(65, waypoints[1].GetProperty("windSpdKt").GetDouble());
    }

    [Fact]
    public void GenerateCsv_ProducesValidCsv()
    {
        var plan = CreateSamplePlan();
        var csv = FmcWindExporter.GenerateCsv(plan);

        Assert.StartsWith("Identifier,Latitude,Longitude,AltitudeFt,WindDirectionDeg,WindSpeedKt,TemperatureC,Stage", csv);
        Assert.Contains("KENET,51.5200,-1.8200,36000,275,65,-52.0,Cruise", csv);
    }

    [Fact]
    public void WritePmdgWindFile_WritesFileSuccessfully()
    {
        var plan = CreateSamplePlan();
        var tempDir = Path.Combine(Path.GetTempPath(), "SkyWeave_FmcTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var filePath = FmcWindExporter.WritePmdgWindFile(plan, tempDir);

            Assert.True(File.Exists(filePath));
            Assert.EndsWith("EGLLKJFK01.wx", filePath);

            var readBack = File.ReadAllText(filePath);
            Assert.Contains("EGLLKJFK01", readBack);
            Assert.Contains("KENET", readBack);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
