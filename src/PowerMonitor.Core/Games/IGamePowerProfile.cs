using PowerMonitor.Core.Settings;

namespace PowerMonitor.Core.Games;

/// <summary>
/// On-disk graphics preset for one game. Display refresh and the GPU power limit stay shared
/// in BackupPlayController; a game profile only rewrites that game's files.
/// </summary>
public interface IGamePowerProfile
{
    string Id { get; }
    string Name { get; }

    /// <summary>Write the backup or grid preset. Null when this game has nothing to do.</summary>
    string? Apply(bool onBackup, AppSettings settings);

    /// <summary>
    /// Called on every inverter poll. Override when a running game rewrites its config on exit.
    /// </summary>
    string? Observe(bool gridConnected, AppSettings settings) => null;
}
