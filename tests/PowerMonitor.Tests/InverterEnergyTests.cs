using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Tests;

public sealed class InverterEnergyTests
{
    [Fact]
    public void Decodes_the_inverter_energy_counters()
    {
        var words = new ushort[DeyeRegisters.EnergyBlockCount];
        words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayBatteryCharge)] = 12;
        words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayBatteryDischarge)] = 8;
        SetTotal(words, DeyeRegisters.TotalBatteryCharge, 4000);
        SetTotal(words, DeyeRegisters.TotalBatteryDischarge, 3900);
        words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayLoad)] = 35;
        SetTotal(words, DeyeRegisters.TotalLoad, 12000);
        words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayGridImport)] = 20;
        words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayGridExport)] = 5;

        var energy = RegisterDecoder.TryDecodeEnergy(words);

        Assert.NotNull(energy);
        Assert.Equal(1.2m, energy.TodayBatteryChargeKwh);
        Assert.Equal(0.8m, energy.TodayBatteryDischargeKwh);
        Assert.Equal(400.0m, energy.TotalBatteryChargeKwh);
        Assert.Equal(390.0m, energy.TotalBatteryDischargeKwh);
        Assert.Equal(3.5m, energy.TodayLoadKwh);
        Assert.Equal(1200.0m, energy.TotalLoadKwh);
        Assert.Equal(2.0m, energy.TodayGridImportKwh);
        Assert.Equal(0.5m, energy.TodayGridExportKwh);
    }

    [Fact]
    public void Totals_use_the_low_word_first()
    {
        var words = new ushort[DeyeRegisters.EnergyBlockCount];
        SetTotal(words, DeyeRegisters.TotalBatteryCharge, 65536);

        var energy = RegisterDecoder.TryDecodeEnergy(words);

        Assert.Equal(6553.6m, energy!.TotalBatteryChargeKwh);
    }

    [Fact]
    public void Rejects_a_today_total_this_inverter_cannot_report()
    {
        var words = new ushort[DeyeRegisters.EnergyBlockCount];
        words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayBatteryCharge)] = 2001;

        Assert.Null(RegisterDecoder.TryDecodeEnergy(words));
    }

    [Fact]
    public void Csv_writes_the_inverter_counters()
    {
        var path = Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"), "energy.csv");
        var readAt = new DateTimeOffset(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);
        var energy = new InverterEnergy(1.2m, 0.8m, 400m, 390m, 3.5m, 1200m, 2m, 0.5m);

        var written = InverterEnergyCsv.Write(energy, readAt, path);
        var lines = File.ReadAllLines(written);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Time,BatteryChargeTodayKwh", lines[0]);
        Assert.Contains("1.2,0.8,400,390,3.5,1200,2,0.5", lines[1]);
    }

    private static void SetTotal(ushort[] words, ushort address, uint raw)
    {
        words[DeyeRegisters.EnergyIndex(address)] = (ushort)raw;
        words[DeyeRegisters.EnergyIndex(address) + 1] = (ushort)(raw >> 16);
    }
}
