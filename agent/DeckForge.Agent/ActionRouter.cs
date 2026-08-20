using DeckForge.Agent.Layout;
using DeckForge.Agent.Providers;

namespace DeckForge.Agent;

/// <summary>Resolves a pressed button to its action and dispatches it to the owning provider.</summary>
public sealed class ActionRouter
{
    private readonly ProviderRegistry _providers;
    private readonly LayoutStore _layout;
    private readonly Log _log;

    public ActionRouter(ProviderRegistry providers, LayoutStore layout, Log log)
    {
        _providers = providers;
        _layout = layout;
        _log = log;
    }

    public async Task PressAsync(string buttonId, bool hold = false, CancellationToken ct = default)
    {
        var button = _layout.Current.FindButton(buttonId);
        if (button is null) { _log.Warn($"press: unknown button '{buttonId}'."); return; }

        var action = hold ? button.HoldAction : button.Action;
        if (action is null) { _log.Warn($"press: button '{buttonId}' has no {(hold ? "hold " : "")}action."); return; }

        if (!_providers.TryGet(action.Provider, out var provider))
        {
            _log.Warn($"press: no provider '{action.Provider}' for button '{buttonId}'.");
            return;
        }

        try
        {
            _log.Info($"press {buttonId} -> {action.Provider}.{action.Verb} ({button.Label})");
            await provider.InvokeAsync(action.Verb, action.Params, ct);
        }
        catch (Exception ex)
        {
            _log.Error($"action failed for '{buttonId}' ({action.Provider}.{action.Verb})", ex);
        }
    }
}
