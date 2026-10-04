using System.Collections.Generic;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Plugins;

public class WeatherPluginContribution
{
    public List<CloudLayer>? CloudLayers { get; set; }
    public List<WindLayer>? WindLayers { get; set; }
    public List<IcingLayer>? IcingLayers { get; set; }
    public List<TurbulenceLayer>? TurbulenceLayers { get; set; }
    public List<WeatherHazard>? Hazards { get; set; }
    public List<StormCell>? StormCells { get; set; }
    public Dictionary<string, string>? DiagnosticProperties { get; set; }
}
