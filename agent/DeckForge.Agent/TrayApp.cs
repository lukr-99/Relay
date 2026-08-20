using System.Diagnostics;
using DeckForge.Agent.Layout;
using DeckForge.Agent.Server;

namespace DeckForge.Agent;

/// <summary>Tray shell (same pattern as MicForge/DL-FOV-Fixer). Right-click for pairing info,
/// the deck editor, and quit.</summary>
public sealed class TrayApp : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly SessionManager _sessions;
    private readonly LayoutStore _layout;
    private readonly Log _log;
    private readonly NotifyIcon _tray;
    private EditorForm? _editor;

    public TrayApp(AppConfig config, SessionManager sessions, LayoutStore layout, Log log)
    {
        _config = config;
        _sessions = sessions;
        _layout = layout;
        _log = log;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Edit deck…", null, (_, _) => ShowEditor());
        menu.Items.Add("Pairing info…", null, (_, _) => ShowPairing());
        menu.Items.Add("Open data folder", null, (_, _) => Process.Start(new ProcessStartInfo(_config.DataDir) { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = $"DeckForge — {Pairing.Pairing.LocalIpv4()}:{_config.Port}",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowEditor();
    }

    private void ShowEditor()
    {
        if (_editor is null || _editor.IsDisposed)
        {
            _editor = new EditorForm(_layout, _log);
            _editor.FormClosed += (_, _) => _editor = null;
            _editor.Show();
        }
        _editor.WindowState = FormWindowState.Normal;
        _editor.BringToFront();
        _editor.Activate();
    }

    private void ShowPairing()
    {
        var ip = Pairing.Pairing.LocalIpv4();
        var uri = Pairing.Pairing.BuildUri(ip, _config.Port, _config.Token, _config.AgentId);

        var form = new Form
        {
            Text = "DeckForge — Pair a phone",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(360, 470),
        };

        var pic = new PictureBox
        {
            Image = Pairing.Pairing.Qr(uri),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(30, 20),
            Size = new Size(300, 300),
        };

        var info = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 335),
            Size = new Size(320, 90),
            Text =
                $"Host:  {ip}\r\n" +
                $"Port:  {_config.Port}\r\n" +
                $"Token: {_config.Token}\r\n\r\n" +
                $"URI:   {uri}",
        };

        var copy = new Button { Text = "Copy URI", Location = new Point(20, 432), Size = new Size(100, 26) };
        copy.Click += (_, _) => { try { Clipboard.SetText(uri); } catch { } };
        var close = new Button { Text = "Close", Location = new Point(240, 432), Size = new Size(100, 26) };
        close.Click += (_, _) => form.Close();

        form.Controls.Add(pic);
        form.Controls.Add(info);
        form.Controls.Add(copy);
        form.Controls.Add(close);
        form.Show();
        form.BringToFront();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
