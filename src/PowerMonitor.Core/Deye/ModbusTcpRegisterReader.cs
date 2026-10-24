using System.Net.Sockets;

namespace PowerMonitor.Core.Deye;

public interface IRegisterReader
{
    Task<ushort[]> ReadHoldingRegistersAsync(
        byte unitId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken);
}

public sealed class ModbusTcpRegisterReader : IRegisterReader
{
    private readonly string _host;
    private readonly int _port;

    public ModbusTcpRegisterReader(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        byte unitId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken)
    {
        var transactionId = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        var request = ModbusTcp.BuildReadHoldingRequest(transactionId, unitId, startAddress, count);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var token = timeout.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, token);
            var stream = client.GetStream();
            await stream.WriteAsync(request, token);

            var prefix = new byte[6];
            await ReadExactAsync(stream, prefix, token);
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(prefix) != transactionId)
                throw new InvalidOperationException("The logger response did not match the request.");
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(prefix.AsSpan(2)) != 0)
                throw new InvalidOperationException("The logger returned an unexpected response.");

            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(prefix.AsSpan(4));
            if (length is < 2 or > 260)
                throw new InvalidOperationException("The logger returned an unexpected response.");

            var body = new byte[length];
            await ReadExactAsync(stream, body, token);
            return ModbusTcp.ParseBody(body, unitId, count);
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

    internal static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0)
                throw new IOException("The logger closed the connection.");
            read += count;
        }
    }
}
