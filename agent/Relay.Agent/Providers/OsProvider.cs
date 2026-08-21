using System.Diagnostics;
using System.Text.Json;

namespace Relay.Agent.Providers;

/// <summary>Operating-system actions: hotkeys, media keys, launch, open, type text.</summary>
public sealed class OsProvider : IProvider
{
    private readonly Log _log;
    public string Id => "os";

    public OsProvider(Log log) => _log = log;

    public Task InvokeAsync(string verb, JsonElement p, CancellationToken ct = default)
    {
        switch (verb.ToLowerInvariant())
        {
            case "hotkey":
                var keys = ReadStringArray(p, "keys");
                if (keys.Count > 0) NativeInput.SendChord(keys);
                break;

            case "media":
                NativeInput.Media(GetString(p, "cmd") ?? "");
                break;

            case "keydown":
                var down = ReadStringArray(p, "keys");
                if (down.Count > 0) NativeInput.HoldDown(down);
                break;

            case "keyup":
                var up = ReadStringArray(p, "keys");
                if (up.Count > 0) NativeInput.HoldUp(up);
                break;

            case "text":
                NativeInput.TypeText(GetString(p, "value") ?? "");
                break;

            case "launch":
                var path = GetString(p, "path");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                    if (GetString(p, "args") is { Length: > 0 } args) psi.Arguments = args;
                    if (GetString(p, "cwd") is { Length: > 0 } cwd) psi.WorkingDirectory = cwd;
                    Process.Start(psi);
                }
                break;

            case "open":
                var url = GetString(p, "url");
                if (!string.IsNullOrWhiteSpace(url))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                break;

            default:
                _log.Warn($"os: unknown verb '{verb}'.");
                break;
        }
        return Task.CompletedTask;
    }

    private static string? GetString(JsonElement p, string name)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static List<string> ReadStringArray(JsonElement p, string name)
    {
        var list = new List<string>();
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String && el.GetString() is { } s)
                    list.Add(s);
        return list;
    }
}
