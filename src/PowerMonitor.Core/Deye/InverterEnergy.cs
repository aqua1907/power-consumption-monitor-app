namespace PowerMonitor.Core.Deye;

/// <summary>
/// Energy counters stored on the inverter. Today's figures reset at the inverter's midnight
/// and keep counting while this PC is off.
/// </summary>
public sealed record InverterEnergy(
    decimal TodayBatteryChargeKwh,
    decimal TodayBatteryDischargeKwh,
    decimal TotalBatteryChargeKwh,
    decimal TotalBatteryDischargeKwh,
    decimal TodayLoadKwh,
    decimal TotalLoadKwh,
    decimal TodayGridImportKwh,
    decimal TodayGridExportKwh);
