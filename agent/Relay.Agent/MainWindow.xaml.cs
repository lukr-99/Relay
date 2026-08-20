using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Relay.Agent.Views;

namespace Relay.Agent;

public partial class MainWindow : Window
{
    private readonly AppServices _svc;
    private readonly DeckEditorView _deck;
    private readonly DevicesView _devices;
    private readonly SettingsView _settings;
    private readonly DispatcherTimer _statusTimer;

    public MainWindow(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;

        Icon = IconFactory.CreateImageSource(64);
        Brand.Source = IconFactory.CreateImageSource(48);

        _deck = new DeckEditorView(svc);
        _devices = new DevicesView(svc);
        _settings = new SettingsView(svc);
        Host.Content = _deck;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int n = _svc.Sessions.Count;
        var conn = n > 0 ? $"{n} phone{(n == 1 ? "" : "s")} connected" : "No phones connected";
        StatusText.Text = $"{conn}\n{Pairing.Pairing.LocalIpv4()}:{_svc.Config.Port}";
    }

    private void NavDeck_Checked(object sender, RoutedEventArgs e) { if (Host != null) Host.Content = _deck; }
    private void NavDevices_Checked(object sender, RoutedEventArgs e) { if (Host != null) { Host.Content = _devices; _devices.Refresh(); } }
    private void NavSettings_Checked(object sender, RoutedEventArgs e) { if (Host != null) Host.Content = _settings; }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimise to tray instead of exiting, unless the app is really quitting.
        if (!App.IsQuitting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
