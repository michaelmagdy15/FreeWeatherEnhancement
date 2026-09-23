using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SkyWeave.Api;

public static class EfbConnectionHelper
{
    public const int DefaultPort = 54170;

    public static string GetLocalUrl(int port = DefaultPort) => $"http://127.0.0.1:{port}";

    public static IReadOnlyList<string> GetLocalIpAddresses()
    {
        var addresses = new List<string>();
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up ||
                    iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProps = iface.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(unicast.Address))
                    {
                        var ipStr = unicast.Address.ToString();
                        if (!addresses.Contains(ipStr))
                        {
                            addresses.Add(ipStr);
                        }
                    }
                }
            }
        }
        catch
        {
            // Network information query failure fallback
        }
        return addresses;
    }

    public static IReadOnlyList<string> GetLanUrls(int port = DefaultPort)
    {
        return GetLocalIpAddresses()
            .Select(ip => $"http://{ip}:{port}")
            .ToList();
    }

    public static bool IsPortInUse(int port, IPAddress? address = null)
    {
        address ??= IPAddress.Loopback;
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.ExclusiveAddressUse = true;
            socket.Bind(new IPEndPoint(address, port));
            return false;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                                         ex.SocketErrorCode == SocketError.AccessDenied ||
                                         ex.ErrorCode == 10048 || ex.ErrorCode == 10013)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPortConflictException(Exception ex)
    {
        if (ex is SocketException se && (se.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                                         se.SocketErrorCode == SocketError.AccessDenied ||
                                         se.ErrorCode == 10048 || se.ErrorCode == 10013))
            return true;

        if (ex.InnerException is SocketException innerSe && (innerSe.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                                                             innerSe.SocketErrorCode == SocketError.AccessDenied ||
                                                             innerSe.ErrorCode == 10048 || innerSe.ErrorCode == 10013))
            return true;

        if (ex is IOException && (ex.Message.IndexOf("address already in use", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ex.Message.IndexOf("failed to bind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  ex.Message.IndexOf("only one usage of each socket address", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        return false;
    }
}
