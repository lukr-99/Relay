using System.Windows.Forms;

namespace Relay.Agent;

/// <summary>System-tray presence. Double-click or "Open Relay" shows the main window; "Quit" exits.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(AppServices svc, Action showWindow, Action quit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Relay", null, (_, _) => showWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Relay", null, (_, _) => quit());

        _icon = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Visible = true,
            Text = $"Relay — {svc.Config.DeviceName}",
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => showWindow();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
