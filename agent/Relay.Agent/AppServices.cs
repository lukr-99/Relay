using Relay.Agent.Discovery;
using Relay.Agent.Layout;
using Relay.Agent.Profiles;
using Relay.Agent.Providers;
using Relay.Agent.Server;

namespace Relay.Agent;

/// <summary>Composition root — the live services shared across the WPF views.</summary>
public sealed class AppServices
{
    public required AppConfig Config { get; init; }
    public required Log Log { get; init; }
    public required LayoutStore Layout { get; init; }
    public required ProviderRegistry Providers { get; init; }
    public required ActionRouter Router { get; init; }
    public required SessionManager Sessions { get; init; }
    public required DeckServer Server { get; init; }
    public required MdnsAdvertiser Mdns { get; init; }
    public required Cert Cert { get; init; }
    public required ProfileStore ProfileStore { get; init; }
    public required ProfileManager Profiles { get; init; }
}
