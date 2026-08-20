using System.Text.Json;
using DeckForge.Agent.Layout;

namespace DeckForge.Agent;

/// <summary>Desktop deck editor. Edit buttons (label / icon / colour / position / action) on a
/// working copy; Save persists to layout.json and pushes the new layout to connected phones.</summary>
public sealed class EditorForm : Form
{
    private static readonly string[] Types =
        { "Hotkey", "Media key", "Type text", "Chat macro", "Launch app", "Open URL" };

    private readonly LayoutStore _store;
    private readonly Log _log;
    private DeckLayout _work = new();
    private Page _page = new();

    private readonly ListBox _list = new();
    private readonly NumericUpDown _numCols = new() { Minimum = 1, Maximum = 12, Value = 4 };
    private readonly NumericUpDown _numRows = new() { Minimum = 1, Maximum = 12, Value = 3 };

    private readonly TextBox _txtLabel = new();
    private readonly ComboBox _cmbIcon = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _txtColor = new();
    private readonly Button _btnColor = new() { Text = "…", Width = 30 };
    private readonly NumericUpDown _numRow = new() { Minimum = 0, Maximum = 11 };
    private readonly NumericUpDown _numCol = new() { Minimum = 0, Maximum = 11 };
    private readonly ComboBox _cmbType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Panel _paramPanel = new() { BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.Gray };

    private readonly Dictionary<string, Control> _p = new();
    private int _editing = -1;
    private bool _loading;

    public EditorForm(LayoutStore store, Log log)
    {
        _store = store;
        _log = log;

        Text = "DeckForge — Deck editor";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 560);
        MinimumSize = new Size(900, 560);

        BuildUi();
        LoadFromStore();
    }

    private void BuildUi()
    {
        // Left: button list + add/delete + grid size
        _list.SetBounds(12, 12, 240, 430);
        _list.SelectedIndexChanged += OnSelectionChanged;
        Controls.Add(_list);

        var btnAdd = new Button { Text = "Add", Left = 12, Top = 448, Width = 75 };
        btnAdd.Click += (_, _) => AddButton();
        var btnDel = new Button { Text = "Delete", Left = 92, Top = 448, Width = 75 };
        btnDel.Click += (_, _) => DeleteButton();
        Controls.Add(btnAdd);
        Controls.Add(btnDel);

        Controls.Add(new Label { Text = "Grid  cols", Left = 12, Top = 486, Width = 60 });
        _numCols.SetBounds(78, 483, 48, 24);
        Controls.Add(_numCols);
        Controls.Add(new Label { Text = "rows", Left = 134, Top = 486, Width = 34 });
        _numRows.SetBounds(172, 483, 48, 24);
        Controls.Add(_numRows);

        // Right: properties
        int x = 280, lx = 280, cx = 420, y = 14;
        Controls.Add(new Label { Text = "Label", Left = lx, Top = y + 3, Width = 120 });
        _txtLabel.SetBounds(cx, y, 460, 24); Controls.Add(_txtLabel); y += 34;

        Controls.Add(new Label { Text = "Icon", Left = lx, Top = y + 3, Width = 120 });
        _cmbIcon.Items.AddRange(IconCatalog.Names);
        _cmbIcon.SetBounds(cx, y, 240, 24); Controls.Add(_cmbIcon); y += 34;

        Controls.Add(new Label { Text = "Colour (#RRGGBB)", Left = lx, Top = y + 3, Width = 130 });
        _txtColor.SetBounds(cx, y, 200, 24); Controls.Add(_txtColor);
        _btnColor.SetBounds(cx + 208, y - 1, 30, 26); _btnColor.Click += PickColour; Controls.Add(_btnColor); y += 34;

        Controls.Add(new Label { Text = "Position  row", Left = lx, Top = y + 3, Width = 120 });
        _numRow.SetBounds(cx, y, 48, 24); Controls.Add(_numRow);
        Controls.Add(new Label { Text = "col", Left = cx + 60, Top = y + 3, Width = 30 });
        _numCol.SetBounds(cx + 92, y, 48, 24); Controls.Add(_numCol); y += 40;

        Controls.Add(new Label { Text = "Action", Left = lx, Top = y + 3, Width = 120 });
        _cmbType.Items.AddRange(Types);
        _cmbType.SetBounds(cx, y, 200, 24);
        _cmbType.SelectedIndexChanged += (_, _) => { if (!_loading) BuildParamsUi(CurrentType()); };
        Controls.Add(_cmbType); y += 36;

        _paramPanel.SetBounds(x, y, 600, 150);
        Controls.Add(_paramPanel); y += 160;

        var btnApply = new Button { Text = "Apply to button", Left = x, Top = 470, Width = 130 };
        btnApply.Click += (_, _) => { ApplyCurrent(); RefreshList(_editing); SetStatus("Applied."); };
        Controls.Add(btnApply);

        var btnSave = new Button { Text = "Save && Push", Left = 690, Top = 470, Width = 130, Height = 30 };
        btnSave.Click += (_, _) => Save();
        Controls.Add(btnSave);

        var btnClose = new Button { Text = "Close", Left = 690, Top = 508, Width = 130 };
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        _status.SetBounds(x, 514, 380, 20);
        Controls.Add(_status);
    }

    private void LoadFromStore()
    {
        // deep clone the current layout so edits are only committed on Save
        _work = JsonSerializer.Deserialize<DeckLayout>(
            JsonSerializer.Serialize(_store.Current, LayoutStore.Json), LayoutStore.Json) ?? new DeckLayout();
        if (_work.Pages.Count == 0) _work.Pages.Add(new Page { Id = "p-main", Name = "Main" });
        _page = _work.Pages[0];
        _numCols.Value = Math.Clamp(_work.Grid.Cols, 1, 12);
        _numRows.Value = Math.Clamp(_work.Grid.Rows, 1, 12);
        RefreshList(_page.Buttons.Count > 0 ? 0 : -1);
    }

    private void RefreshList(int select)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var b in _page.Buttons) _list.Items.Add(ItemText(b));
        _list.EndUpdate();
        if (select >= 0 && select < _list.Items.Count) _list.SelectedIndex = select;
        else { _editing = -1; ClearProps(); }
    }

    private static string ItemText(ButtonDef b)
        => $"{(string.IsNullOrWhiteSpace(b.Label) ? "(no label)" : b.Label)}  —  {b.Action?.Provider}.{b.Action?.Verb}";

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        if (_editing >= 0 && _editing < _page.Buttons.Count)
        {
            ApplyCurrent();
            _list.Items[_editing] = ItemText(_page.Buttons[_editing]);
        }
        _editing = _list.SelectedIndex;
        LoadButton();
    }

    private void LoadButton()
    {
        if (_editing < 0 || _editing >= _page.Buttons.Count) { ClearProps(); return; }
        _loading = true;
        var b = _page.Buttons[_editing];
        _txtLabel.Text = b.Label;
        _cmbIcon.Text = b.Icon ?? "";
        _txtColor.Text = b.Color ?? "";
        _numRow.Value = Math.Clamp(b.Row, 0, 11);
        _numCol.Value = Math.Clamp(b.Col, 0, 11);
        var type = DetectType(b.Action);
        _cmbType.SelectedItem = type;
        BuildParamsUi(type);
        FillParams(type, b.Action);
        _loading = false;
    }

    private void ClearProps()
    {
        _loading = true;
        _txtLabel.Text = ""; _cmbIcon.Text = ""; _txtColor.Text = "";
        _numRow.Value = 0; _numCol.Value = 0;
        _cmbType.SelectedIndex = -1;
        _paramPanel.Controls.Clear(); _p.Clear();
        _loading = false;
    }

    private string CurrentType() => _cmbType.SelectedItem as string ?? "Hotkey";

    // ── params UI ────────────────────────────────────────────────────────────────────────
    private void BuildParamsUi(string type)
    {
        _paramPanel.Controls.Clear();
        _p.Clear();
        int y = 12;
        switch (type)
        {
            case "Hotkey":
                AddText("keys", "Keys (e.g. ctrl+shift+m)", ref y);
                break;
            case "Media key":
                AddCombo("cmd", "Command", new[] { "playpause", "next", "prev", "stop", "volup", "voldown", "volmute" }, ref y);
                break;
            case "Type text":
                AddText("value", "Text to type", ref y, wide: true);
                break;
            case "Chat macro":
                AddCombo("open", "Open-chat key", new[] { "enter", "t", "y", "u", "alt+enter" }, ref y);
                AddText("message", "Message", ref y, wide: true);
                AddCombo("send", "Send key", new[] { "enter" }, ref y);
                break;
            case "Launch app":
                AddText("path", "Path / command", ref y);
                var browse = new Button { Text = "Browse…", Left = 470, Top = y - 40, Width = 90 };
                browse.Click += (_, _) => { using var d = new OpenFileDialog(); if (d.ShowDialog() == DialogResult.OK && _p.TryGetValue("path", out var c)) c.Text = d.FileName; };
                _paramPanel.Controls.Add(browse);
                AddText("args", "Arguments (optional)", ref y);
                break;
            case "Open URL":
                AddText("url", "URL", ref y, wide: true);
                break;
        }
    }

    private void AddText(string key, string label, ref int y, bool wide = false)
    {
        _paramPanel.Controls.Add(new Label { Text = label, Left = 10, Top = y + 3, Width = 150 });
        var t = new TextBox { Left = 165, Top = y, Width = wide ? 400 : 290 };
        _paramPanel.Controls.Add(t);
        _p[key] = t;
        y += 36;
    }

    private void AddCombo(string key, string label, string[] items, ref int y)
    {
        _paramPanel.Controls.Add(new Label { Text = label, Left = 10, Top = y + 3, Width = 150 });
        var c = new ComboBox { Left = 165, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
        c.Items.AddRange(items);
        _paramPanel.Controls.Add(c);
        _p[key] = c;
        y += 36;
    }

    private void FillParams(string type, ActionDef? a)
    {
        if (a is null) return;
        var p = a.Params;
        switch (type)
        {
            case "Hotkey": SetVal("keys", KeysToString(p, "keys")); break;
            case "Media key": SetVal("cmd", GetStr(p, "cmd")); break;
            case "Type text": SetVal("value", GetStr(p, "value")); break;
            case "Launch app": SetVal("path", GetStr(p, "path")); SetVal("args", GetStr(p, "args")); break;
            case "Open URL": SetVal("url", GetStr(p, "url")); break;
            case "Chat macro":
                if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("steps", out var steps)
                    && steps.ValueKind == JsonValueKind.Array)
                {
                    var arr = steps.EnumerateArray().ToArray();
                    if (arr.Length > 0) SetVal("open", KeysToString(GetParams(arr[0]), "keys"));
                    if (arr.Length > 1) SetVal("message", GetStr(GetParams(arr[1]), "value"));
                    if (arr.Length > 2) SetVal("send", KeysToString(GetParams(arr[2]), "keys"));
                }
                break;
        }
    }

    private void SetVal(string key, string val) { if (_p.TryGetValue(key, out var c)) c.Text = val; }

    // ── apply / add / delete / save ──────────────────────────────────────────────────────
    private void ApplyCurrent()
    {
        if (_editing < 0 || _editing >= _page.Buttons.Count) return;
        var b = _page.Buttons[_editing];
        b.Label = _txtLabel.Text.Trim();
        b.Icon = string.IsNullOrWhiteSpace(_cmbIcon.Text) ? null : _cmbIcon.Text.Trim();
        b.Color = string.IsNullOrWhiteSpace(_txtColor.Text) ? null : _txtColor.Text.Trim();
        b.Row = (int)_numRow.Value;
        b.Col = (int)_numCol.Value;
        b.Action = ReadAction(CurrentType());
    }

    private ActionDef ReadAction(string type)
    {
        object payload;
        string provider = "os", verb;
        switch (type)
        {
            case "Media key": verb = "media"; payload = new { cmd = Val("cmd") }; break;
            case "Type text": verb = "text"; payload = new { value = Val("value") }; break;
            case "Launch app": verb = "launch"; payload = new { path = Val("path"), args = Val("args") }; break;
            case "Open URL": verb = "open"; payload = new { url = Val("url") }; break;
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
            default: verb = "hotkey"; payload = new { keys = ParseKeys(Val("keys")) }; break;
        }
        return new ActionDef
        {
            Provider = provider,
            Verb = verb,
            Params = JsonSerializer.SerializeToElement(payload, LayoutStore.Json),
        };
    }

    private void AddButton()
    {
        if (_editing >= 0) { ApplyCurrent(); if (_editing < _list.Items.Count) _list.Items[_editing] = ItemText(_page.Buttons[_editing]); }
        var id = "b-" + Guid.NewGuid().ToString("n")[..6];
        var next = _page.Buttons.Count;
        var cols = (int)_numCols.Value;
        var b = new ButtonDef
        {
            Id = id, Label = "New button", Icon = "touch_app", Color = "#2c3e50",
            Row = next / cols, Col = next % cols,
            Action = new ActionDef { Provider = "os", Verb = "hotkey", Params = JsonSerializer.SerializeToElement(new { keys = Array.Empty<string>() }, LayoutStore.Json) },
        };
        _page.Buttons.Add(b);
        RefreshList(_page.Buttons.Count - 1);
        SetStatus("Added — set its action, then Save & Push.");
    }

    private void DeleteButton()
    {
        if (_editing < 0 || _editing >= _page.Buttons.Count) return;
        _page.Buttons.RemoveAt(_editing);
        RefreshList(Math.Min(_editing, _page.Buttons.Count - 1));
        SetStatus("Deleted.");
    }

    private void Save()
    {
        if (_editing >= 0) ApplyCurrent();
        _work.Grid.Cols = (int)_numCols.Value;
        _work.Grid.Rows = (int)_numRows.Value;
        if (string.IsNullOrEmpty(_work.ActivePage)) _work.ActivePage = _page.Id;
        _store.Save(_work);
        RefreshList(_editing);
        SetStatus($"Saved & pushed at {DateTime.Now:HH:mm:ss}.");
    }

    private void PickColour(object? sender, EventArgs e)
    {
        using var d = new ColorDialog();
        if (d.ShowDialog() == DialogResult.OK)
            _txtColor.Text = $"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";
    }

    private void SetStatus(string s) => _status.Text = s;

    // ── helpers ──────────────────────────────────────────────────────────────────────────
    private string Val(string key) => _p.TryGetValue(key, out var c) ? c.Text.Trim() : "";

    private static string DetectType(ActionDef? a)
    {
        if (a is null) return "Hotkey";
        if (string.Equals(a.Verb, "macro", StringComparison.OrdinalIgnoreCase)) return "Chat macro";
        return a.Verb.ToLowerInvariant() switch
        {
            "media" => "Media key",
            "text" => "Type text",
            "launch" => "Launch app",
            "open" => "Open URL",
            _ => "Hotkey",
        };
    }

    private static string[] ParseKeys(string s)
        => s.Split(new[] { '+', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant()).ToArray();

    private static string KeysToString(JsonElement p, string name)
    {
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            return string.Join("+", arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));
        return "";
    }

    private static string GetStr(JsonElement p, string name)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static JsonElement GetParams(JsonElement step)
        => step.ValueKind == JsonValueKind.Object && step.TryGetProperty("params", out var p) ? p : default;
}
