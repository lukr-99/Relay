using System.Windows;
using System.Windows.Controls;
using Relay.Agent.Layout;

namespace Relay.Agent.Views;

/// <summary>Manages the deck presets — switch the active one, and New / Duplicate / Rename / Delete.
/// The Deck editor edits whichever is active here; the ＋MicForge starter lives in Settings.</summary>
public partial class PresetsView : UserControl
{
    private readonly AppServices _svc;
    private bool _loading;

    public PresetsView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;
        Refresh();
    }

    /// <summary>Reloads the preset list + selection from the store.</summary>
    public void Refresh()
    {
        _loading = true;
        PresetList.Items.Clear();
        foreach (var p in _svc.Layout.Presets) PresetList.Items.Add(p);
        PresetList.SelectedItem = _svc.Layout.ActivePreset;
        _loading = false;
        Hint.Text = "";
    }

    private void Preset_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (PresetList.SelectedItem is not string name || name == _svc.Layout.ActivePreset) return;
        _svc.Layout.SetActive(name);   // loads + pushes to phones
        Hint.Text = $"Active preset: “{name}”.";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var name = _svc.Prompts.AskLine("New preset", "Name", "")?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_svc.Layout.Exists(name)) { _svc.Prompts.Inform("A preset with that name already exists."); return; }
        var blank = new DeckLayout
        {
            Grid = new Relay.Agent.Layout.Grid { Cols = 4, Rows = 3 }, ActivePage = "p-main",
            Pages = { new Relay.Agent.Layout.Page { Id = "p-main", Name = "Main" } },
        };
        if (!_svc.Layout.Create(name, blank)) { _svc.Prompts.Inform("Couldn't create that preset."); return; }
        _svc.Layout.SetActive(name);
        Refresh();
        Hint.Text = $"Created “{name}”.";
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        var from = _svc.Layout.ActivePreset;
        var name = _svc.Prompts.AskLine("Duplicate preset", "New name", from + " copy")?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_svc.Layout.Exists(name)) { _svc.Prompts.Inform("A preset with that name already exists."); return; }
        if (!_svc.Layout.Duplicate(from, name)) { _svc.Prompts.Inform("Couldn't duplicate the preset."); return; }
        _svc.Layout.SetActive(name);
        Refresh();
        Hint.Text = $"Duplicated to “{name}”.";
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        var current = _svc.Layout.ActivePreset;
        var name = _svc.Prompts.AskLine("Rename preset", "New name", current)?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name == current) return;
        if (_svc.Layout.Exists(name)) { _svc.Prompts.Inform("A preset with that name already exists."); return; }
        if (!_svc.Layout.Rename(current, name)) { _svc.Prompts.Inform("Couldn't rename the preset."); return; }
        Refresh();
        Hint.Text = $"Renamed to “{name}”.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var name = _svc.Layout.ActivePreset;
        if (_svc.Layout.Presets.Count <= 1) { _svc.Prompts.Inform("You need at least one preset."); return; }
        if (!_svc.Prompts.ConfirmDanger($"Delete preset “{name}”?", "Its pages and buttons go.", "Delete", Window.GetWindow(this))) return;
        _svc.Layout.Delete(name);   // another preset becomes active + is pushed
        Refresh();
        Hint.Text = $"Deleted “{name}”; now on “{_svc.Layout.ActivePreset}”.";
    }
}
