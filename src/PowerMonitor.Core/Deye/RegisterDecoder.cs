namespace PowerMonitor.Core.Deye;

public static class RegisterDecoder
{
    public const double MaxBatteryVolts = 70;
    public const double MaxBatteryAmps = 200;
    public const double MaxGridVolts = 300;
    public const double MaxGridAmps = 100;
    public const decimal MaxTodayKwh = 200;
    public const decimal MaxTotalKwh = 1_000_000;
    public const int MaxPowerWatts = 20_000;

    public static InverterSnapshot Decode(ReadOnlySpan<ushort> words, bool invertPowerSign, DateTimeOffset readAt)
    {
        if (words.Length != DeyeRegisters.BlockCount)
            throw new InvalidOperationException("The logger returned an unexpected number of registers.");

        var soc = words[DeyeRegisters.Index(DeyeRegisters.BatterySoc)];
        var volts = words[DeyeRegisters.Index(DeyeRegisters.BatteryVoltage)] * 0.01;
        var amps = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.BatteryCurrent)]) * 0.01;
        var gridVolts = words[DeyeRegisters.Index(DeyeRegisters.GridVoltage)] * 0.1;
        var gridAmps = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.GridCurrent)]) * 0.01;
        var gridFlag = words[DeyeRegisters.Index(DeyeRegisters.GridConnected)];
        var batteryPower = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.BatteryPower)]);
        var gridPower = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.GridPower)]);
        var loadPower = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.LoadPower)]);
        if (soc > 100 || volts > MaxBatteryVolts || gridFlag > 1 ||
            Math.Abs(amps) > MaxBatteryAmps ||
            gridVolts > MaxGridVolts || Math.Abs(gridAmps) > MaxGridAmps ||
            Math.Abs(batteryPower) > MaxPowerWatts ||
            Math.Abs(gridPower) > MaxPowerWatts ||
            Math.Abs(loadPower) > MaxPowerWatts)
            throw new InvalidOperationException("Register values do not look like this inverter. Try the other address offset.");

        var sign = invertPowerSign ? -1 : 1;
        return new InverterSnapshot(
            soc,
            volts,
            sign * batteryPower,
            sign * gridPower,
            sign * loadPower,
            gridFlag == 1,
            readAt,
            sign * amps,
            gridVolts,
            sign * gridAmps);
    }

    /// <summary>
    /// Reads the inverter's own energy counters. Returns null when the block does not look
    /// like this inverter, so a bad counter read does not discard the live snapshot.
    /// </summary>
    public static InverterEnergy? TryDecodeEnergy(ReadOnlySpan<ushort> words)
    {
        if (words.Length != DeyeRegisters.EnergyBlockCount)
            return null;

        var todayCharge = Kwh16(words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayBatteryCharge)]);
        var todayDischarge = Kwh16(words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayBatteryDischarge)]);
        var totalCharge = Kwh32(words, DeyeRegisters.TotalBatteryCharge);
        var totalDischarge = Kwh32(words, DeyeRegisters.TotalBatteryDischarge);
        var todayLoad = Kwh16(words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayLoad)]);
        var totalLoad = Kwh32(words, DeyeRegisters.TotalLoad);
        var todayImport = Kwh16(words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayGridImport)]);
        var todayExport = Kwh16(words[DeyeRegisters.EnergyIndex(DeyeRegisters.TodayGridExport)]);

        if (!Plausible(todayCharge, MaxTodayKwh) || !Plausible(todayDischarge, MaxTodayKwh) ||
            !Plausible(todayLoad, MaxTodayKwh) || !Plausible(todayImport, MaxTodayKwh) ||
            !Plausible(todayExport, MaxTodayKwh) ||
            !Plausible(totalCharge, MaxTotalKwh) || !Plausible(totalDischarge, MaxTotalKwh) ||
            !Plausible(totalLoad, MaxTotalKwh))
            return null;

        return new InverterEnergy(
            todayCharge, todayDischarge, totalCharge, totalDischarge,
            todayLoad, totalLoad, todayImport, todayExport);
    }

    private static decimal Kwh16(ushort raw) => raw * 0.1m;

    private static decimal Kwh32(ReadOnlySpan<ushort> words, ushort address)
    {
        var low = words[DeyeRegisters.EnergyIndex(address)];
        var high = words[DeyeRegisters.EnergyIndex(address) + 1];
        return ((uint)low | ((uint)high << 16)) * 0.1m;
    }

    private static bool Plausible(decimal kwh, decimal max) => kwh >= 0 && kwh <= max;

    private static int ToSigned(ushort raw) => (short)raw;
}
