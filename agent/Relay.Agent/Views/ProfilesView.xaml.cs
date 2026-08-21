using System.Windows;
using System.Windows.Controls;
using Relay.Agent.Profiles;

namespace Relay.Agent.Views;

/// <summary>Edits the auto-switch profiles: a master toggle, an optional default deck, and a list of
/// (app + optional title → deck) rules. Applies via <see cref="ProfileManager"/> on Save.</summary>
public partial class ProfilesView : UserControl
{
    private const string LeaveAsIs = "(leave as-is)";

    private readonly AppServices _svc;
    private readonly List<RuleRowUi> _rows = new();
    private bool _enabled;

    public ProfilesView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;
        Refresh();
    }

    /// <summary>Reloads the UI from the stored config + current preset list.</summary>
    public void Refresh()
    {
        var cfg = _svc.ProfileStore.Config;
        _enabled = cfg.Enabled;
        UpdateToggle();

        var presets = _svc.Layout.Presets;

        DefaultBox.Items.Clear();
        DefaultBox.Items.Add(LeaveAsIs);
        foreach (var p in presets) DefaultBox.Items.Add(p);
        DefaultBox.SelectedItem = string.IsNullOrWhiteSpace(cfg.DefaultPreset) ? LeaveAsIs
            : (presets.Contains(cfg.DefaultPreset) ? cfg.DefaultPreset : LeaveAsIs);

        RulesHost.Children.Clear();
        _rows.Clear();
        foreach (var r in cfg.Rules) AddRow(r.Exe, r.TitleContains, r.Preset);
        Hint.Text = "";
    }

    private IReadOnlyList<string> Presets => _svc.Layout.Presets;

    private void AddRow(string exe, string title, string preset)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        var exeBox = new TextBox { Text = exe };
        var titleBox = new TextBox { Text = title, Margin = new Thickness(8, 0, 0, 0) };
        var presetBox = new ComboBox { Margin = new Thickness(8, 0, 0, 0) };
        foreach (var p in Presets) presetBox.Items.Add(p);
        if (!string.IsNullOrWhiteSpace(preset) && !presetBox.Items.Contains(preset)) presetBox.Items.Add(preset);
        presetBox.SelectedItem = preset;

        var del = new Button { Content = "✕", Margin = new Thickness(6, 0, 0, 0), ToolTip = "Remove rule" };

        Grid.SetColumn(exeBox, 0);
        Grid.SetColumn(titleBox, 1);
        Grid.SetColumn(presetBox, 2);
        Grid.SetColumn(del, 3);
        grid.Children.Add(exeBox);
        grid.Children.Add(titleBox);
        grid.Children.Add(presetBox);
        grid.Children.Add(del);

        var row = new RuleRowUi(grid, exeBox, titleBox, presetBox);
        del.Click += (_, _) => { RulesHost.Children.Remove(grid); _rows.Remove(row); };

        _rows.Add(row);
        RulesHost.Children.Add(grid);
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
        => AddRow("", "", Presets.FirstOrDefault() ?? "");

    private void AddCurrent_Click(object sender, RoutedEventArgs e)
    {
        var exe = _svc.Profiles.CurrentExe;
        if (string.IsNullOrWhiteSpace(exe) || string.Equals(exe, "relay.agent.exe", StringComparison.OrdinalIgnoreCase))
        {
            Hint.Text = "Focus the app you want a rule for, then click again.";
            return;
        }
        AddRow(exe, "", Presets.FirstOrDefault() ?? "");
    }

    private void EnableToggle_Click(object sender, RoutedEventArgs e)
    {
        _enabled = !_enabled;
        UpdateToggle();
    }

    private void UpdateToggle()
    {
        EnableToggle.Content = _enabled ? "Auto-switch: ON" : "Auto-switch: OFF";
        EnableToggle.Style = (Style)FindResource(_enabled ? "AccentButton" : "DangerButton");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var def = DefaultBox.SelectedItem as string ?? LeaveAsIs;
        var cfg = new ProfilesConfig
        {
            Enabled = _enabled,
            DefaultPreset = def == LeaveAsIs ? "" : def,
            Rules = _rows
                .Select(r => new ProfileRule
                {
                    Exe = r.Exe.Text.Trim(),
                    TitleContains = r.Title.Text.Trim(),
                    Preset = (r.Preset.SelectedItem as string ?? r.Preset.Text)?.Trim() ?? "",
                })
                .Where(r => !string.IsNullOrWhiteSpace(r.Exe) && !string.IsNullOrWhiteSpace(r.Preset))
                .ToList(),
        };
        _svc.ProfileStore.Save(cfg);
        _svc.Profiles.ReapplyNow();
        Hint.Text = _enabled
            ? $"Saved · {cfg.Rules.Count} rule(s), auto-switch on."
            : "Saved · auto-switch off.";
    }

    private sealed record RuleRowUi(Grid Grid, TextBox Exe, TextBox Title, ComboBox Preset);
}
