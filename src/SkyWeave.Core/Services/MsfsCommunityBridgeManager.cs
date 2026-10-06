using System;
using System.IO;
using System.Text.Json;

namespace SkyWeave.Core.Services;

/// <summary>
/// Detects MSFS 2024 Community folder and manages automated deployment
/// of the SkyWeaveWeatherBridge in-game toolbar panel package.
/// </summary>
public class MsfsCommunityBridgeManager
{
    public const string BridgePackageName = "SkyWeaveWeatherBridge";

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string LimitlessBase = Path.Combine(
        LocalAppData, "Packages", "Microsoft.Limitless_8wekyb3d8bbwe");

    public static readonly string[] UserCfgCandidates = new[]
    {
        Path.Combine(LimitlessBase, "LocalCache", "UserCfg.opt"),
        Path.Combine(AppData, "Microsoft Flight Simulator 2024", "UserCfg.opt"),
        // Legacy MSFS 2020 paths as fallback
        Path.Combine(LocalAppData, "Packages", "Microsoft.FlightSimulator_8wekyb3d8bbwe", "LocalCache", "UserCfg.opt"),
        Path.Combine(AppData, "Microsoft Flight Simulator", "UserCfg.opt")
    };

    public static readonly string[] DirectCommunityCandidates = new[]
    {
        Path.Combine(LimitlessBase, "LocalCache", "Packages", "Community"),
        Path.Combine(LimitlessBase, "LocalCache", "Packages", "Community2024"),
        Path.Combine(AppData, "Microsoft Flight Simulator 2024", "Packages", "Community"),
        Path.Combine(AppData, "Microsoft Flight Simulator 2024", "Packages", "Community2024"),
        Path.Combine(AppData, "Microsoft Flight Simulator", "Packages", "Community")
    };

    /// <summary>
    /// Parses the InstalledPackagesPath from a UserCfg.opt configuration file.
    /// </summary>
    public static string? ParseInstalledPackagesPath(string userCfgContent)
    {
        if (string.IsNullOrWhiteSpace(userCfgContent))
            return null;

        var lines = userCfgContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("InstalledPackagesPath", StringComparison.OrdinalIgnoreCase))
            {
                var firstQuote = trimmed.IndexOf('"');
                if (firstQuote >= 0)
                {
                    var secondQuote = trimmed.IndexOf('"', firstQuote + 1);
                    if (secondQuote > firstQuote)
                    {
                        var path = trimmed.Substring(firstQuote + 1, secondQuote - firstQuote - 1).Trim();
                        if (!string.IsNullOrEmpty(path))
                            return path;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Locates the active MSFS 2024 Community folder.
    /// </summary>
    public string? FindCommunityFolder()
    {
        // 1. Try resolving via UserCfg.opt (handles custom drive installations)
        foreach (var cfgPath in UserCfgCandidates)
        {
            try
            {
                if (File.Exists(cfgPath))
                {
                    var content = File.ReadAllText(cfgPath);
                    var packagesPath = ParseInstalledPackagesPath(content);
                    if (!string.IsNullOrEmpty(packagesPath))
                    {
                        var candidateCommunity = Path.Combine(packagesPath, "Community");
                        if (Directory.Exists(candidateCommunity))
                            return candidateCommunity;

                        if (packagesPath.EndsWith("Community", StringComparison.OrdinalIgnoreCase) && Directory.Exists(packagesPath))
                            return packagesPath;
                    }
                }
            }
            catch { }
        }

        // 2. Try direct standard candidate paths
        foreach (var candidate in DirectCommunityCandidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Checks whether the SkyWeave in-sim bridge package is installed in Community.
    /// </summary>
    public bool CheckBridgeStatus(out string? communityPath, out string? installedVersion, out bool isUpToDate)
    {
        communityPath = FindCommunityFolder();
        installedVersion = null;
        isUpToDate = false;

        if (string.IsNullOrEmpty(communityPath) || !Directory.Exists(communityPath))
            return false;

        var bridgePath = Path.Combine(communityPath, BridgePackageName);
        var manifestPath = Path.Combine(bridgePath, "manifest.json");

        if (!Directory.Exists(bridgePath) || !File.Exists(manifestPath))
            return false;

        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("package_version", out var verProp))
            {
                installedVersion = verProp.GetString();
                isUpToDate = string.Equals(installedVersion, "0.7.0", StringComparison.OrdinalIgnoreCase);
                return true;
            }
        }
        catch { }

        installedVersion = "installed";
        return true;
    }

    /// <summary>
    /// Finds the source SkyWeaveWeatherBridge directory distributed with the app.
    /// </summary>
    public string? FindSourceBridgeFolder()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = new[]
        {
            Path.Combine(baseDir, "bridge", BridgePackageName),
            Path.Combine(baseDir, BridgePackageName),
            Path.Combine(Directory.GetCurrentDirectory(), "bridge", BridgePackageName),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "bridge", BridgePackageName),
            Path.Combine(baseDir, "..", "..", "..", "..", "bridge", BridgePackageName),
            Path.Combine(baseDir, "..", "..", "..", "bridge", BridgePackageName)
        };

        foreach (var path in candidates)
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(full) && File.Exists(Path.Combine(full, "manifest.json")))
                return full;
        }

        return null;
    }

    /// <summary>
    /// Deploys or updates the SkyWeaveWeatherBridge package into the MSFS Community folder.
    /// </summary>
    public bool DeployBridge(string? targetCommunityFolder, out string message)
    {
        var source = FindSourceBridgeFolder();
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
        {
            message = "Source SkyWeaveWeatherBridge package not found in app directory.";
            return false;
        }

        var destinationCommunity = targetCommunityFolder ?? FindCommunityFolder();
        if (string.IsNullOrEmpty(destinationCommunity) || !Directory.Exists(destinationCommunity))
        {
            message = "MSFS Community folder could not be detected. Please verify MSFS 2024 is installed.";
            return false;
        }

        var destination = Path.Combine(destinationCommunity, BridgePackageName);

        try
        {
            CopyDirectory(source, destination);
            message = $"Successfully deployed SkyWeaveWeatherBridge to '{destination}'.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to deploy bridge: {ex.Message}";
            return false;
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
