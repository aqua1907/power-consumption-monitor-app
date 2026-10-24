using System.Net.Sockets;

namespace PowerMonitor.Core.Deye;

public sealed class SolarmanV5RegisterReader : IRegisterReader
{
    private readonly string _host;
    private readonly int _port;
    private readonly uint _serial;

    public SolarmanV5RegisterReader(string host, int port, uint serial)
    {
        _host = host;
        _port = port;
        _serial = serial;
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        byte unitId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken)
    {
        var sequence = (byte)Random.Shared.Next(1, 256);
        var request = SolarmanFrame.EncodeRequest(
            _serial,
            sequence,
            ModbusRtu.BuildReadHolding(unitId, startAddress, count));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var token = timeout.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, token);
            client.NoDelay = true;
            var stream = client.GetStream();
            await stream.WriteAsync(request, token);

            var buffer = new List<byte>();
            var scratch = new byte[1024];
            while (true)
            {
                var read = await stream.ReadAsync(scratch, token);
                if (read == 0)
                    throw new IOException("The logger closed the connection.");

                buffer.AddRange(scratch.AsSpan(0, read).ToArray());
                while (SolarmanFrame.TryTakeFrame(buffer, out var frame))
                {
                    var parsed = SolarmanFrame.Parse(frame, _serial, sequence);
                    if (parsed.Kind == SolarmanFrameKind.ModbusResponse)
                    {
                        if (parsed.Modbus is not byte[] modbus)
                            throw new InvalidOperationException("The logger returned an unexpected response.");
                        return ModbusRtu.ParseReadHoldingResponse(modbus, unitId, count);
                    }
                    if (parsed.Kind == SolarmanFrameKind.NeedsReply)
                    {
                        var reply = SolarmanFrame.BuildTimeReply(parsed.Control, parsed.SequenceLow, parsed.SequenceHigh, _serial);
                        await stream.WriteAsync(reply, token);
                        continue;
                    }

                    throw new InvalidOperationException(parsed.Error ?? "The logger returned an unexpected response.");
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The logger did not answer.");
        }
        catch (SocketException)
        {
            throw new IOException($"Cannot reach the logger at {_host}:{_port}.");
        }
    }
}
