using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class OfflineWeatherTests : IDisposable
{
    private readonly string _testDir;
    private readonly StationFinder _stationFinder;

    public OfflineWeatherTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "SkyWeave_Test_Offline_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _stationFinder = new StationFinder();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void StationFinder_GetAirportsByPrefix_ReturnsEgyptianAirports()
    {
        var egyptianAirports = _stationFinder.GetAirportsByPrefix("HE");
        Assert.NotEmpty(egyptianAirports);
        Assert.True(egyptianAirports.Count >= 20, $"Expected >= 20 airports, found {egyptianAirports.Count}");
        Assert.Contains(egyptianAirports, a => a.IcaoId == "HECA");
        Assert.Contains(egyptianAirports, a => a.IcaoId == "HESH");
        Assert.Contains(egyptianAirports, a => a.IcaoId == "HELX");
        Assert.Contains(egyptianAirports, a => a.IcaoId == "HEGN");
        Assert.Contains(egyptianAirports, a => a.IcaoId == "HEBA");
    }

    [Fact]
    public void OfflineWeatherPackage_SerializationRoundtrip_Succeeds()
    {
        var package = new OfflineWeatherPackage
        {
            Id = "eg_test_1",
            Name = "Egypt FIR Test",
            Description = "Test package",
            RegionCode = "EG",
            IcaoPrefix = "HE",
            MinLatitude = 22.0,
            MaxLatitude = 31.5,
            MinLongitude = 25.0,
            MaxLongitude = 36.0
        };

        package.Stations["HECA"] = new MetarData
        {
            StationId = "HECA",
            ObservationTime = DateTime.UtcNow,
            TemperatureCelsius = 29.0,
            DewpointCelsius = 15.0,
            AltimeterHpa = 1013.25,
            WindDirectionDegrees = 330,
            WindSpeedKnots = 9,
            VisibilityMeters = 10000,
            FlightCategory = "VFR"
        };

        package.WindsGrid.Add(new WindsAloftData
        {
            Latitude = 30.0,
            Longitude = 31.0,
            ForecastTime = DateTime.UtcNow,
            SourceModel = "Test GFS",
            PressureLevels = new List<PressureLevelData>
            {
                new() { PressureHpa = 850, AltitudeFeet = 5000, TemperatureCelsius = 18, WindSpeedKnots = 15, WindDirectionDegrees = 320 },
                new() { PressureHpa = 500, AltitudeFeet = 18000, TemperatureCelsius = -12, WindSpeedKnots = 35, WindDirectionDegrees = 290 }
            }
        });

        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
        Assert.False(string.IsNullOrWhiteSpace(json));

        var deserialized = JsonSerializer.Deserialize<OfflineWeatherPackage>(json);
        Assert.NotNull(deserialized);
        Assert.Equal("eg_test_1", deserialized.Id);
        Assert.Equal("Egypt FIR Test", deserialized.Name);
        Assert.Single(deserialized.Stations);
        Assert.Equal(29.0, deserialized.Stations["HECA"].TemperatureCelsius);
        Assert.Single(deserialized.WindsGrid);
        Assert.Equal(2, deserialized.WindsGrid[0].PressureLevels.Count);
    }

    [Fact]
    public void OfflineWeatherPackage_FindNearestMetarAndWinds_ResolvesClosest()
    {
        var service = new OfflineWeatherService(_stationFinder, storageDirectory: _testDir);
        var pkg = service.CreateSyntheticBaselinePackage("egypt");

        // Cairo Coordinates approx 30.12, 31.40
        var cairoMetar = pkg.FindNearestMetar(30.12, 31.40, _stationFinder);
        Assert.NotNull(cairoMetar);
        Assert.Equal("HECA", cairoMetar.StationId);

        // Sharm El Sheikh Coordinates approx 27.97, 34.39
        var sharmMetar = pkg.FindNearestMetar(27.97, 34.39, _stationFinder);
        Assert.NotNull(sharmMetar);
        Assert.Equal("HESH", sharmMetar.StationId);

        // Upper Air Winds resolution
        var winds = pkg.FindNearestWinds(30.0, 31.0);
        Assert.NotNull(winds);
        Assert.NotEmpty(winds.PressureLevels);
        Assert.True(winds.PressureLevels.Count >= 5);
    }

    [Fact]
    public async Task OfflineWeatherService_SaveListDelete_OperatesCleanly()
    {
        var service = new OfflineWeatherService(_stationFinder, storageDirectory: _testDir);
        var pkg = service.CreateSyntheticBaselinePackage("egypt");

        await service.SavePackageAsync(pkg);
        var list = service.ListPackages();
        Assert.NotEmpty(list);
        Assert.Contains(list, s => s.Id == pkg.Id);

        var loaded = await service.GetPackageAsync(pkg.Id);
        Assert.NotNull(loaded);
        Assert.Equal(pkg.Name, loaded.Name);
        Assert.Equal(pkg.Stations.Count, loaded.Stations.Count);

        var activated = await service.ActivatePackageAsync(pkg.Id);
        Assert.True(activated);
        Assert.True(service.IsOfflineMode);
        Assert.Equal(pkg.Id, service.ActivePackage?.Id);

        service.DeactivateOfflineMode();
        Assert.False(service.IsOfflineMode);

        var deleted = service.DeletePackage(pkg.Id);
        Assert.True(deleted);
        Assert.Empty(service.ListPackages());
    }

    [Fact]
    public async Task WeatherPipeline_InOfflineMode_ServesWeatherWithoutNetworkCalls()
    {
        var service = new OfflineWeatherService(_stationFinder, storageDirectory: _testDir);
        var egyptPackage = service.CreateSyntheticBaselinePackage("egypt");
        await service.SavePackageAsync(egyptPackage);
        await service.ActivatePackageAsync(egyptPackage.Id);

        // Build pipeline with dummy/offline components
        var cache = new WeatherCache();
        var client = new System.Net.Http.HttpClient();
        var metarFetcher = new MetarFetcher(client, _stationFinder);
        var windsAloftFetcher = new WindsAloftFetcher(client);
        var tafFetcher = new TafFetcher(client, _stationFinder);

        var pipeline = new WeatherPipeline(
            cache,
            metarFetcher,
            windsAloftFetcher,
            new SigmetFetcher(client),
            new LightningFetcher(client),
            new RadarFetcher(client),
            tafFetcher,
            new CloudLayerBuilder(),
            new WindLayerBuilder(),
            new IcingCalculator(),
            new TurbulenceCalculator(),
            new StormModeler(),
            new WakeTurbulenceEngine(),
            _stationFinder,
            new HazardAggregator(),
            offlineService: service);

        // Aircraft at Cairo HECA (lat 30.12, lon 31.40, alt 382 ft)
        var data = await pipeline.FetchAllDataAsync(30.12, 31.40, 382, DateTime.UtcNow, new List<AircraftTraffic>());
        Assert.NotNull(data);
        Assert.NotNull(data.Metar);
        Assert.Equal("HECA", data.Metar.StationId);
        Assert.NotNull(data.Winds);
        Assert.NotEmpty(data.CloudLayers);
        Assert.NotEmpty(data.WindLayers);
        Assert.Contains("OFFLINE", data.SpatialBlendSummary);

        var state = pipeline.BuildWeatherState(data, 30.12, 31.40, 382);
        Assert.NotNull(state);
        Assert.Equal("HECA", state.StationId);
        Assert.Equal(data.Metar.TemperatureCelsius, state.TemperatureCelsius);
        Assert.Equal(data.Metar.AltimeterHpa, state.AltimeterHpa);
    }

    [Fact]
    public async Task WeatherEngine_SetOfflineMode_TogglesCleanly()
    {
        var service = new OfflineWeatherService(_stationFinder, storageDirectory: _testDir);
        var engine = new WeatherEngine(_stationFinder, service);

        Assert.False(engine.IsOfflineMode);

        // Activate offline mode (automatically seeds Egypt if no packages exist)
        var ok = await engine.SetOfflineModeAsync(true);
        Assert.True(ok);
        Assert.True(engine.IsOfflineMode);
        Assert.NotNull(engine.ActiveOfflinePackage);
        Assert.Contains("Egypt", engine.ActiveOfflinePackage.Name);

        // Deactivate
        await engine.SetOfflineModeAsync(false);
        Assert.False(engine.IsOfflineMode);
    }
}
