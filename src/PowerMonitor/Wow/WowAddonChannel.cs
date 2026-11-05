using System.Runtime.InteropServices;

namespace PowerMonitor.Wow;

/// <summary>
/// Delivers a status packet to the focused WoW: Forever window as Ctrl+Shift+F6/F7/F8 chords.
/// The addon binds those chords, so this never opens chat or presses Enter.
/// </summary>
internal static class WowAddonChannel
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const ushort VkF6 = 0x75;
    private const ushort VkF7 = 0x76;
    private const ushort VkF8 = 0x77;
    private const uint KeyUp = 0x0002;

    private static int _busy;
    private static byte[]? _last;
    private static DateTime _sentAt;

    public static void Push(byte[] bits)
    {
        if (bits.Length != 71)
            return;
        if (_last is not null &&
            _last.AsSpan().SequenceEqual(bits) &&
            DateTime.UtcNow - _sentAt < TimeSpan.FromSeconds(30))
            return;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return;

        var copy = (byte[])bits.Clone();
        Task.Run(() =>
        {
            try
            {
                if (!ForeverFocused() || ModifierDown())
                    return;
                if (!Send(copy))
                    return;
                _last = copy;
                _sentAt = DateTime.UtcNow;
            }
            catch
            {
                // A missed packet is retried on the next reading.
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        });
    }

    private static bool Send(byte[] bits)
    {
        // One queue drain in WoW handles the whole packet, so this stays a short burst
        // instead of a multi-second hold. F8 is 0, F7 is 1, and F6 ends the packet.
        var events = new List<Input>(bits.Length * 6 + 6);
        foreach (var bit in bits)
            Chord(events, bit == 0 ? VkF8 : VkF7);
        Chord(events, VkF6);
        return SendInput((uint)events.Count, events.ToArray(), Marshal.SizeOf<Input>()) == events.Count;
    }

    private static void Chord(List<Input> events, ushort key)
    {
        events.Add(Key(VkControl, down: true));
        events.Add(Key(VkShift, down: true));
        events.Add(Key(key, down: true));
        events.Add(Key(key, down: false));
        events.Add(Key(VkShift, down: false));
        events.Add(Key(VkControl, down: false));
    }

    private static Input Key(ushort key, bool down) => new()
    {
        Type = 1,
        Ki = new KeybdInput
        {
            Vk = key,
            Flags = down ? 0 : KeyUp
        }
    };

    private static bool ModifierDown() =>
        Down(VkControl) || Down(VkShift) || Down(VkMenu);

    private static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool ForeverFocused()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return false;
        _ = GetWindowThreadProcessId(window, out var pid);
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            if (!process.ProcessName.Equals("Wow", StringComparison.OrdinalIgnoreCase))
                return false;
            var path = process.MainModule?.FileName;
            return path is not null && path.Contains(@"\_forever_\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr Extra;
    }
}
