namespace Relay.Agent;

/// <summary>One thing Relay ships or builds on: its name, what it is used for, its licence and a link.</summary>
public sealed record Credit(string Name, string Use, string License, string Url);

/// <summary>The About page's facts in one place: who made Relay, where to reach them, and what Relay
/// builds on. The page only shows these, so a change here is the whole edit.</summary>
public static class AboutLinks
{
    public const string AuthorName = "Lukáš Krejčí";
    public const string InstagramUrl = "https://www.instagram.com/lukas9krejci/";
    public const string InstagramHandle = "@lukas9krejci";
    public const string GitHubUrl = "https://github.com/lukr-99";
    public const string GitHubHandle = "lukr-99";
    public const string RepoUrl = "https://github.com/lukr-99/Relay";
    public const string IssuesUrl = "https://github.com/lukr-99/Relay/issues";
    public const string LicenseName = "PolyForm Noncommercial 1.0.0";
    public const string LicenseUrl = "https://polyformproject.org/licenses/noncommercial/1.0.0";

    /// <summary>The author's initials for the seal on the card: first letter of the first and last word.</summary>
    public static string Monogram(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "",
            1 => words[0][..1].ToUpperInvariant(),
            _ => (words[0][..1] + words[^1][..1]).ToUpperInvariant(),
        };
    }

    /// <summary>The libraries the agent and the phone app use, as listed in their project files.</summary>
    public static IReadOnlyList<Credit> Credits { get; } =
    [
        new("DotNetLib", "Tray icon, theme, dialogs, settings page and updater", "The author's own library", "https://github.com/lukr-99/dotnetlib"),
        new("WPF UI", "Controls behind the kit's dialogs and settings page", "MIT", "https://github.com/lepoco/wpfui"),
        new("H.NotifyIcon", "The tray icon", "MIT", "https://github.com/HavenDV/H.NotifyIcon"),
        new("QRCoder", "The pairing QR code", "MIT", "https://github.com/codebude/QRCoder"),
        new("Makaretu.Dns", "mDNS, so the phone finds the PC", "MIT", "https://github.com/richardschneider/net-mdns"),
        new("Jetpack Compose and Material Icons", "The phone app's screens and icons", "Apache 2.0", "https://developer.android.com/jetpack/compose"),
        new("OkHttp", "The phone app's WebSocket connection", "Apache 2.0", "https://github.com/square/okhttp"),
        new("kotlinx.serialization and coroutines", "JSON and background work in the phone app", "Apache 2.0", "https://github.com/Kotlin/kotlinx.serialization"),
    ];
}
