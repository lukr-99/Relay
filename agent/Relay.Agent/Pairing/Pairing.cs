using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace Relay.Agent.Pairing;

/// <summary>Pairing helpers — the LAN address, the pairing URI, and its QR bitmap.</summary>
public static class Pairing
{
    /// <summary>Best-guess LAN IPv4 address for display in the pairing dialog.</summary>
    public static string LocalIpv4()
    {
        try
        {
            // Prefer an interface that's up, not loopback/virtual, with a routable IPv4.
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                     n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address))
                    {
                        var s = ip.Address.ToString();
                        if (!s.StartsWith("169.254.")) return s; // skip link-local
                    }
                }
            }
        }
        catch { /* fall through */ }
        return "127.0.0.1";
    }

    public static string BuildUri(string host, int port, string token, string agentId)
        => $"relay://pair?host={host}&port={port}&token={Uri.EscapeDataString(token)}&id={agentId}";

    public static Bitmap Qr(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qr = new QRCode(data);
        return qr.GetGraphic(6, Color.Black, Color.White, drawQuietZones: true);
    }
}
