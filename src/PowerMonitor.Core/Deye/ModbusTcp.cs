using System.Buffers.Binary;

namespace PowerMonitor.Core.Deye;

public static class ModbusTcp
{
    public static byte[] BuildReadHoldingRequest(ushort transactionId, byte unitId, ushort startAddress, ushort count)
    {
        var frame = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), 6);
        frame[6] = unitId;
        frame[7] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(10), count);
        return frame;
    }

    public static ushort[] ParseBody(ReadOnlySpan<byte> body, byte unitId, ushort expectedCount)
    {
        if (body.Length < 2)
            throw new InvalidOperationException("The logger returned an unexpected response.");
        if (body[0] != unitId)
            throw new InvalidOperationException("The logger answered a different device address.");
        if ((body[1] & 0x80) != 0)
            throw new InvalidOperationException(ExceptionMessage(body.Length > 2 ? body[2] : (byte)0));
        if (body[1] != 0x03 || body.Length < 3)
            throw new InvalidOperationException("The logger returned an unexpected response.");

        var byteCount = body[2];
        if (body.Length != 3 + byteCount || byteCount != expectedCount * 2)
            throw new InvalidOperationException("The logger returned an unexpected number of registers.");

        var values = new ushort[expectedCount];
        for (var index = 0; index < expectedCount; index++)
            values[index] = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(3 + (index * 2), 2));
        return values;
    }

    public static string ExceptionMessage(byte code) => code switch
    {
        1 => "The logger does not support that request.",
        2 => "The logger rejected the register address. Try the other address offset.",
        3 => "The logger rejected the register count.",
        4 => "The logger failed while reading.",
        _ => "The logger rejected the read."
    };
}
