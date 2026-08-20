namespace DeckForge.Agent.Discovery;

/// <summary>
/// Advertises the agent as <c>_deckforge._tcp</c> over mDNS/DNS-SD so phones on the LAN can find it
/// with zero configuration.
///
/// TODO(mDNS): wire a real DNS-SD responder. Phase 0 ships with this stubbed — the phone pairs by
/// scanning the tray QR (or typing host:port + token), which carries the IP directly. A minimal
/// UDP multicast responder on 224.0.0.251:5353 (or a maintained DNS-SD package) lands next so the
/// Android NsdManager browse can auto-discover us. See docs/PROTOCOL.md §1.
/// </summary>
public sealed class MdnsAdvertiser : IDisposable
{
    private readonly AppConfig _config;
    private readonly Log _log;

    public MdnsAdvertiser(AppConfig config, Log log)
    {
        _config = config;
        _log = log;
    }

    public void Start()
        => _log.Info($"mDNS advertising not enabled yet — pair via the tray QR / manual host:port " +
                     $"({Pairing.Pairing.LocalIpv4()}:{_config.Port}).");

    public void Dispose() { }
}
