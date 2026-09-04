using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Relay.Agent.Layout;

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
        AboutLine.Text = $"Relay {AppInfo.Version} — an Android phone as a Stream Deck for Windows.";
        UpdateLine.Text = $"You're on v{AppInfo.Version}.";
        UpdateScriptToggle();
    }

    private void UpdateScriptToggle()
    {
        var on = _svc.Config.ScriptEnabled;
        ScriptToggle.Content = on ? "Run-command: ON" : "Run-command: OFF";
        ScriptToggle.Style = (Style)FindResource(on ? "AccentButton" : "DangerButton");
    }

    private void ScriptToggle_Click(object sender, RoutedEventArgs e)
    {
        _svc.Config.ScriptEnabled = !_svc.Config.ScriptEnabled;
        _svc.Config.PersistState();
        UpdateScriptToggle();
    }

    private void AddMicForge_Click(object sender, RoutedEventArgs e)
    {
        var name = "MicForge";
        for (int n = 2; _svc.Layout.Exists(name); n++) name = $"MicForge {n}";
        if (!_svc.Layout.Create(name, PresetTemplates.MicForge()))
        { MessageBox.Show("Couldn't add the MicForge preset.", "Relay"); return; }
        _svc.Layout.SetActive(name);   // becomes active + pushed; the Presets/Deck tabs pick it up
        MessageBox.Show($"Added the MicForge preset “{name}” and made it active.", "Relay");
    }

    private void AddCoding_Click(object sender, RoutedEventArgs e)
    {
        var name = "Coding";
        for (int n = 2; _svc.Layout.Exists(name); n++) name = $"Coding {n}";
        if (!_svc.Layout.Create(name, PresetTemplates.Coding()))
        { MessageBox.Show("Couldn't add the Coding preset.", "Relay"); return; }
        _svc.Layout.SetActive(name);
        MessageBox.Show($"Added the Coding preset “{name}” and made it active.", "Relay");
    }

    private DotNetLib.Core.Updating.ReleaseInfo? _pendingUpdate;

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateLine.Text = "Checking…";
        var info = await _svc.Updater.CheckForUpdateAsync();
        CheckUpdateBtn.IsEnabled = true;
        _pendingUpdate = info;
        if (info is null)
        {
            UpdateLine.Text = $"You're up to date (v{AppInfo.Version}).";
            InstallUpdateBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            UpdateLine.Text = $"Update available: v{info.Version} (you're on v{AppInfo.Version}).";
            InstallUpdateBtn.Visibility = Visibility.Visible;
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is not { } info) return;
        InstallUpdateBtn.IsEnabled = false;
        UpdateLine.Text = $"Downloading v{info.Version}…";
        try
        {
            await _svc.Updater.DownloadAndLaunchAsync(info, "/SILENT /SUPPRESSMSGBOXES /NORESTART");
            MessageBox.Show("Installing the update — Relay will close and reopen.", "Relay");
            App.QuitForUpdate();
        }
        catch (Exception ex)
        {
            InstallUpdateBtn.IsEnabled = true;
            UpdateLine.Text = "Update failed to download. Try again, or grab it from GitHub Releases.";
            _svc.Log.Error("update install failed.", ex);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
        => Open(_svc.Config.DataDir);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
        => Open(_svc.Config.LogPath);

    private void Repo_Click(object sender, RoutedEventArgs e)
        => Open("https://github.com/lukr-99/Relay");

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        using var d = new System.Windows.Forms.SaveFileDialog
        {
            Filter = "Relay deck (*.json)|*.json", FileName = "relay-deck.json",
        };
        if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            File.WriteAllText(d.FileName, JsonSerializer.Serialize(_svc.Layout.Current, LayoutStore.Json));
        }
        catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message, "Relay"); }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        using var d = new System.Windows.Forms.OpenFileDialog { Filter = "Relay deck (*.json)|*.json" };
        if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            var layout = JsonSerializer.Deserialize<DeckLayout>(File.ReadAllText(d.FileName), LayoutStore.Json);
            if (layout is null || layout.Pages.Count == 0)
            {
                MessageBox.Show("That file isn't a valid deck.", "Relay");
                return;
            }
            _svc.Layout.Save(layout);
            MessageBox.Show("Deck imported and pushed to connected phones.", "Relay");
        }
        catch (Exception ex) { MessageBox.Show("Import failed: " + ex.Message, "Relay"); }
    }

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
