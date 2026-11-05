using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Core.Presentation;
using PowerMonitor.Core.Settings;
using PowerMonitor.Core.Wow;

namespace PowerMonitor.Tests;

public class WowStatusFeedTests
{
    [Fact]
    public void Render_writes_a_lua_table_the_addon_can_load()
    {
        var snapshot = new InverterSnapshot(72, 52, -400, 200, 640, true, DateTimeOffset.Parse("2026-11-05T12:02:11Z"));
        var settings = new AppSettings { ReserveSocPercent = 10 };
        var backup = RuntimeEstimator.Estimate(snapshot, settings);
        var state = DashboardPresentation.Create(snapshot, backup, error: null);

        var lua = WowStatusFeed.Render(state, pcWatts: 95, lowBattery: false);

        Assert.Contains("PowerStatus_Feed = {", lua);
        Assert.Contains("hasData = true", lua);
        Assert.Contains("soc = 72", lua);
        Assert.Contains("grid = true", lua);
        Assert.Contains("summary = \"Grid on\"", lua);
        Assert.Contains("pc = \"95 W\"", lua);
        Assert.DoesNotContain("error = nil", lua);
    }

    [Fact]
    public void Path_sits_beside_the_forever_client()
    {
        var path = WowStatusFeed.PathFor(@"D:\Games\Battle.net\World of Warcraft\_forever_\WTF\Config.wtf");
        Assert.Equal(
            @"D:\Games\Battle.net\World of Warcraft\_forever_\Interface\AddOns\PowerStatus\status.lua",
            path);
    }

    [Fact]
    public void Packet_round_trips_the_fields_the_addon_reads()
    {
        var snapshot = new InverterSnapshot(75, 52, 436, 0, 407, false, DateTimeOffset.Parse("2026-11-05T12:49:26Z"));
        var backup = RuntimeEstimator.Estimate(snapshot, new AppSettings());
        var bits = WowStatusBits.Pack(true, 75, false, false, 407, BatteryFlow.Discharging, 436, 129, backup);

        Assert.Equal(WowStatusBits.Length, bits.Length);
        var cursor = 0;
        int Take(int width)
        {
            var value = 0;
            for (var i = 0; i < width; i++)
                value = (value << 1) | bits[cursor++];
            return value;
        }

        var sum = bits.Take(65).Sum(bit => bit);
        Assert.Equal(1, Take(1));
        Assert.Equal(0, Take(1));
        Assert.Equal(0, Take(1));
        Assert.Equal(75, Take(7));
        Assert.Equal(407, Take(14));
        Assert.Equal(2, Take(2));
        Assert.Equal(436, Take(14));
        Assert.Equal(129, Take(12));
        Assert.Equal(0, Take(1));
        Assert.True(Take(12) > 0);
        Assert.Equal(sum % 64, Take(6));
    }
}
