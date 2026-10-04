using System;

namespace SkyWeave.Core.Plugins;

public class PluginInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string AssemblyPath { get; set; } = string.Empty;
    public DateTime? LastExecutionUtc { get; set; }
    public long ExecutionCount { get; set; }
    public long ErrorCount { get; set; }
    public string Status { get; set; } = "Loaded"; // "Loaded", "Disabled", "Error"
    public string? LastError { get; set; }
}
