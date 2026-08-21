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

    public string Id => "micforge";

    /// <summary>Set by the server: pushes a button.state to phones (mirrors MicForge's real state).</summary>
    public Func<string, bool, Task>? OnButtonState;

    public MicForgeProvider(LayoutStore layout, Log log)
    {
        _layout = layout;
        _log = log;
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task InvokeAsync(string verb, JsonElement p, CancellationToken ct = default)
    {
        var line = verb.ToLowerInvariant() switch
        {
            "mute" => SetOrToggle(p, "mute"),
            "bypass" => SetOrToggle(p, "bypass"),
            "startstop" => JsonSerializer.Serialize(new { op = "toggle", target = "running" }),
            "preset" => PresetCommand(p),
            _ => null,
        };
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
                _log.Info("micforge: connected to MicForge control pipe.");
                // Do NOT write anything here: MicForge pushes hello+state unsolicited on connect, and
                // writing before we start reading would deadlock (both sides blocked writing, neither
                // reading). We only ever write in response to a button press, by which point MicForge
                // is in its read loop.
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
        if (!(root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "state"))
            return;

        var s = new State(
            Bool(root, "mute"), Bool(root, "bypass"), Bool(root, "running"),
            root.TryGetProperty("preset", out var pr) && pr.ValueKind == JsonValueKind.String ? pr.GetString() ?? "" : "");
        _last = s;
        PushButtonStates(s);
    }

    // ── deck mirroring ───────────────────────────────────────────────────────────────────────
    /// <summary>Re-push the last known MicForge state — called when a phone (re)connects.</summary>
    public void RepushState()
    {
        if (_last is { } s) PushButtonStates(s);
        else ClearButtonStates();
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
                _ => false,
            };
            _ = push(b.Id, on);
        }
    }

    private void ClearButtonStates()
    {
        if (OnButtonState is not { } push) return;
        foreach (var b in _layout.Current.AllButtons)
            if (b.Action is { } a && string.Equals(a.Provider, Id, StringComparison.OrdinalIgnoreCase))
                _ = push(b.Id, false);
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

    private static string? PresetName(JsonElement p)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _writer = null;
        try { _writeSem.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }

    private readonly record struct State(bool Mute, bool Bypass, bool Running, string Preset);
}
