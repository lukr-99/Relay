namespace Relay.Agent.Layout;

/// <summary>Icon names the phone app currently knows how to render (see the app's DeckIcons.kt).
/// Keep this in sync when the app adds more.</summary>
public static class IconCatalog
{
    public static readonly string[] Names =
    {
        "play_pause", "play_arrow", "pause", "stop", "skip_previous", "skip_next",
        "volume_up", "volume_down", "volume_off", "mic", "mic_off",
        "content_cut", "photo_camera", "videocam",
        "chat", "edit_note", "keyboard", "open_in_browser", "folder", "terminal",
        "refresh", "lock", "home", "settings", "star", "bolt", "power",
        "touch_app",
    };
}
