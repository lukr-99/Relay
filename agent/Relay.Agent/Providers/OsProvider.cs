using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Relay.Agent.Providers;

/// <summary>Operating-system actions: hotkeys, media keys, launch, open, type text.</summary>
public sealed class OsProvider : IProvider
{
    private readonly Log _log;
    public string Id => "os";

    public OsProvider(Log log) => _log = log;

    public Task InvokeAsync(string verb, JsonElement p, CancellationToken ct = default)
    {
        switch (verb.ToLowerInvariant())
        {
            case "hotkey":
                var keys = ReadStringArray(p, "keys");
                if (keys.Count > 0) NativeInput.SendChord(keys);
                break;

            case "media":
                NativeInput.Media(GetString(p, "cmd") ?? "");
                break;

            case "keydown":
                var down = ReadStringArray(p, "keys");
                if (down.Count > 0) NativeInput.HoldDown(down);
                break;

            case "keyup":
                var up = ReadStringArray(p, "keys");
                if (up.Count > 0) NativeInput.HoldUp(up);
                break;

            case "text":
                NativeInput.TypeText(GetString(p, "value") ?? "");
                break;

            case "launch":
                var path = GetString(p, "path");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                    if (GetString(p, "args") is { Length: > 0 } args) psi.Arguments = args;
                    if (GetString(p, "cwd") is { Length: > 0 } cwd) psi.WorkingDirectory = cwd;
                    Process.Start(psi);
                }
                break;

            case "newtextfile":
                NewTextFile(p);
                break;

            case "open":
                // A `special` token resolves to a known folder (portable across PCs); a `urls` array
                // opens every entry (multi-URL button); otherwise open the single `url`.
                if (GetString(p, "special") == "screenshots") { Open(ScreenshotsDir()); break; }
                var urls = ReadStringArray(p, "urls");
                if (urls.Count > 0)
                {
                    foreach (var u in urls)
                        if (!string.IsNullOrWhiteSpace(u)) Open(u);
                    break;
                }
                var target = GetString(p, "url");
                if (!string.IsNullOrWhiteSpace(target)) Open(target);
                break;

            case "screenshot":
                Screenshot();
                break;

            default:
                _log.Warn($"os: unknown verb '{verb}'.");
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>Opens a URL, file, or folder via the shell (the default app / browser).</summary>
    private static void Open(string target)
        => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    private static void NewTextFile(JsonElement p)
    {
        var dir = GetString(p, "dir");
        if (string.IsNullOrWhiteSpace(dir)) return;

        Directory.CreateDirectory(dir);
        var prefix = GetString(p, "prefix");
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "note";

        var file = UniqueTextFile(dir, prefix);
        File.WriteAllText(file, "");
        Process.Start(new ProcessStartInfo("notepad.exe")
        {
            Arguments = QuoteArg(file),
            WorkingDirectory = dir,
            UseShellExecute = true,
        });
    }

    private static string UniqueTextFile(string dir, string prefix)
    {
        var safePrefix = SafeFilePrefix(prefix);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        var file = Path.Combine(dir, $"{safePrefix}-{stamp}.txt");
        for (var n = 2; File.Exists(file); n++)
            file = Path.Combine(dir, $"{safePrefix}-{stamp}-{n}.txt");
        return file;
    }

    private static string SafeFilePrefix(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Trim().Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "note" : safe;
    }

    private static string QuoteArg(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    /// <summary>Captures all monitors to a PNG in Pictures\Screenshots and copies it to the clipboard —
    /// an actual capture, unlike the Win+Shift+S overlay which only opens the snip tool.</summary>
    private void Screenshot()
    {
        try
        {
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);

            var file = Path.Combine(ScreenshotsDir(), $"Relay-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);

            // Clipboard access must be on an STA thread; best-effort so a paste is ready immediately.
            var t = new Thread(() =>
            {
                try { System.Windows.Forms.Clipboard.SetImage(bmp); } catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(2000);

            _log.Info($"screenshot saved to {file} (and copied to clipboard).");
        }
        catch (Exception ex) { _log.Error("screenshot failed.", ex); }
    }

    /// <summary>The folder screenshots are saved to (Pictures\Screenshots), created if missing.</summary>
    private static string ScreenshotsDir()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string? GetString(JsonElement p, string name)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static List<string> ReadStringArray(JsonElement p, string name)
    {
        var list = new List<string>();
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String && el.GetString() is { } s)
                    list.Add(s);
        return list;
    }
}
