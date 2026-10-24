using System.Buffers.Binary;

namespace PowerMonitor.Core.Deye;

public enum SolarmanFrameKind
{
    ModbusResponse,
    NeedsReply,
    Invalid
}

public readonly record struct SolarmanParse(
    SolarmanFrameKind Kind,
    byte[]? Modbus,
    byte Control,
    byte SequenceLow,
    byte SequenceHigh,
    string? Error)
{
    public static SolarmanParse FromModbus(byte[] modbus) =>
        new(SolarmanFrameKind.ModbusResponse, modbus, 0x15, 0, 0, null);

    public static SolarmanParse Reply(byte control, byte sequenceLow, byte sequenceHigh) =>
        new(SolarmanFrameKind.NeedsReply, null, control, sequenceLow, sequenceHigh, null);

    public static SolarmanParse Invalid(string error) =>
        new(SolarmanFrameKind.Invalid, null, 0, 0, 0, error);
}

public static class SolarmanFrame
{
    public static byte[] EncodeRequest(uint serial, byte sequence, ReadOnlySpan<byte> modbus)
    {
        var length = 15 + modbus.Length;
        var frame = new byte[13 + length];
        frame[0] = 0xA5;
        frame[1] = (byte)length;
        frame[2] = (byte)(length >> 8);
        frame[3] = 0x10;
        frame[4] = 0x45;
        frame[5] = sequence;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(7), serial);
        frame[11] = 0x02;
        modbus.CopyTo(frame.AsSpan(26));
        frame[^2] = Checksum(frame.AsSpan(1, frame.Length - 3));
        frame[^1] = 0x15;
        return frame;
    }

    public static byte[] BuildTimeReply(byte requestControl, byte sequenceLow, byte sequenceHigh, uint serial)
    {
        const int length = 10;
        var frame = new byte[13 + length];
        frame[0] = 0xA5;
        frame[1] = length;
        frame[3] = 0x10;
        frame[4] = (byte)(requestControl - 0x30);
        frame[5] = (byte)((sequenceLow + 1) & 0xFF);
        frame[6] = sequenceHigh;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(7), serial);
        frame[11] = 0x01;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(13), (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        frame[^2] = Checksum(frame.AsSpan(1, frame.Length - 3));
        frame[^1] = 0x15;
        return frame;
    }

    public static SolarmanParse Parse(ReadOnlySpan<byte> frame, uint serial, byte expectedSequence)
    {
        if (frame.Length < 13 || frame[0] != 0xA5 || frame[^1] != 0x15)
            return SolarmanParse.Invalid("The logger returned an unreadable frame.");
        if (Checksum(frame[1..^2]) != frame[^2])
            return SolarmanParse.Invalid("The logger returned a damaged frame.");

        var control = frame[4];
        var sequenceLow = frame[5];
        var sequenceHigh = frame[6];
        var frameSerial = BinaryPrimitives.ReadUInt32LittleEndian(frame.Slice(7, 4));
        if (frameSerial != serial)
            return SolarmanParse.Invalid("The logger serial number does not match.");

        if (control == 0x15)
        {
            if (sequenceLow != expectedSequence)
                return SolarmanParse.Invalid("The logger response did not match the request.");
            if (frame.Length < 27 || frame[11] != 0x02)
                return SolarmanParse.Invalid("The logger returned an unexpected response.");

            // Responses start the Modbus RTU payload one byte earlier than requests.
            var modbus = frame.Slice(25, frame.Length - 27).ToArray();
            return SolarmanParse.FromModbus(modbus);
        }

        if (control is 0x41 or 0x42 or 0x43 or 0x47 or 0x48)
            return SolarmanParse.Reply(control, sequenceLow, sequenceHigh);

        return SolarmanParse.Invalid("The logger returned an unexpected response.");
    }

    public static byte Checksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
            sum = (sum + value) & 0xFF;
        return (byte)sum;
    }

    public static bool TryTakeFrame(List<byte> buffer, out byte[] frame)
    {
        frame = Array.Empty<byte>();
        while (buffer.Count > 0 && buffer[0] != 0xA5)
            buffer.RemoveAt(0);

        if (buffer.Count < 3)
            return false;

        var length = buffer[1] | (buffer[2] << 8);
        if (length is < 1 or > 400)
        {
            buffer.RemoveAt(0);
            return TryTakeFrame(buffer, out frame);
        }

        var total = 13 + length;
        if (buffer.Count < total)
            return false;

        frame = buffer.GetRange(0, total).ToArray();
        buffer.RemoveRange(0, total);
        return true;
    }
}
