using System.Net;
using System.Text;
using PowerMonitor.Core.Deye;

namespace PowerMonitor.Tests;

public class LoggerTrustTests
{
    private static readonly byte[] Reply = Encoding.ASCII.GetBytes("192.168.1.40,ACCF23ABCDEF,1000000001");

    [Fact]
    public void Discovery_accepts_a_reply_that_advertises_its_own_address()
    {
        Assert.True(LoggerFinder.TryParseDiscovery(Reply, IPAddress.Parse("192.168.1.40"), out var endpoint));

        Assert.Equal("192.168.1.40", endpoint.IpAddress);
        Assert.Equal(1000000001u, endpoint.Serial);
    }

    [Fact]
    public void Discovery_rejects_a_reply_that_advertises_another_host()
    {
        Assert.False(LoggerFinder.TryParseDiscovery(Reply, IPAddress.Parse("192.168.1.66"), out _));
    }

    [Fact]
    public void Soc_jump_waits_for_a_second_reading()
    {
        var guard = new SocJumpGuard();
        var start = DateTimeOffset.UnixEpoch;

        Assert.True(guard.Accept(Reading(80, start)));
        Assert.True(guard.Accept(Reading(79, start.AddSeconds(10))));
        Assert.False(guard.Accept(Reading(5, start.AddSeconds(20))));
        Assert.True(guard.Accept(Reading(5, start.AddSeconds(30))));
    }

    [Fact]
    public void Soc_may_move_further_after_a_long_gap()
    {
        var guard = new SocJumpGuard();
        var start = DateTimeOffset.UnixEpoch;

        Assert.True(guard.Accept(Reading(80, start)));
        Assert.True(guard.Accept(Reading(50, start.AddMinutes(15))));
    }

    private static InverterSnapshot Reading(int soc, DateTimeOffset at) =>
        new(soc, 51.2, 0, 0, 500, true, at);
}
