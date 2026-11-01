using System.Net;
using System.Net.Sockets;

namespace Hoard.Host.Tests.Support;

internal static class FreePort
{
    /// <summary>A loopback TCP port that was free a moment ago.</summary>
    public static int Get()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Whether anything accepts TCP connections on <paramref name="address"/>:<paramref name="port"/>.</summary>
    public static bool IsListening(IPAddress address, int port)
    {
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            return client.ConnectAsync(address, port).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch (AggregateException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
