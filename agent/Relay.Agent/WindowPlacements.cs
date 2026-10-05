using System.Windows;

namespace Relay.Agent;

/// <summary>Checks a saved window spot against the monitors that are connected now.</summary>
public static class WindowPlacements
{
    /// <summary>
    /// True when enough of the window's title bar is on a connected monitor to grab it: at least
    /// 100 by 40 device-independent pixels of its top edge fall inside the virtual screen.
    /// </summary>
    public static bool IsVisible(WindowPlacement w) =>
        IsVisible(w, new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));

    /// <summary>The rule above against a given virtual screen, for tests.</summary>
    public static bool IsVisible(WindowPlacement w, Rect virtualScreen)
    {
        var titleBar = new Rect(w.Left, w.Top, w.Width, Math.Min(40, w.Height));
        var shown = Rect.Intersect(titleBar, virtualScreen);
        return !shown.IsEmpty && shown.Width >= 100 && shown.Height >= 20;
    }
}
