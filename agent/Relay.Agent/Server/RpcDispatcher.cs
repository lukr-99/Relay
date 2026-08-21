using System.Text.Json;
using Relay.Agent.Layout;

namespace Relay.Agent.Server;

/// <summary>
/// Handles one JSON-RPC 2.0 message for a session. Requests (with <c>id</c>) get a response string;
/// notifications (no <c>id</c>) return null. See docs/PROTOCOL.md for the method catalog.
/// </summary>
public sealed class RpcDispatcher
{
    public static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private readonly AppConfig _config;
    private readonly LayoutStore _layout;
    private readonly ActionRouter _router;
    private readonly Providers.ProviderRegistry _providers;
    private readonly Log _log;

    public RpcDispatcher(AppConfig config, LayoutStore layout, ActionRouter router,
        Providers.ProviderRegistry providers, Log log)
    {
        _config = config;
        _layout = layout;
        _router = router;
        _providers = providers;
        _log = log;
    }

    public async Task<string?> DispatchAsync(WsSession session, string json, CancellationToken ct)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement; }
        catch { return Error(null, -32700, "parse error"); }

        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        JsonElement? id = root.TryGetProperty("id", out var idEl) ? idEl.Clone() : null;
        var p = root.TryGetProperty("params", out var pe) ? pe : default;

        if (string.IsNullOrEmpty(method))
            return id is null ? null : Error(id, -32600, "invalid request");

        switch (method)
        {
            case "session.hello":
                session.DeviceName = TryGetPath(p, "device", "name");
                _log.Info($"session {session.Id} hello from '{session.DeviceName ?? "?"}'");
                return Result(id, new
                {
                    agent = new { id = _config.AgentId, name = _config.DeviceName, version = "0.2.0" },
                    capabilities = System.Linq.Enumerable.ToArray(_providers.Ids),
                });

            case "deck.getLayout":
                return Result(id, _layout.Current);

            case "deck.subscribe":
                return Result(id, new { ok = true });

            case "ping":
                return Result(id, new { t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            case "button.press":
                if (GetString(p, "id") is { } pid) await _router.PressAsync(pid, hold: false, ct);
                return null; // notification

            case "button.hold":
                if (GetString(p, "id") is { } hid)
                    await _router.HoldAsync(hid, GetString(p, "phase") ?? "start", ct);
                return null;

            default:
                return id is null ? null : Error(id, -32601, $"method not found: {method}");
        }
    }

    /// <summary>Builds a <c>deck.layout</c> notification for pushing an updated layout to phones.</summary>
    public static string LayoutNotification(DeckLayout layout)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "deck.layout", @params = layout }, Wire);

    /// <summary>Builds a <c>button.state</c> notification (e.g. a toggle flipping on/off).</summary>
    public static string ButtonStateNotification(string id, bool on)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "button.state", @params = new { id, on } }, Wire);

    private static string Result(JsonElement? id, object result)
        => JsonSerializer.Serialize(new Envelope { Id = id, Result = result }, Wire);

    private static string Error(JsonElement? id, int code, string message)
        => JsonSerializer.Serialize(new Envelope { Id = id, Error = new RpcError { Code = code, Message = message } }, Wire);

    private static string? GetString(JsonElement p, string name)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static string? TryGetPath(JsonElement p, string a, string b)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(a, out var inner) ? GetString(inner, b) : null;

    private sealed class Envelope
    {
        public string Jsonrpc => "2.0";
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public JsonElement? Id { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public object? Result { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public RpcError? Error { get; set; }
    }

    public sealed class RpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = "";
    }
}
