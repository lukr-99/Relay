using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relay.Agent.Layout;

/// <summary>The PC-owned description of what the deck shows. Pushed to phones as <c>deck.layout</c>.</summary>
public sealed class DeckLayout
{
    public int Version { get; set; } = 1;
    public Grid Grid { get; set; } = new();
    public string? ActivePage { get; set; }
    public List<Page> Pages { get; set; } = new();

    /// <summary>Continuous controls shown as a row under the deck (e.g. MicForge gain). Deck-wide.</summary>
    public List<SliderDef> Sliders { get; set; } = new();

    [JsonIgnore]
    public IEnumerable<ButtonDef> AllButtons => Pages.SelectMany(p => p.Buttons);

    public ButtonDef? FindButton(string id) => AllButtons.FirstOrDefault(b => b.Id == id);
}

public sealed class Grid
{
    public int Cols { get; set; } = 4;
    public int Rows { get; set; } = 3;
}

public sealed class Page
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Grid? Grid { get; set; }
    public List<ButtonDef> Buttons { get; set; } = new();
}

public sealed class ButtonDef
{
    public string Id { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
    public string Label { get; set; } = "";
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public ActionDef? Action { get; set; }
    public ActionDef? HoldAction { get; set; }
}

/// <summary>A continuous control: drags on the phone set <see cref="Action"/>'s target to a value in
/// [<see cref="Min"/>, <see cref="Max"/>]. Today the target is a MicForge param (micforge/param).</summary>
public sealed class SliderDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;
    public string? Unit { get; set; }
    public string? Color { get; set; }
    public double Value { get; set; }
    public ActionDef? Action { get; set; }
}

/// <summary>A provider + verb + free-form params. The core never interprets <see cref="Params"/>.</summary>
public sealed class ActionDef
{
    public string Provider { get; set; } = "os";
    public string Verb { get; set; } = "";

    /// <summary>Provider-scoped arguments, passed through verbatim to the provider adapter.</summary>
    public JsonElement Params { get; set; }
}
