using System.Windows.Media;
using DotNetLib.Tray;

namespace Relay.Agent.Theming;

/// <summary>
/// Relay's light and dark colors as kit palettes, with the violet brand accent. The pairs used for
/// text are checked against WCAG AA in the tests.
/// </summary>
public static class RelayPalettes
{
    /// <summary>The brand violet, also the tray icon and the phone app's launcher icon.</summary>
    public static Color Brand { get; } = Color.FromRgb(0x7C, 0x5C, 0xFF);

    public static TrayPalette Light { get; } = new(
        Background: Color.FromRgb(0xF4, 0xF5, 0xF8),
        Surface: Color.FromRgb(0xFF, 0xFF, 0xFF),
        SurfaceRaised: Color.FromRgb(0xEE, 0xEF, 0xF4),
        TextPrimary: Color.FromRgb(0x1A, 0x1D, 0x23),
        TextSecondary: Color.FromRgb(0x56, 0x5D, 0x69),
        Border: Color.FromRgb(0xD3, 0xD6, 0xDE),
        Primary: Color.FromRgb(0x5B, 0x3C, 0xE0),
        OnPrimary: Color.FromRgb(0xFF, 0xFF, 0xFF),
        Danger: Color.FromRgb(0xB4, 0x23, 0x18),
        Success: Color.FromRgb(0x1A, 0x7A, 0x3A),
        Warning: Color.FromRgb(0x9A, 0x5B, 0x00),
        Focus: Color.FromRgb(0x5B, 0x3C, 0xE0));

    public static TrayPalette Dark { get; } = new(
        Background: Color.FromRgb(0x0E, 0x11, 0x16),
        Surface: Color.FromRgb(0x16, 0x1B, 0x22),
        SurfaceRaised: Color.FromRgb(0x1F, 0x25, 0x2E),
        TextPrimary: Color.FromRgb(0xE6, 0xE6, 0xE6),
        TextSecondary: Color.FromRgb(0x9A, 0xA4, 0xB2),
        Border: Color.FromRgb(0x2E, 0x36, 0x41),
        // A lighter violet than the brand, so it reads as text on the dark background and carries
        // dark text when it fills a button.
        Primary: Color.FromRgb(0x9E, 0x86, 0xFF),
        OnPrimary: Color.FromRgb(0x10, 0x13, 0x1A),
        Danger: Color.FromRgb(0xFF, 0x6B, 0x60),
        Success: Color.FromRgb(0x4A, 0xC8, 0x6E),
        Warning: Color.FromRgb(0xF0, 0xB4, 0x29),
        Focus: Color.FromRgb(0x9E, 0x86, 0xFF));

    /// <summary>Text on a danger button: white on the deep light red, near black on the light dark-mode red.</summary>
    public static Color OnDanger(TrayPalette palette) =>
        TrayColors.Contrast(Colors.White, palette.Danger) >= 4.5 ? Colors.White : palette.Background;

    /// <summary>The accent under the mouse: a little lighter in dark mode, a little darker in light mode.</summary>
    public static Color AccentHover(TrayPalette palette, bool isDark) =>
        TrayColors.Mix(isDark ? Colors.White : Colors.Black, palette.Primary, 0.14);

    /// <summary>The scroll bar thumb, at rest.</summary>
    public static Color ScrollThumb(TrayPalette palette) =>
        TrayColors.Mix(palette.TextSecondary, palette.Background, 0.30);

    /// <summary>The scroll bar thumb under the mouse or while dragged.</summary>
    public static Color ScrollThumbHover(TrayPalette palette) =>
        TrayColors.Mix(palette.TextSecondary, palette.Background, 0.50);
}
