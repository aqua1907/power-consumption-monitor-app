namespace PowerMonitor.Core.Deye;

public static class RegisterDecoder
{
    public static InverterSnapshot Decode(ReadOnlySpan<ushort> words, bool invertPowerSign, DateTimeOffset readAt)
    {
        if (words.Length != DeyeRegisters.BlockCount)
            throw new InvalidOperationException("The logger returned an unexpected number of registers.");

        var soc = words[DeyeRegisters.Index(DeyeRegisters.BatterySoc)];
        if (soc > 100)
            throw new InvalidOperationException("Register values do not look like this inverter. Try the other address offset.");

        var sign = invertPowerSign ? -1 : 1;
        return new InverterSnapshot(
            soc,
            words[DeyeRegisters.Index(DeyeRegisters.BatteryVoltage)] * 0.01,
            sign * ToSigned(words[DeyeRegisters.Index(DeyeRegisters.BatteryPower)]),
            sign * ToSigned(words[DeyeRegisters.Index(DeyeRegisters.GridPower)]),
            sign * ToSigned(words[DeyeRegisters.Index(DeyeRegisters.LoadPower)]),
            words[DeyeRegisters.Index(DeyeRegisters.GridConnected)] != 0,
            readAt);
    }

    private static int ToSigned(ushort raw) => (short)raw;
}
