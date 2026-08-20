using System.Text.Json;

namespace DeckForge.Agent.Layout;

/// <summary>Loads the deck layout from %AppData%\DeckForge\layout.json, seeding it from the bundled
/// default on first run. This is the single source of truth pushed to phones.</summary>
public sealed class LayoutStore
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Log _log;

    public DeckLayout Current { get; private set; } = new();

    public LayoutStore(AppConfig config, Log log)
    {
        _path = config.LayoutPath;
        _log = log;
        Load();
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
}
