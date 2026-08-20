using DeckForge.Agent.Discovery;
using DeckForge.Agent.Layout;
using DeckForge.Agent.Providers;
using DeckForge.Agent.Server;

namespace DeckForge.Agent;

internal static class Program
{
    // DeckForge agent entry point — wires the WebSocket server, providers, mDNS, and the tray shell,
    // then runs the WinForms message loop. See docs/ARCHITECTURE.md for the component map.
    [STAThread]
    private static void Main()
    {
        var config = AppConfig.Load();
        var log = new Log(config.LogPath);
        log.Info($"DeckForge agent 0.1.0 starting — id={config.AgentId} port={config.Port}");

        var layout = new LayoutStore(config, log);
        var providers = new ProviderRegistry(log);
        var router = new ActionRouter(providers, layout, log);
        var sessions = new SessionManager();

        var server = new DeckServer(config, layout, router, sessions, log);
        server.Start();

        var mdns = new MdnsAdvertiser(config, log);
        mdns.Start();

        ApplicationConfiguration.Initialize();
        using var tray = new TrayApp(config, sessions, layout, log);

        Application.ApplicationExit += (_, _) =>
        {
            log.Info("shutting down");
            mdns.Dispose();
            server.Dispose();
        };

        Application.Run(tray);
    }
}
