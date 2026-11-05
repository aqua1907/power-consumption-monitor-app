using System.Runtime.InteropServices;

namespace PowerMonitor.BackupMode;

internal static class DisplayRefresh
{
    private const int EnumCurrentSettings = -1;
    private const int CdsUpdateregistry = 0x00000001;
    private const int DispChangeSuccessful = 0;

    public static int? TrySetHz(int hz)
    {
        var mode = new DevMode { dmSize = (ushort)Marshal.SizeOf<DevMode>() };
        if (EnumDisplaySettings(null, EnumCurrentSettings, ref mode) == 0)
            return null;

        if (mode.dmDisplayFrequency == hz)
            return hz;

        mode.dmFields = 0x400000; // DM_DISPLAYFREQUENCY
        mode.dmDisplayFrequency = (uint)hz;
        var result = ChangeDisplaySettingsEx(null, ref mode, IntPtr.Zero, CdsUpdateregistry, IntPtr.Zero);
        return result == DispChangeSuccessful ? hz : null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(
        string? deviceName,
        ref DevMode deviceMode,
        IntPtr hwnd,
        int flags,
        IntPtr param);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
