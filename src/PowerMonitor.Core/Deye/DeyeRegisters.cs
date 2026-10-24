namespace PowerMonitor.Core.Deye;

/// <summary>
/// Read-only holding registers for a single-phase Deye hybrid such as the SUN-6K-SG05LP1.
/// </summary>
public static class DeyeRegisters
{
    public const ushort GridPower = 0x00A9;
    public const ushort LoadPower = 0x00B2;
    public const ushort BatteryVoltage = 0x00B7;
    public const ushort BatterySoc = 0x00B8;
    public const ushort BatteryPower = 0x00BE;
    public const ushort GridConnected = 0x00C2;

    public const ushort BlockStart = GridPower;
    public const ushort BlockCount = GridConnected - GridPower + 1;

    public static int Index(ushort address) => address - BlockStart;
}
