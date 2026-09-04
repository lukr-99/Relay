using System.IO;
using System.Text.Json;

namespace Relay.Agent.Layout;

public sealed class ButtonLibraryStore
{
    private readonly string _path;
    private readonly Log _log;
    private readonly List<ButtonLibraryEntry> _entries = new();

    public ButtonLibraryStore(AppConfig config, Log log)
    {
        _path = Path.Combine(config.DataDir, "button-library.json");
        _log = log;
        Load();
    }

    public IReadOnlyList<ButtonLibraryEntry> Entries => _entries
        .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public ButtonLibraryEntry? Find(string id)
        => _entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    public ButtonLibraryEntry Add(string name, ButtonDef button)
    {
        var entry = new ButtonLibraryEntry
        {
            Id = "lib-" + Guid.NewGuid().ToString("n")[..8],
            Name = SafeName(name),
            Button = Clone(button),
        };
        entry.Button.Row = 0;
        entry.Button.Col = 0;
        _entries.Add(entry);
        Save();
        return entry;
    }

    public bool Delete(string id)
    {
        var removed = _entries.RemoveAll(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) Save();
        return removed;
    }

    public static ButtonDef Clone(ButtonDef button)
        => JsonSerializer.Deserialize<ButtonDef>(JsonSerializer.Serialize(button, LayoutStore.Json), LayoutStore.Json)
           ?? new ButtonDef();

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var entries = JsonSerializer.Deserialize<List<ButtonLibraryEntry>>(File.ReadAllText(_path), LayoutStore.Json);
            if (entries is null) return;
            _entries.Clear();
            _entries.AddRange(entries.Where(e => !string.IsNullOrWhiteSpace(e.Id)));
        }
        catch (Exception ex)
        {
            _log.Error("Button library load failed.", ex);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, LayoutStore.Json));
        }
        catch (Exception ex)
        {
            _log.Error("Button library save failed.", ex);
        }
    }

    private static string SafeName(string name)
    {
        var cleaned = string.IsNullOrWhiteSpace(name) ? "Button" : name.Trim();
        return cleaned.Length > 64 ? cleaned[..64] : cleaned;
    }
}
