using System.Net.WebSockets;
using System.Text;
using Relay.Agent.Layout;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Relay.Agent.Server;

/// <summary>
/// Kestrel-hosted WebSocket endpoint at <c>/rpc</c>. Bearer-token auth on the handshake, then a
/// JSON-RPC 2.0 message loop. Kestrel binds sockets directly — no admin / urlacl needed.
/// Phase 0 uses ws:// on the LAN; WSS + fingerprint pinning is the next step (see docs/SECURITY.md).
/// </summary>
public sealed class DeckServer : IDisposable
{
    private readonly AppConfig _config;
    private readonly SessionManager _sessions;
    private readonly LayoutStore _layout;
    private readonly RpcDispatcher _dispatcher;
    private readonly Providers.ProviderRegistry _providers;
    private readonly Cert _cert;
    private readonly Log _log;
    private WebApplication? _app;

    public DeckServer(AppConfig config, LayoutStore layout, ActionRouter router, SessionManager sessions,
        Providers.ProviderRegistry providers, Cert cert, Log log)
    {
        _config = config;
        _sessions = sessions;
        _layout = layout;
        _providers = providers;
        _cert = cert;
        _log = log;
        _dispatcher = new RpcDispatcher(config, layout, router, providers, log);
        _layout.Changed += OnLayoutChanged;
        Func<string, bool, Task> pushState = (id, on) => _sessions.BroadcastAsync(RpcDispatcher.ButtonStateNotification(id, on));
        router.OnButtonState = pushState;
        providers.MicForge.OnButtonState = pushState;
    }

    private void OnLayoutChanged()
    {
        if (_sessions.Count == 0) return;
        _ = _sessions.BroadcastAsync(RpcDispatcher.LayoutNotification(_layout.Current));
        _log.Info($"pushed updated layout to {_sessions.Count} phone(s).");
    }

    public void Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
            k.ListenAnyIP(_config.Port, listen => listen.UseHttps(_cert.Certificate)));

        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/rpc", HandleAsync);
        app.MapGet("/", () => Results.Text("Relay agent — connect a phone to /rpc", "text/plain"));

        _app = app;
        _ = app.RunAsync();
        _log.Info($"WebSocket server listening on wss://0.0.0.0:{_config.Port}/rpc");
    }

    private async Task HandleAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var auth = ctx.Request.Headers.Authorization.ToString();
        var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth["Bearer ".Length..].Trim()
            : ctx.Request.Query["token"].ToString();

        if (!string.Equals(token, _config.Token, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            _log.Warn($"rejected unauthorized connection from {ctx.Connection.RemoteIpAddress}");
            return;
        }

        using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
        var session = new WsSession(socket);
        _sessions.Add(session);
        _log.Info($"session {session.Id} connected from {ctx.Connection.RemoteIpAddress} ({_sessions.Count} total)");
        _providers.MicForge.RepushState();   // mirror MicForge's current state onto the fresh deck
        try
        {
            await ReceiveLoop(session, socket, ctx.RequestAborted);
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            _sessions.Remove(session);
            _log.Info($"session {session.Id} disconnected ({_sessions.Count} remaining)");
        }
    }

    private async Task ReceiveLoop(WsSession session, WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        var sb = new StringBuilder();

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            sb.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text) continue;
            var json = sb.ToString();
            if (string.IsNullOrWhiteSpace(json)) continue;

            var response = await _dispatcher.DispatchAsync(session, json, ct);
            if (response is not null)
                await session.SendAsync(response, ct);
        }
    }

    public void Dispose()
    {
        try { _app?.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); } catch { }
    }
}
