using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace PowerMonitor.Views;

internal readonly record struct MonitorMetrics(RectInt32 WorkArea, uint Dpi)
{
    public double Scale => Dpi / 96d;
    public double DipWidth => WorkArea.Width / Scale;
    public double DipHeight => WorkArea.Height / Scale;

    public int ToPixels(double dip) => (int)Math.Round(dip * Scale);

    public static MonitorMetrics FromCursor()
    {
        return GetCursorPos(out var point)
            ? FromPoint(point.X, point.Y)
            : FromPrimary();
    }

    public static MonitorMetrics ForTray()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero)
            return FromCursor();

        var monitor = MonitorFromWindow(tray, DefaultToNearest);
        return monitor == IntPtr.Zero ? FromCursor() : FromMonitor(monitor);
    }

    public static int TrayIconPixels() => Math.Max(16, ForTray().ToPixels(32));

    private static MonitorMetrics FromPrimary()
    {
        var area = DisplayArea.Primary.WorkArea;
        var x = area.X + Math.Max(0, area.Width / 2);
        var y = area.Y + Math.Max(0, area.Height / 2);
        return FromPoint(x, y);
    }

    private static MonitorMetrics FromPoint(int x, int y)
    {
        var monitor = MonitorFromPoint(new Point { X = x, Y = y }, DefaultToNearest);
        return FromMonitor(monitor);
    }

    private static MonitorMetrics FromMonitor(IntPtr monitor)
    {
        var area = DisplayArea.GetFromPoint(CenterOf(monitor), DisplayAreaFallback.Primary).WorkArea;
        return new MonitorMetrics(area, DpiOf(monitor));
    }

    private static PointInt32 CenterOf(IntPtr monitor)
    {
        if (monitor != IntPtr.Zero && TryGetMonitorInfo(monitor, out var info))
        {
            var work = info.Work;
            return new PointInt32((work.Left + work.Right) / 2, (work.Top + work.Bottom) / 2);
        }

        var primary = DisplayArea.Primary.WorkArea;
        return new PointInt32(primary.X + primary.Width / 2, primary.Y + primary.Height / 2);
    }

    private static uint DpiOf(IntPtr monitor)
    {
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, EffectiveDpi, out var dpi, out _) == 0 && dpi >= 96)
            return dpi;
        return 96;
    }

    private const uint DefaultToNearest = 2;
    private const int EffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private static bool TryGetMonitorInfo(IntPtr monitor, out MonitorInfo info)
    {
        info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(monitor, ref info);
    }

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);
}

internal static class WindowSizing
{
    public static AppWindow AppWindowOf(Window window) =>
        AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle(window)));

    public static int ToPixels(Window window, double dip)
    {
        var dpi = GetDpiForWindow(Handle(window));
        if (dpi < 96)
            dpi = 96;
        return (int)Math.Round(dip * dpi / 96d);
    }

    public static void Fit(Window window, double preferredWidth, double preferredHeight)
    {
        var monitor = MonitorMetrics.FromCursor();
        var width = monitor.ToPixels(Math.Min(preferredWidth, monitor.DipWidth * 0.92));
        var height = monitor.ToPixels(Math.Min(preferredHeight, monitor.DipHeight * 0.9));
        var area = monitor.WorkArea;
        var appWindow = AppWindowOf(window);
        appWindow.Move(new PointInt32(
            area.X + Math.Max(0, (area.Width - width) / 2),
            area.Y + Math.Max(0, (area.Height - height) / 2)));
        appWindow.Resize(new SizeInt32(width, height));
    }

    /// <summary>
    /// Sizes a compact flyout for the monitor under the cursor and anchors it above the taskbar.
    /// Returns the pixel size that was applied.
    /// </summary>
    public static SizeInt32 PlaceFlyout(Window window, double edgeMargin)
    {
        var monitor = MonitorMetrics.FromCursor();
        var width = monitor.ToPixels(Math.Clamp(monitor.DipWidth * 0.2, 360, 520));
        var height = monitor.ToPixels(Math.Clamp(monitor.DipHeight * 0.46, 440, 680));
        var margin = monitor.ToPixels(edgeMargin);
        var area = monitor.WorkArea;
        var appWindow = AppWindowOf(window);
        appWindow.Move(new PointInt32(area.X, area.Y));
        appWindow.Resize(new SizeInt32(width, height));
        appWindow.Move(new PointInt32(
            area.X + area.Width - width - margin,
            area.Y + area.Height - height - margin));
        return new SizeInt32(width, height);
    }

    private static IntPtr Handle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
}
