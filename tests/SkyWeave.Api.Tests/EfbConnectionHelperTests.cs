using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SkyWeave.Api;
using Xunit;

namespace SkyWeave.Api.Tests;

public class EfbConnectionHelperTests
{
    [Fact]
    public void GetLocalUrl_ReturnsStandardLoopbackUrl()
    {
        var urlDefault = EfbConnectionHelper.GetLocalUrl();
        Assert.Equal("http://127.0.0.1:54170", urlDefault);

        var urlCustom = EfbConnectionHelper.GetLocalUrl(8080);
        Assert.Equal("http://127.0.0.1:8080", urlCustom);
    }

    [Fact]
    public void GetLocalIpAddresses_DoesNotReturnLoopbackAddresses()
    {
        var ips = EfbConnectionHelper.GetLocalIpAddresses();
        Assert.NotNull(ips);

        foreach (var ip in ips)
        {
            Assert.False(ip.StartsWith("127."), $"IP {ip} should not be a loopback address");
            Assert.True(IPAddress.TryParse(ip, out var parsed));
            Assert.Equal(AddressFamily.InterNetwork, parsed.AddressFamily);
        }
    }

    [Fact]
    public void GetLanUrls_MatchesLocalIpAddresses()
    {
        var ips = EfbConnectionHelper.GetLocalIpAddresses();
        var urls = EfbConnectionHelper.GetLanUrls(54170);

        Assert.Equal(ips.Count, urls.Count);
        for (int i = 0; i < ips.Count; i++)
        {
            Assert.Equal($"http://{ips[i]}:54170", urls[i]);
        }
    }

    [Fact]
    public void IsPortInUse_AccuratelyDetectsActiveSocket()
    {
        // Pick an ephemeral port by binding to 0
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            Assert.True(EfbConnectionHelper.IsPortInUse(port, IPAddress.Loopback));
        }
        finally
        {
            listener.Stop();
        }

        // Port is now free
        Assert.False(EfbConnectionHelper.IsPortInUse(port, IPAddress.Loopback));
    }

    [Fact]
    public void IsPortConflictException_DetectsAddressInUse()
    {
        var socketEx = new SocketException((int)SocketError.AddressAlreadyInUse);
        Assert.True(EfbConnectionHelper.IsPortConflictException(socketEx));

        var wrappedSocketEx = new IOException("Binding error", socketEx);
        Assert.True(EfbConnectionHelper.IsPortConflictException(wrappedSocketEx));

        var msgEx = new IOException("Failed to bind to address http://127.0.0.1:54170: address already in use.");
        Assert.True(EfbConnectionHelper.IsPortConflictException(msgEx));

        var unrelatedEx = new InvalidOperationException("Something else went wrong");
        Assert.False(EfbConnectionHelper.IsPortConflictException(unrelatedEx));
    }
}
