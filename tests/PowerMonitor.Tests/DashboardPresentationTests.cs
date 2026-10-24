using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Tests;

public sealed class DashboardPresentationTests
{
    [Fact]
    public void Waiting_state_keeps_connection_error()
    {
        var state = DashboardPresentation.Create(null, null, "Logger timed out");

        Assert.False(state.HasData);
        Assert.Equal("No connection", state.Summary);
        Assert.Equal("Logger timed out", state.Error);
    }

    [Fact]
    public void Charging_snapshot_maps_to_live_dashboard_state()
    {
        var readAt = new DateTimeOffset(2026, 10, 21, 23, 52, 9, TimeSpan.FromHours(3));
        var snapshot = new InverterSnapshot(
            94,
            54.86,
            -2135,
            2541,
            346,
            true,
            readAt);
        var backup = new BackupEstimate(
            BackupKind.Duration,
            new TimeSpan(12, 25, 0),
            "If the grid dropped now",
            4300,
            346);

        var state = DashboardPresentation.Create(snapshot, backup, null);

        Assert.True(state.HasData);
        Assert.Equal(BatteryFlow.Charging, state.Flow);
        Assert.Equal("Grid on", state.Summary);
        Assert.StartsWith("Charging · ", state.FlowSummary);
        Assert.EndsWith(" kW", state.FlowSummary);
        Assert.Equal("12 h 25 m", state.Runtime);
        Assert.Equal($"{4.3:0.0} kWh usable", state.UsableEnergy);
        Assert.Equal(readAt.LocalDateTime.ToString("HH:mm:ss"), state.Updated);
    }

    [Theory]
    [InlineData(346, "346 W")]
    [InlineData(-346, "346 W")]
    [InlineData(0, "0 W")]
    public void Power_below_one_kilowatt_is_shown_in_watts(int watts, string expected) =>
        Assert.Equal(expected, DashboardPresentation.FormatPower(watts));

    [Fact]
    public void Power_from_one_kilowatt_is_shown_in_kilowatts() =>
        Assert.Equal($"{2.5:0.00} kW", DashboardPresentation.FormatPower(-2500));

    [Fact]
    public void History_is_bounded_and_keeps_newest_samples()
    {
        var history = new PowerHistory(capacity: 3);
        var start = DateTimeOffset.UtcNow;

        for (var index = 0; index < 5; index++)
            history.Add(new PowerSample(start.AddSeconds(index * 10), index, index * 2));

        Assert.Equal(3, history.Samples.Count);
        Assert.Equal(2, history.Samples[0].BatteryWatts);
        Assert.Equal(4, history.Samples[^1].BatteryWatts);
    }

    [Fact]
    public void History_rejects_out_of_order_samples()
    {
        var history = new PowerHistory();
        var now = DateTimeOffset.UtcNow;
        history.Add(new PowerSample(now, 100, 300));

        Assert.Throws<ArgumentException>(() =>
            history.Add(new PowerSample(now.AddSeconds(-1), 200, 400)));
    }
}
