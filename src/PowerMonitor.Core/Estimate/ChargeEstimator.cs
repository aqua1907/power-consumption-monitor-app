using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Core.Estimate;

public enum ChargeKind
{
    Duration,
    Full
}

/// <summary>Time until the pack reaches 100% at the current charging power.</summary>
public sealed record ChargeEstimate(ChargeKind Kind, TimeSpan Duration, decimal RemainingWh, int ChargeWatts)
{
    public string DurationText => Kind == ChargeKind.Full
        ? "Full"
        : $"~{BackupEstimate.Format(Duration)}";

    public string Hint => Kind == ChargeKind.Full
        ? "The battery is full."
        : $"{RemainingWh / 1000m:0.0} kWh left to 100% at the current {ChargeWatts} W. " +
          "Charging slows near full, so the real time is usually longer.";
}

public static class ChargeEstimator
{
    /// <summary>Returns null unless the battery is charging.</summary>
    public static ChargeEstimate? Estimate(InverterSnapshot snapshot, AppSettings settings)
    {
        if (snapshot.BatteryFlow != BatteryFlow.Charging)
            return null;

        var power = Math.Abs(snapshot.BatteryPowerWatts);
        var missingPercent = Math.Max(0, 100 - snapshot.SocPercent);
        var remainingWh = missingPercent / 100m * settings.PackCount * AppSettings.PackCapacityKwh * 1000m;
        if (missingPercent == 0)
            return new ChargeEstimate(ChargeKind.Full, TimeSpan.Zero, 0, power);

        var seconds = remainingWh / power * 3600m;
        return new ChargeEstimate(
            ChargeKind.Duration,
            TimeSpan.FromTicks((long)(seconds * TimeSpan.TicksPerSecond)),
            remainingWh,
            power);
    }
}
