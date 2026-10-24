using System.Buffers.Binary;

namespace PowerMonitor.Core.Deye;

public static class ModbusRtu
{
    public static byte[] BuildReadHolding(byte unitId, ushort startAddress, ushort count)
    {
        var frame = new byte[8];
        frame[0] = unitId;
        frame[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), count);
        WriteCrc(frame.AsSpan(0, 6), frame.AsSpan(6));
        return frame;
    }

    public static ushort[] ParseReadHoldingResponse(ReadOnlySpan<byte> frame, byte unitId, ushort expectedCount)
    {
        if (frame.Length >= 6 && frame[^1] == 0 && frame[^2] == 0 && CrcMatches(frame[..^2]))
            frame = frame[..^2];

        if (!CrcMatches(frame))
            throw new InvalidOperationException("The logger returned a damaged reading.");

        if (frame.Length < 5)
            throw new InvalidOperationException("The logger returned a damaged reading.");
        if (frame[0] != unitId)
            throw new InvalidOperationException("The logger answered a different device address.");
        if ((frame[1] & 0x80) != 0)
            throw new InvalidOperationException(ModbusTcp.ExceptionMessage(frame.Length > 2 ? frame[2] : (byte)0));
        if (frame[1] != 0x03)
            throw new InvalidOperationException("The logger returned an unexpected response.");

        var byteCount = frame[2];
        if (frame.Length != 3 + byteCount + 2 || byteCount != expectedCount * 2)
            throw new InvalidOperationException("The logger returned an unexpected number of registers.");

        var values = new ushort[expectedCount];
        for (var index = 0; index < expectedCount; index++)
            values[index] = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(3 + (index * 2), 2));
        return values;
    }

    private static bool CrcMatches(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4)
            return false;

        var actual = ModbusCrc.Compute(frame[..^2]);
        return frame[^2] == (byte)actual && frame[^1] == (byte)(actual >> 8);
    }

    private static void WriteCrc(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        var crc = ModbusCrc.Compute(data);
        destination[0] = (byte)crc;
        destination[1] = (byte)(crc >> 8);
    }
}
