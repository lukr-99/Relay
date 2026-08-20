using System.Runtime.InteropServices;

namespace DeckForge.Agent.Providers;

/// <summary>
/// Windows keyboard synthesis. Uses <c>keybd_event</c> — simple and reliable for hotkey chords and
/// media keys. (Fullscreen-exclusive / elevated games may ignore synthetic input unless the agent
/// itself runs elevated — see docs/ARCHITECTURE.md "Known Windows gotchas".)
/// </summary>
public static class NativeInput
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    /// <summary>Press a chord (modifiers + key), then release in reverse order.</summary>
    public static void SendChord(IReadOnlyList<string> keys)
    {
        var codes = keys.Select(Resolve).Where(c => c != 0).ToArray();
        if (codes.Length == 0) return;

        foreach (var vk in codes) Down(vk);
        for (int i = codes.Length - 1; i >= 0; i--) Up(codes[i]);
    }

    /// <summary>Tap a single media/transport key.</summary>
    public static void Media(string cmd)
    {
        byte vk = cmd.ToLowerInvariant() switch
        {
            "playpause" or "play" or "pause" => 0xB3,
            "next" => 0xB0,
            "prev" or "previous" => 0xB1,
            "stop" => 0xB2,
            "volup" or "volumeup" => 0xAF,
            "voldown" or "volumedown" => 0xAE,
            "volmute" or "mute" => 0xAD,
            _ => 0,
        };
        if (vk == 0) return;
        // Media keys are "extended" keys.
        keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>Type a short unicode string (used by the os.text verb).</summary>
    public static void TypeText(string text)
    {
        foreach (var ch in text)
        {
            // VkKeyScan gives a VK + shift-state for the char on the current layout.
            short scan = VkKeyScan(ch);
            if (scan == -1) continue;
            byte vk = (byte)(scan & 0xFF);
            bool shift = (scan & 0x100) != 0;
            if (shift) Down(0x10);
            Down(vk);
            Up(vk);
            if (shift) Up(0x10);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScan(char ch);

    private static void Down(byte vk) => keybd_event(vk, 0, IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0, UIntPtr.Zero);
    private static void Up(byte vk) => keybd_event(vk, 0, (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0) | KEYEVENTF_KEYUP, UIntPtr.Zero);

    private static bool IsExtended(byte vk) => vk is 0x5B or 0x5C or 0xB0 or 0xB1 or 0xB2 or 0xB3 or 0xAD or 0xAE or 0xAF;

    private static byte Resolve(string key)
    {
        key = key.Trim().ToLowerInvariant();
        switch (key)
        {
            case "ctrl": case "control": return 0x11;
            case "shift": return 0x10;
            case "alt": case "menu": return 0x12;
            case "win": case "super": case "meta": return 0x5B;
            case "enter": case "return": return 0x0D;
            case "esc": case "escape": return 0x1B;
            case "space": return 0x20;
            case "tab": return 0x09;
            case "backspace": return 0x08;
            case "delete": case "del": return 0x2E;
            case "up": return 0x26;
            case "down": return 0x28;
            case "left": return 0x25;
            case "right": return 0x27;
        }

        if (key.Length == 1)
        {
            char c = key[0];
            if (c is >= 'a' and <= 'z') return (byte)(0x41 + (c - 'a'));
            if (c is >= '0' and <= '9') return (byte)(0x30 + (c - '0'));
        }

        if (key.Length >= 2 && key[0] == 'f' && int.TryParse(key.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
            return (byte)(0x70 + (fn - 1));

        return 0;
    }
}
