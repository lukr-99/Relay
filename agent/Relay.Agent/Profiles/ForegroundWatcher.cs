using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace Relay.Agent.Profiles;

/// <summary>
/// Polls the Windows foreground window and raises <see cref="Changed"/> whenever the focused app's
/// executable or title changes. Reads the process image name via <c>QueryFullProcessImageName</c> with
/// <c>PROCESS_QUERY_LIMITED_INFORMATION</c>, which works across integrity levels — so it still sees a
/// game running as administrator even though the agent runs unelevated.
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    /// <summary>(exeFileName, windowTitle) of the current foreground window. exe is the bare file name
    /// (e.g. "VALORANT-Win64-Shipping.exe"), lower-cased; either field may be empty.</summary>
    public event Action<string, string>? Changed;

    public string CurrentExe { get; private set; } = "";
    public string CurrentTitle { get; private set; } = "";

    private readonly DispatcherTimer _timer;

    public ForegroundWatcher(TimeSpan? interval = null)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval ?? TimeSpan.FromMilliseconds(700),
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();

    private void Poll()
    {
        var (exe, title) = Read();
        if (exe == CurrentExe && title == CurrentTitle) return;
        CurrentExe = exe;
        CurrentTitle = title;
        try { Changed?.Invoke(exe, title); } catch { }
    }

    private static (string exe, string title) Read()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return ("", "");

            GetWindowThreadProcessId(hwnd, out var pid);
            var exe = ExeForPid(pid);

            var len = GetWindowTextLength(hwnd);
            var title = "";
            if (len > 0)
            {
                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                title = sb.ToString();
            }
            return (exe.ToLowerInvariant(), title);
        }
        catch { return ("", ""); }
    }

    private static string ExeForPid(uint pid)
    {
        if (pid == 0) return "";
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return "";
        try
        {
            var buf = new StringBuilder(1024);
            uint size = (uint)buf.Capacity;
            if (QueryFullProcessImageName(h, 0, buf, ref size))
                return System.IO.Path.GetFileName(buf.ToString());
            return "";
        }
        finally { CloseHandle(h); }
    }

    public void Dispose() => _timer.Stop();

    // ── P/Invoke ─────────────────────────────────────────────────────────────────────────────
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int max);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder buf, ref uint size);
}
