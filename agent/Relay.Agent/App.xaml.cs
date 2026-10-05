using System.Windows;
using Relay.Agent.Discovery;
using Relay.Agent.Layout;
using Relay.Agent.Profiles;
using System.Net.Http;
using Relay.Agent.Providers;
using Relay.Agent.Server;
using DotNetLib.Core.Updating;
using DotNetLib.Tray;
using Relay.Agent.Theming;

namespace Relay.Agent;

public partial class App : Application
{
    private const string InstanceName = "Relay.Agent";
    private static readonly Uri AgentStyles = new("pack://application:,,,/Relay.Agent;component/Themes/Agent.xaml");

    public static bool IsQuitting { get; private set; }

    private SingleInstance? _instance;
    private AppServices? _svc;
    private AgentTray? _tray;
    private MainWindow? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One agent per user: a second launch (from the Start menu or the installer) brings the
        // running window to the front instead of fighting over the port.
        _instance = SingleInstance.TryAcquire(InstanceName);
        if (_instance is null)
        {
            SingleInstance.Knock(InstanceName);
            Shutdown();
            return;
        }

        TrayResources.Merge(Resources);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = AgentStyles });

        var config = AppConfig.Load();
        var theme = new AgentTheme(Resources);
        theme.Apply(config.Theme);
        var log = new Log(config.LogPath);
        log.Info($"Relay agent {AppInfo.Version} starting — id={config.AgentId} port={config.Port}");
        log.Info($"pairing address (QR host): {Pairing.Pairing.LocalIpv4()}:{config.Port}");

        var cert = Cert.LoadOrCreate(config, log);
        var layout = new LayoutStore(config, log);
        var buttonLibrary = new ButtonLibraryStore(config, log);
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
            Config = config, Log = log, Layout = layout, ButtonLibrary = buttonLibrary, Providers = providers,
            Router = router, Sessions = sessions, Server = server, Mdns = mdns, Cert = cert,
            ProfileStore = profileStore, Profiles = profiles, Updater = updater,
            Theme = theme, Prompts = new AppPrompts(theme),
        };

        // Best-effort startup update check — logs if a newer build is on GitHub Releases.
        _ = Task.Run(async () =>
        {
            var release = await updater.CheckForUpdateAsync();
            if (release is null) return;
            log.Info($"update available: {release.Version} (running {AppInfo.Version}).");
            _ = Dispatcher.BeginInvoke(() =>
                _tray?.Notify("Relay update", $"Version {release.Version} is available. Open Settings, Updates to install it."));
        });

        _tray = new AgentTray(_svc, theme, ShowMain, ShowSettings, QuitApp);
        _instance.Listen(() => Dispatcher.BeginInvoke(ShowMain));
        ShowMain();
    }

    private void ShowMain()
    {
        _main ??= new MainWindow(_svc!);
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void ShowSettings()
    {
        ShowMain();
        _main!.ShowSettings();
    }

    /// <summary>Quit the agent so a downloaded installer can replace its files (used by the updater).</summary>
    public static void QuitForUpdate() => (Current as App)?.QuitApp();

    private void QuitApp()
    {
        IsQuitting = true;
        try { _main?.SavePlacement(); } catch { }
        _tray?.Dispose();                        // remove the tray icon immediately
        try { _svc?.Mdns.Dispose(); } catch { }
        try { _svc?.Profiles.Dispose(); } catch { }
        try { _svc?.Theme.Dispose(); } catch { }
        try { _instance?.Dispose(); } catch { }
        _svc?.Log.Info("agent quit from tray.");

        // Stop Kestrel off the UI thread with a hard cap: Server.Dispose() blocks on StopAsync(), which
        // must not run on the WPF UI thread (it can freeze the quit). Then force-terminate — WPF
        // Shutdown() alone can leave the process alive when a hosted component (Kestrel / mDNS) still
        // holds a non-background thread, which is what left the tray process running after "Quit Relay".
        try { Task.Run(() => { try { _svc?.Server.Dispose(); } catch { } }).Wait(TimeSpan.FromSeconds(2)); }
        catch { }
        Environment.Exit(0);
    }
}
