using System.IO;
using System.Security.Cryptography;

namespace Relay.Agent;

/// <summary>
/// Runtime configuration + on-disk locations. Everything lives under %AppData%\Relay\ so it
/// survives reinstalls (same convention as MicForge).
/// </summary>
public sealed class AppConfig
{
    public required string DataDir { get; init; }
    public required string LogPath { get; init; }
    public required string LayoutPath { get; init; }
    public required string StatePath { get; init; }

    /// <summary>Port the WebSocket server binds on all interfaces.</summary>
    public int Port { get; set; } = 8731;

    /// <summary>Bearer token a phone must present to connect. Minted once, persisted in state.</summary>
    public required string Token { get; set; }

    /// <summary>Friendly name advertised over mDNS and shown in the phone's device list.</summary>
    public string DeviceName { get; set; } = Environment.MachineName;

    /// <summary>Stable agent id (survives IP changes; lets the phone re-find us).</summary>
    public required string AgentId { get; init; }

    /// <summary>Whether the Script (run-command) provider is allowed to execute. Off by default.</summary>
    public bool ScriptEnabled { get; set; }

    /// <summary>Persists the mutable state fields (token, port, script toggle) back to disk.</summary>
    public void PersistState()
    {
        var s = new AgentState { AgentId = AgentId, Token = Token, Port = Port, ScriptEnabled = ScriptEnabled };
        try { File.WriteAllText(StatePath, System.Text.Json.JsonSerializer.Serialize(s)); } catch { }
    }

    public static AppConfig Load()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Relay");
        Directory.CreateDirectory(dir);

        var statePath = Path.Combine(dir, "relay.state.json");
        var state = AgentState.LoadOrCreate(statePath);

        return new AppConfig
        {
            DataDir = dir,
            LogPath = Path.Combine(dir, "logs", "relay.log"),
            LayoutPath = Path.Combine(dir, "layout.json"),
            StatePath = statePath,
            Port = state.Port,
            Token = state.Token,
            AgentId = state.AgentId,
            ScriptEnabled = state.ScriptEnabled,
        };
    }

    public static string NewToken()
    {
        // URL-safe, ~256-bit.
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>Persisted, non-secret-plus-token agent identity/state.</summary>
public sealed class AgentState
{
    public string AgentId { get; set; } = Guid.NewGuid().ToString("n");
    public string Token { get; set; } = AppConfig.NewToken();
    public int Port { get; set; } = 8731;
    public bool ScriptEnabled { get; set; }

    public static AgentState LoadOrCreate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<AgentState>(File.ReadAllText(path));
                if (s is not null && !string.IsNullOrWhiteSpace(s.Token)) return s;
            }
        }
        catch { /* fall through to fresh state */ }

        var fresh = new AgentState();
        try { File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(fresh)); } catch { }
        return fresh;
    }
}
