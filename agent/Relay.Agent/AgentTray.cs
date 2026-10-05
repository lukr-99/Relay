using System.Windows.Threading;
using DotNetLib.Tray;
using Relay.Agent.Theming;

namespace Relay.Agent;

/// <summary>
/// The tray icon and its menu, on the kit's <see cref="TrayIconHost"/>. The icon color shows the
/// status (see <see cref="AgentStatus"/>) and the tooltip says it in words. The icon and the menu are
/// built again whenever the status or the theme changes, so nothing is stale when the menu opens.
/// A left click opens the main window, which is also the bold default item.
/// </summary>
internal sealed class AgentTray : IDisposable
{
    private readonly AppServices _svc;
    private readonly AgentTheme _theme;
    private readonly Action _showWindow;
    private readonly Action _showSettings;
    private readonly Action _quit;
    private readonly TrayIconHost _icon;
    private readonly DispatcherTimer _poll;
    private (AgentStatus Status, int Phones, TrayThemeMode Theme)? _shown;

    public AgentTray(AppServices svc, AgentTheme theme, Action showWindow, Action showSettings, Action quit)
    {
        _svc = svc;
        _theme = theme;
        _showWindow = showWindow;
        _showSettings = showSettings;
        _quit = quit;
        _icon = new TrayIconHost("Relay", leftClick: showWindow);

        // Phones come and go on server threads with no event, so the status is read once a second
        // and the tray is built again only when it changed.
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _poll.Tick += (_, _) => Render();
        _poll.Start();
        _theme.Applied += (_, _) => Render(force: true);
        Render(force: true);
    }

    public AgentStatus Status => _svc.Server.IsFaulted ? AgentStatus.Failed
        : _svc.Sessions.Count > 0 ? AgentStatus.Connected
        : AgentStatus.Waiting;

    /// <summary>The status in words, for the tooltip and the menu's first line.</summary>
    public string StatusLine
    {
        get
        {
            int phones = _svc.Sessions.Count;
            return Status switch
            {
                AgentStatus.Failed => $"Relay {AppInfo.Version}: not running (port {_svc.Config.Port} may be in use)",
                AgentStatus.Connected => $"Relay {AppInfo.Version}: {phones} phone{(phones == 1 ? "" : "s")} connected",
                _ => $"Relay {AppInfo.Version}: waiting for a phone",
            };
        }
    }

    public void Notify(string title, string message) => _icon.Notify(title, message);

    public void Render(bool force = false)
    {
        var now = (Status, _svc.Sessions.Count, _theme.Mode);
        if (!force && _shown == now)
        {
            return;
        }

        _shown = now;
        _icon.SetIcon(IconFactory.TrayIcon(now.Status));
        _icon.SetToolTip(StatusLine);
        _icon.SetMenu(BuildMenu());
    }

    private System.Windows.Controls.ContextMenu BuildMenu()
    {
        var menu = new TrayMenuBuilder();
        menu.Label(StatusLine);
        menu.Separator();
        menu.Item("Open Relay", _showWindow, isDefault: true);
        menu.Item("Settings", _showSettings);
        menu.Submenu("Theme", theme =>
        {
            theme.Item("Same as Windows", () => _svc.SetTheme(TrayThemeMode.System), isChecked: _theme.Mode == TrayThemeMode.System);
            theme.Item("Light", () => _svc.SetTheme(TrayThemeMode.Light), isChecked: _theme.Mode == TrayThemeMode.Light);
            theme.Item("Dark", () => _svc.SetTheme(TrayThemeMode.Dark), isChecked: _theme.Mode == TrayThemeMode.Dark);
        });
        menu.Separator();
        menu.Item("Quit Relay", _quit);
        return menu.Build();
    }

    public void Dispose()
    {
        _poll.Stop();
        _icon.Dispose();
    }
}
