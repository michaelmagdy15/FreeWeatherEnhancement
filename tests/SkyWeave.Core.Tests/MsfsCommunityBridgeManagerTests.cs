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
