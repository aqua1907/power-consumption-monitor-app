using System.Buffers.Binary;
using PowerMonitor.Core.Deye;

namespace PowerMonitor.Tests;

public class ProtocolFrameTests
{
    [Fact]
    public void Modbus_tcp_read_request_matches_the_hybrid_block()
    {
        var frame = ModbusTcp.BuildReadHoldingRequest(1, 1, DeyeRegisters.BlockStart, DeyeRegisters.BlockCount);

        Assert.Equal("000100000006010300A9001A", Convert.ToHexString(frame));
    }

    [Fact]
    public void Modbus_tcp_parses_a_holding_register_body()
    {
        var values = ModbusTcp.ParseBody(new byte[] { 1, 3, 2, 0x00, 0x48 }, unitId: 1, expectedCount: 1);

        Assert.Equal(new ushort[] { 0x0048 }, values);
    }

    [Fact]
    public void Modbus_tcp_explains_an_illegal_address()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ModbusTcp.ParseBody(new byte[] { 1, 0x83, 0x02 }, unitId: 1, expectedCount: 1));

        Assert.Contains("address offset", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Solarman_request_matches_a_known_v5_frame()
    {
        var modbus = ModbusRtu.BuildReadHolding(1, 3, 5);
        var frame = SolarmanFrame.EncodeRequest(1782345394, 0xBB, modbus);

        Assert.Equal(
            "A517001045BB00B26E3C6A02" +
            "0000000000000000000000000000" +
            "01030003000575C93915",
            Convert.ToHexString(frame));
    }

    [Fact]
    public void Solarman_response_rtu_starts_at_byte_25()
    {
        var rtu = ReadResponse(new ushort[] { 0x0048 });
        var frame = WrapResponse(rtu, serial: 1782345394, sequence: 0xBB);

        var parsed = SolarmanFrame.Parse(frame, 1782345394, 0xBB);

        Assert.Equal(SolarmanFrameKind.ModbusResponse, parsed.Kind);
        var values = ModbusRtu.ParseReadHoldingResponse(parsed.Modbus, 1, 1);
        Assert.Equal(new ushort[] { 0x0048 }, values);
    }

    [Fact]
    public void Solarman_reader_accepts_the_double_crc_some_sticks_append()
    {
        var rtu = ReadResponse(new ushort[] { 72 });
        var doubled = new byte[rtu.Length + 2];
        rtu.CopyTo(doubled, 0);

        var values = ModbusRtu.ParseReadHoldingResponse(doubled, 1, 1);

        Assert.Equal(new ushort[] { 72 }, values);
    }

    [Fact]
    public void Heartbeat_frames_ask_for_a_time_reply()
    {
        var heartbeat = WrapControl(0x47, serial: 99, sequence: 7);
        var parsed = SolarmanFrame.Parse(heartbeat, 99, expectedSequence: 1);

        Assert.Equal(SolarmanFrameKind.NeedsReply, parsed.Kind);
        Assert.Equal(0x47, parsed.Control);
        var reply = SolarmanFrame.BuildTimeReply(parsed.Control, parsed.SequenceLow, parsed.SequenceHigh, 99);
        Assert.Equal(0xA5, reply[0]);
        Assert.Equal(0x15, reply[^1]);
        Assert.Equal(0x17, reply[4]);
        Assert.Equal(8, reply[5]);
    }

    private static byte[] ReadResponse(ushort[] registers)
    {
        var frame = new byte[3 + (registers.Length * 2) + 2];
        frame[0] = 1;
        frame[1] = 0x03;
        frame[2] = (byte)(registers.Length * 2);
        for (var index = 0; index < registers.Length; index++)
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(3 + (index * 2)), registers[index]);
        var crc = ModbusCrc.Compute(frame.AsSpan(0, frame.Length - 2));
        frame[^2] = (byte)crc;
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    private static byte[] WrapResponse(byte[] rtu, uint serial, byte sequence)
    {
        var length = 14 + rtu.Length;
        var frame = new byte[13 + length];
        frame[0] = 0xA5;
        frame[1] = (byte)length;
        frame[2] = (byte)(length >> 8);
        frame[3] = 0x10;
        frame[4] = 0x15;
        frame[5] = sequence;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(7), serial);
        frame[11] = 0x02;
        rtu.CopyTo(frame.AsSpan(25));
        frame[^2] = SolarmanFrame.Checksum(frame.AsSpan(1, frame.Length - 3));
        frame[^1] = 0x15;
        return frame;
    }

    private static byte[] WrapControl(byte control, uint serial, byte sequence)
    {
        const int length = 10;
        var frame = new byte[13 + length];
        frame[0] = 0xA5;
        frame[1] = length;
        frame[3] = 0x10;
        frame[4] = control;
        frame[5] = sequence;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(7), serial);
        frame[^2] = SolarmanFrame.Checksum(frame.AsSpan(1, frame.Length - 3));
        frame[^1] = 0x15;
        return frame;
    }
}
