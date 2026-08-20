namespace DeckForge.Agent.Layout;

/// <summary>Icon names the phone app currently knows how to render (see the app's DeckIcons.kt).
/// Keep this in sync when the app adds more.</summary>
public static class IconCatalog
{
    public static readonly string[] Names =
    {
        "play_pause", "skip_previous", "skip_next",
        "volume_off", "mic_off",
        "content_cut", "chat", "edit_note",
        "open_in_browser", "keyboard",
        "touch_app",
    };
}
