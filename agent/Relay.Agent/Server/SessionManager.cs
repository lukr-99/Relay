using System.Collections.Concurrent;

namespace Relay.Agent.Server;

/// <summary>Tracks connected phones so state events can be broadcast to them.</summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, WsSession> _sessions = new();

    public void Add(WsSession s) => _sessions[s.Id] = s;
    public void Remove(WsSession s) => _sessions.TryRemove(s.Id, out _);
    public int Count => _sessions.Count;

    public IEnumerable<WsSession> All => _sessions.Values;

    /// <summary>Fire-and-forget broadcast of a pre-serialized JSON message to every session.</summary>
    public async Task BroadcastAsync(string json, CancellationToken ct = default)
    {
        foreach (var s in _sessions.Values)
        {
            try { await s.SendAsync(json, ct); } catch { /* dropped session; receive loop will clean up */ }
        }
    }
}
