using Relay.Agent.Layout;

namespace Relay.Agent.Providers;

/// <summary>Holds the available providers, keyed by <see cref="IProvider.Id"/>.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The MicForge bridge, exposed so the server can push its live state to phones.</summary>
    public MicForgeProvider MicForge { get; }

    public ProviderRegistry(AppConfig config, LayoutStore layout, Log log)
    {
        Register(new OsProvider(log));
        Register(new ScriptProvider(config, log));
        MicForge = new MicForgeProvider(layout, log);
        Register(MicForge);
        // Future: ObsProvider (see docs/INTEGRATIONS.md).
    }

    public void Register(IProvider p) => _providers[p.Id] = p;

    public bool TryGet(string id, out IProvider provider) => _providers.TryGetValue(id, out provider!);

    public IReadOnlyCollection<string> Ids => _providers.Keys;
}
