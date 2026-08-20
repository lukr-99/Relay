using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Relay.Agent;

/// <summary>Draws the Relay icon at runtime — a deck of buttons on the accent colour.
/// Used for the tray icon and the window icon (no .ico asset needed).</summary>
internal static class IconFactory
{
    private static readonly Color Accent = Color.FromArgb(0x7C, 0x5C, 0xFF);
    private static readonly Color Tile = Color.FromArgb(0xFF, 0xFF, 0xFF);

    private static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float r = size * 0.22f;
        using (var bg = new SolidBrush(Accent))
        using (var path = Rounded(0.5f, 0.5f, size - 1f, size - 1f, r))
            g.FillPath(bg, path);

        // 2x2 grid of rounded button tiles
        float pad = size * 0.20f;
        float gap = size * 0.10f;
        float cell = (size - pad * 2 - gap) / 2f;
        float tr = cell * 0.28f;
        using var tile = new SolidBrush(Tile);
        for (int row = 0; row < 2; row++)
        for (int col = 0; col < 2; col++)
        {
            float x = pad + col * (cell + gap);
            float y = pad + row * (cell + gap);
            using var p = Rounded(x, y, cell, cell, tr);
            g.FillPath(tile, p);
        }
        return bmp;
    }

    private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Icon CreateTrayIcon(int size = 32)
    {
        using var bmp = Draw(size);
        var h = bmp.GetHicon();
        return (Icon)Icon.FromHandle(h).Clone(); // clone so we can free the HICON
    }

    public static System.Windows.Media.ImageSource CreateImageSource(int size = 64)
    {
        using var bmp = Draw(size);
        var src = Imaging.CreateBitmapSourceFromHBitmap(
            bmp.GetHbitmap(Color.Transparent), IntPtr.Zero, Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }
}
