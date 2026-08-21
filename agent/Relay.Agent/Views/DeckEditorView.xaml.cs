using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Relay.Agent.Layout;
using Page = Relay.Agent.Layout.Page;

namespace Relay.Agent.Views;

public partial class DeckEditorView : UserControl
{
    private static readonly string[] Types =
        { "Hotkey", "Media key", "Type text", "Chat macro", "Launch app", "Open URL", "Run command", "Hold key (PTT)", "Toggle", "MicForge" };

    private static readonly string[] MicForgeControls =
        { "Mute", "Bypass", "Start / Stop", "Next preset", "Previous preset", "Preset by name" };

    private readonly AppServices _svc;
    private DeckLayout _work = new();
    private Page _page = new();

    private readonly Dictionary<string, Control> _p = new();
    private string? _selectedId;
    private bool _loading;
    private Point _dragStart;

    public DeckEditorView(AppServices svc)
    {
        InitializeComponent();
        _svc = svc;

        for (int i = 1; i <= 8; i++) { ColsBox.Items.Add(i); RowsBox.Items.Add(i); }
        foreach (var n in IconCatalog.Names) IconBox.Items.Add(new IconChoice(n, Glyph(n)));
        foreach (var t in Types) TypeBox.Items.Add(t);

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

        RebuildGrid();
        Select(null);
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

    private string? PromptText(string title, string initial)
    {
        var tb = new TextBox { Text = initial, Margin = new Thickness(16, 16, 16, 8), MinWidth = 260 };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("AccentButton") };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 0, 16, 12) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(tb);

        var win = new Window
        {
            Title = title, SizeToContent = SizeToContent.Height, Width = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow,
            Background = (Brush)FindResource("Bg"), Content = panel,
        };
        string? result = null;
        ok.Click += (_, _) => { result = tb.Text; win.DialogResult = true; };
        return win.ShowDialog() == true ? result : null;
    }

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
        IconBox.SelectedItem = IconBox.Items.Cast<IconChoice>().FirstOrDefault(x => x.Name == b.Icon);
        ColorBox.Text = b.Color ?? "";
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
            case "Run command": AddText("command", "Command (cmd)"); AddText("args", "Arguments (optional)"); break;
            case "Hold key (PTT)": AddText("keys", "Key(s) to hold while pressed (e.g. v)"); break;
            case "Toggle":
                AddText("onkeys", "On hotkey (e.g. ctrl+shift+m)");
                AddText("offkeys", "Off hotkey (optional; defaults to On)");
                break;
            case "MicForge":
                AddCombo("mf_target", "Control", MicForgeControls);
                AddText("mf_preset", "Preset name (for 'Preset by name')");
                break;
        }
    }

    private void AddText(string key, string label)
    {
        ParamHost.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 4) });
        var t = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        ParamHost.Children.Add(t);
        _p[key] = t;
    }

    private void AddCombo(string key, string label, string[] items)
    {
        ParamHost.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 4) });
        var c = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
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
                    _ => "Mute",
                });
                SetVal("mf_preset", Str(p, "name"));
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
        b.Icon = (IconBox.SelectedItem as IconChoice)?.Name;
        b.Color = string.IsNullOrWhiteSpace(ColorBox.Text) ? null : ColorBox.Text.Trim();
        var type = TypeBox.SelectedItem as string ?? "Hotkey";
        var act = ReadAction(type);
        if (type == "Hold key (PTT)") { b.HoldAction = act; b.Action = null; }
        else { b.Action = act; b.HoldAction = null; }
        RebuildGrid();
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
        ApplySelected();
        _work.Grid.Cols = ColsBox.SelectedItem is int c ? c : 4;
        _work.Grid.Rows = RowsBox.SelectedItem is int r ? r : 3;
        if (string.IsNullOrEmpty(_work.ActivePage)) _work.ActivePage = _page.Id;
        _svc.Layout.Save(_work);
        Hint.Text = $"Saved & pushed at {DateTime.Now:HH:mm:ss}.";
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
        return a.Verb.ToLowerInvariant() switch
        {
            "media" => "Media key",
            "text" => "Type text",
            "launch" => "Launch app",
            "open" => "Open URL",
            _ => "Hotkey",
        };
    }
}
