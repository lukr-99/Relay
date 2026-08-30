using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Relay.Agent.Icons;
using Relay.Agent.Layout;
using Page = Relay.Agent.Layout.Page;

namespace Relay.Agent.Views;

public partial class DeckEditorView : UserControl
{
    private static readonly string[] Types =
        { "Hotkey", "Media key", "Type text", "Chat macro", "Launch app", "Open URL", "Open folder", "Run command", "Hold key (PTT)", "Toggle", "Screenshot", "Open screenshots", "MicForge" };

    // Press animations the phone can play; "None" maps to no effect.
    private static readonly string[] Effects =
        { "None", "Pop", "Bounce", "Glow", "Shake", "Ripple", "Flash",
          "Fire", "Explosion", "Confetti", "Sparkle", "Hearts", "Stars" };

    private static readonly string[] MicForgeControls =
        { "Mute", "Bypass", "Start / Stop", "Next preset", "Previous preset", "Preset by name", "DSP stage", "Input meter" };

    private readonly AppServices _svc;
    // For the MicForge "DSP stage" picker: maps between a stage's display title and its stable id.
    private readonly Dictionary<string, string> _stageIdByTitle = new();
    private readonly Dictionary<string, string> _stageTitleById = new();
    private DeckLayout _work = new();
    private Page _page = new();

    private readonly Dictionary<string, Control> _p = new();
    private string? _selectedId;
    // The selected button's current icon: a catalog name, or a "data:image/..." URI for a custom image.
    private string? _icon;
    private string _loadedPreset = "";
    private bool _loading;
    private bool _loadingPresets;
    private Point _dragStart;

    public DeckEditorView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;

        for (int i = 1; i <= 8; i++) { ColsBox.Items.Add(i); RowsBox.Items.Add(i); }
        foreach (var n in IconCatalog.Names) IconBox.Items.Add(new IconChoice(n, Glyph(n)));
        foreach (var t in Types) TypeBox.Items.Add(t);
        foreach (var ef in Effects) EffectBox.Items.Add(ef);

        LoadFromStore();
    }

    private void LoadFromStore()
    {
        _work = JsonSerializer.Deserialize<DeckLayout>(
            JsonSerializer.Serialize(_svc.Layout.Current, LayoutStore.Json), LayoutStore.Json) ?? new DeckLayout();
        if (_work.Pages.Count == 0) _work.Pages.Add(new Page { Id = "p-main", Name = "Main" });
        _page = _work.Pages[0];

        _loading = true;
        ColsBox.SelectedItem = Clamp(_work.Grid.Cols);
        RowsBox.SelectedItem = Clamp(_work.Grid.Rows);
        PopulatePages(0);
        _loading = false;

        PopulatePresets(_svc.Layout.ActivePreset);
        _loadedPreset = _svc.Layout.ActivePreset;
        RebuildGrid();
        PopulateSliders();
        Select(null);
    }

    // ── presets ──────────────────────────────────────────────────────────────────────────────
    private void PopulatePresets(string select)
    {
        _loadingPresets = true;
        PresetBox.Items.Clear();
        foreach (var p in _svc.Layout.Presets) PresetBox.Items.Add(p);
        PresetBox.SelectedItem = select;
        if (PresetBox.SelectedItem is null && PresetBox.Items.Count > 0) PresetBox.SelectedIndex = 0;
        _loadingPresets = false;
    }

    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPresets) return;
        if (PresetBox.SelectedItem is not string name || name == _svc.Layout.ActivePreset) return;
        CommitWork();                    // persist edits to the outgoing preset
        _svc.Layout.SetActive(name);     // load + push the new one
        LoadFromStore();                 // reload the editor from the new active preset
        Hint.Text = $"Switched to preset “{name}”.";
    }

    /// <summary>Called when the Deck editor tab is shown: reload if the active preset changed elsewhere
    /// (e.g. in the Presets tab), else just refresh the chooser names.</summary>
    public void SyncActivePreset()
    {
        if (_svc.Layout.ActivePreset != _loadedPreset) LoadFromStore();
        else PopulatePresets(_svc.Layout.ActivePreset);
    }

    private void PopulatePages(int select)
    {
        PageBox.Items.Clear();
        foreach (var p in _work.Pages)
            PageBox.Items.Add(string.IsNullOrWhiteSpace(p.Name) ? p.Id : p.Name);
        if (select >= 0 && select < PageBox.Items.Count) PageBox.SelectedIndex = select;
    }

    private void Page_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ApplySelected();
        var idx = PageBox.SelectedIndex;
        if (idx < 0 || idx >= _work.Pages.Count) return;
        _page = _work.Pages[idx];
        Select(null);
    }

    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        ApplySelected();
        var page = new Page { Id = "p-" + Guid.NewGuid().ToString("n")[..6], Name = $"Page {_work.Pages.Count + 1}" };
        _work.Pages.Add(page);
        _page = page;
        _loading = true; PopulatePages(_work.Pages.Count - 1); _loading = false;
        Select(null);
    }

    private void RenamePage_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptText("Rename page", _page.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        _page.Name = name.Trim();
        var idx = _work.Pages.IndexOf(_page);
        _loading = true; PopulatePages(idx); _loading = false;
    }

    private void DeletePage_Click(object sender, RoutedEventArgs e)
    {
        if (_work.Pages.Count <= 1) { MessageBox.Show("A deck needs at least one page.", "Relay"); return; }
        if (MessageBox.Show($"Delete page \"{_page.Name}\" and its buttons?", "Relay",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        _work.Pages.Remove(_page);
        _page = _work.Pages[0];
        _loading = true; PopulatePages(0); _loading = false;
        Select(null);
    }

    private string? PromptText(string title, string initial) => Prompt.Text(this, title, initial);

    private static int Clamp(int v) => Math.Clamp(v, 1, 8);

    // ── grid rendering ───────────────────────────────────────────────────────────────────
    private void RebuildGrid()
    {
        int cols = ColsBox.SelectedItem is int c ? c : 4;
        int rows = RowsBox.SelectedItem is int r ? r : 3;
        GridHost.Columns = cols;
        GridHost.Rows = rows;
        GridHost.Children.Clear();

        for (int rr = 0; rr < rows; rr++)
        for (int cc = 0; cc < cols; cc++)
            GridHost.Children.Add(BuildCell(rr, cc, ButtonAt(rr, cc)));
    }

    private ButtonDef? ButtonAt(int row, int col) => _page.Buttons.FirstOrDefault(b => b.Row == row && b.Col == col);

    private Border BuildCell(int row, int col, ButtonDef? b)
    {
        var cell = new Border
        {
            Margin = new Thickness(6),
            CornerRadius = new CornerRadius(14),
            AllowDrop = true,
            Tag = (row, col),
            Width = 104,
            Height = 104,
        };
        cell.Drop += Cell_Drop;
        cell.DragOver += (_, e) => { e.Effects = DragDropEffects.Move; e.Handled = true; };

        if (b is null)
        {
            cell.Background = Brushes.Transparent;
            cell.BorderBrush = (Brush)FindResource("Border");
            cell.BorderThickness = new Thickness(1);
        }
        else
        {
            var bg = ParseColor(b.Color, ((SolidColorBrush)FindResource("Surface2")).Color);
            var fg = Luminance(bg) > 0.5 ? Color.FromRgb(0x10, 0x14, 0x1A) : Colors.White;
            cell.Background = new SolidColorBrush(bg);
            cell.BorderThickness = new Thickness(_selectedId == b.Id ? 2.5 : 0);
            cell.BorderBrush = (Brush)FindResource("Accent");

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var iconImg = IconResolver.Decode(b.Icon);
            if (iconImg is not null)
                stack.Children.Add(new Image { Source = iconImg, Width = 30, Height = 30, Stretch = Stretch.Uniform });
            else
                stack.Children.Add(new TextBlock
                {
                    Text = Glyph(b.Icon),
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24,
                    Foreground = new SolidColorBrush(fg),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            stack.Children.Add(new TextBlock
            {
                Text = b.Label,
                Foreground = new SolidColorBrush(fg),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 90,
            });
            cell.Child = stack;

            var id = b.Id;
            cell.PreviewMouseLeftButtonDown += (_, e) => { _dragStart = e.GetPosition(null); Select(id); };
            cell.MouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                var diff = _dragStart - e.GetPosition(null);
                if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                DragDrop.DoDragDrop((DependencyObject)s, id, DragDropEffects.Move);
            };
        }
        return cell;
    }

    private void Cell_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border cell || cell.Tag is not ValueTuple<int, int> pos) return;
        var srcId = e.Data.GetData(DataFormats.StringFormat) as string;
        var src = _page.Buttons.FirstOrDefault(b => b.Id == srcId);
        if (src is null) return;

        var (tr, tc) = pos;
        var dst = ButtonAt(tr, tc);
        if (dst is null)
        {
            src.Row = tr; src.Col = tc;
        }
        else if (dst != src)
        {
            (dst.Row, dst.Col, src.Row, src.Col) = (src.Row, src.Col, tr, tc);
        }
        RebuildGrid();
        Select(src.Id);
    }

    // ── selection + properties ───────────────────────────────────────────────────────────
    private void Select(string? id)
    {
        _selectedId = id;
        RebuildGrid();

        var b = id is null ? null : _page.Buttons.FirstOrDefault(x => x.Id == id);
        Fields.Visibility = b is null ? Visibility.Collapsed : Visibility.Visible;
        EmptyHint.Visibility = b is null ? Visibility.Visible : Visibility.Collapsed;
        if (b is null) return;

        _loading = true;
        LabelBox.Text = b.Label;
        _icon = b.Icon;
        SyncIconUi();
        ColorBox.Text = b.Color ?? "";
        EffectBox.SelectedItem = Effects.FirstOrDefault(x => x.Equals(b.Effect, StringComparison.OrdinalIgnoreCase)) ?? "None";
        var act = b.Action ?? b.HoldAction;
        var type = DetectType(act);
        TypeBox.SelectedItem = type;
        BuildParams(type);
        FillParams(type, act);
        _loading = false;
    }

    private void BuildParams(string type)
    {
        ParamHost.Children.Clear();
        _p.Clear();
        switch (type)
        {
            case "Hotkey": AddText("keys", "Keys (e.g. ctrl+shift+m)"); break;
            case "Media key": AddCombo("cmd", "Command", new[] { "playpause", "next", "prev", "stop", "volup", "voldown", "volmute" }); break;
            case "Type text": AddText("value", "Text to type"); break;
            case "Chat macro":
                AddCombo("open", "Open-chat key", new[] { "enter", "t", "y", "u", "alt+enter" });
                AddText("message", "Message");
                AddCombo("send", "Send key", new[] { "enter" });
                break;
            case "Launch app": AddText("path", "Path / command"); AddText("args", "Arguments (optional)"); break;
            case "Open URL": AddText("url", "URL"); break;
            case "Open folder": AddFolderPicker("folder", "Folder to open"); break;
            case "Screenshot": AddInfo("Captures all screens to Pictures\\Screenshots and copies it to the clipboard."); break;
            case "Open screenshots": AddInfo("Opens your Pictures\\Screenshots folder in Explorer."); break;
            case "Run command": AddText("command", "Command (cmd)"); AddText("args", "Arguments (optional)"); break;
            case "Hold key (PTT)": AddText("keys", "Key(s) to hold while pressed (e.g. v)"); break;
            case "Toggle":
                AddText("onkeys", "On hotkey (e.g. ctrl+shift+m)");
                AddText("offkeys", "Off hotkey (optional; defaults to On)");
                break;
            case "MicForge":
                AddCombo("mf_target", "Control", MicForgeControls);
                AddStageCombo();
                AddText("mf_preset", "Preset name (for 'Preset by name')");
                break;
        }
    }

    /// <summary>The "DSP stage" dropdown, populated from the stages MicForge last reported. Editable so a
    /// stage can still be typed by id when MicForge isn't connected.</summary>
    private void AddStageCombo()
    {
        _stageIdByTitle.Clear();
        _stageTitleById.Clear();
        var titles = new List<string>();
        foreach (var s in _svc.Providers.MicForge.KnownStages)
        {
            _stageIdByTitle[s.Title] = s.Id;
            _stageTitleById[s.Id] = s.Title;
            titles.Add(s.Title);
        }
        AddCombo("mf_stage", "DSP stage (for 'DSP stage')", titles.ToArray(), editable: true);
    }

    private string ResolveStageId(string title)
        => _stageIdByTitle.TryGetValue(title, out var id) ? id : title;

    private void AddText(string key, string label)
    {
        ParamHost.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 4) });
        var t = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        ParamHost.Children.Add(t);
        _p[key] = t;
    }

    private void AddInfo(string text)
        => ParamHost.Children.Add(new TextBlock
        {
            Text = text, Style = (Style)FindResource("Muted"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4),
        });

    /// <summary>A folder path field with a Browse… button (WinForms folder dialog).</summary>
    private void AddFolderPicker(string key, string label)
    {
        ParamHost.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 4) });
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tb = new TextBox();
        var browse = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(tb.Text)) d.SelectedPath = tb.Text;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) tb.Text = d.SelectedPath;
        };
        System.Windows.Controls.Grid.SetColumn(tb, 0);
        System.Windows.Controls.Grid.SetColumn(browse, 1);
        grid.Children.Add(tb);
        grid.Children.Add(browse);
        ParamHost.Children.Add(grid);
        _p[key] = tb;
    }

    private void AddCombo(string key, string label, string[] items, bool editable = false)
    {
        ParamHost.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 4) });
        var c = new ComboBox { Margin = new Thickness(0, 0, 0, 8), IsEditable = editable };
        foreach (var i in items) c.Items.Add(i);
        ParamHost.Children.Add(c);
        _p[key] = c;
    }

    private void FillParams(string type, ActionDef? a)
    {
        if (a is null) return;
        var p = a.Params;
        switch (type)
        {
            case "Hotkey": SetVal("keys", Keys(p, "keys")); break;
            case "Media key": SetVal("cmd", Str(p, "cmd")); break;
            case "Type text": SetVal("value", Str(p, "value")); break;
            case "Launch app": SetVal("path", Str(p, "path")); SetVal("args", Str(p, "args")); break;
            case "Open URL": SetVal("url", Str(p, "url")); break;
            case "Open folder": SetVal("folder", Str(p, "url")); break;
            case "Screenshot": break;
            case "Open screenshots": break;
            case "Run command": SetVal("command", Str(p, "command")); SetVal("args", Str(p, "args")); break;
            case "Hold key (PTT)": SetVal("keys", Keys(p, "keys")); break;
            case "Toggle":
                if (p.ValueKind == JsonValueKind.Object)
                {
                    if (p.TryGetProperty("on", out var onA)) SetVal("onkeys", Keys(Params(onA), "keys"));
                    if (p.TryGetProperty("off", out var offA)) SetVal("offkeys", Keys(Params(offA), "keys"));
                }
                break;
            case "Chat macro":
                if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
                {
                    var arr = steps.EnumerateArray().ToArray();
                    if (arr.Length > 0) SetVal("open", Keys(Params(arr[0]), "keys"));
                    if (arr.Length > 1) SetVal("message", Str(Params(arr[1]), "value"));
                    if (arr.Length > 2) SetVal("send", Keys(Params(arr[2]), "keys"));
                }
                break;
            case "MicForge":
                SetVal("mf_target", a.Verb.ToLowerInvariant() switch
                {
                    "bypass" => "Bypass",
                    "startstop" => "Start / Stop",
                    "preset" => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("dir", out var dir) && dir.ValueKind == JsonValueKind.String
                        ? (dir.GetString() == "prev" ? "Previous preset" : "Next preset")
                        : "Preset by name",
                    "stage" => "DSP stage",
                    "meter" => "Input meter",
                    _ => "Mute",
                });
                SetVal("mf_preset", Str(p, "name"));
                var sid = Str(p, "id");
                if (sid.Length > 0) SetVal("mf_stage", _stageTitleById.TryGetValue(sid, out var st) ? st : sid);
                break;
        }
    }

    private void SetVal(string key, string val)
    {
        if (!_p.TryGetValue(key, out var c)) return;
        if (c is TextBox t) t.Text = val;
        else if (c is ComboBox cb) { if (!cb.Items.Contains(val)) cb.Items.Add(val); cb.SelectedItem = val; }
    }

    private string Val(string key)
    {
        if (!_p.TryGetValue(key, out var c)) return "";
        return c switch
        {
            TextBox t => t.Text.Trim(),
            ComboBox cb => (cb.SelectedItem as string ?? cb.Text).Trim(),
            _ => "",
        };
    }

    // ── commands ─────────────────────────────────────────────────────────────────────────
    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        BuildParams(TypeBox.SelectedItem as string ?? "Hotkey");
    }

    private void Color_Changed(object sender, TextChangedEventArgs e)
        => Swatch.Background = new SolidColorBrush(ParseColor(ColorBox.Text, Colors.Transparent));

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        using var d = new System.Windows.Forms.ColorDialog();
        if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            ColorBox.Text = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => ApplySelected();

    private void ApplySelected()
    {
        var b = _selectedId is null ? null : _page.Buttons.FirstOrDefault(x => x.Id == _selectedId);
        if (b is null) return;
        b.Label = LabelBox.Text.Trim();
        b.Icon = _icon;
        b.Color = string.IsNullOrWhiteSpace(ColorBox.Text) ? null : ColorBox.Text.Trim();
        b.Effect = EffectBox.SelectedItem is string ef && ef != "None" ? ef.ToLowerInvariant() : null;
        var type = TypeBox.SelectedItem as string ?? "Hotkey";
        var act = ReadAction(type);
        if (type == "Hold key (PTT)") { b.HoldAction = act; b.Action = null; }
        else { b.Action = act; b.HoldAction = null; }
        RebuildGrid();
    }

    // ── icon picker (catalog name, resolved-from-URL image, or uploaded image) ─────────────
    private void Icon_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (IconBox.SelectedItem is IconChoice ic) { _icon = ic.Name; UpdateIconPreview(); }
    }

    private async void IconFromUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = PromptText("Icon from website (e.g. github.com)", "");
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            _icon = await IconResolver.FromUrlAsync(url.Trim());
            IconBox.SelectedItem = null;
            UpdateIconPreview();
            RebuildGrid();
        }
        catch (Exception ex) { MessageBox.Show("Couldn't fetch an icon: " + ex.Message, "Relay"); }
    }

    private void IconUpload_Click(object sender, RoutedEventArgs e)
    {
        using var d = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Choose a button icon",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif|All files (*.*)|*.*",
        };
        if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            _icon = IconResolver.FromFile(d.FileName);
            IconBox.SelectedItem = null;
            UpdateIconPreview();
            RebuildGrid();
        }
        catch (Exception ex) { MessageBox.Show("Couldn't load that image: " + ex.Message, "Relay"); }
    }

    private void IconClear_Click(object sender, RoutedEventArgs e)
    {
        _icon = null;
        IconBox.SelectedItem = null;
        UpdateIconPreview();
        RebuildGrid();
    }

    /// <summary>Reflect <see cref="_icon"/> into the picker: select the matching catalog entry for a named
    /// icon, or clear the selection for a custom image. Always refreshes the preview.</summary>
    private void SyncIconUi()
    {
        IconBox.SelectedItem = IconResolver.IsImage(_icon)
            ? null
            : IconBox.Items.Cast<IconChoice>().FirstOrDefault(x => x.Name == _icon);
        UpdateIconPreview();
    }

    private void UpdateIconPreview()
    {
        var img = IconResolver.Decode(_icon);
        IconPreview.Child = img is not null
            ? new Image { Source = img, Stretch = Stretch.Uniform, Margin = new Thickness(6) }
            : new TextBlock
            {
                Text = Glyph(_icon),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = (Brush)FindResource("Text"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
    }

    private ActionDef ReadAction(string type)
    {
        object payload; string provider = "os", verb;
        switch (type)
        {
            case "Media key": verb = "media"; payload = new { cmd = Val("cmd") }; break;
            case "Type text": verb = "text"; payload = new { value = Val("value") }; break;
            case "Launch app": verb = "launch"; payload = new { path = Val("path"), args = Val("args") }; break;
            case "Open URL": verb = "open"; payload = new { url = Val("url") }; break;
            case "Open folder": verb = "open"; payload = new { url = Val("folder"), folder = true }; break;
            case "Screenshot": verb = "screenshot"; payload = new { }; break;
            case "Open screenshots": verb = "open"; payload = new { special = "screenshots" }; break;
            case "Run command": provider = "script"; verb = "run"; payload = new { command = Val("command"), args = Val("args") }; break;
            case "Hold key (PTT)": verb = "holdkey"; payload = new { keys = ParseKeys(Val("keys")) }; break;
            case "Toggle":
                provider = "core"; verb = "toggle";
                var offRaw = Val("offkeys");
                payload = new
                {
                    on = new { provider = "os", verb = "hotkey", @params = new { keys = ParseKeys(Val("onkeys")) } },
                    off = new { provider = "os", verb = "hotkey", @params = new { keys = ParseKeys(offRaw.Length > 0 ? offRaw : Val("onkeys")) } },
                };
                break;
            case "Chat macro":
                provider = "core"; verb = "macro";
                payload = new
                {
                    gapMs = 60,
                    steps = new object[]
                    {
                        new { provider = "os", verb = "hotkey", @params = new { keys = ParseKeys(Val("open")) } },
                        new { provider = "os", verb = "text", @params = new { value = Val("message") } },
                        new { provider = "os", verb = "hotkey", @params = new { keys = ParseKeys(Val("send")) } },
                    },
                };
                break;
            case "MicForge":
                provider = "micforge";
                (verb, payload) = Val("mf_target") switch
                {
                    "Bypass" => ("bypass", (object)new { }),
                    "Start / Stop" => ("startstop", new { }),
                    "Next preset" => ("preset", new { dir = "next" }),
                    "Previous preset" => ("preset", new { dir = "prev" }),
                    "Preset by name" => ("preset", new { name = Val("mf_preset") }),
                    "DSP stage" => ("stage", new { id = ResolveStageId(Val("mf_stage")) }),
                    "Input meter" => ("meter", new { }),
                    _ => ("mute", new { }),
                };
                break;
            default: verb = "hotkey"; payload = new { keys = ParseKeys(Val("keys")) }; break;
        }
        return new ActionDef { Provider = provider, Verb = verb, Params = JsonSerializer.SerializeToElement(payload, LayoutStore.Json) };
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        ApplySelected();
        int cols = ColsBox.SelectedItem is int c ? c : 4;
        int rows = RowsBox.SelectedItem is int r ? r : 3;
        // first free cell
        for (int rr = 0; rr < rows; rr++)
        for (int cc = 0; cc < cols; cc++)
            if (ButtonAt(rr, cc) is null)
            {
                var b = new ButtonDef
                {
                    Id = "b-" + Guid.NewGuid().ToString("n")[..6],
                    Label = "New", Icon = "touch_app", Color = "#2c3e50", Row = rr, Col = cc,
                    Action = new ActionDef { Provider = "os", Verb = "hotkey", Params = JsonSerializer.SerializeToElement(new { keys = Array.Empty<string>() }, LayoutStore.Json) },
                };
                _page.Buttons.Add(b);
                RebuildGrid();
                Select(b.Id);
                return;
            }
        MessageBox.Show("The grid is full — add rows/cols first.", "Relay");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId is null) return;
        _page.Buttons.RemoveAll(b => b.Id == _selectedId);
        Select(null);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        ApplySelected();
        var src = _selectedId is null ? null : _page.Buttons.FirstOrDefault(b => b.Id == _selectedId);
        if (src is null) return;
        int cols = ColsBox.SelectedItem is int c ? c : 4;
        int rows = RowsBox.SelectedItem is int r ? r : 3;
        var free = FirstFree(cols, rows);
        if (free is not { } cell) { MessageBox.Show("The grid is full — add rows/cols first.", "Relay"); return; }

        var clone = JsonSerializer.Deserialize<ButtonDef>(
            JsonSerializer.Serialize(src, LayoutStore.Json), LayoutStore.Json)!;
        clone.Id = "b-" + Guid.NewGuid().ToString("n")[..6];
        clone.Row = cell.Item1;
        clone.Col = cell.Item2;
        _page.Buttons.Add(clone);
        RebuildGrid();
        Select(clone.Id);
    }

    private void Grid_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ReflowOutOfBounds();
        RebuildGrid();
    }

    /// <summary>After the grid shrinks, relocate any button that now falls outside it into a free cell.</summary>
    private void ReflowOutOfBounds()
    {
        int cols = ColsBox.SelectedItem is int c ? c : 4;
        int rows = RowsBox.SelectedItem is int r ? r : 3;
        foreach (var b in _page.Buttons.Where(x => x.Row >= rows || x.Col >= cols).ToList())
        {
            var free = FirstFree(cols, rows);
            if (free is { } cell) { b.Row = cell.Item1; b.Col = cell.Item2; }
        }
    }

    private (int, int)? FirstFree(int cols, int rows)
    {
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            if (ButtonAt(r, c) is null) return (r, c);
        return null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        CommitWork();
        Hint.Text = $"Saved & pushed at {DateTime.Now:HH:mm:ss}.";
    }

    /// <summary>Flush the selected button + grid dims into <c>_work</c> and persist it to the active preset.</summary>
    private void CommitWork()
    {
        ApplySelected();
        _work.Grid.Cols = ColsBox.SelectedItem is int c ? c : 4;
        _work.Grid.Rows = RowsBox.SelectedItem is int r ? r : 3;
        if (string.IsNullOrEmpty(_work.ActivePage)) _work.ActivePage = _page.Id;
        _work.Sliders = ReadSliders();
        _svc.Layout.Save(_work);
    }

    // ── sliders ──────────────────────────────────────────────────────────────────────────
    private readonly List<SliderRow> _sliderRows = new();

    private void PopulateSliders()
    {
        SlidersHost.Children.Clear();
        _sliderRows.Clear();
        foreach (var sl in _work.Sliders) AddSliderRow(sl);
        SlidersHint.Text = _svc.Providers.MicForge.KnownParams.Count == 0
            ? "Start MicForge to pick a parameter for a slider."
            : (_work.Sliders.Count == 0 ? "Add a slider to control a MicForge parameter from the phone." : "");
    }

    private void AddSlider_Click(object sender, RoutedEventArgs e)
    {
        if (_svc.Providers.MicForge.KnownParams.Count == 0)
        {
            SlidersHint.Text = "Start MicForge first — its parameters then appear here to pick from.";
            return;
        }
        AddSliderRow(null);
        SlidersHint.Text = "";
    }

    private void AddSliderRow(SliderDef? existing)
    {
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBox = new TextBox { Text = existing?.Label ?? "", VerticalAlignment = VerticalAlignment.Center };
        var paramBox = new ComboBox { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var p in _svc.Providers.MicForge.KnownParams) paramBox.Items.Add(new ParamChoice(p));

        // Preserve an existing binding even if MicForge is offline (its metadata lives on the def).
        var existingKey = existing is null ? null : SliderKey(existing);
        if (existingKey is not null)
        {
            var match = paramBox.Items.Cast<ParamChoice>().FirstOrDefault(c => c.Key == existingKey);
            if (match is null)
            {
                match = ParamChoice.FromDef(existing!, existingKey);
                paramBox.Items.Add(match);
            }
            paramBox.SelectedItem = match;
        }

        var del = new Button { Content = "✕", Width = 30, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Remove slider" };
        var row = new SliderRow { Grid = grid, Label = labelBox, Param = paramBox, Id = existing?.Id ?? NewSliderId(), Color = existing?.Color };

        paramBox.SelectionChanged += (_, _) =>
        {
            if (paramBox.SelectedItem is ParamChoice pc && string.IsNullOrWhiteSpace(labelBox.Text))
                labelBox.Text = pc.Info.Label;
        };
        del.Click += (_, _) => { SlidersHost.Children.Remove(grid); _sliderRows.Remove(row); };

        System.Windows.Controls.Grid.SetColumn(labelBox, 0);
        System.Windows.Controls.Grid.SetColumn(paramBox, 1);
        System.Windows.Controls.Grid.SetColumn(del, 2);
        grid.Children.Add(labelBox);
        grid.Children.Add(paramBox);
        grid.Children.Add(del);

        _sliderRows.Add(row);
        SlidersHost.Children.Add(grid);
    }

    private List<SliderDef> ReadSliders()
    {
        var list = new List<SliderDef>();
        foreach (var r in _sliderRows)
        {
            if (r.Param.SelectedItem is not ParamChoice pc) continue;
            var info = pc.Info;
            list.Add(new SliderDef
            {
                Id = r.Id,
                Label = string.IsNullOrWhiteSpace(r.Label.Text) ? info.Label : r.Label.Text.Trim(),
                Min = info.Min, Max = info.Max, Step = info.Step, Unit = info.Unit,
                Color = string.IsNullOrWhiteSpace(r.Color) ? "#2980b9" : r.Color,
                Value = info.Value,
                Action = new ActionDef { Provider = "micforge", Verb = "param", Params = JsonSerializer.SerializeToElement(new { key = pc.Key }, LayoutStore.Json) },
            });
        }
        return list;
    }

    private static string NewSliderId() => "sl-" + Guid.NewGuid().ToString("n")[..8];

    private static string? SliderKey(SliderDef s)
        => s.Action?.Params.ValueKind == JsonValueKind.Object && s.Action.Params.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String
            ? k.GetString() : null;

    private sealed class SliderRow
    {
        public required System.Windows.Controls.Grid Grid;
        public required TextBox Label;
        public required ComboBox Param;
        public string Id = "";
        public string? Color;
    }

    /// <summary>A MicForge param option in a slider's dropdown ("Stage · Label"), carrying its range.</summary>
    private sealed class ParamChoice
    {
        public ParamChoice(Providers.MicForgeProvider.ParamInfo info) { Info = info; }
        public Providers.MicForgeProvider.ParamInfo Info { get; }
        public string Key => Info.Key;
        public override string ToString() => string.IsNullOrEmpty(Info.Stage) ? Info.Label : $"{Info.Stage} · {Info.Label}";

        public static ParamChoice FromDef(SliderDef s, string key)
            => new(new Providers.MicForgeProvider.ParamInfo(key, "", s.Label, s.Value, s.Min, s.Max, s.Step, s.Unit ?? ""));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────
    private static string Glyph(string? icon) => icon switch
    {
        "play_arrow" => "",
        "pause" => "",
        "stop" => "",
        "volume_up" => "",
        "volume_down" => "",
        "mic" => "",
        "photo_camera" => "",
        "videocam" => "",
        "folder" => "",
        "terminal" => "",
        "refresh" => "",
        "lock" => "",
        "home" => "",
        "settings" => "",
        "star" => "",
        "bolt" => "",
        "power" => "",
        "play_pause" => "",
        "skip_previous" => "",
        "skip_next" => "",
        "volume_off" => "",
        "mic_off" => "",
        "content_cut" => "",
        "chat" => "",
        "edit_note" => "",
        "open_in_browser" => "",
        "keyboard" => "",
        _ => "",
    };

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return (Color)ColorConverter.ConvertFromString(hex); } catch { return fallback; }
    }

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private static string[] ParseKeys(string s)
        => s.Split(new[] { '+', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant()).ToArray();

    private static string Keys(JsonElement p, string name)
    {
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return string.Join("+", arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));
        return "";
    }

    private static string Str(JsonElement p, string name)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static JsonElement Params(JsonElement step)
        => step.ValueKind == JsonValueKind.Object && step.TryGetProperty("params", out var p) ? p : default;

    /// <summary>An icon option shown in the picker: its layout name + the glyph to display.</summary>
    private sealed class IconChoice
    {
        public IconChoice(string name, string glyph) { Name = name; Glyph = glyph; }
        public string Name { get; }
        public string Glyph { get; }
        public override string ToString() => Name;
    }

    private static string DetectType(ActionDef? a)
    {
        if (a is null) return "Hotkey";
        if (string.Equals(a.Verb, "macro", StringComparison.OrdinalIgnoreCase)) return "Chat macro";
        if (string.Equals(a.Verb, "holdkey", StringComparison.OrdinalIgnoreCase)) return "Hold key (PTT)";
        if (string.Equals(a.Verb, "toggle", StringComparison.OrdinalIgnoreCase)) return "Toggle";
        if (string.Equals(a.Provider, "micforge", StringComparison.OrdinalIgnoreCase)) return "MicForge";
        if (string.Equals(a.Provider, "script", StringComparison.OrdinalIgnoreCase)) return "Run command";
        if (string.Equals(a.Verb, "screenshot", StringComparison.OrdinalIgnoreCase)) return "Screenshot";
        if (string.Equals(a.Verb, "open", StringComparison.OrdinalIgnoreCase))
        {
            if (a.Params.ValueKind == JsonValueKind.Object && a.Params.TryGetProperty("special", out var sp)
                && sp.ValueKind == JsonValueKind.String && sp.GetString() == "screenshots")
                return "Open screenshots";
            return a.Params.ValueKind == JsonValueKind.Object && a.Params.TryGetProperty("folder", out var f)
                && f.ValueKind == JsonValueKind.True ? "Open folder" : "Open URL";
        }
        return a.Verb.ToLowerInvariant() switch
        {
            "media" => "Media key",
            "text" => "Type text",
            "launch" => "Launch app",
            _ => "Hotkey",
        };
    }
}
