using Microsoft.Win32;

namespace PowerMonitor.Startup;

internal static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeyePowerMonitor";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Windows startup settings are not available.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot find this program's path.");
        key.SetValue(ValueName, $"\"{path}\"");
    }
}
