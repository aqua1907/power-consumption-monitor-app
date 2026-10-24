namespace PowerMonitor.Core.Deye;

public static class ModbusCrc
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                var lsb = (crc & 1) != 0;
                crc >>= 1;
                if (lsb)
                    crc ^= 0xA001;
            }
        }

        return crc;
    }
}
