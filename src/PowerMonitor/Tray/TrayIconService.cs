using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace PowerMonitor.Tray;

internal sealed class TrayIconService : IDisposable
{
    private const int CallbackMessage = 0x8001;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDoubleClick = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifInfo = 0x00000010;
    private const uint NotifyIconVersion4 = 4;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const int OpenCommand = 1;
    private const int SettingsCommand = 2;
    private const int ExitCommand = 3;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly DispatcherQueue _dispatcher;
    private readonly WndProc _wndProc;
    private readonly string _className = $"PowerMonitorTray.{Environment.ProcessId}";
    private readonly IntPtr _instance;
    private IntPtr _window;
    private Icon? _icon;
    private bool _disposed;

    public event Action? OpenRequested;
    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService(DispatcherQueue dispatcher, Icon icon)
    {
        _dispatcher = dispatcher;
        _wndProc = WindowProc;
        _instance = GetModuleHandle(null);

        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Instance = _instance,
            WindowProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            ClassName = _className
        };
        if (RegisterClassEx(ref windowClass) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the tray window.");

        _window = CreateWindowEx(
            0, _className, "", 0, 0, 0, 0, 0,
            HwndMessage, IntPtr.Zero, _instance, IntPtr.Zero);
        if (_window == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray window.");

        _icon = icon;
        var data = Data(NifMessage | NifIcon | NifTip);
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon.Handle;
        data.Tooltip = "Power monitor";
        if (!ShellNotifyIcon(NimAdd, ref data))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not add the tray icon.");

        data.VersionOrTimeout = NotifyIconVersion4;
        ShellNotifyIcon(NimSetVersion, ref data);
    }

    public void Update(Icon icon, string tooltip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var previous = _icon;
        _icon = icon;
        var data = Data(NifIcon | NifTip);
        data.Icon = icon.Handle;
        data.Tooltip = Clip(tooltip, 127);
        ShellNotifyIcon(NimModify, ref data);
        previous?.Dispose();
    }

    public void UpdateTooltip(string tooltip)
    {
        if (_disposed)
            return;

        var data = Data(NifTip);
        data.Tooltip = Clip(tooltip, 127);
        ShellNotifyIcon(NimModify, ref data);
    }

    public void ShowNotification(string title, string message)
    {
        if (_disposed)
            return;

        var data = Data(NifInfo);
        data.InfoTitle = Clip(title, 63);
        data.Info = Clip(message, 255);
        data.InfoFlags = 0x00000001;
        ShellNotifyIcon(NimModify, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_window != IntPtr.Zero)
        {
            var data = Data(0);
            ShellNotifyIcon(NimDelete, ref data);
            DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        UnregisterClass(_className, _instance);
        _icon?.Dispose();
        _icon = null;
    }

    private IntPtr WindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == CallbackMessage)
        {
            var mouseMessage = lParam.ToInt32() & 0xFFFF;
            switch (mouseMessage)
            {
                case WmLButtonUp:
                    _dispatcher.TryEnqueue(() => ToggleRequested?.Invoke());
                    return IntPtr.Zero;
                case WmLButtonDoubleClick:
                    return IntPtr.Zero;
                case WmRButtonUp:
                case WmContextMenu:
                    ShowContextMenu();
                    return IntPtr.Zero;
            }
        }

        return DefWindowProc(window, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        try
        {
            AppendMenu(menu, MfString, OpenCommand, "Open dashboard");
            AppendMenu(menu, MfString, SettingsCommand, "Settings");
            AppendMenu(menu, MfSeparator, 0, "");
            AppendMenu(menu, MfString, ExitCommand, "Exit");
            GetCursorPos(out var point);
            SetForegroundWindow(_window);
            var command = TrackPopupMenu(
                menu,
                TpmRightButton | TpmReturnCommand,
                point.X,
                point.Y,
                0,
                _window,
                IntPtr.Zero);
            _dispatcher.TryEnqueue(() =>
            {
                switch (command)
                {
                    case OpenCommand:
                        OpenRequested?.Invoke();
                        break;
                    case SettingsCommand:
                        SettingsRequested?.Invoke();
                        break;
                    case ExitCommand:
                        ExitRequested?.Invoke();
                        break;
                }
            });
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private NotifyIconData Data(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Window = _window,
        Id = 1,
        Flags = flags
    };

    private static string Clip(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private delegate IntPtr WndProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tooltip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;
        public uint VersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;
        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, int id, string text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        int reserved,
        IntPtr window,
        IntPtr rectangle);
}
