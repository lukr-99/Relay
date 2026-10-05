using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Relay.Agent.Views;

namespace Relay.Agent;

public partial class MainWindow : Window
{
    private readonly AppServices _svc;
    private readonly DeckEditorView _deck;
    private readonly PresetsView _presets;
    private readonly DevicesView _devices;
    private readonly ProfilesView _profiles;
    private readonly SettingsView _settings;
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _placementTimer;

    public MainWindow(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;
        svc.Theme.Track(this);

        Icon = IconFactory.CreateImageSource(64);
        Brand.Source = IconFactory.CreateImageSource(48);

        _deck = new DeckEditorView(svc);
        _presets = new PresetsView(svc);
        _devices = new DevicesView(svc);
        _profiles = new ProfilesView(svc);
        _settings = new SettingsView(svc);
        Host.Content = _deck;

        RestorePlacement();
        // Remember where the window is, a second after it stops moving, so even a hard exit keeps it.
        _placementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _placementTimer.Tick += (_, _) => { _placementTimer.Stop(); SavePlacement(); };
        LocationChanged += (_, _) => { _placementTimer.Stop(); _placementTimer.Start(); };
        SizeChanged += (_, _) => { _placementTimer.Stop(); _placementTimer.Start(); };
        StateChanged += (_, _) => { _placementTimer.Stop(); _placementTimer.Start(); };

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

    /// <summary>Opens the Settings tab (from the tray menu).</summary>
    public void ShowSettings() => NavSettings.IsChecked = true;

    private void NavDeck_Checked(object sender, RoutedEventArgs e) { if (Host != null) { Host.Content = _deck; _deck.SyncActivePreset(); } }
    private void NavPresets_Checked(object sender, RoutedEventArgs e) { if (Host != null) { Host.Content = _presets; _presets.Refresh(); } }
    private void NavDevices_Checked(object sender, RoutedEventArgs e) { if (Host != null) { Host.Content = _devices; _devices.Refresh(); } }
    private void NavProfiles_Checked(object sender, RoutedEventArgs e) { if (Host != null) { Host.Content = _profiles; _profiles.Refresh(); } }
    private void NavSettings_Checked(object sender, RoutedEventArgs e) { if (Host != null) Host.Content = _settings; }

    /// <summary>
    /// Opens where the window was last, on whichever monitor that was. A spot that is no longer on any
    /// screen (a monitor was unplugged) is ignored, so the window never opens out of sight.
    /// </summary>
    private void RestorePlacement()
    {
        if (_svc.Config.Window is not { } w || w.Width < MinWidth / 2 || w.Height < MinHeight / 2) return;
        if (!WindowPlacements.IsVisible(w)) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = w.Left;
        Top = w.Top;
        Width = w.Width;
        Height = w.Height;
        if (w.Maximized) WindowState = WindowState.Maximized;
    }

    /// <summary>Saves the window's normal bounds and maximized state. Called on hide, quit and after moves.</summary>
    public void SavePlacement()
    {
        if (WindowState == WindowState.Minimized) return;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty || bounds.Width <= 0) return;
        var placement = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
        if (placement == _svc.Config.Window) return;
        _svc.Config.Window = placement;
        _svc.Config.PersistState();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        // Minimise to tray instead of exiting, unless the app is really quitting.
        if (!App.IsQuitting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
