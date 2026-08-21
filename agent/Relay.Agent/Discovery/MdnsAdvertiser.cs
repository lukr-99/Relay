using Makaretu.Dns;

namespace Relay.Agent.Discovery;

/// <summary>
/// Advertises the agent as <c>_relay._tcp</c> over mDNS/DNS-SD (via Makaretu) so phones on the LAN
/// can find it with zero configuration and re-find it by <c>id</c> after the PC's IP changes. The TXT
/// record carries <c>v</c>, <c>id</c>, <c>name</c> and the cert <c>fp</c> — never the token; pairing
/// still requires the token (QR or manual), so discovery only locates an agent, it doesn't authorise.
/// See docs/PROTOCOL.md §1.
/// </summary>
public sealed class MdnsAdvertiser : IDisposable
{
    private readonly AppConfig _config;
    private readonly Log _log;
    private readonly string _fingerprint;
    private ServiceDiscovery? _sd;

    public MdnsAdvertiser(AppConfig config, Log log, string fingerprint)
    {
        _config = config;
        _log = log;
        _fingerprint = fingerprint;
    }

    public void Start()
    {
        try
        {
            var instance = $"Relay on {Environment.MachineName}";
            // Advertise only reachable LAN addresses, so a phone never resolves a host-only/virtual
            // address (e.g. a VirtualBox 192.168.56.x) that it can't connect to.
            var addresses = Pairing.Pairing.LanAddresses();
            var profile = addresses.Count > 0
                ? new ServiceProfile(instance, "_relay._tcp", (ushort)_config.Port, addresses)
                : new ServiceProfile(instance, "_relay._tcp", (ushort)_config.Port);
            profile.AddProperty("v", "1");
            profile.AddProperty("id", _config.AgentId);
            profile.AddProperty("name", _config.DeviceName);
            if (!string.IsNullOrEmpty(_fingerprint)) profile.AddProperty("fp", _fingerprint);

            _sd = new ServiceDiscovery();
            _sd.Advertise(profile);
            _log.Info($"mDNS advertising _relay._tcp as '{instance}' on port {_config.Port}.");
        }
        catch (Exception ex)
        {
            // Never fatal: the phone can still pair by QR / manual host:port.
            _log.Warn($"mDNS advertising failed ({ex.GetType().Name}: {ex.Message}); pair via the tray QR " +
                      $"/ manual host:port ({Pairing.Pairing.LocalIpv4()}:{_config.Port}).");
        }
    }

    public void Dispose()
    {
        try { _sd?.Dispose(); } catch { }
    }
}
