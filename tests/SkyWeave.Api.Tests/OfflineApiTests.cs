using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SkyWeave.Api;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Api.Tests;

public class OfflineApiTests : IDisposable
{
    private readonly string _testDir;
    private readonly StationFinder _stationFinder;
    private readonly OfflineWeatherService _offlineService;
    private readonly WeatherEngine _engine;
    private readonly EngineWeatherDataProvider _provider;

    public OfflineApiTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "SkyWeave_Api_Test_Offline_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _stationFinder = new StationFinder();
        _offlineService = new OfflineWeatherService(_stationFinder, storageDirectory: _testDir);
        _engine = new WeatherEngine(_stationFinder, _offlineService);
        _provider = new EngineWeatherDataProvider(_engine);
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
    public async Task Provider_GetStatusAsync_IncludesOfflineModeFields()
    {
        var status = await _provider.GetStatusAsync();
        Assert.False(status.IsOfflineMode);
        Assert.Null(status.ActiveOfflinePackageName);

        // Turn on offline mode
        await _provider.SetOfflineModeAsync(true);

        var statusOffline = await _provider.GetStatusAsync();
        Assert.True(statusOffline.IsOfflineMode);
        Assert.NotNull(statusOffline.ActiveOfflinePackageName);
        Assert.Contains("Egypt", statusOffline.ActiveOfflinePackageName);
    }

    [Fact]
    public async Task Provider_OfflinePackageLifecycle_OperatesCleanly()
    {
        var packages = await _provider.GetOfflinePackagesAsync();
        Assert.Empty(packages);

        // Create package
        var req = new OfflinePackageCreateRequest
        {
            Preset = "egypt",
            ActivateImmediately = true
        };
        var created = await _provider.CreateOfflinePackageAsync(req);
        Assert.NotNull(created);
        Assert.True(_provider.IsOfflineMode);
        Assert.Equal(created.Id, _provider.ActiveOfflinePackageId);

        packages = await _provider.GetOfflinePackagesAsync();
        Assert.Single(packages);
        Assert.Equal(created.Id, packages[0].Id);
        Assert.True(packages[0].IsActive);

        // Deactivate
        await _provider.SetOfflineModeAsync(false);
        Assert.False(_provider.IsOfflineMode);

        // Delete
        var deleted = await _provider.DeleteOfflinePackageAsync(created.Id);
        Assert.True(deleted);

        packages = await _provider.GetOfflinePackagesAsync();
        Assert.Empty(packages);
    }
}
