using System.IO;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class MsfsCommunityBridgeManagerTests
{
    [Fact]
    public void ParseInstalledPackagesPath_ValidUserCfg_ExtractsCorrectPath()
    {
        var sampleOpt = @"
{Graphics
    Version 1.1.0
    Preset Custom
}
InstalledPackagesPath ""D:\MSFS2024\Packages""
";
        var path = MsfsCommunityBridgeManager.ParseInstalledPackagesPath(sampleOpt);
        Assert.Equal(@"D:\MSFS2024\Packages", path);
    }

    [Fact]
    public void ParseInstalledPackagesPath_EmptyOrMissing_ReturnsNull()
    {
        Assert.Null(MsfsCommunityBridgeManager.ParseInstalledPackagesPath(""));
        Assert.Null(MsfsCommunityBridgeManager.ParseInstalledPackagesPath("{Graphics Version 1.1.0}"));
    }

    [Fact]
    public void DeployBridge_CopiesFilesToTargetDirectory()
    {
        var manager = new MsfsCommunityBridgeManager();
        var tempCommunity = Path.Combine(Path.GetTempPath(), "SkyWeaveTest_Community_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempCommunity);

        try
        {
            var success = manager.DeployBridge(tempCommunity, out var message);
            Assert.True(success, message);

            var deployedBridge = Path.Combine(tempCommunity, MsfsCommunityBridgeManager.BridgePackageName);
            Assert.True(Directory.Exists(deployedBridge));
            Assert.True(File.Exists(Path.Combine(deployedBridge, "manifest.json")));

            // MSFS 2024 requires compiled SPB in InGamePanels and registered in layout.json
            var spbPath = Path.Combine(deployedBridge, "InGamePanels", "skyweave-weather-bridge.spb");
            Assert.True(File.Exists(spbPath), "Compiled SPB file must be present in InGamePanels");
            Assert.True(new FileInfo(spbPath).Length > 0, "Compiled SPB file must not be empty");

            var layoutPath = Path.Combine(deployedBridge, "layout.json");
            Assert.True(File.Exists(layoutPath), "layout.json must exist in deployed bridge package");
            var layoutContent = File.ReadAllText(layoutPath);
            Assert.Contains("ingamepanels/skyweave-weather-bridge.spb", layoutContent);
            Assert.DoesNotContain("skyweave-weather-bridge.xml", layoutContent);

            var iconPath = Path.Combine(deployedBridge, "html_ui", "Textures", "Menu", "toolbar", "ICON_TOOLBAR_SKYWEAVE_WEATHER_BRIDGE.svg");
            Assert.True(File.Exists(iconPath), "Toolbar icon SVG must be present in html_ui/Textures/Menu/toolbar");
        }
        finally
        {
            if (Directory.Exists(tempCommunity))
            {
                Directory.Delete(tempCommunity, recursive: true);
            }
        }
    }
}
