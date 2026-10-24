using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace PowerMonitor.Core.Deye;

public sealed record LoggerEndpoint(string IpAddress, int Port, uint? Serial, string Source);

public static class LoggerFinder
{
    private const int SolarmanDiscoveryPort = 48899;
    private const int MaxBroadcastReplies = 256;
    private static readonly byte[] DiscoveryRequest = Encoding.ASCII.GetBytes("WIFIKIT-214028-READ");

    public static async Task<IReadOnlyList<LoggerEndpoint>> FindAsync(CancellationToken cancellationToken)
    {
        var broadcastTask = DiscoverByBroadcastAsync(cancellationToken);
        var portsTask = ScanOpenPortsAsync(cancellationToken);
        await Task.WhenAll(broadcastTask, portsTask);

        var broadcast = await broadcastTask;
        var openPorts = await portsTask;
        var serialByIp = broadcast
            .Where(found => found.Serial is not null)
            .GroupBy(found => found.IpAddress)
            .ToDictionary(group => group.Key, group => group.First().Serial);

        var results = new List<LoggerEndpoint>();
        var seenPorts = new HashSet<(string Ip, int Port)>();
        foreach (var hit in openPorts.OrderBy(hit => AddressSortKey(hit.Ip)).ThenBy(hit => hit.Port))
        {
            if (!seenPorts.Add((hit.Ip, hit.Port)))
                continue;

            serialByIp.TryGetValue(hit.Ip, out var serial);
            results.Add(new LoggerEndpoint(hit.Ip, hit.Port, serial, $"TCP {hit.Port}"));
        }

        var seenBroadcast = new HashSet<string>(StringComparer.Ordinal);
        foreach (var found in broadcast.OrderBy(found => AddressSortKey(found.IpAddress)))
        {
            if (openPorts.Any(hit => hit.Ip == found.IpAddress) || !seenBroadcast.Add(found.IpAddress))
                continue;

            results.Add(found with { Port = 8899, Source = "Solarman broadcast" });
        }

        return results;
    }

    private static Task<IReadOnlyList<LoggerEndpoint>> DiscoverByBroadcastAsync(CancellationToken cancellationToken) =>
        Task.Run(() => DiscoverByBroadcast(cancellationToken), cancellationToken);

    private static IReadOnlyList<LoggerEndpoint> DiscoverByBroadcast(CancellationToken cancellationToken)
    {
        var found = new List<LoggerEndpoint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.EnableBroadcast = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            socket.ReceiveTimeout = 1500;

            SendDiscovery(socket, IPAddress.Broadcast);
            foreach (var address in LocalAddresses())
            {
                var broadcast = BroadcastAddress(address);
                if (broadcast is not null && !broadcast.Equals(IPAddress.Broadcast))
                    SendDiscovery(socket, broadcast);
            }

            var buffer = new byte[512];
            var deadline = DateTime.UtcNow.AddMilliseconds(1500);
            while (DateTime.UtcNow < deadline && found.Count < MaxBroadcastReplies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!socket.Poll(200_000, SelectMode.SelectRead))
                    continue;

                EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                int length;
                try
                {
                    length = socket.ReceiveFrom(buffer, ref remote);
                }
                catch (SocketException)
                {
                    break;
                }

                var sender = ((IPEndPoint)remote).Address;
                if (TryParseDiscovery(buffer.AsSpan(0, length), sender, out var endpoint) &&
                    seen.Add(endpoint.IpAddress))
                    found.Add(endpoint);
            }
        }
        catch (SocketException)
        {
            return found;
        }

        return found;
    }

    private static async Task<IReadOnlyList<(string Ip, int Port)>> ScanOpenPortsAsync(CancellationToken cancellationToken)
    {
        var hosts = LocalAddresses()
            .SelectMany(HostsOn)
            .Distinct()
            .ToArray();

        var hits = new System.Collections.Concurrent.ConcurrentBag<(string Ip, int Port)>();
        await Parallel.ForEachAsync(
            hosts,
            new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancellationToken },
            async (host, token) =>
            {
                var port502 = PortOpenAsync(host, 502, token);
                var port8899 = PortOpenAsync(host, 8899, token);
                await Task.WhenAll(port502, port8899);
                var ip = host.ToString();
                if (await port502)
                    hits.Add((ip, 502));
                if (await port8899)
                    hits.Add((ip, 8899));
            });

        return hits.ToArray();
    }

    private static async Task<bool> PortOpenAsync(IPAddress ip, int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(300));
        try
        {
            await client.ConnectAsync(ip, port, timeout.Token);
            return client.Connected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static void SendDiscovery(Socket socket, IPAddress address)
    {
        try
        {
            socket.SendTo(DiscoveryRequest, new IPEndPoint(address, SolarmanDiscoveryPort));
        }
        catch (SocketException)
        {
        }
    }

    public static bool TryParseDiscovery(ReadOnlySpan<byte> data, IPAddress sender, out LoggerEndpoint endpoint)
    {
        endpoint = new LoggerEndpoint("", 0, null, "Solarman broadcast");
        var text = Encoding.ASCII.GetString(data).Trim('\0', ' ', '\r', '\n');
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return false;
        // A reply may only advertise its own address, so one LAN host cannot plant fake loggers.
        if (!ip.Equals(sender.IsIPv4MappedToIPv6 ? sender.MapToIPv4() : sender))
            return false;

        uint? serial = null;
        foreach (var part in parts.Skip(1))
        {
            if (ulong.TryParse(part, out var value) && value is > 0 and <= uint.MaxValue && part.Length >= 6)
            {
                serial = (uint)value;
                break;
            }
        }

        endpoint = new LoggerEndpoint(ip.ToString(), 8899, serial, "Solarman broadcast");
        return true;
    }

    private static IEnumerable<UnicastIPAddressInformation> LocalAddresses()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(address.Address))
                    continue;
                var bytes = address.Address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                    continue;
                yield return address;
            }
        }
    }

    private static IPAddress? BroadcastAddress(UnicastIPAddressInformation address)
    {
        if (address.IPv4Mask is null)
            return null;

        var ip = address.Address.GetAddressBytes();
        var mask = address.IPv4Mask.GetAddressBytes();
        if (ip.Length != 4 || mask.Length != 4)
            return null;

        var broadcast = new byte[4];
        for (var index = 0; index < 4; index++)
            broadcast[index] = (byte)(ip[index] | ~mask[index]);
        return new IPAddress(broadcast);
    }

    private static IEnumerable<IPAddress> HostsOn(UnicastIPAddressInformation address)
    {
        if (address.IPv4Mask is null)
            yield break;

        var ipBytes = address.Address.GetAddressBytes();
        var maskBytes = address.IPv4Mask.GetAddressBytes();
        if (ipBytes.Length != 4 || maskBytes.Length != 4)
            yield break;

        var network = new byte[4];
        var broadcast = new byte[4];
        for (var index = 0; index < 4; index++)
        {
            network[index] = (byte)(ipBytes[index] & maskBytes[index]);
            broadcast[index] = (byte)(ipBytes[index] | ~maskBytes[index]);
        }

        var start = ToUInt(network);
        var end = ToUInt(broadcast);
        if (end <= start + 1)
            yield break;

        if (end - start - 1 > 254)
        {
            start = ToUInt(new byte[] { ipBytes[0], ipBytes[1], ipBytes[2], 0 });
            end = start + 255;
        }

        var own = ToUInt(ipBytes);
        for (var value = start + 1; value < end; value++)
        {
            if (value == own)
                continue;
            yield return new IPAddress(FromUInt(value));
        }
    }

    private static uint AddressSortKey(string ip) =>
        IPAddress.TryParse(ip, out var address) ? ToUInt(address.GetAddressBytes()) : 0;

    private static uint ToUInt(byte[] bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

    private static byte[] FromUInt(uint value) =>
        new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
}
