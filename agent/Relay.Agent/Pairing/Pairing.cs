using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace Relay.Agent.Pairing;

/// <summary>Pairing helpers — the LAN address, the pairing URI, and its QR bitmap.</summary>
public static class Pairing
{
    /// <summary>Best-guess LAN IPv4 address for the pairing QR/dialog — the address a phone on the same
    /// Wi-Fi can actually reach.</summary>
    public static string LocalIpv4()
        => RankedIpv4().FirstOrDefault()?.ToString() ?? "127.0.0.1";

    /// <summary>IPv4 addresses to advertise over mDNS, best first. Restricted to adapters that have a
    /// default gateway (i.e. reachable from other LAN hosts) so we never advertise a host-only/virtual
    /// address like a VirtualBox/Hyper-V 192.168.56.x. Empty only if nothing qualifies.</summary>
    public static IReadOnlyList<IPAddress> LanAddresses()
    {
        var ranked = RankedIpv4WithScore();
        var reachable = ranked.Where(x => x.HasGateway).Select(x => x.Addr).ToList();
        return reachable.Count > 0 ? reachable : ranked.Select(x => x.Addr).ToList();
    }

    /// <summary>Candidate LAN IPv4 addresses, most-reachable first: real adapters with a default gateway
    /// beat virtual/host-only ones. Skips loopback, tunnels, and 169.254.x link-local.</summary>
    private static List<IPAddress> RankedIpv4()
        => RankedIpv4WithScore().Select(x => x.Addr).ToList();

    private static List<(IPAddress Addr, bool HasGateway, int Score)> RankedIpv4WithScore()
    {
        var list = new List<(IPAddress Addr, bool HasGateway, int Score)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var props = ni.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                bool virtualish = IsVirtual(ni.Description) || IsVirtual(ni.Name);

                foreach (var ip in props.UnicastAddresses)
                {
                    var a = ip.Address;
                    if (a.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a)) continue;
                    if (a.ToString().StartsWith("169.254.")) continue;   // link-local

                    // Reachable adapters (with a gateway) rank highest; virtual adapters are demoted.
                    int score = (hasGateway ? 2 : 0) + (virtualish ? 0 : 1);
                    list.Add((a, hasGateway, score));
                }
            }
        }
        catch { /* fall through to whatever we collected */ }
        return list.OrderByDescending(x => x.Score).ToList();
    }

    private static bool IsVirtual(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        s = s.ToLowerInvariant();
        return s.Contains("virtual") || s.Contains("vmware") || s.Contains("virtualbox")
            || s.Contains("hyper-v") || s.Contains("vethernet") || s.Contains("loopback")
            || s.Contains("vpn") || s.Contains("tailscale") || s.Contains("zerotier");
    }

    public static string BuildUri(string host, int port, string token, string agentId, string fp)
        => $"relay://pair?host={host}&port={port}&token={Uri.EscapeDataString(token)}&id={agentId}&fp={fp}";

    public static Bitmap Qr(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qr = new QRCode(data);
        return qr.GetGraphic(6, Color.Black, Color.White, drawQuietZones: true);
    }
}
