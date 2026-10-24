using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;

namespace PowerMonitor.Core.Presentation;

public sealed record DashboardState(
    bool HasData,
    int SocPercent,
    BatteryFlow Flow,
    bool GridConnected,
    int GridPowerWatts,
    int BatteryPowerWatts,
    double BatteryVoltageVolts,
    int LoadPowerWatts,
    string Summary,
    string FlowSummary,
    string Runtime,
    string RuntimeHint,
    string UsableEnergy,
    string Updated,
    string? Error)
{
    public static DashboardState Waiting(string? error = null) => new(
        false, 0, BatteryFlow.Idle, false, 0, 0, 0, 0,
        error is null ? "Connecting…" : "No connection",
        "Waiting for the first reading",
        "—", "Waiting for live energy data", "", "", error);
}

public static class DashboardPresentation
{
    public static DashboardState Create(
        InverterSnapshot? snapshot,
        BackupEstimate? backup,
        string? error)
    {
        if (snapshot is null)
            return DashboardState.Waiting(error);

        var flow = snapshot.BatteryFlow;
        var flowSummary = flow switch
        {
            BatteryFlow.Charging => $"Charging · {FormatPower(snapshot.BatteryPowerWatts)}",
            BatteryFlow.Discharging => $"Discharging · {FormatPower(snapshot.BatteryPowerWatts)}",
            _ => "Idle"
        };

        return new DashboardState(
            true,
            Math.Clamp(snapshot.SocPercent, 0, 100),
            flow,
            snapshot.GridConnected,
            snapshot.GridPowerWatts,
            snapshot.BatteryPowerWatts,
            snapshot.BatteryVoltageVolts,
            snapshot.LoadPowerWatts,
            snapshot.GridConnected ? "Grid on" : "Grid off · on battery",
            flowSummary,
            backup?.DurationText ?? "—",
            backup?.Hint ?? "Runtime estimate is not available yet",
            backup is null ? "" : $"{backup.RemainingWh / 1000m:0.0} kWh usable",
            snapshot.ReadAt.LocalDateTime.ToString("HH:mm:ss"),
            error);
    }

    public static string FormatPower(double watts)
    {
        var value = Math.Abs(watts);
        return value >= 1000 ? $"{value / 1000:0.00} kW" : $"{value:0} W";
    }
}
