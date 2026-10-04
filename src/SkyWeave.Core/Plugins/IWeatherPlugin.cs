using System.Threading;
using System.Threading.Tasks;

namespace SkyWeave.Core.Plugins;

public interface IWeatherPlugin
{
    string PluginId { get; }
    string PluginName { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }
    bool IsEnabled { get; set; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<WeatherPluginContribution?> FetchContributionAsync(double latitude, double longitude, double altitudeFeet, CancellationToken cancellationToken = default);
    void Shutdown();
}
