using System.Text.Json;
using Relay.Agent.Layout;
using Relay.Agent.Providers;

namespace Relay.Agent;

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

        _log.Info($"press {buttonId} -> {action.Provider}.{action.Verb} ({button.Label})");
        await RunAsync(action, buttonId, ct);
    }

    /// <summary>Handles press-and-hold. A <c>holdkey</c> hold action holds the keys down between
    /// start and end (push-to-talk); any other hold action just runs once on start.</summary>
    public async Task HoldAsync(string buttonId, string phase, CancellationToken ct = default)
    {
        var button = _layout.Current.FindButton(buttonId);
        if (button?.HoldAction is not { } action) { _log.Warn($"hold: button '{buttonId}' has no hold action."); return; }

        if (string.Equals(action.Verb, "holdkey", StringComparison.OrdinalIgnoreCase))
        {
            var verb = phase == "end" ? "keyup" : "keydown";
            _log.Info($"hold {buttonId} {phase} -> os.{verb} ({button.Label})");
            if (_providers.TryGet("os", out var os))
            {
                try { await os.InvokeAsync(verb, action.Params, ct); }
                catch (Exception ex) { _log.Error($"hold failed ({buttonId})", ex); }
            }
        }
        else if (phase != "end")
        {
            _log.Info($"hold {buttonId} start -> {action.Provider}.{action.Verb} ({button.Label})");
            await RunAsync(action, buttonId, ct);
        }
    }

    /// <summary>Runs one action. <c>verb == "macro"</c> is handled here so its steps can target any
    /// provider; everything else is dispatched to the named provider.</summary>
    public async Task RunAsync(ActionDef action, string ctx, CancellationToken ct = default)
    {
        if (string.Equals(action.Verb, "macro", StringComparison.OrdinalIgnoreCase))
        {
            await RunMacroAsync(action.Params, ctx, ct);
            return;
        }

        if (!_providers.TryGet(action.Provider, out var provider))
        {
            _log.Warn($"{ctx}: no provider '{action.Provider}'.");
            return;
        }

        try
        {
            await provider.InvokeAsync(action.Verb, action.Params, ct);
        }
        catch (Exception ex)
        {
            _log.Error($"action failed ({ctx}: {action.Provider}.{action.Verb})", ex);
        }
    }

    /// <summary>Runs an ordered list of sub-actions with an optional gap between them (default 40 ms).
    /// Used for in-game chat macros: open-chat key → type text → send.</summary>
    private async Task RunMacroAsync(JsonElement p, string ctx, CancellationToken ct)
    {
        int gap = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("gapMs", out var g)
            && g.TryGetInt32(out var gv) ? gv : 40;

        if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("steps", out var steps)
            || steps.ValueKind != JsonValueKind.Array)
        {
            _log.Warn($"{ctx}: macro has no steps.");
            return;
        }

        bool first = true;
        foreach (var step in steps.EnumerateArray())
        {
            if (!first && gap > 0) await Task.Delay(gap, ct);
            first = false;
            var sub = step.Deserialize<ActionDef>(LayoutStore.Json);
            if (sub is not null) await RunAsync(sub, $"{ctx}/macro", ct);
        }
    }
}
