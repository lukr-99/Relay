using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Relay.Agent.Icons;

/// <summary>Turns a website URL or a local image file into a small PNG data URI that can be stored in a
/// button's <see cref="Layout.ButtonDef.Icon"/> and rendered as-is on the phone. Site icons are resolved
/// through Google's favicon service (the same approach SubTrackr uses); every source is normalised to a
/// PNG no larger than <see cref="MaxPixels"/> so the embedded data stays small.</summary>
public static class IconResolver
{
    public const int MaxPixels = 64;
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private const string DataUriPrefix = "data:image";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Resolve a site's icon from a full URL or a bare domain into a PNG data URI.</summary>
    public static async Task<string> FromUrlAsync(string urlOrDomain, CancellationToken ct = default)
    {
        var host = HostOf(urlOrDomain);
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Enter a website address, e.g. github.com");

        var favicon = $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(host)}&sz={MaxPixels}";
        var bytes = await Http.GetByteArrayAsync(favicon, ct).ConfigureAwait(false);
        if (bytes.Length == 0) throw new InvalidOperationException("No icon found for that site.");
        return ToDataUri(NormalizePng(bytes));
    }

    /// <summary>Load a local image file (png/jpg/ico/bmp/gif) into a PNG data URI.</summary>
    public static string FromFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("File not found.", path);
        if (info.Length > MaxFileBytes) throw new InvalidOperationException("Image is too large (max 8 MB).");
        return ToDataUri(NormalizePng(File.ReadAllBytes(path)));
    }

    /// <summary>True when an icon value is an embedded image rather than a catalog icon name.</summary>
    public static bool IsImage(string? icon)
        => icon is not null && icon.StartsWith(DataUriPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Decode an image data URI to a bitmap for previewing in the editor; null if it isn't one.</summary>
    public static BitmapSource? Decode(string? icon)
    {
        if (!IsImage(icon)) return null;
        var comma = icon!.IndexOf(',');
        if (comma < 0) return null;
        try
        {
            var bytes = Convert.FromBase64String(icon[(comma + 1)..]);
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static string ToDataUri(byte[] png) => DataUriPrefix + "/png;base64," + Convert.ToBase64String(png);

    /// <summary>Decode arbitrary image bytes, downscale so the longest edge is at most
    /// <see cref="MaxPixels"/>, and re-encode as PNG — giving one uniform, small format for every source.</summary>
    private static byte[] NormalizePng(byte[] input)
    {
        using var ms = new MemoryStream(input);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames[0];

        var longest = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (longest > MaxPixels)
        {
            var scale = (double)MaxPixels / longest;
            frame = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var outMs = new MemoryStream();
        encoder.Save(outMs);
        return outMs.ToArray();
    }

    /// <summary>Extract the host from a full URL or a bare domain ("github.com", "https://github.com/x").</summary>
    private static string HostOf(string input)
    {
        input = input.Trim();
        if (input.Length == 0) return "";
        if (!input.Contains("://")) input = "https://" + input;
        return Uri.TryCreate(input, UriKind.Absolute, out var uri) ? uri.Host : "";
    }
}
