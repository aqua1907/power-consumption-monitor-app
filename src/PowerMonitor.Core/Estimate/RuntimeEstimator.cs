using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Core.Estimate;

public static class RuntimeEstimator
{
    public static BackupEstimate Estimate(InverterSnapshot snapshot, AppSettings settings)
    {
        var label = snapshot.GridConnected
            ? "If the grid dropped now"
            : "Backup time remaining";

        var usablePercent = Math.Max(0, snapshot.SocPercent - settings.ReserveSocPercent);
        var remainingWh = usablePercent / 100m * settings.PackCount * AppSettings.PackCapacityKwh * 1000m;
        var load = Math.Abs(snapshot.LoadPowerWatts);

        if (usablePercent == 0)
            return new BackupEstimate(BackupKind.Empty, TimeSpan.Zero, label, remainingWh, load);

        if (load < AppSettings.IdleLoadWatts)
            return new BackupEstimate(BackupKind.Idle, null, label, remainingWh, load);

        var seconds = remainingWh / load * 3600m;
        var duration = TimeSpan.FromTicks((long)(seconds * TimeSpan.TicksPerSecond));
        return new BackupEstimate(BackupKind.Duration, duration, label, remainingWh, load);
    }
}
