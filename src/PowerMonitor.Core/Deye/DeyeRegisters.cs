namespace PowerMonitor.Core.Deye;

/// <summary>
/// Read-only holding registers for a single-phase Deye hybrid such as the SUN-6K-SG05LP1.
/// </summary>
public static class DeyeRegisters
{
    public const ushort TodayBatteryCharge = 0x0046;
    public const ushort TodayBatteryDischarge = 0x0047;
    public const ushort TotalBatteryCharge = 0x0048;
    public const ushort TotalBatteryDischarge = 0x004A;
    public const ushort TodayGridImport = 0x004C;
    public const ushort TodayGridExport = 0x004D;
    public const ushort TodayLoad = 0x0054;
    public const ushort TotalLoad = 0x0055;

    public const ushort EnergyBlockStart = TodayBatteryCharge;
    public const ushort EnergyBlockCount = TotalLoad + 2 - TodayBatteryCharge;

    public const ushort GridVoltage = 0x0096;
    public const ushort GridCurrent = 0x00A0;
    public const ushort GridPower = 0x00A9;
    public const ushort LoadPower = 0x00B2;
    public const ushort BatteryVoltage = 0x00B7;
    public const ushort BatterySoc = 0x00B8;
    public const ushort BatteryPower = 0x00BE;
    public const ushort BatteryCurrent = 0x00BF;
    public const ushort GridConnected = 0x00C2;

    public const ushort BlockStart = GridVoltage;
    public const ushort BlockCount = GridConnected - GridVoltage + 1;

    public static int Index(ushort address) => address - BlockStart;

    public static int EnergyIndex(ushort address) => address - EnergyBlockStart;
}
