using System.Text.Json;

namespace Relay.Agent.Layout;

/// <summary>Ready-made deck presets the editor can drop in with one click.</summary>
public static class PresetTemplates
{
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

    private static JsonElement Empty() => JsonSerializer.SerializeToElement(new { }, LayoutStore.Json);
}
