namespace PowerMonitor.Core.Deye;

public static class RegisterDecoder
{
    public const double MaxBatteryVolts = 70;
    public const int MaxPowerWatts = 20_000;

    public static InverterSnapshot Decode(ReadOnlySpan<ushort> words, bool invertPowerSign, DateTimeOffset readAt)
    {
        if (words.Length != DeyeRegisters.BlockCount)
            throw new InvalidOperationException("The logger returned an unexpected number of registers.");

        var soc = words[DeyeRegisters.Index(DeyeRegisters.BatterySoc)];
        var volts = words[DeyeRegisters.Index(DeyeRegisters.BatteryVoltage)] * 0.01;
        var gridFlag = words[DeyeRegisters.Index(DeyeRegisters.GridConnected)];
        var batteryPower = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.BatteryPower)]);
        var gridPower = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.GridPower)]);
        var loadPower = ToSigned(words[DeyeRegisters.Index(DeyeRegisters.LoadPower)]);
        if (soc > 100 || volts > MaxBatteryVolts || gridFlag > 1 ||
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
            readAt);
    }

    private static int ToSigned(ushort raw) => (short)raw;
}
