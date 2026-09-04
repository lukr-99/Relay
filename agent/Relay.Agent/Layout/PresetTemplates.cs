using System.IO;
using System.Text.Json;

namespace Relay.Agent.Layout;

/// <summary>Ready-made deck presets the editor can drop in with one click.</summary>
public static class PresetTemplates
{
    private static readonly (string Label, string Url)[] GitHubProjects =
    {
        ("AirplaneMode", "https://github.com/lukr-99/AirplaneMode"),
        ("CodePrint", "https://github.com/lukr-99/CodePrint"),
        ("DL FOV Fixer", "https://github.com/lukr-99/DL-FOV-Fixer"),
        ("dotnetlib", "https://github.com/lukr-99/dotnetlib"),
        ("GameScout", "https://github.com/lukr-99/GameScout"),
        ("jsinatahu", "https://github.com/jsinatahu/jsinatahu"),
        ("MicForge", "https://github.com/lukr-99/MicForge"),
        ("NotionCall", "https://github.com/lukr-99/NotionCall"),
        ("QRingSet", "https://github.com/lukr-99/ring-set"),
        ("Relay", "https://github.com/lukr-99/Relay"),
        ("Startup", "https://github.com/lukr-99/startup-profiles"),
        ("SubTrackr", "https://github.com/lukr-99/SubTrackr"),
        ("VrtAim", "https://github.com/lukr-99/VrtAimTrainer"),
        ("Workout", "https://github.com/lukr-99/workout-tracker"),
    };

    /// <summary>A coding launch deck for daily tools and GitHub repositories.</summary>
    public static DeckLayout Coding()
    {
        var codeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Code");
        var ideasDir = Path.Combine(codeRoot, "ideas");
        var ideasFile = Path.Combine(ideasDir, "ideas.txt");
        Directory.CreateDirectory(ideasDir);
        if (!File.Exists(ideasFile)) File.WriteAllText(ideasFile, "");

        var layout = new DeckLayout
        {
            Version = 1,
            Grid = new Grid { Cols = 4, Rows = 2 },
            ActivePage = "p-tools",
            Pages =
            {
                new Page
                {
                    Id = "p-tools",
                    Name = "Coding",
                    Grid = new Grid { Cols = 4, Rows = 2 },
                    Buttons =
                    {
                        Open("coding-github-repos", 0, 0, "GH repos", "open_in_browser", "#24292F",
                            "https://github.com/lukr-99?tab=repositories"),
                        Open("coding-claude", 0, 1, "Claude", "chat", "#6F4E37", "https://claude.ai/new"),
                        Open("coding-chatgpt", 0, 2, "GPT", "chat", "#10A37F", "https://chatgpt.com/"),
                        Launch("coding-vscode", 0, 3, "VS Code", "terminal", "#007ACC", "code.cmd", codeRoot, codeRoot),
                        Open("coding-notion", 1, 0, "Notion", "star", "#2F3437", "https://www.notion.so/"),
                        Launch("coding-ideas", 1, 1, "Ideas", "edit_note", "#F2C94C", "notepad.exe", Quote(ideasFile), ideasDir),
                        NewTextFile("coding-new-idea", 1, 2, "New idea", "edit_note", "#27AE60", ideasDir, "idea"),
                    },
                },
            },
        };

        AddGitHubProjectPages(layout);
        return layout;
    }

    /// <summary>A MicForge control deck: mute / bypass / start-stop + preset cycling. These buttons
    /// mirror MicForge's live state (a Mute button lights up when the mic is actually muted).</summary>
    public static DeckLayout MicForge() => new()
    {
        Version = 1,
        Grid = new Grid { Cols = 3, Rows = 2 },
        ActivePage = "p-main",
        Pages =
        {
            new Page
            {
                Id = "p-main",
                Name = "MicForge",
                Buttons =
                {
                    Btn("mf-mute", 0, 0, "Mute", "mic_off", "#C0392B", "mute"),
                    Btn("mf-bypass", 0, 1, "Bypass", "bolt", "#8E44AD", "bypass"),
                    Btn("mf-run", 0, 2, "Start / Stop", "power", "#27AE60", "startstop"),
                    Preset("mf-prev", 1, 0, "Prev preset", "skip_previous", "prev"),
                    Preset("mf-next", 1, 1, "Next preset", "skip_next", "next"),
                },
            },
        },
        Sliders =
        {
            Slider("mf-gain", "Input Gain", -24, 24, 0.5, " dB", "#2980b9", "Input Gain|Gain"),
        },
    };

    private static void AddGitHubProjectPages(DeckLayout layout)
    {
        const int cols = 4;
        const int rows = 4;
        const int pageSize = cols * rows;

        for (var pageIndex = 0; pageIndex * pageSize < GitHubProjects.Length; pageIndex++)
        {
            var page = new Page
            {
                Id = $"p-github-{pageIndex + 1}",
                Name = pageIndex == 0 ? "GitHub" : $"GitHub {pageIndex + 1}",
                Grid = new Grid { Cols = cols, Rows = rows },
            };

            foreach (var (project, index) in GitHubProjects
                         .Skip(pageIndex * pageSize)
                         .Take(pageSize)
                         .Select((project, index) => (project, index)))
            {
                page.Buttons.Add(Open(
                    $"coding-gh-{SafeId(project.Label)}",
                    index / cols,
                    index % cols,
                    project.Label,
                    "open_in_browser",
                    "#2C3E50",
                    project.Url));
            }

            layout.Pages.Add(page);
        }
    }

    private static SliderDef Slider(string id, string label, double min, double max, double step,
        string unit, string color, string key)
        => new()
        {
            Id = id, Label = label, Min = min, Max = max, Step = step, Unit = unit, Color = color,
            Action = new ActionDef
            {
                Provider = "micforge", Verb = "param",
                Params = JsonSerializer.SerializeToElement(new { key }, LayoutStore.Json),
            },
        };

    private static ButtonDef Btn(string id, int row, int col, string label, string icon, string color, string verb)
        => new()
        {
            Id = id, Row = row, Col = col, Label = label, Icon = icon, Color = color,
            Action = new ActionDef { Provider = "micforge", Verb = verb, Params = Empty() },
        };

    private static ButtonDef Open(string id, int row, int col, string label, string icon, string color, string url)
        => new()
        {
            Id = id, Row = row, Col = col, Label = label, Icon = icon, Color = color,
            Action = new ActionDef
            {
                Provider = "os", Verb = "open",
                Params = JsonSerializer.SerializeToElement(new { url }, LayoutStore.Json),
            },
        };

    private static ButtonDef Launch(string id, int row, int col, string label, string icon, string color,
        string path, string? args = null, string? cwd = null)
        => new()
        {
            Id = id, Row = row, Col = col, Label = label, Icon = icon, Color = color,
            Action = new ActionDef
            {
                Provider = "os", Verb = "launch",
                Params = JsonSerializer.SerializeToElement(new { path, args, cwd }, LayoutStore.Json),
            },
        };

    private static ButtonDef NewTextFile(string id, int row, int col, string label, string icon, string color,
        string dir, string prefix)
        => new()
        {
            Id = id, Row = row, Col = col, Label = label, Icon = icon, Color = color,
            Action = new ActionDef
            {
                Provider = "os", Verb = "newtextfile",
                Params = JsonSerializer.SerializeToElement(new { dir, prefix }, LayoutStore.Json),
            },
        };

    private static ButtonDef Preset(string id, int row, int col, string label, string icon, string dir)
        => new()
        {
            Id = id, Row = row, Col = col, Label = label, Icon = icon, Color = "#2C3E50",
            Action = new ActionDef
            {
                Provider = "micforge", Verb = "preset",
                Params = JsonSerializer.SerializeToElement(new { dir }, LayoutStore.Json),
            },
        };

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string SafeId(string value)
        => new(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    private static JsonElement Empty() => JsonSerializer.SerializeToElement(new { }, LayoutStore.Json);
}
