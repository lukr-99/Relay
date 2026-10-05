using System.Windows.Media;
using DotNetLib.Tray;
using Relay.Agent.Theming;

namespace Relay.Agent.Tests;

/// <summary>Every text and background pair in Relay's palettes meets WCAG AA.</summary>
public class RelayPalettesTests
{
    public static TheoryData<string> Modes => ["light", "dark"];

    private static TrayPalette For(string mode) => mode == "dark" ? RelayPalettes.Dark : RelayPalettes.Light;

    [Theory]
    [MemberData(nameof(Modes))]
    public void TextReadsOnEverySurface(string mode)
    {
        var p = For(mode);
        foreach (var surface in new[] { p.Background, p.Surface, p.SurfaceRaised })
        {
            Assert.True(TrayColors.Contrast(p.TextPrimary, surface) >= 4.5, $"{mode}: primary text on {surface}");
            Assert.True(TrayColors.Contrast(p.TextSecondary, surface) >= 4.5, $"{mode}: secondary text on {surface}");
            Assert.True(TrayColors.Contrast(p.Danger, surface) >= 4.5, $"{mode}: danger text on {surface}");
            Assert.True(TrayColors.Contrast(p.Primary, surface) >= 4.5, $"{mode}: accent text on {surface}");
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void ButtonTextReadsOnFilledButtons(string mode)
    {
        var p = For(mode);
        Assert.True(TrayColors.Contrast(p.OnPrimary, p.Primary) >= 4.5, $"{mode}: text on the accent button");
        Assert.True(TrayColors.Contrast(RelayPalettes.OnDanger(p), p.Danger) >= 4.5, $"{mode}: text on the danger button");
        Assert.True(TrayColors.Contrast(p.OnPrimary, RelayPalettes.AccentHover(p, mode == "dark")) >= 4.5, $"{mode}: text on the hovered accent");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void StatusAndFocusColorsStandOut(string mode)
    {
        var p = For(mode);
        foreach (var color in new[] { p.Success, p.Warning, p.Focus })
        {
            Assert.True(TrayColors.Contrast(color, p.Background) >= 3, $"{mode}: {color} on the background");
        }
    }

    [Fact]
    public void HighlightTitleReadsOnItsTint()
    {
        foreach (var p in new[] { RelayPalettes.Light, RelayPalettes.Dark })
        {
            var h = TrayHighlightPalette.From(p);
            Assert.True(TrayColors.Contrast(h.Title, h.Tint) >= 4.5);
        }
    }
}
