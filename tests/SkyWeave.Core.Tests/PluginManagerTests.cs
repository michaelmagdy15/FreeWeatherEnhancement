using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Models;
using SkyWeave.Core.Plugins;
using Xunit;

namespace SkyWeave.Core.Tests;

public class MockWeatherPlugin : IWeatherPlugin
{
    public string PluginId => "com.skyweave.test.mock";
    public string PluginName => "Mock Test Weather Plugin";
    public string Version => "1.0.0";
    public string Author => "SkyWeave Community";
    public string Description => "A mock plugin for testing the plugin architecture";
    public bool IsEnabled { get; set; } = true;
    public bool ThrowOnError { get; set; } = false;

    public int ExecutionCount { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<WeatherPluginContribution?> FetchContributionAsync(double latitude, double longitude, double altitudeFeet, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        if (ThrowOnError)
        {
            throw new InvalidOperationException("Simulated plugin crash!");
        }

        var contribution = new WeatherPluginContribution
        {
            Hazards = new List<WeatherHazard>
            {
                new WeatherHazard
                {
                    Type = HazardType.TurbulenceSigmet,
                    Severity = 0.5,
                    Description = "Plugin Custom Thermal Updraft"
                }
            },
            DiagnosticProperties = new Dictionary<string, string>
            {
                ["Sensor"] = "Laser Ceilometer"
            }
        };

        return Task.FromResult<WeatherPluginContribution?>(contribution);
    }

    public void Shutdown() { }
}

public class PluginManagerTests : IDisposable
{
    private readonly PluginManager _manager;

    public PluginManagerTests()
    {
        _manager = new PluginManager(customPluginsDir: "non_existent_empty_dir");
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    [Fact]
    public void RegisterPlugin_RegistersAndExposesPluginInfo()
    {
        var mock = new MockWeatherPlugin();
        _manager.RegisterPlugin(mock);

        var plugins = _manager.GetInstalledPlugins();
        Assert.Single(plugins);

        var info = plugins[0];
        Assert.Equal("com.skyweave.test.mock", info.PluginId);
        Assert.Equal("Mock Test Weather Plugin", info.PluginName);
        Assert.True(info.IsEnabled);
        Assert.Equal("Loaded", info.Status);
    }

    [Fact]
    public void SetPluginEnabled_TogglesState()
    {
        var mock = new MockWeatherPlugin();
        _manager.RegisterPlugin(mock);

        var ok = _manager.SetPluginEnabled("com.skyweave.test.mock", false);
        Assert.True(ok);

        var plugins = _manager.GetInstalledPlugins();
        Assert.False(plugins[0].IsEnabled);
        Assert.Equal("Disabled", plugins[0].Status);

        _manager.SetPluginEnabled("com.skyweave.test.mock", true);
        plugins = _manager.GetInstalledPlugins();
        Assert.True(plugins[0].IsEnabled);
        Assert.Equal("Loaded", plugins[0].Status);
    }

    [Fact]
    public async Task FetchAllContributionsAsync_ExecutesEnabledPluginAndCollectsData()
    {
        var mock = new MockWeatherPlugin();
        _manager.RegisterPlugin(mock);

        var contributions = await _manager.FetchAllContributionsAsync(40.0, -74.0, 5000);
        Assert.Single(contributions);
        Assert.Single(contributions[0].Hazards!);
        Assert.Equal("Plugin Custom Thermal Updraft", contributions[0].Hazards![0].Description);

        var plugins = _manager.GetInstalledPlugins();
        Assert.Equal(1, plugins[0].ExecutionCount);
        Assert.Equal(0, plugins[0].ErrorCount);
        Assert.Equal("Active", plugins[0].Status);
    }

    [Fact]
    public async Task FetchAllContributionsAsync_WhenPluginThrows_IsolatesErrorAndDoesNotCrash()
    {
        var mock = new MockWeatherPlugin { ThrowOnError = true };
        _manager.RegisterPlugin(mock);

        // Must NOT throw exception to caller!
        var contributions = await _manager.FetchAllContributionsAsync(40.0, -74.0, 5000);
        Assert.Empty(contributions);

        var plugins = _manager.GetInstalledPlugins();
        Assert.Equal(1, plugins[0].ErrorCount);
        Assert.Equal("Error", plugins[0].Status);
        Assert.Contains("Simulated plugin crash", plugins[0].LastError);
    }

    [Fact]
    public async Task FetchAllContributionsAsync_WhenDisabled_SkipsExecution()
    {
        var mock = new MockWeatherPlugin();
        _manager.RegisterPlugin(mock);
        _manager.SetPluginEnabled("com.skyweave.test.mock", false);

        var contributions = await _manager.FetchAllContributionsAsync(40.0, -74.0, 5000);
        Assert.Empty(contributions);
        Assert.Equal(0, mock.ExecutionCount);
    }
}
