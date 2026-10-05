using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DotNetLib.Core.Updating;
using DotNetLib.Tray;
using Microsoft.Win32;
using Relay.Agent.Layout;

namespace Relay.Agent.Views;

public partial class SettingsView : UserControl
{
    private readonly AppServices _svc;
    private ReleaseInfo? _pendingUpdate;
    private bool _loading;

    public SettingsView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;

        _loading = true;
        ThemeRow.SelectedItem = svc.Config.Theme.ToString();
        ScriptRow.IsOn = svc.Config.ScriptEnabled;
        _loading = false;

        NameRow.Value = svc.Config.DeviceName;
        AddressRow.Value = $"{Pairing.Pairing.LocalIpv4()}:{svc.Config.Port}";
        VersionRow.Value = AppInfo.Version;
        RegenRow.PrepareConfirm = svc.Theme.Attach;

        // The tray menu can change the theme too, so the row follows it.
        svc.Theme.Applied += (_, _) =>
        {
            _loading = true;
            ThemeRow.SelectedItem = svc.Theme.Mode.ToString();
            _loading = false;
        };
    }

    /// <summary>Scrolls to a section and lights it up, for deep links such as an update notice.</summary>
    public void JumpTo(string sectionId) => Page.JumpTo(sectionId);

    private void Theme_Changed(object? sender, object option)
    {
        if (_loading || !Enum.TryParse<TrayThemeMode>(option as string, out var mode)) return;
        _svc.SetTheme(mode);
    }

    private void Script_Toggled(object? sender, bool on)
    {
        if (_loading) return;
        _svc.Config.ScriptEnabled = on;
        _svc.Config.PersistState();
    }

    private void AddMicForge_Click(object sender, RoutedEventArgs e) =>
        AddPreset(MicForgeRow, "MicForge", PresetTemplates.MicForge());

    private void AddCoding_Click(object sender, RoutedEventArgs e) =>
        AddPreset(CodingRow, "Coding", PresetTemplates.Coding());

    private void AddPreset(ButtonRow row, string baseName, DeckLayout layout)
    {
        var name = baseName;
        for (int n = 2; _svc.Layout.Exists(name); n++) name = $"{baseName} {n}";
        if (!_svc.Layout.Create(name, layout))
        {
            row.ShowResult($"Couldn't add the {baseName} preset.", isError: true);
            return;
        }

        _svc.Layout.SetActive(name);   // becomes active and is pushed; the Presets and Deck tabs pick it up
        row.ShowResult($"Added “{name}” and made it active.");
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckRow.IsBusy = true;
        try
        {
            _pendingUpdate = await _svc.Updater.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            _svc.Log.Error("update check failed.", ex);
            CheckRow.ShowResult("Couldn't reach GitHub. Try again later.", isError: true);
            return;
        }
        finally
        {
            CheckRow.IsBusy = false;
        }

        if (_pendingUpdate is null)
        {
            InstallRow.Visibility = Visibility.Collapsed;
            CheckRow.ShowResult($"You have the latest version ({AppInfo.Version}).");
        }
        else
        {
            InstallRow.Hint = $"Version {_pendingUpdate.Version} is ready. Relay closes and opens again.";
            InstallRow.Visibility = Visibility.Visible;
            CheckRow.ShowResult($"Version {_pendingUpdate.Version} is available.");
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is not { } info) return;
        InstallRow.IsBusy = true;
        try
        {
            await _svc.Updater.DownloadAndLaunchAsync(info, "/SILENT /SUPPRESSMSGBOXES /NORESTART");
            App.QuitForUpdate();
        }
        catch (Exception ex)
        {
            InstallRow.IsBusy = false;
            InstallRow.ShowResult("The download failed. Try again, or get it from the release notes.", isError: true);
            _svc.Log.Error("update install failed.", ex);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var d = new SaveFileDialog { Filter = "Relay deck (*.json)|*.json", FileName = "relay-deck.json" };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            File.WriteAllText(d.FileName, JsonSerializer.Serialize(_svc.Layout.Current, LayoutStore.Json));
            ExportRow.ShowResult($"Saved to {Path.GetFileName(d.FileName)}.");
        }
        catch (Exception ex) { ExportRow.ShowResult("Export failed: " + ex.Message, isError: true); }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "Relay deck (*.json)|*.json" };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            var layout = JsonSerializer.Deserialize<DeckLayout>(File.ReadAllText(d.FileName), LayoutStore.Json);
            if (layout is null || layout.Pages.Count == 0)
            {
                ImportRow.ShowResult("That file isn't a valid deck.", isError: true);
                return;
            }
            _svc.Layout.Save(layout);
            ImportRow.ShowResult("Imported and sent to connected phones.");
        }
        catch (Exception ex) { ImportRow.ShowResult("Import failed: " + ex.Message, isError: true); }
    }

    // The DangerRow has already asked; this runs only when the user chose Regenerate.
    private void Regen_Click(object sender, RoutedEventArgs e)
    {
        // The new token takes effect on the next start, so the running config keeps the old one.
        var state = new AgentState
        {
            AgentId = _svc.Config.AgentId,
            Port = _svc.Config.Port,
            Token = AppConfig.NewToken(),
            ScriptEnabled = _svc.Config.ScriptEnabled,
            Theme = _svc.Config.Theme.ToString(),
        };
        try
        {
            File.WriteAllText(_svc.Config.StatePath, JsonSerializer.Serialize(state));
            RegenRow.ShowResult("New token saved. Restart Relay and pair your phones again.");
        }
        catch (Exception ex)
        {
            RegenRow.ShowResult("Couldn't save: " + ex.Message, isError: true);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => Open(_svc.Config.DataDir);

    private void OpenLog_Click(object sender, RoutedEventArgs e) => Open(_svc.Config.LogPath);

    private void Releases_Click(object sender, RoutedEventArgs e) => Open("https://github.com/lukr-99/Relay/releases");

    private void Repo_Click(object sender, RoutedEventArgs e) => Open("https://github.com/lukr-99/Relay");

    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }
}
