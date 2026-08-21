using System.Diagnostics;
using System.Text.Json;

namespace Relay.Agent.Providers;

/// <summary>
/// Runs a command via <c>cmd.exe /c</c>. Commands are authored on the trusted PC (the editor), not
/// supplied by the phone, but this is still gated behind <see cref="AppConfig.ScriptEnabled"/>
/// (off by default) because it can run anything.
/// </summary>
public sealed class ScriptProvider : IProvider
{
    private readonly AppConfig _config;
    private readonly Log _log;
    public string Id => "script";

    public ScriptProvider(AppConfig config, Log log)
    {
        _config = config;
        _log = log;
    }

    public Task InvokeAsync(string verb, JsonElement p, CancellationToken ct = default)
    {
        if (!_config.ScriptEnabled)
        {
            _log.Warn("script: the Run-command provider is disabled (enable it in Settings).");
            return Task.CompletedTask;
        }
        if (!string.Equals(verb, "run", StringComparison.OrdinalIgnoreCase))
        {
            _log.Warn($"script: unknown verb '{verb}'.");
            return Task.CompletedTask;
        }

        var command = GetString(p, "command");
        if (string.IsNullOrWhiteSpace(command)) return Task.CompletedTask;
        var args = GetString(p, "args") ?? "";

        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c {command} {args}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _log.Error("script run failed", ex);
        }
        return Task.CompletedTask;
    }

    private static string? GetString(JsonElement p, string name)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
