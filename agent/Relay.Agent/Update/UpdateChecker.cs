using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Relay.Agent.Update;

/// <summary>
/// Checks GitHub Releases for a newer agent build and can download + launch the installer. Uses the
/// public <c>releases/latest</c> endpoint (no token — the repo's releases are public), compares the
/// release's version to <see cref="AppInfo.Version"/>, and returns the installer asset to run.
/// Entirely best-effort: any failure (offline, rate-limited, no release) just yields "no update".
/// </summary>
public sealed class UpdateChecker
{
    private const string LatestUrl = "https://api.github.com/repos/lukr-99/Relay/releases/latest";
    public const string ReleasesPage = "https://github.com/lukr-99/Relay/releases/latest";

    private static readonly HttpClient Http = CreateClient();
    private readonly Log _log;

    public UpdateChecker(Log log) => _log = log;

    /// <summary>A newer release: its <paramref name="Version"/>, the installer download <paramref name="Url"/>,
    /// and the release <paramref name="Notes"/>.</summary>
    public sealed record UpdateInfo(string Version, string Url, string Notes);

    /// <summary>Returns the latest release if it is newer than the running build, else null.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync(LatestUrl, ct);
            if (!resp.IsSuccessStatusCode) { _log.Info($"update check: HTTP {(int)resp.StatusCode}."); return null; }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            var version = ParseVersion(root.TryGetProperty("tag_name", out var t) ? t.GetString() : null);
            if (version is null || !IsNewer(version)) return null;

            var url = InstallerAssetUrl(root);
            if (url is null) { _log.Info("update check: release has no .exe asset."); return null; }

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            _log.Info($"update available: {version} (running {AppInfo.Version}).");
            return new UpdateInfo(version, url, notes);
        }
        catch (Exception ex) { _log.Info($"update check failed: {ex.Message}"); return null; }
    }

    /// <summary>Downloads the installer to a temp file and starts it silently. The caller should then
    /// exit the agent so the installer can replace its files.</summary>
    public async Task<bool> DownloadAndRunAsync(UpdateInfo info, CancellationToken ct = default)
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"Relay-Setup-{info.Version}.exe");
            using (var resp = await Http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(tmp);
                await resp.Content.CopyToAsync(fs, ct);
            }
            Process.Start(new ProcessStartInfo(tmp)
            {
                UseShellExecute = true,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
            });
            _log.Info($"launched installer {tmp}.");
            return true;
        }
        catch (Exception ex) { _log.Error("update download/run failed.", ex); return false; }
    }

    private static string? InstallerAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var a in assets.EnumerateArray())
            if (a.TryGetProperty("name", out var n) && n.GetString() is { } name
                && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && a.TryGetProperty("browser_download_url", out var u))
                return u.GetString();
        return null;
    }

    /// <summary>Extracts an "X.Y.Z" version from a tag like "agent-v0.7.0" or "v0.7.0".</summary>
    private static string? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var m = Regex.Match(tag, @"(\d+)\.(\d+)\.(\d+)");
        return m.Success ? m.Value : null;
    }

    private static bool IsNewer(string candidate)
        => Version.TryParse(candidate, out var other) && Version.TryParse(AppInfo.Version, out var cur) && other > cur;

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.TryParseAdd("Relay-Agent");
        c.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github+json");
        return c;
    }
}
