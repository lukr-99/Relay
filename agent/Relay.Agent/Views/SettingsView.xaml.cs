using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Relay.Agent.Views;

public partial class SettingsView : UserControl
{
    private readonly AppServices _svc;

    public SettingsView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;
        NameLine.Text = $"Device name   {svc.Config.DeviceName}";
        PortLine.Text = $"Port          {svc.Config.Port}";
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
        => Open(_svc.Config.DataDir);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
        => Open(_svc.Config.LogPath);

    private void Repo_Click(object sender, RoutedEventArgs e)
        => Open("https://github.com/lukr-99/Relay");

    private void Regen_Click(object sender, RoutedEventArgs e)
    {
        var ok = MessageBox.Show(
            "Regenerate the pairing token? Every paired phone will need to re-pair after you restart Relay.",
            "Relay", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.OK) return;

        var state = new AgentState
        {
            AgentId = _svc.Config.AgentId,
            Port = _svc.Config.Port,
            Token = AppConfig.NewToken(),
        };
        try
        {
            File.WriteAllText(_svc.Config.StatePath, JsonSerializer.Serialize(state));
            MessageBox.Show("New token saved. Restart Relay and re-pair your phone.", "Relay");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Couldn't save: " + ex.Message, "Relay");
        }
    }

    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }
}
