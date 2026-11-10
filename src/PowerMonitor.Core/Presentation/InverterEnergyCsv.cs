using System.Globalization;
using PowerMonitor.Core.Deye;

namespace PowerMonitor.Core.Presentation;

/// <summary>Exports the energy counters stored on the inverter.</summary>
public static class InverterEnergyCsv
{
    public static string SuggestedFileName(DateTimeOffset? at = null) =>
        $"inverter-history-{(at ?? DateTimeOffset.Now):yyyyMMdd-HHmmss}";

    public static string Write(InverterEnergy? energy, DateTimeOffset? readAt, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine(
            "Time,BatteryChargeTodayKwh,BatteryDischargeTodayKwh,BatteryChargeTotalKwh,BatteryDischargeTotalKwh," +
            "LoadTodayKwh,LoadTotalKwh,GridBoughtTodayKwh,GridSoldTodayKwh");
        if (energy is not null && readAt is not null)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{readAt:O},{energy.TodayBatteryChargeKwh},{energy.TodayBatteryDischargeKwh}," +
                $"{energy.TotalBatteryChargeKwh},{energy.TotalBatteryDischargeKwh}," +
                $"{energy.TodayLoadKwh},{energy.TotalLoadKwh}," +
                $"{energy.TodayGridImportKwh},{energy.TodayGridExportKwh}"));
        }

        return path;
    }
}
