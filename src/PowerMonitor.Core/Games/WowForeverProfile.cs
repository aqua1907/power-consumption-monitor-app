using System.Diagnostics;
using PowerMonitor.Core.Settings;
using PowerMonitor.Core.Wow;

namespace PowerMonitor.Core.Games;

public sealed class WowForeverProfile : IGamePowerProfile
{
    private bool _running;

    public WowForeverProfile(string? snapshotPath = null)
    {
        SnapshotPath = snapshotPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerMonitor",
            "wow-forever-grid.wtf");
    }

    public string Id => "wow-forever";
    public string Name => "WoW: Forever";
    public string SnapshotPath { get; }

    public string? Apply(bool onBackup, AppSettings settings)
    {
        var path = settings.WowForeverConfigPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "WoW Config.wtf path is missing.";

        if (onBackup)
        {
            WowConfigPatcher.SnapshotIfQuality(path, SnapshotPath);
            WowConfigPatcher.ApplyCvars(path, WowConfigPatcher.BackupCvars);
            return "WoW low-power graphics written (restart WoW to apply).";
        }

        if (!File.Exists(SnapshotPath))
            return "No saved quality graphics snapshot yet.";

        WowConfigPatcher.RestoreSnapshot(path, SnapshotPath);
        return "WoW quality graphics restored (restart WoW to apply).";
    }

    public string? Observe(bool gridConnected, AppSettings settings)
    {
        if (gridConnected)
        {
            _running = WowRunning();
            return null;
        }

        var running = WowRunning();
        if (_running && !running)
        {
            _running = false;
            if (string.IsNullOrWhiteSpace(settings.WowForeverConfigPath) ||
                !File.Exists(settings.WowForeverConfigPath))
                return null;

            // WoW rewrites Config.wtf from memory on exit.
            WowConfigPatcher.ApplyCvars(settings.WowForeverConfigPath, WowConfigPatcher.BackupCvars);
            return "WoW exited on battery — low-power graphics kept for the next launch.";
        }

        _running = running;
        return null;
    }

    private static bool WowRunning()
    {
        try
        {
            return Process.GetProcessesByName("Wow").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
