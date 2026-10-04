using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace SkyWeave.Core.Plugins;

public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver? _resolver;

    public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
    {
        if (File.Exists(pluginPath))
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (_resolver != null)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
        }
        return null;
    }
}

public class LoadedPluginInstance
{
    public IWeatherPlugin Plugin { get; }
    public PluginInfo Info { get; }
    public AssemblyLoadContext? LoadContext { get; }

    public LoadedPluginInstance(IWeatherPlugin plugin, PluginInfo info, AssemblyLoadContext? context = null)
    {
        Plugin = plugin;
        Info = info;
        LoadContext = context;
    }
}

public class PluginManager : IDisposable
{
    private readonly List<LoadedPluginInstance> _plugins = new();
    private readonly object _lock = new();
    private readonly TimeSpan _pluginTimeout = TimeSpan.FromSeconds(5);
    private bool _disposed;

    public string AppDataPluginsDirectory { get; }
    public string BaseDirectoryPluginsDirectory { get; }

    public PluginManager(string? customPluginsDir = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppDataPluginsDirectory = Path.Combine(appData, "SkyWeave", "plugins");
        BaseDirectoryPluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

        try
        {
            Directory.CreateDirectory(AppDataPluginsDirectory);
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(customPluginsDir))
        {
            DiscoverPlugins(customPluginsDir);
        }
        else
        {
            DiscoverAll();
        }
    }

    public void DiscoverAll()
    {
        DiscoverPlugins(AppDataPluginsDirectory);
        DiscoverPlugins(BaseDirectoryPluginsDirectory);
    }

    public void DiscoverPlugins(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        try
        {
            var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);
            foreach (var dll in dllFiles)
            {
                LoadPluginFromAssembly(dll);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginManager] Directory scan error: {ex.Message}");
        }
    }

    public void LoadPluginFromAssembly(string assemblyPath)
    {
        lock (_lock)
        {
            if (_plugins.Any(p => string.Equals(p.Info.AssemblyPath, assemblyPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            try
            {
                var loadContext = new PluginAssemblyLoadContext(assemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IWeatherPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IWeatherPlugin instance)
                    {
                        var info = new PluginInfo
                        {
                            PluginId = instance.PluginId,
                            PluginName = instance.PluginName,
                            Version = instance.Version,
                            Author = instance.Author,
                            Description = instance.Description,
                            IsEnabled = instance.IsEnabled,
                            AssemblyPath = assemblyPath,
                            Status = instance.IsEnabled ? "Loaded" : "Disabled"
                        };

                        _ = instance.InitializeAsync(CancellationToken.None);
                        _plugins.Add(new LoadedPluginInstance(instance, info, loadContext));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginManager] Failed to load plugin {assemblyPath}: {ex.Message}");
            }
        }
    }

    public void RegisterPlugin(IWeatherPlugin plugin, string? assemblyPath = null)
    {
        if (plugin == null) throw new ArgumentNullException(nameof(plugin));

        lock (_lock)
        {
            if (_plugins.Any(p => p.Info.PluginId == plugin.PluginId))
            {
                return;
            }

            var info = new PluginInfo
            {
                PluginId = plugin.PluginId,
                PluginName = plugin.PluginName,
                Version = plugin.Version,
                Author = plugin.Author,
                Description = plugin.Description,
                IsEnabled = plugin.IsEnabled,
                AssemblyPath = assemblyPath ?? "in-memory",
                Status = plugin.IsEnabled ? "Loaded" : "Disabled"
            };

            _ = plugin.InitializeAsync(CancellationToken.None);
            _plugins.Add(new LoadedPluginInstance(plugin, info, null));
        }
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        lock (_lock)
        {
            return _plugins.Select(p => p.Info).ToList();
        }
    }

    public bool SetPluginEnabled(string pluginId, bool enabled)
    {
        lock (_lock)
        {
            var match = _plugins.FirstOrDefault(p => string.Equals(p.Info.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
            if (match == null) return false;

            match.Info.IsEnabled = enabled;
            match.Plugin.IsEnabled = enabled;
            match.Info.Status = enabled ? "Loaded" : "Disabled";
            return true;
        }
    }

    public async Task<List<WeatherPluginContribution>> FetchAllContributionsAsync(
        double latitude,
        double longitude,
        double altitudeFeet,
        CancellationToken cancellationToken = default)
    {
        List<LoadedPluginInstance> activePlugins;
        lock (_lock)
        {
            activePlugins = _plugins.Where(p => p.Info.IsEnabled).ToList();
        }

        if (activePlugins.Count == 0)
        {
            return new List<WeatherPluginContribution>();
        }

        var tasks = activePlugins.Select(p => RunPluginSafelyAsync(p, latitude, longitude, altitudeFeet, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).Cast<WeatherPluginContribution>().ToList();
    }

    private async Task<WeatherPluginContribution?> RunPluginSafelyAsync(
        LoadedPluginInstance instance,
        double lat,
        double lon,
        double altFeet,
        CancellationToken ct)
    {
        var info = instance.Info;
        try
        {
            using var timeoutCts = new CancellationTokenSource(_pluginTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var contribution = await instance.Plugin.FetchContributionAsync(lat, lon, altFeet, linkedCts.Token);
            info.LastExecutionUtc = DateTime.UtcNow;
            info.ExecutionCount++;
            info.Status = "Active";
            return contribution;
        }
        catch (Exception ex)
        {
            info.ErrorCount++;
            info.LastError = ex.Message;
            info.Status = info.ErrorCount > 3 ? "Degraded" : "Error";
            System.Diagnostics.Debug.WriteLine($"[PluginManager] Plugin {info.PluginId} error: {ex.Message}");
            return null;
        }
    }

    public void Shutdown()
    {
        lock (_lock)
        {
            foreach (var p in _plugins)
            {
                try
                {
                    p.Plugin.Shutdown();
                }
                catch { }
            }
            _plugins.Clear();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Shutdown();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
