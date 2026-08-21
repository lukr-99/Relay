using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Relay.Agent.Layout;

namespace Relay.Agent.Providers;

/// <summary>
/// Drives MicForge — mute / bypass / start-stop / preset — over its loopback control pipe, and
/// mirrors MicForge's live state back onto the deck via <c>button.state</c> (so a Mute button lights
/// up when the mic is actually muted, however it was muted). MicForge need not be running: the
/// connection is retried in the background and verbs are harmless no-ops until it comes up.
/// Contract: newline-delimited JSON, see MicForge's <c>DeckBridge</c> ("Deck Control Contract" v1).
/// </summary>
public sealed class MicForgeProvider : IProvider, IDisposable
{
    private const string PipeName = "MicForge.DeckControl";

    private readonly LayoutStore _layout;
    private readonly Log _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeSem = new(1, 1);
    private StreamWriter? _writer;
    private State? _last;
    private bool _meterWanted;

    public string Id => "micforge";

    /// <summary>Set by the server: pushes a button.state to phones (mirrors MicForge's real state).</summary>
    public Func<string, bool, Task>? OnButtonState;

    /// <summary>Set by the server: pushes a button.level (0..1) to phones for a live meter badge.</summary>
    public Func<string, double, Task>? OnButtonLevel;

    /// <summary>Set by the server: pushes a slider.value to phones so a slider reflects the real param.</summary>
    public Func<string, double, Task>? OnSliderValue;

    /// <summary>The DSP stages MicForge last reported (id + display title), for the editor's stage picker.</summary>
    public IReadOnlyList<StageInfo> KnownStages { get; private set; } = Array.Empty<StageInfo>();

    /// <summary>The DSP parameters MicForge last reported, for the editor's slider param picker.</summary>
    public IReadOnlyList<ParamInfo> KnownParams { get; private set; } = Array.Empty<ParamInfo>();

    public MicForgeProvider(LayoutStore layout, Log log)
    {
        _layout = layout;
        _log = log;
        _layout.Changed += OnLayoutChanged;
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    private void OnLayoutChanged() => _ = EvaluateMeterSubscriptionAsync();

    public async Task InvokeAsync(string verb, JsonElement p, CancellationToken ct = default)
    {
        var line = verb.ToLowerInvariant() switch
        {
            "mute" => SetOrToggle(p, "mute"),
            "bypass" => SetOrToggle(p, "bypass"),
            "startstop" => JsonSerializer.Serialize(new { op = "toggle", target = "running" }),
            "preset" => PresetCommand(p),
            "stage" => StageCommand(p),
            "param" => ParamCommand(p),
            "meter" => null,   // a meter button has no press action; it just displays the live level
            _ => null,
        };
        if (verb.Equals("meter", StringComparison.OrdinalIgnoreCase)) return;
        if (line is null) { _log.Warn($"micforge: unknown verb '{verb}'."); return; }

        if (!await WriteAsync(line, ct))
            _log.Warn("micforge: not connected — is MicForge running?");
    }

    /// <summary>A <c>value</c> param sets an explicit state; otherwise the target is toggled.</summary>
    private static string SetOrToggle(JsonElement p, string target)
    {
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("value", out var v) &&
            (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
            return JsonSerializer.Serialize(new { op = "set", target, value = v.GetBoolean() });
        return JsonSerializer.Serialize(new { op = "toggle", target });
    }

    private static string PresetCommand(JsonElement p)
    {
        if (p.ValueKind == JsonValueKind.Object)
        {
            if (p.TryGetProperty("dir", out var d) && d.ValueKind == JsonValueKind.String)
                return JsonSerializer.Serialize(new { op = "preset", dir = d.GetString() == "prev" ? "prev" : "next" });
            if (p.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                return JsonSerializer.Serialize(new { op = "preset", name = n.GetString() });
        }
        return JsonSerializer.Serialize(new { op = "preset", dir = "next" });
    }

    /// <summary>Toggle (or set) a DSP stage by id. A <c>value</c> param sets it explicitly; else it toggles.</summary>
    private static string? StageCommand(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("id", out var idEl) ||
            idEl.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(idEl.GetString()))
            return null;
        var id = idEl.GetString();
        if (p.TryGetProperty("value", out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
            return JsonSerializer.Serialize(new { op = "stage", id, value = v.GetBoolean() });
        return JsonSerializer.Serialize(new { op = "stage", id });
    }

    /// <summary>Set a DSP param to an absolute value: <c>{key, value}</c> → MicForge <c>param</c> op.</summary>
    private static string? ParamCommand(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        if (!p.TryGetProperty("key", out var k) || k.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(k.GetString())) return null;
        if (!p.TryGetProperty("value", out var v) || v.ValueKind != JsonValueKind.Number) return null;
        return JsonSerializer.Serialize(new { op = "param", key = k.GetString(), value = v.GetDouble() });
    }

    // ── connection loop ──────────────────────────────────────────────────────────────────────
    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(1500, ct);
                var reader = new StreamReader(pipe, new UTF8Encoding(false));
                var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                _writer = writer;
                _meterWanted = false;
                _log.Info("micforge: connected to MicForge control pipe.");
                // Do NOT write synchronously here before reading: MicForge pushes hello+state unsolicited
                // on connect, and writing before we start reading would deadlock. We only write on the
                // async path — a fire-and-forget meter (un)subscribe below, and button presses — by which
                // point MicForge is in its read loop.
                _ = EvaluateMeterSubscriptionAsync();
                string? line;
                while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) != null)
                    HandleMessage(line);
            }
            catch (OperationCanceledException) { break; }
            catch (TimeoutException) { /* MicForge isn't running yet */ }
            catch (Exception ex) { _log.Info($"micforge: disconnected ({ex.GetType().Name})."); }
            finally
            {
                _writer = null;
                _last = null;
                _meterWanted = false;
                KnownStages = Array.Empty<StageInfo>();
                KnownParams = Array.Empty<ParamInfo>();
                ClearButtonStates();
            }

            try { await Task.Delay(2000, ct); } catch { break; }
        }
    }

    private void HandleMessage(string line)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(line).RootElement; }
        catch { return; }
        if (root.ValueKind != JsonValueKind.Object) return;
        if (!(root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)) return;

        switch (t.GetString())
        {
            case "state": HandleState(root); break;
            case "meter": HandleMeter(root); break;
        }
    }

    private void HandleState(JsonElement root)
    {
        var stages = ParseStages(root);
        KnownStages = stages;
        KnownParams = ParseParams(root);
        var s = new State(
            Bool(root, "mute"), Bool(root, "bypass"), Bool(root, "running"),
            root.TryGetProperty("preset", out var pr) && pr.ValueKind == JsonValueKind.String ? pr.GetString() ?? "" : "",
            stages);
        _last = s;
        PushButtonStates(s);
        PushSliderValues();
    }

    private void HandleMeter(JsonElement root)
    {
        if (OnButtonLevel is not { } push) return;
        double level = root.TryGetProperty("in", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;
        foreach (var b in _layout.Current.AllButtons)
            if (b.Action is { } a && string.Equals(a.Provider, Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Verb, "meter", StringComparison.OrdinalIgnoreCase))
                _ = push(b.Id, level);
    }

    private static IReadOnlyList<StageInfo> ParseStages(JsonElement root)
    {
        if (!root.TryGetProperty("stages", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<StageInfo>();
        var list = new List<StageInfo>();
        foreach (var e in arr.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            var id = e.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            var title = e.TryGetProperty("title", out var ti) && ti.ValueKind == JsonValueKind.String ? ti.GetString() ?? id : id;
            list.Add(new StageInfo(id!, title!, Bool(e, "enabled"), Bool(e, "canToggle")));
        }
        return list;
    }

    private static IReadOnlyList<ParamInfo> ParseParams(JsonElement root)
    {
        if (!root.TryGetProperty("params", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<ParamInfo>();
        var list = new List<ParamInfo>();
        foreach (var e in arr.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            var key = Str(e, "key");
            if (string.IsNullOrEmpty(key)) continue;
            list.Add(new ParamInfo(key, Str(e, "stage"), Str(e, "label"),
                Num(e, "value"), Num(e, "min"), Num(e, "max"), Num(e, "step"), Str(e, "unit")));
        }
        return list;
    }

    /// <summary>Push each MicForge-bound slider's current param value to phones (initial sync + updates).</summary>
    private void PushSliderValues()
    {
        if (OnSliderValue is not { } push) return;
        foreach (var sl in _layout.Current.Sliders)
        {
            if (sl.Action is not { } a || !string.Equals(a.Provider, Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.Verb, "param", StringComparison.OrdinalIgnoreCase)) continue;
            var key = ParamKeyOf(a.Params);
            if (key is null) continue;
            foreach (var pi in KnownParams)
                if (string.Equals(pi.Key, key, StringComparison.Ordinal)) { _ = push(sl.Id, pi.Value); break; }
        }
    }

    private static string? ParamKeyOf(JsonElement p)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("key", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    // ── deck mirroring ───────────────────────────────────────────────────────────────────────
    /// <summary>Re-push the last known MicForge state — called when a phone (re)connects.</summary>
    public void RepushState()
    {
        if (_last is { } s) PushButtonStates(s);
        else ClearButtonStates();
        PushSliderValues();
    }

    private void PushButtonStates(State s)
    {
        if (OnButtonState is not { } push) return;
        foreach (var b in _layout.Current.AllButtons)
        {
            if (b.Action is not { } a || !string.Equals(a.Provider, Id, StringComparison.OrdinalIgnoreCase)) continue;
            bool on = a.Verb.ToLowerInvariant() switch
            {
                "mute" => s.Mute,
                "bypass" => s.Bypass,
                "startstop" => s.Running,
                "preset" => PresetName(a.Params) is { Length: > 0 } name && string.Equals(name, s.Preset, StringComparison.OrdinalIgnoreCase),
                "stage" => StageId(a.Params) is { Length: > 0 } sid && s.Stages.Any(st => string.Equals(st.Id, sid, StringComparison.OrdinalIgnoreCase) && st.Enabled),
                _ => false,
            };
            _ = push(b.Id, on);
        }
    }

    private void ClearButtonStates()
    {
        foreach (var b in _layout.Current.AllButtons)
        {
            if (b.Action is not { } a || !string.Equals(a.Provider, Id, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(a.Verb, "meter", StringComparison.OrdinalIgnoreCase)) { if (OnButtonLevel is { } pl) _ = pl(b.Id, 0.0); }
            else if (OnButtonState is { } push) _ = push(b.Id, false);
        }
    }

    /// <summary>Writes one command line. Async throughout: the pipe is opened for overlapped I/O and a
    /// read is always pending, so a *synchronous* write on the same handle would deadlock — use the async
    /// path and serialize writers with a semaphore.</summary>
    private async Task<bool> WriteAsync(string line, CancellationToken ct)
    {
        var writer = _writer;
        if (writer is null) return false;
        try
        {
            await _writeSem.WaitAsync(ct);
            try { await writer.WriteLineAsync(line.AsMemory(), ct); return true; }
            finally { _writeSem.Release(); }
        }
        catch { return false; }
    }

    private static bool Bool(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double Num(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;

    private static string? PresetName(JsonElement p)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static string? StageId(JsonElement p)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    /// <summary>Subscribe to MicForge's level stream only while the active deck has a meter button —
    /// no point streaming ~10 Hz to nothing. Called on connect and whenever the layout changes.</summary>
    private async Task EvaluateMeterSubscriptionAsync()
    {
        bool want = _layout.Current.AllButtons.Any(b =>
            b.Action is { } a && string.Equals(a.Provider, Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Verb, "meter", StringComparison.OrdinalIgnoreCase));
        if (want == _meterWanted) return;
        _meterWanted = want;
        await WriteAsync(JsonSerializer.Serialize(new { op = "meter", enabled = want }), _cts.Token);
    }

    public void Dispose()
    {
        try { _layout.Changed -= OnLayoutChanged; } catch { }
        try { _cts.Cancel(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _writer = null;
        try { _writeSem.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }

    private readonly record struct State(bool Mute, bool Bypass, bool Running, string Preset, IReadOnlyList<StageInfo> Stages);

    /// <summary>A DSP stage MicForge reports: stable <paramref name="Id"/> (processor name) + display
    /// <paramref name="Title"/>, its current enabled state, and whether it can be toggled at all.</summary>
    public readonly record struct StageInfo(string Id, string Title, bool Enabled, bool CanToggle);

    /// <summary>A MicForge DSP parameter: stable <paramref name="Key"/> ("stageId|label"), its display
    /// stage/label, current value, and range for a slider.</summary>
    public readonly record struct ParamInfo(string Key, string Stage, string Label,
        double Value, double Min, double Max, double Step, string Unit);
}
