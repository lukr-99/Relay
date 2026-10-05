using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DotNetLib.Tray;

namespace Relay.Agent.Theming;

/// <summary>
/// The agent's theme: the kit's <see cref="TrayThemeApplier"/> with Relay's palettes, plus the few
/// <c>Relay.*</c> brushes the agent's own styles need. Views use every brush with DynamicResource, so
/// a switch, or Windows changing its mode while <see cref="TrayThemeMode.System"/> is on, applies at once.
/// </summary>
public sealed class AgentTheme : IDisposable
{
    public const string AccentHoverKey = "Relay.AccentHoverBrush";
    public const string OnDangerKey = "Relay.OnDangerBrush";
    public const string ScrollThumbKey = "Relay.ScrollThumbBrush";
    public const string ScrollThumbHoverKey = "Relay.ScrollThumbHoverBrush";

    private readonly ResourceDictionary _resources;
    private readonly TrayThemeApplier _applier;
    private readonly List<Window> _windows = [];

    public AgentTheme(ResourceDictionary resources)
    {
        _resources = resources;
        _applier = new TrayThemeApplier(resources, TrayThemeApplier.WindowsAppsUseDark, RelayPalettes.Light, RelayPalettes.Dark);
        _applier.Applied += (_, _) => OnApplied();
    }

    /// <summary>Raised after every apply, once the Relay brushes and title bars are updated.</summary>
    public event EventHandler? Applied;

    public TrayThemeMode Mode => _applier.Mode;

    public bool IsDark => _applier.IsDark;

    public void Apply(TrayThemeMode mode) => _applier.Apply(mode);

    /// <summary>Themes a kit dialog (pass as the dialogs' <c>prepare</c>).</summary>
    public void Attach(Window window) => _applier.Attach(window);

    /// <summary>Keeps a long-lived window's title bar in step with the theme.</summary>
    public void Track(Window window)
    {
        _windows.Add(window);
        window.SourceInitialized += (_, _) => SetDarkTitleBar(window, IsDark);
    }

    public void Dispose() => _applier.Dispose();

    private void OnApplied()
    {
        var palette = IsDark ? RelayPalettes.Dark : RelayPalettes.Light;
        SetBrush(AccentHoverKey, RelayPalettes.AccentHover(palette, IsDark));
        SetBrush(OnDangerKey, RelayPalettes.OnDanger(palette));
        SetBrush(ScrollThumbKey, RelayPalettes.ScrollThumb(palette));
        SetBrush(ScrollThumbHoverKey, RelayPalettes.ScrollThumbHover(palette));
        foreach (var window in _windows)
        {
            SetDarkTitleBar(window, IsDark);
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _resources[key] = brush;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Windows 10 20H1 and later use attribute 20; older builds use 19.
    private static void SetDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int on = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }
    }
}
