using System.Windows;

namespace Relay.Agent.Tests;

/// <summary>A saved window spot is used only when its title bar is on a connected monitor.</summary>
public class WindowPlacementsTests
{
    // Two 2560 x 1440 monitors side by side.
    private static readonly Rect TwoMonitors = new(0, 0, 5120, 1440);

    [Fact]
    public void ASpotOnTheSecondMonitorIsUsed() =>
        Assert.True(WindowPlacements.IsVisible(new WindowPlacement(3400, 300, 1020, 700, false), TwoMonitors));

    [Fact]
    public void ASpotOnAnUnpluggedMonitorIsIgnored() =>
        Assert.False(WindowPlacements.IsVisible(new WindowPlacement(3400, 300, 1020, 700, false), new Rect(0, 0, 2560, 1440)));

    [Fact]
    public void ATitleBarAboveTheScreenIsIgnored() =>
        Assert.False(WindowPlacements.IsVisible(new WindowPlacement(200, -500, 1020, 700, false), TwoMonitors));

    [Fact]
    public void AWindowHangingMostlyOffTheEdgeIsStillUsedWhileItsTitleBarCanBeGrabbed() =>
        Assert.True(WindowPlacements.IsVisible(new WindowPlacement(4900, 300, 1020, 700, false), TwoMonitors));
}
