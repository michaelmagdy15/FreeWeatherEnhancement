using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using SkyWeave.Api;
using Xunit;

namespace SkyWeave.Api.Tests;

public class EfbServerLifecycleTests
{
    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public void ServerProperties_ReflectInitialConfiguration()
    {
        var port = GetAvailablePort();
        using var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: null,
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port);

        Assert.Equal(ServerState.Stopped, server.State);
        Assert.False(server.IsRunning);
        Assert.False(server.IsPortConflict);
        Assert.False(server.AllowLanAccess);
        Assert.False(server.AllowPositionOverride);
        Assert.Equal(port, server.Port);
        Assert.Equal($"http://127.0.0.1:{port}", server.LocalUrl);
        Assert.Empty(server.LanUrls);

        var info = server.ServerInfo;
        Assert.Equal("Stopped", info.ServerState);
        Assert.Equal(port, info.Port);
        Assert.False(info.AllowLanAccess);
    }

    [Fact]
    public async Task StartAndStop_OrderedStateTransitions()
    {
        var port = GetAvailablePort();
        await using var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{port}",
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port);

        Assert.Equal(ServerState.Stopped, server.State);

        await server.StartAsync();
        Assert.Equal(ServerState.Running, server.State);
        Assert.True(server.IsRunning);
        Assert.True(EfbConnectionHelper.IsPortInUse(port));

        await server.StopAsync();
        Assert.Equal(ServerState.Stopped, server.State);
        Assert.False(server.IsRunning);

        await server.DisposeAsync();
        Assert.Equal(ServerState.Disposed, server.State);
    }

    [Fact]
    public async Task RepeatedStart_IsIdempotent()
    {
        var port = GetAvailablePort();
        await using var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{port}",
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port);

        await server.StartAsync();
        Assert.Equal(ServerState.Running, server.State);

        // Second start should return without throwing
        await server.StartAsync();
        Assert.Equal(ServerState.Running, server.State);

        await server.StopAsync();
    }

    [Fact]
    public async Task PortConflict_TransitionsToFaultedWithClearMessage()
    {
        var port = GetAvailablePort();

        // Occupy port with a TcpListener
        var blocker = new TcpListener(IPAddress.Loopback, port);
        blocker.ExclusiveAddressUse = true;
        blocker.Start();

        try
        {
            await using var server = new WeatherApiServer(
                args: null,
                customProvider: new TestWeatherDataProvider(),
                customEngine: null,
                listenUrl: null,
                allowPositionOverride: false,
                allowLanAccess: false,
                port: port);

            // Attempt to start on the blocked port
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => server.StartAsync());

            Assert.Equal(ServerState.Faulted, server.State);
            Assert.True(server.IsPortConflict);
            Assert.NotNull(server.LastError);
            Assert.Contains(port.ToString(), server.LastError);
        }
        finally
        {
            blocker.Stop();
        }
    }

    [Fact]
    public async Task RestartAfterPortConflict_RecoversCleanly()
    {
        var port = GetAvailablePort();

        var blocker = new TcpListener(IPAddress.Loopback, port);
        blocker.ExclusiveAddressUse = true;
        blocker.Start();

        var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: null,
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port);

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => server.StartAsync());
            Assert.True(server.IsPortConflict);
            Assert.Equal(ServerState.Faulted, server.State);

            // Unblock the port
            blocker.Stop();

            // Restart should now succeed
            await server.RestartAsync();
            Assert.Equal(ServerState.Running, server.State);
            Assert.False(server.IsPortConflict);
            Assert.True(server.IsRunning);
        }
        finally
        {
            blocker.Dispose();
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartupCancellation_AbortsCleanly()
    {
        var port = GetAvailablePort();
        await using var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{port}",
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.StartAsync(cts.Token));
        Assert.Equal(ServerState.Stopped, server.State);
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task SynchronousDispose_CleansUpAndReleasesPort()
    {
        var port = GetAvailablePort();
        var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{port}",
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port);

        await server.StartAsync();
        Assert.True(server.IsRunning);

        // Synchronous dispose
        server.Dispose();

        Assert.Equal(ServerState.Disposed, server.State);
        Assert.False(EfbConnectionHelper.IsPortInUse(port));
    }

    [Fact]
    public async Task MissingAssets_ReturnsStyledFallbackHtml()
    {
        var port = GetAvailablePort();
        await using var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{port}",
            allowPositionOverride: false,
            allowLanAccess: false,
            port: port,
            wwwrootDirOverride: "");

        await server.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SkyWeave", content);
        Assert.Contains("API", content);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsEfbMetadata()
    {
        var port = GetAvailablePort();
        await using var server = new WeatherApiServer(
            args: null,
            customProvider: new TestWeatherDataProvider(),
            customEngine: null,
            listenUrl: $"http://127.0.0.1:{port}",
            allowPositionOverride: false,
            allowLanAccess: true,
            port: port);

        await server.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("SkyWeave", root.GetProperty("service").GetString());
        Assert.Equal(port, root.GetProperty("port").GetInt32());
        Assert.True(root.GetProperty("allowLanAccess").GetBoolean());
        Assert.Equal($"http://127.0.0.1:{port}", root.GetProperty("localUrl").GetString());
    }
}
