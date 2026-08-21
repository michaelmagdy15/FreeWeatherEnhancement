using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SkyWeave.Core.Injectors;

/// <summary>
/// Writes SkyWeave weather preset (.WPR) files to two locations:
/// 1. MSFS Weather\Presets folder — for WeatherSetModeTheme auto-load (written every cycle)
/// 2. Community2024 package — written once, persists across MSFS updates, user can select manually as fallback
/// </summary>
public class WprFileWriter
{
    private const string PRESET_NAME = "SkyWeave";
    private const string FILE_NAME = "SkyWeave.WPR";
    private const string PACKAGE_NAME = "SkyWeave";

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string LimitlessBase = Path.Combine(
        LocalAppData, "Packages", "Microsoft.Limitless_8wekyb3d8bbwe");

    private static readonly string[] PresetFolderCandidates = new[]
    {
        Path.Combine(LimitlessBase, "LocalState", "Weather", "Presets"),
        // Steam / legacy paths as fallback
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft Flight Simulator 2024", "Weather", "Presets"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft Flight Simulator", "Weather", "Presets"),
    };

    private static readonly string[] CommunityFolderCandidates = new[]
    {
        Path.Combine(LimitlessBase, "LocalCache", "Packages", "Community2024"),
        // Steam / legacy paths as fallback
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft Flight Simulator 2024", "Packages", "Community2024"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft Flight Simulator", "Packages", "Community"),
    };

    private readonly WprGenerator _generator = new();
    private readonly Action<string>? _log;
    private string? _lastCommunityXml;
    private bool _communityPackageCreated;

    public string? LastPresetXml { get; private set; }

    /// <summary>MSFS weather presets folder currently in use (for diagnostics).</summary>
    public static string? CurrentPresetsFolder => FindPresetsFolder();

    public WprFileWriter(Action<string>? log = null)
    {
        _log = log;
    }

    /// <summary>
    /// Generates WPR XML from weather state and writes it to the MSFS presets
    /// folder (every cycle, for WeatherSetModeTheme). Also writes to the community
    /// package once (safe fallback).
    /// </summary>
    public string? WritePreset(SkyWeave.Core.Models.WeatherState state, double turbulenceBoostKnots = 0)
    {
        var xml = _generator.GenerateWprXml(state, turbulenceBoostKnots);
        LastPresetXml = xml;
        string? primaryPath = null;

        var presetsFolder = FindPresetsFolder();
        if (presetsFolder != null)
        {
            var presetsPath = Path.Combine(presetsFolder, FILE_NAME);
            Directory.CreateDirectory(presetsFolder);
            WriteAtomically(presetsPath, xml);
            primaryPath = presetsPath;
        }

        // Community package: write once on startup, then only if WPR content
        // changed significantly (avoids file-locking with MSFS reading it).
        if (!_communityPackageCreated || xml != _lastCommunityXml)
        {
            var communityPath = WriteCommunityPackage(xml);
            if (communityPath != null)
            {
                _communityPackageCreated = true;
                _lastCommunityXml = xml;
                if (primaryPath == null)
                    primaryPath = communityPath;
            }
        }

        return primaryPath;
    }

    /// <summary>Returns the preset name used for WeatherSetModeTheme.</summary>
    public string PresetName => PRESET_NAME;

    /// <summary>Forces a community package write on next cycle (e.g. after WPR format change).</summary>
    public void InvalidateCommunityPackage() => _communityPackageCreated = false;

    private string? WriteCommunityPackage(string xml)
    {
        var packageFolder = FindCommunityPackageFolder();
        if (packageFolder == null)
            return null;

        try
        {
            var weatherPresetsDir = Path.Combine(packageFolder, "WeatherPresets");
            Directory.CreateDirectory(weatherPresetsDir);

            var wprPath = Path.Combine(weatherPresetsDir, FILE_NAME);
            WriteAtomically(wprPath, xml);

            var fileInfo = new FileInfo(wprPath);

            // layout.json — list all files in the package
            var layoutJson = JsonSerializer.Serialize(new object[]
            {
                new
                {
                    path = $"WeatherPresets/{FILE_NAME}",
                    size = fileInfo.Length,
                    date = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss")
                }
            }, new JsonSerializerOptions { WriteIndented = true });
            WriteAtomically(Path.Combine(packageFolder, "layout.json"), layoutJson);

            // manifest.json — package metadata
            var manifestJson = JsonSerializer.Serialize(new
            {
                dependencies = Array.Empty<object>(),
                content_type = "Weather",
                title = "SkyWeave Weather",
                manufacturer = "SkyWeave",
                creator = "SkyWeave",
                package_version = "1.0.0",
                minimum_game_version = "1.0.0",
                release_notes = new
                {
                    neutral = new
                    {
                        LastUpdate = "",
                        OlderHistory = ""
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true });
            WriteAtomically(Path.Combine(packageFolder, "manifest.json"), manifestJson);

            return wprPath;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Community WPR package write failed: {ex.Message}");
            return null;
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static string? FindPresetsFolder()
    {
        foreach (var candidate in PresetFolderCandidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        var defaultPath = PresetFolderCandidates[0];
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    private static string? FindCommunityPackageFolder()
    {
        foreach (var community in CommunityFolderCandidates)
        {
            if (Directory.Exists(community))
                return Path.Combine(community, PACKAGE_NAME);
        }
        return null;
    }
}
