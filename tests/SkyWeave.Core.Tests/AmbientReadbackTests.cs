using System.Runtime.InteropServices;
using SkyWeave.SimBridge;
using Xunit;

namespace SkyWeave.Core.Tests;

public class AmbientReadbackTests
{
    [Fact]
    public void Readback_ContainsOnlyFiveSupportedDoubles()
    {
        Assert.Equal(40, Marshal.SizeOf<AmbientWeatherData>());
        Assert.Equal(32, Marshal.OffsetOf<AmbientWeatherData>(nameof(AmbientWeatherData.VisibilityMeters)).ToInt32());
    }

    [Fact]
    public void Readback_DecodesVisibilityWithoutInventingCloudCoverage()
    {
        // Extra zero padding makes the old six-double layout safe to reproduce.
        var buffer = Marshal.AllocHGlobal(48);
        try
        {
            Marshal.Copy(new double[] { 96, 2.7, 12, 1020.6, 138600, 0 }, 0, buffer, 6);
            var weather = Marshal.PtrToStructure<AmbientWeatherData>(buffer);
            Assert.Equal(138600, weather.VisibilityMeters);
            Assert.True(double.IsNaN(weather.CloudCoverageOktas));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
