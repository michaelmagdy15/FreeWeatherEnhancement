using System;
using System.Collections.Generic;

namespace SkyWeave.Api;

public class EfbServerInfo
{
    public string ServerState { get; set; } = "Stopped";
    public int Port { get; set; } = WeatherApiServer.DefaultPort;
    public bool AllowLanAccess { get; set; }
    public bool HasStaticAssets { get; set; }
    public string LocalUrl { get; set; } = string.Empty;
    public IReadOnlyList<string> LanUrls { get; set; } = Array.Empty<string>();
    public string? WwwRootDir { get; set; }
    public bool IsPortConflict { get; set; }
    public string? LastError { get; set; }
}
