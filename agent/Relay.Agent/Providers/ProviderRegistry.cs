namespace Relay.Agent.Providers;

/// <summary>Holds the available providers, keyed by <see cref="IProvider.Id"/>.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(AppConfig config, Log log)
    {
        Register(new OsProvider(log));
        Register(new ScriptProvider(config, log));
        // Future: ObsProvider, MicForgeProvider (see docs/INTEGRATIONS.md).
    }

    public void Register(IProvider p) => _providers[p.Id] = p;

    public bool TryGet(string id, out IProvider provider) => _providers.TryGetValue(id, out provider!);

    public IReadOnlyCollection<string> Ids => _providers.Keys;
}
