using System.Text.Json;

namespace DeckForge.Agent.Layout;

/// <summary>Loads the deck layout from %AppData%\DeckForge\layout.json, seeding it from the bundled
/// default on first run. This is the single source of truth pushed to phones. Watches the file so
/// edits re-push to connected phones live.</summary>
public sealed class LayoutStore : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Log _log;
    private FileSystemWatcher? _watcher;
    private DateTime _lastReload = DateTime.MinValue;

    public DeckLayout Current { get; private set; } = new();

    /// <summary>Raised after the layout is reloaded from disk (edited externally).</summary>
    public event Action? Changed;

    public LayoutStore(AppConfig config, Log log)
    {
        _path = config.LayoutPath;
        _log = log;
        Load();
        StartWatching();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                var seed = Path.Combine(AppContext.BaseDirectory, "assets", "layout.default.json");
                if (File.Exists(seed))
                    File.Copy(seed, _path);
            }

            if (File.Exists(_path))
            {
                var layout = JsonSerializer.Deserialize<DeckLayout>(File.ReadAllText(_path), Json);
                if (layout is not null)
                {
                    Current = layout;
                    _log.Info($"Layout loaded: {Current.Pages.Count} page(s), {Current.AllButtons.Count()} button(s).");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load layout; using an empty deck.", ex);
        }

        Current = new DeckLayout { Pages = { new Page { Id = "p-main", Name = "Main" } } };
    }

    private void StartWatching()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(_path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileChanged;
        }
        catch (Exception ex)
        {
            _log.Warn($"layout watcher not started: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Editors fire multiple events per save — debounce.
        var now = DateTime.UtcNow;
        if ((now - _lastReload).TotalMilliseconds < 250) return;
        _lastReload = now;

        // Let the writer finish, then reload + notify.
        Task.Run(async () =>
        {
            await Task.Delay(120);
            Load();
            _log.Info("Layout reloaded from disk.");
            try { Changed?.Invoke(); } catch { }
        });
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
