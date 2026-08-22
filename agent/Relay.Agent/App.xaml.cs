using System.Windows;
using Relay.Agent.Discovery;
using Relay.Agent.Layout;
using Relay.Agent.Profiles;
using System.Net.Http;
using Relay.Agent.Providers;
using Relay.Agent.Server;
using DotNetLib.Core.Updating;

namespace Relay.Agent;

public partial class App : Application
{
    public static bool IsQuitting { get; private set; }

    private AppServices? _svc;
    private TrayIcon? _tray;
    private MainWindow? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = AppConfig.Load();
        var log = new Log(config.LogPath);
        log.Info($"Relay agent {AppInfo.Version} starting — id={config.AgentId} port={config.Port}");
        log.Info($"pairing address (QR host): {Pairing.Pairing.LocalIpv4()}:{config.Port}");

        var cert = Cert.LoadOrCreate(config, log);
        var layout = new LayoutStore(config, log);
        var providers = new ProviderRegistry(config, layout, log);
        var router = new ActionRouter(providers, layout, log);
        var sessions = new SessionManager();
        var server = new DeckServer(config, layout, router, sessions, providers, cert, log);
        server.Start();
        var mdns = new MdnsAdvertiser(config, log, cert.FingerprintHex);
        mdns.Start();
        var profileStore = new ProfileStore(config, log);
        var profiles = new ProfileManager(new ForegroundWatcher(), profileStore, layout, log);
        profiles.Start();
        var http = new HttpClient();
        var updater = new UpdateService(
            new GitHubReleaseSource(http, "lukr-99", "Relay", name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)),
            AppInfo.Version, http);

        _svc = new AppServices
        {
            Config = config, Log = log, Layout = layout, Providers = providers,
            Router = router, Sessions = sessions, Server = server, Mdns = mdns, Cert = cert,
            ProfileStore = profileStore, Profiles = profiles, Updater = updater,
        };

        // Best-effort startup update check — logs if a newer build is on GitHub Releases.
        _ = Task.Run(async () =>
        {
            var release = await updater.CheckForUpdateAsync();
            if (release is not null) log.Info($"update available: {release.Version} (running {AppInfo.Version}).");
        });

        _tray = new TrayIcon(_svc, ShowMain, QuitApp);
        ShowMain();
    }

    private void ShowMain()
    {
        _main ??= new MainWindow(_svc!);
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    /// <summary>Quit the agent so a downloaded installer can replace its files (used by the updater).</summary>
    public static void QuitForUpdate() => (Current as App)?.QuitApp();

    private void QuitApp()
    {
        IsQuitting = true;
        try { _svc?.Server.Dispose(); _svc?.Mdns.Dispose(); _svc?.Profiles.Dispose(); } catch { }
        _tray?.Dispose();
        Shutdown();
    }
}
