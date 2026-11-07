using System.Diagnostics;
using PowerMonitor.Core.Settings;
using PowerMonitor.Core.Wow;

namespace PowerMonitor.Core.Games;

public sealed class WowForeverProfile : IGamePowerProfile
{
    private bool _running;

    public WowForeverProfile(string? snapshotPath = null, string? backupSnapshotPath = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerMonitor");
        SnapshotPath = snapshotPath ?? Path.Combine(root, "wow-forever-grid.wtf");
        BackupSnapshotPath = backupSnapshotPath ?? Path.Combine(root, "wow-forever-backup.wtf");
    }

    public string Id => "wow-forever";
    public string Name => "WoW: Forever";
    public string SnapshotPath { get; }
    public string BackupSnapshotPath { get; }

    public string? Apply(bool onBackup, AppSettings settings)
    {
        var path = settings.WowForeverConfigPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "WoW Config.wtf path is missing.";

        if (onBackup)
        {
            WowConfigPatcher.SnapshotIfQuality(path, SnapshotPath, BackupSnapshotPath);
            ApplyBackup(path);
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
            ApplyBackup(settings.WowForeverConfigPath);
            return "WoW exited on battery — low-power graphics kept for the next launch.";
        }

        _running = running;
        return null;
    }

    private void ApplyBackup(string configPath)
    {
        if (File.Exists(BackupSnapshotPath))
            WowConfigPatcher.RestoreSnapshot(configPath, BackupSnapshotPath);
        else
            WowConfigPatcher.ApplyCvars(configPath, WowConfigPatcher.BackupCvars);
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
