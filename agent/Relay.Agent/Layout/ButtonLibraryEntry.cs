namespace Relay.Agent.Layout;

public sealed class ButtonLibraryEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ButtonDef Button { get; set; } = new();

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Button.Label : Name;
}
