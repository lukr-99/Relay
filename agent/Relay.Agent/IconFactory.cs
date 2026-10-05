using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DotNetLib.Tray;
using Relay.Agent.Theming;

namespace Relay.Agent;

/// <summary>What the tray icon shows by its color. The tooltip says the same in words.</summary>
public enum AgentStatus
{
    /// <summary>At least one phone is connected: the brand violet.</summary>
    Connected,

    /// <summary>The server runs but no phone is connected: grey.</summary>
    Waiting,

    /// <summary>The server could not start: red.</summary>
    Failed,
}

/// <summary>Draws the Relay icon at run time: a 2x2 deck of white tiles on a rounded square.
/// Used for the tray icon (one color per status) and the window icon.</summary>
internal static class IconFactory
{
    private static readonly Color Waiting = Color.FromRgb(0x6B, 0x72, 0x80);
    private static readonly Color Failed = Color.FromRgb(0xC0, 0x39, 0x2B);

    private static readonly Dictionary<AgentStatus, byte[]> TrayIcons = [];

    /// <summary>A multi-size .ico for the tray, in the status color. Made once per status.</summary>
    public static byte[] TrayIcon(AgentStatus status)
    {
        if (!TrayIcons.TryGetValue(status, out var icon))
        {
            var color = status switch
            {
                AgentStatus.Connected => RelayPalettes.Brand,
                AgentStatus.Failed => Failed,
                _ => Waiting,
            };
            icon = IconFile.Create(IconFile.TraySizes, size => IconFile.Render(size, (dc, px) => Draw(dc, px, color)));
            TrayIcons[status] = icon;
        }

        return icon;
    }

    /// <summary>The brand icon as an image, for the window and the nav rail.</summary>
    public static ImageSource CreateImageSource(int size = 64)
    {
        var pixels = IconFile.Render(size, (dc, px) => Draw(dc, px, RelayPalettes.Brand));
        var source = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        source.Freeze();
        return source;
    }

    private static void Draw(DrawingContext dc, int size, Color background)
    {
        var bg = new SolidColorBrush(background);
        bg.Freeze();
        double r = size * 0.22;
        dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, size, size), r, r);

        double pad = size * 0.20;
        double gap = size * 0.10;
        double cell = (size - pad * 2 - gap) / 2;
        double tr = cell * 0.28;
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                var tile = new Rect(pad + col * (cell + gap), pad + row * (cell + gap), cell, cell);
                dc.DrawRoundedRectangle(Brushes.White, null, tile, tr, tr);
            }
        }
    }
}
