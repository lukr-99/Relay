using System.Net.WebSockets;
using System.Text;

namespace Relay.Agent.Server;

/// <summary>One connected phone. Serializes outbound sends over the single WebSocket.</summary>
public sealed class WsSession
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string Id { get; } = Guid.NewGuid().ToString("n")[..8];
    public string? DeviceName { get; set; }

    public WsSession(WebSocket socket) => _socket = socket;

    public async Task SendAsync(string json, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(ct);
        try
        {
            if (_socket.State == WebSocketState.Open)
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally { _sendLock.Release(); }
    }
}
