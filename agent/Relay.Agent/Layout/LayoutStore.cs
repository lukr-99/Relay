using System.IO;
using System.Text.Json;

namespace Relay.Agent.Layout;

/// <summary>
/// Manages the deck's <b>presets</b> — named, switchable whole decks stored one-per-file in
/// %AppData%\Relay\presets\&lt;name&gt;.json. Exactly one is <see cref="ActivePreset"/>; its layout is
/// <see cref="Current"/>, the single source of truth pushed to phones and edited in the desktop
/// editor. The active preset file is watched so on-disk edits re-push live. Switching presets
/// re-points at another file and pushes it. First run migrates the old single layout.json into a
/// "Default" preset (or seeds it from the bundled default).
/// </summary>
public sealed class LayoutStore : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private const string DefaultName = "Default";

    private readonly string _presetsDir;
    private readonly string _activePtr;   // presets\active.txt — remembers the active preset across restarts
    private readonly string _legacyPath;  // old %AppData%\Relay\layout.json (pre-presets)
    private readonly Log _log;
    private FileSystemWatcher? _watcher;
    private DateTime _lastReload = DateTime.MinValue;
    private DateTime _suppressUntil = DateTime.MinValue;

    public DeckLayout Current { get; private set; } = new();

    /// <summary>The name of the preset currently active (edited + pushed).</summary>
    public string ActivePreset { get; private set; } = DefaultName;

    /// <summary>Raised after the layout changes (reloaded from disk, saved by the editor, or a
    /// different preset made active).</summary>
    public event Action? Changed;

    public LayoutStore(AppConfig config, Log log)
    {
        _log = log;
        _presetsDir = Path.Combine(config.DataDir, "presets");
        _activePtr = Path.Combine(_presetsDir, "active.txt");
        _legacyPath = config.LayoutPath;

        Directory.CreateDirectory(_presetsDir);
        Migrate();

        ActivePreset = ReadActivePointer();
        LoadActive();
        StartWatching();
    }

    // ── preset catalogue ─────────────────────────────────────────────────────────────────────
    /// <summary>All preset names (the *.json files in the presets folder), sorted.</summary>
    public IReadOnlyList<string> Presets
    {
        get
        {
            try
            {
                return Directory.EnumerateFiles(_presetsDir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList()!;
            }
            catch { return new List<string> { ActivePreset }; }
        }
    }

    private string PathFor(string name) => Path.Combine(_presetsDir, SafeName(name) + ".json");

    public bool Exists(string name) => File.Exists(PathFor(name));

    /// <summary>Switch the active preset. Loads it, remembers it, and pushes it to phones.</summary>
    public bool SetActive(string name)
    {
        name = SafeName(name);
        if (string.IsNullOrWhiteSpace(name) || !Exists(name)) return false;
        ActivePreset = name;
        WriteActivePointer();
        LoadActive();
        try { Changed?.Invoke(); } catch { }
        return true;
    }

    /// <summary>Create a new preset from the given layout (fails if the name is taken).</summary>
    public bool Create(string name, DeckLayout layout)
    {
        name = SafeName(name);
        if (string.IsNullOrWhiteSpace(name) || Exists(name)) return false;
        WriteFile(PathFor(name), layout);
        _log.Info($"Preset created: {name}.");
        return true;
    }

    public bool Duplicate(string name, string newName)
    {
        if (!Exists(name)) return false;
        newName = SafeName(newName);
        if (string.IsNullOrWhiteSpace(newName) || Exists(newName)) return false;
        try { File.Copy(PathFor(name), PathFor(newName)); return true; }
        catch (Exception ex) { _log.Error("Preset duplicate failed.", ex); return false; }
    }

    public bool Rename(string oldName, string newName)
    {
        if (!Exists(oldName)) return false;
        newName = SafeName(newName);
        if (string.IsNullOrWhiteSpace(newName) || Exists(newName)) return false;
        try
        {
            File.Move(PathFor(oldName), PathFor(newName));
            if (string.Equals(ActivePreset, oldName, StringComparison.OrdinalIgnoreCase))
            {
                ActivePreset = newName;
                WriteActivePointer();
            }
            return true;
        }
        catch (Exception ex) { _log.Error("Preset rename failed.", ex); return false; }
    }

    /// <summary>Delete a preset. Refuses to delete the last one; if it was active, another becomes active.</summary>
    public bool Delete(string name)
    {
        if (!Exists(name) || Presets.Count <= 1) return false;
        try { File.Delete(PathFor(name)); }
        catch (Exception ex) { _log.Error("Preset delete failed.", ex); return false; }

        if (string.Equals(ActivePreset, name, StringComparison.OrdinalIgnoreCase))
            SetActive(Presets.First());
        return true;
    }

    // ── active-preset persistence ────────────────────────────────────────────────────────────
    /// <summary>Persists a layout edited in the desktop editor into the active preset file and pushes
    /// it to connected phones.</summary>
    public void Save(DeckLayout layout)
    {
        Current = layout;
        _suppressUntil = DateTime.UtcNow.AddMilliseconds(800); // ignore the watcher event for our own write
        WriteFile(PathFor(ActivePreset), layout);
        _log.Info($"Layout saved from editor: preset '{ActivePreset}', {layout.AllButtons.Count()} button(s).");
        try { Changed?.Invoke(); } catch { }
    }

    private void WriteFile(string path, DeckLayout layout)
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(layout, Json)); }
        catch (Exception ex) { _log.Error($"Failed to write preset '{path}'.", ex); }
    }

    private void LoadActive()
    {
        var path = PathFor(ActivePreset);
        try
        {
            if (File.Exists(path))
            {
                var layout = JsonSerializer.Deserialize<DeckLayout>(File.ReadAllText(path), Json);
                if (layout is not null)
                {
                    Current = layout;
                    _log.Info($"Preset '{ActivePreset}' loaded: {Current.Pages.Count} page(s), {Current.AllButtons.Count()} button(s).");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load preset '{ActivePreset}'; using an empty deck.", ex);
        }

        Current = new DeckLayout { Pages = { new Page { Id = "p-main", Name = "Main" } } };
    }

    /// <summary>Moves a pre-presets layout.json into a Default preset, or seeds one, on first run.</summary>
    private void Migrate()
    {
        try
        {
            if (Directory.EnumerateFiles(_presetsDir, "*.json").Any()) return;   // already have presets

            var defaultPath = PathFor(DefaultName);
            if (File.Exists(_legacyPath))
            {
                File.Copy(_legacyPath, defaultPath);
                try { File.Move(_legacyPath, _legacyPath + ".migrated"); } catch { }
                _log.Info("Migrated layout.json into the 'Default' preset.");
                return;
            }

            var seed = Path.Combine(AppContext.BaseDirectory, "assets", "layout.default.json");
            if (File.Exists(seed)) File.Copy(seed, defaultPath);
            else WriteFile(defaultPath, new DeckLayout { Pages = { new Page { Id = "p-main", Name = "Main" } } });
            _log.Info("Seeded the 'Default' preset.");
        }
        catch (Exception ex) { _log.Error("Preset migration failed.", ex); }
    }

    private string ReadActivePointer()
    {
        try
        {
            if (File.Exists(_activePtr))
            {
                var name = SafeName(File.ReadAllText(_activePtr).Trim());
                if (!string.IsNullOrWhiteSpace(name) && Exists(name)) return name;
            }
        }
        catch { }
        return Exists(DefaultName) ? DefaultName : (Presets.FirstOrDefault() ?? DefaultName);
    }

    private void WriteActivePointer()
    {
        try { File.WriteAllText(_activePtr, ActivePreset); } catch { }
    }

    // ── live-reload of the active preset file ────────────────────────────────────────────────
    private void StartWatching()
    {
        try
        {
            _watcher = new FileSystemWatcher(_presetsDir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileChanged;
        }
        catch (Exception ex)
        {
            _log.Warn($"preset watcher not started: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Only care about on-disk edits to the *active* preset file.
        if (!string.Equals(Path.GetFileNameWithoutExtension(e.Name), ActivePreset, StringComparison.OrdinalIgnoreCase))
            return;

        var now = DateTime.UtcNow;
        if (now < _suppressUntil) return;               // our own editor write — already pushed
        if ((now - _lastReload).TotalMilliseconds < 250) return;   // editors fire multiple events per save
        _lastReload = now;

        Task.Run(async () =>
        {
            await Task.Delay(120);
            LoadActive();
            _log.Info("Active preset reloaded from disk.");
            try { Changed?.Invoke(); } catch { }
        });
    }

    /// <summary>Sanitises a preset name into a safe file base name.</summary>
    public static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var cleaned = new string(name.Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        return cleaned.Length > 64 ? cleaned[..64] : cleaned;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
