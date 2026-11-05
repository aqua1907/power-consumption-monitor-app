using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;

namespace PowerMonitor.Core.Wow;

/// <summary>
/// Fixed bit packet the Power Status addon decodes from key chords.
/// Layout: hasData, low, grid, soc:7, load:14, flow:2, flowAbs:14, pc:12, backupIdle, backupMin:12, checksum:6.
/// flow is 0 idle, 1 charging, 2 discharging. backupMin 4095 means unknown. Checksum is the sum of the earlier bits mod 64.
/// </summary>
public static class WowStatusBits
{
    public const int Length = 71;

    public static byte[] Pack(
        bool hasData,
        int soc,
        bool low,
        bool grid,
        int loadWatts,
        BatteryFlow flow,
        int batteryWatts,
        int pcWatts,
        BackupEstimate? backup)
    {
        var bits = new List<byte>(Length);
        Add(bits, hasData ? 1 : 0, 1);
        Add(bits, low ? 1 : 0, 1);
        Add(bits, grid ? 1 : 0, 1);
        Add(bits, Math.Clamp(soc, 0, 100), 7);
        Add(bits, Math.Clamp(Math.Abs(loadWatts), 0, 16383), 14);
        Add(bits, flow switch
        {
            BatteryFlow.Charging => 1,
            BatteryFlow.Discharging => 2,
            _ => 0
        }, 2);
        Add(bits, Math.Clamp(Math.Abs(batteryWatts), 0, 16383), 14);
        Add(bits, Math.Clamp(pcWatts, 0, 4095), 12);

        var idle = hasData && backup?.Kind == BackupKind.Idle;
        var minutes = 4095;
        if (hasData && backup?.Duration is TimeSpan duration && !idle)
            minutes = Math.Clamp((int)duration.TotalMinutes, 0, 4094);
        Add(bits, idle ? 1 : 0, 1);
        Add(bits, minutes, 12);

        var sum = 0;
        for (var i = 0; i < bits.Count; i++)
            sum += bits[i];
        Add(bits, sum % 64, 6);
        return bits.ToArray();
    }

    private static void Add(List<byte> bits, int value, int width)
    {
        for (var shift = width - 1; shift >= 0; shift--)
            bits.Add((byte)((value >> shift) & 1));
    }
}
