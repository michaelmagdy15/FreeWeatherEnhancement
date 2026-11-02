using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class AtmosphereModTests
{
    [Fact]
    public void ComputeOzoneDobsonUnits_MatchesFortuinKelderLatitudinalAndSeasonalCycle()
    {
        // Tropical equator should stay near 250-265 DU year-round
        double tropicalJan = AtmosphereModCalculator.ComputeOzoneDobsonUnits(0.0, 15);
        double tropicalJul = AtmosphereModCalculator.ComputeOzoneDobsonUnits(0.0, 196);
        Assert.InRange(tropicalJan, 245.0, 265.0);
        Assert.InRange(tropicalJul, 250.0, 268.0);

        // Northern high latitude (70N) spring maximum (~March/April > 415 DU) vs autumn minimum (~Oct < 310 DU)
        double arcticSpring = AtmosphereModCalculator.ComputeOzoneDobsonUnits(70.0, 85); // late March
        double arcticAutumn = AtmosphereModCalculator.ComputeOzoneDobsonUnits(70.0, 288); // mid October
        Assert.True(arcticSpring > 415.0, $"Expected Arctic spring ozone > 415 DU, got {arcticSpring:F1}");
        Assert.True(arcticAutumn < 310.0, $"Expected Arctic autumn ozone < 310 DU, got {arcticAutumn:F1}");
        Assert.True(arcticSpring > tropicalJan + 140.0);

        // Southern high latitude (-80S) austral spring ozone hole (~Oct ~200 DU)
        double antarcticOct = AtmosphereModCalculator.ComputeOzoneDobsonUnits(-80.0, 288);
        Assert.InRange(antarcticOct, 198.0, 215.0);
    }

    [Fact]
    public void ClassifyAirMass_OrdersOpticalRegimesByPriorityAndDetectsPrecipAndFog()
    {
        // Clean CAVOK
        var clean = AtmosphereModCalculator.ClassifyAirMass(
            Array.Empty<string>(),
            "KJFK 181200Z 27010KT 9999 FEW040 20/10 Q1018",
            out bool hasPrecip1,
            out bool hasFog1);
        Assert.Equal(AtmosphereAirMass.Continental, clean);
        Assert.False(hasPrecip1);
        Assert.False(hasFog1);

        // Mist / Fog -> Haze + hasMistOrFog
        var fog = AtmosphereModCalculator.ClassifyAirMass(
            new[] { "FG" },
            "EGLL 180600Z 00000KT 0400 FG VV002 08/08 Q1020",
            out bool hasPrecip2,
            out bool hasFog2);
        Assert.Equal(AtmosphereAirMass.Haze, fog);
        Assert.False(hasPrecip2);
        Assert.True(hasFog2);

        // Cairo desert sand/dust -> Dust
        var dust = AtmosphereModCalculator.ClassifyAirMass(
            new[] { "DU", "SA" },
            "HECA 181200Z 22018G28KT 2500 DU SA NSC 31/06 Q1010",
            out bool hasPrecip3,
            out bool hasFog3);
        Assert.Equal(AtmosphereAirMass.Dust, dust);
        Assert.False(hasPrecip3);
        Assert.False(hasFog3);

        // Wildfire smoke -> Smoke
        var smoke = AtmosphereModCalculator.ClassifyAirMass(
            new[] { "FU" },
            "KSFO 181800Z 29012KT 4000 FU SCT050 22/11 A2995",
            out _,
            out _);
        Assert.Equal(AtmosphereAirMass.Smoke, smoke);

        // Volcanic ash overrides Dust and Smoke
        var va = AtmosphereModCalculator.ClassifyAirMass(
            new[] { "FU", "DU", "VA" },
            "LICC 181200Z 31015KT 3000 VA FU DU BKN040 19/12 Q1015",
            out _,
            out _);
        Assert.Equal(AtmosphereAirMass.VolcanicAsh, va);
    }

    [Fact]
    public void ComputeState_DerivesHazeOptics_DryHazeInference_AndRainWashout()
    {
        var utc = new DateTime(2026, 6, 15, 11, 0, 0, DateTimeKind.Utc);

        // 1. Inferred dry haze at Cairo (HECA): restricted visibility (5000m), low RH, no weather code reported
        var dryHazeMetar = new MetarData
        {
            StationId = "HECA",
            RawText = "HECA 151100Z 02010KT 5000 NSC 34/08 Q1011",
            TemperatureCelsius = 34,
            DewpointCelsius = 8,
            VisibilityMeters = 5000
        };
        var dryState = AtmosphereModCalculator.ComputeState(dryHazeMetar, 30.1219, 31.4056, 74.0, utc);
        Assert.True(dryState.InferredDryHaze);
        Assert.Equal(AtmosphereAirMass.Haze, dryState.AirMass);
        Assert.Equal(1.40, dryState.AngstromExponent, 2);
        Assert.Equal(0.10, dryState.BrownCarbonAbsorption, 2);
        Assert.True(dryState.BoundaryLayerHazeMultiplier > 3.0);

        // 2. Dust storm (DU): coarse particles -> low Angstrom (~0.25), high brown carbon (~0.30), elevated T5 (3.5 km)
        var dustMetar = new MetarData
        {
            StationId = "HECA",
            RawText = "HECA 151100Z 24022KT 1800 DU NSC 35/07 Q1008",
            TemperatureCelsius = 35,
            DewpointCelsius = 7,
            VisibilityMeters = 1800,
            WeatherConditions = new List<string> { "DU" }
        };
        var dustState = AtmosphereModCalculator.ComputeState(dustMetar, 30.1219, 31.4056, 74.0, utc);
        Assert.Equal(AtmosphereAirMass.Dust, dustState.AirMass);
        Assert.Equal(0.25, dustState.AngstromExponent, 2);
        Assert.Equal(0.30, dustState.BrownCarbonAbsorption, 2);
        Assert.Equal(0.76, dustState.AerosolAsymmetryG, 2);
        Assert.Equal(3.5, dustState.FreeTroposphereScaleHeightKm, 2);

        // 3. Unrestricted rain washout vs non-precipitating baseline
        var clearMetar = new MetarData
        {
            StationId = "EGLL",
            RawText = "EGLL 151100Z 25012KT 9999 SCT035 16/11 Q1016",
            TemperatureCelsius = 16,
            DewpointCelsius = 11,
            VisibilityMeters = 10000
        };
        var rainMetar = new MetarData
        {
            StationId = "EGLL",
            RawText = "EGLL 151100Z 25012KT 9999 -RA SCT035 16/11 Q1016",
            TemperatureCelsius = 16,
            DewpointCelsius = 11,
            VisibilityMeters = 10000,
            WeatherConditions = new List<string> { "-RA" }
        };
        var clearState = AtmosphereModCalculator.ComputeState(clearMetar, 51.47, -0.45, 25.0, utc);
        var rainState = AtmosphereModCalculator.ComputeState(rainMetar, 51.47, -0.45, 25.0, utc);
        Assert.True(rainState.BoundaryLayerHazeMultiplier < clearState.BoundaryLayerHazeMultiplier,
            "Rain washout in unrestricted visibility should scavenge boundary-layer aerosols.");
    }

    [Fact]
    public void BuildCloudAnchorSlices_Populates32QuadraticSlices_AndDetectsConvectiveTowers()
    {
        var cbMetar = new MetarData
        {
            StationId = "KMIA",
            RawText = "KMIA 181900Z 14015G25KT 6000 +TSRA SCT018 BKN030CB 29/25 A2990",
            TemperatureCelsius = 29,
            DewpointCelsius = 25,
            VisibilityMeters = 6000,
            WeatherConditions = new List<string> { "+TSRA" },
            Clouds = new List<MetarCloud>
            {
                new() { Coverage = "SCT", BaseFeet = 1800 },
                new() { Coverage = "BKN", BaseFeet = 3000, Type = "CB" }
            }
        };

        var slices = AtmosphereModCalculator.BuildCloudAnchorSlices(
            cbMetar,
            stationElevationMeters: 3.0,
            relativeHumidity01: 0.80,
            hasMistOrFog: false,
            out double stormStrength);

        Assert.Equal(32, slices.Count);
        Assert.Equal(0.0, slices[0].AltitudeMetersMsl, 3);
        Assert.Equal(15.625 * 10 * 10, slices[10].AltitudeMetersMsl, 3);
        Assert.True(stormStrength >= 0.85, $"Expected strong convective tower evidence, got {stormStrength}");

        // CB anvil top extends up to ~11.5 km MSL, so mid/upper troposphere slices (e.g. k=16 -> 4000m, k=24 -> 9000m) must be active and Convective
        var slice4km = slices[16];
        var slice9km = slices[24];
        Assert.True(slice4km.Coverage > 0.5);
        Assert.Equal(AtmosphereCloudKind.Convective, slice4km.Kind);
        Assert.True(slice9km.Coverage > 0.5);
        Assert.Equal(AtmosphereCloudKind.Convective, slice9km.Kind);
    }

    [Fact]
    public void FormatCommBusMetarJson_Respects051PrimaryVsSurveyCoordinateProtocol()
    {
        var state = new WeatherState
        {
            StationId = "HECA",
            RawMetar = "HECA 181200Z 03012KT 6000 HZ SCT030 BKN080 30/14 Q1013",
            TemperatureCelsius = 30.0,
            DewpointCelsius = 14.0,
            VisibilityMeters = 6000.0,
            WindDirectionDegrees = 30.0,
            WindSpeedKnots = 12.0,
            AltimeterHpa = 1013.0,
            CloudLayers = new List<CloudLayer>
            {
                new() { BaseFeetAgl = 3000, CoveragePercent = 0.50, Type = CloudType.SCT },
                new() { BaseFeetAgl = 8000, CoveragePercent = 0.75, Type = CloudType.BKN }
            }
        };

        // 1. Primary aircraft station push omits root lat/lon so AtmosphereMod 0.5.1 applies immediately without distance-hold gating
        string primaryJson = AtmosphereModCalculator.FormatCommBusMetarJson(state);
        using (var doc = JsonDocument.Parse(primaryJson))
        {
            var root = doc.RootElement;
            Assert.Equal("HECA", root.GetProperty("ident").GetString());
            Assert.False(root.TryGetProperty("lat", out _));
            Assert.False(root.TryGetProperty("lon", out _));

            var metarObj = root.GetProperty("metar");
            Assert.Equal("HECA", metarObj.GetProperty("icao").GetString());
            Assert.Equal(6000.0, metarObj.GetProperty("vis").GetDouble());
            Assert.False(metarObj.GetProperty("cavok").GetBoolean());

            var phenom = metarObj.GetProperty("phenom");
            Assert.Equal(1, phenom.GetArrayLength());
            var codes = phenom[0].GetProperty("phenom").EnumerateArray().Select(x => x.GetInt32()).ToList();
            Assert.Contains(16, codes); // 16 = HZ

            var layers = metarObj.GetProperty("layers");
            Assert.Equal(2, layers.GetArrayLength());
            Assert.Equal(30, layers[0].GetProperty("alt").GetInt32()); // 3000 ft -> 30
            Assert.Equal(2, layers[0].GetProperty("cover").GetInt32()); // SCT -> 2
            Assert.Equal(80, layers[1].GetProperty("alt").GetInt32()); // 8000 ft -> 80
            Assert.Equal(3, layers[1].GetProperty("cover").GetInt32()); // BKN -> 3
        }

        // 2. Survey slot response echoes 6-decimal requestLat/requestLon within < 1e-5 deg tolerance
        string surveyJson = AtmosphereModCalculator.FormatCommBusMetarJson(state, 30.1234564, 31.6543214);
        using (var doc = JsonDocument.Parse(surveyJson))
        {
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("lat", out var latProp));
            Assert.True(root.TryGetProperty("lon", out var lonProp));
            Assert.Equal(30.123456, latProp.GetDouble(), 6);
            Assert.Equal(31.654321, lonProp.GetDouble(), 6);
        }
    }

    [Fact]
    public void AtmosphereModService_LoadsAndSavesTunablesLosslessly_AndDetects051ShadersSubdirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SkyWeave_AtmoModTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            string iniPath = Path.Combine(tempDir, "tunables.ini");
            File.WriteAllText(iniPath, """
                # Custom header comment preserved
                enabled = 1 ; inline comment
                T0 = 1.0
                T8 = 1.0 # full-spectrum sky
                C0 = 1.0
                C16 = 2.0
                C32 = 0.0
                """);

            var svc = new AtmosphereModService(tempDir);
            var loaded = svc.LoadTunables();
            Assert.True(loaded.Enabled);
            Assert.Equal(2.0, loaded.C[16], 3);
            Assert.Equal(0.0, loaded.C[32], 3);

            // Modify C16 and C32 and save
            loaded.C[16] = 2.75;
            loaded.C[32] = 1.0;
            loaded.T[14] = 345.0;
            Assert.True(svc.SaveTunables(loaded));

            string updatedText = File.ReadAllText(iniPath);
            Assert.Contains("# Custom header comment preserved", updatedText);
            Assert.Contains("# full-spectrum sky", updatedText);
            Assert.Contains("C16 = 2.75", updatedText);
            Assert.Contains("C32 = 1.0", updatedText);

            var reloaded = svc.LoadTunables();
            Assert.Equal(2.75, reloaded.C[16], 3);
            Assert.Equal(1.0, reloaded.C[32], 3);
            Assert.Equal(345.0, reloaded.T[14], 3);

            // Verify 0.5.1 shaders subdirectory detection
            string shadersSubdir = Path.Combine(tempDir, "shaders");
            Directory.CreateDirectory(shadersSubdir);
            File.WriteAllBytes(Path.Combine(shadersSubdir, "0x59732B67C3E0AD4C.cso"), new byte[] { 0x44, 0x58, 0x42, 0x43 });
            File.WriteAllBytes(Path.Combine(shadersSubdir, "0xC9219EC7EB0451E6.cso"), new byte[] { 0x44, 0x58, 0x42, 0x43 });

            bool isInstalled = svc.IsInstalled(out int shaderCount, out _);
            Assert.True(isInstalled);
            Assert.Equal(2, shaderCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void SmoothingPipeline_PreservesAtmosphereModStateAcrossInterpolationAndCloning()
    {
        var pipeline = new SmoothingPipeline();
        var metar = new MetarData
        {
            StationId = "HECA",
            RawText = "HECA 181200Z 02010KT 4500 HZ SCT035 31/14 Q1012",
            TemperatureCelsius = 31,
            DewpointCelsius = 14,
            VisibilityMeters = 4500,
            WeatherConditions = new List<string> { "HZ" }
        };

        var atmo = AtmosphereModCalculator.ComputeState(metar, 30.1219, 31.4056, 74.0, DateTime.UtcNow);
        var state1 = new WeatherState
        {
            StationId = "HECA",
            RawMetar = metar.RawText,
            TemperatureCelsius = 30,
            DewpointCelsius = 14,
            AltimeterHpa = 1012,
            VisibilityMeters = 4500,
            Atmosphere = atmo
        };

        pipeline.SetTarget(state1);
        var firstOut = pipeline.GetCurrentState();
        Assert.NotNull(firstOut.Atmosphere);
        Assert.Equal(AtmosphereAirMass.Haze, firstOut.Atmosphere!.AirMass);

        var state2 = new WeatherState
        {
            StationId = "HECA",
            RawMetar = metar.RawText,
            TemperatureCelsius = 31,
            DewpointCelsius = 14,
            AltimeterHpa = 1012,
            VisibilityMeters = 4500,
            Atmosphere = atmo
        };

        pipeline.SetTarget(state2);
        var secondOut = pipeline.GetCurrentState();
        Assert.NotNull(secondOut.Atmosphere);
        Assert.Equal(AtmosphereAirMass.Haze, secondOut.Atmosphere!.AirMass);
        Assert.Equal(atmo.OzoneDobsonUnits, secondOut.Atmosphere.OzoneDobsonUnits, 3);
    }
}
