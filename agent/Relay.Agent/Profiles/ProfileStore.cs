using System.IO;
using System.Text.Json;

namespace Relay.Agent.Profiles;

/// <summary>One auto-switch rule: focus an app matching <see cref="Exe"/> (bare file name, case-
/// insensitive) and, if <see cref="TitleContains"/> is set, whose title contains it, then activate the
/// deck <see cref="Preset"/>.</summary>
public sealed class ProfileRule
{
    public string Exe { get; set; } = "";
    public string TitleContains { get; set; } = "";
    public string Preset { get; set; } = "";
}

/// <summary>The persisted auto-switch configuration (%AppData%\Relay\profiles.json).</summary>
public sealed class ProfilesConfig
{
    /// <summary>Master switch. Off by default — nothing auto-switches until the user turns it on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Deck to activate when the focused app matches no rule. Blank = leave the deck as-is.</summary>
    public string DefaultPreset { get; set; } = "";

    public List<ProfileRule> Rules { get; set; } = new();
}

/// <summary>Loads and saves the auto-switch <see cref="ProfilesConfig"/>.</summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Log _log;

    public ProfilesConfig Config { get; private set; } = new();

    public ProfileStore(AppConfig config, Log log)
    {
        _log = log;
        _path = Path.Combine(config.DataDir, "profiles.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var cfg = JsonSerializer.Deserialize<ProfilesConfig>(File.ReadAllText(_path), Json);
                if (cfg is not null) { Config = cfg; return; }
            }
        }
        catch (Exception ex) { _log.Warn($"profiles load failed: {ex.Message}"); }
        Config = new ProfilesConfig();
    }

    public void Save(ProfilesConfig cfg)
    {
        Config = cfg;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(cfg, Json)); }
        catch (Exception ex) { _log.Error("profiles save failed.", ex); }
    }
}
