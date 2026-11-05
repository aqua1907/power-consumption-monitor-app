using PowerMonitor.Core.Games;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.BackupMode;

internal sealed class BackupPlayController
{
    private bool? _grid;

    public string? Observe(bool gridConnected, AppSettings settings)
    {
        if (!settings.BackupPlayModeEnabled)
        {
            _grid = null;
            return null;
        }

        string? message = null;
        if (_grid != gridConnected)
        {
            var first = _grid is null;
            _grid = gridConnected;

            // First observation only arms the controller; wait for a real transition.
            if (!first)
                message = gridConnected ? ApplyGrid(settings) : ApplyBackup(settings);
        }

        return message ?? ObserveGames(gridConnected, settings);
    }

    public string ApplyBackup(AppSettings settings)
    {
        var notes = ApplyGames(onBackup: true, settings);

        var hz = DisplayRefresh.TrySetHz(settings.BackupDisplayHz);
        notes.Add(hz is null
            ? $"Display {settings.BackupDisplayHz} Hz failed."
            : $"Display {hz} Hz.");

        if (settings.BackupGpuPowerEnabled)
        {
            var error = GpuPowerLimit.TryApply(settings.BackupGpuPowerWatts);
            notes.Add(error is null
                ? $"GPU {settings.BackupGpuPowerWatts} W."
                : error);
        }

        return "Backup play mode: " + string.Join(" ", notes);
    }

    public string ApplyGrid(AppSettings settings)
    {
        var notes = ApplyGames(onBackup: false, settings);

        var hz = DisplayRefresh.TrySetHz(settings.GridDisplayHz);
        notes.Add(hz is null
            ? $"Display {settings.GridDisplayHz} Hz failed."
            : $"Display {hz} Hz.");

        if (settings.BackupGpuPowerEnabled)
        {
            var error = GpuPowerLimit.TryApply(settings.GridGpuPowerWatts);
            notes.Add(error is null
                ? $"GPU {settings.GridGpuPowerWatts} W."
                : error);
        }

        return "Grid play mode: " + string.Join(" ", notes);
    }

    private static List<string> ApplyGames(bool onBackup, AppSettings settings)
    {
        var notes = new List<string>();
        foreach (var game in GameProfiles.All)
        {
            try
            {
                var note = game.Apply(onBackup, settings);
                if (note is not null)
                    notes.Add(note);
            }
            catch (Exception ex)
            {
                notes.Add($"{game.Name}: {ex.Message}");
            }
        }

        return notes;
    }

    private static string? ObserveGames(bool gridConnected, AppSettings settings)
    {
        var notes = new List<string>();
        foreach (var game in GameProfiles.All)
        {
            try
            {
                var note = game.Observe(gridConnected, settings);
                if (note is not null)
                    notes.Add(note);
            }
            catch (Exception ex)
            {
                notes.Add($"{game.Name}: {ex.Message}");
            }
        }

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }
}
