using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Core.Presentation;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Tests;

public sealed class ChargeEstimatorTests
{
    [Fact]
    public void Estimates_time_to_full_from_charging_power()
    {
        var estimate = ChargeEstimator.Estimate(Snapshot(soc: 50, batteryWatts: -1000), new AppSettings());

        Assert.NotNull(estimate);
        Assert.Equal(ChargeKind.Duration, estimate.Kind);
        Assert.Equal(2560m, estimate.RemainingWh);
        Assert.Equal("~2 h 33 m", estimate.DurationText);
    }

    [Fact]
    public void Scales_with_the_number_of_packs()
    {
        var estimate = ChargeEstimator.Estimate(
            Snapshot(soc: 50, batteryWatts: -1000), new AppSettings { PackCount = 2 });

        Assert.Equal("~5 h 07 m", estimate!.DurationText);
    }

    [Fact]
    public void Reports_full_at_one_hundred_percent()
    {
        var estimate = ChargeEstimator.Estimate(Snapshot(soc: 100, batteryWatts: -80), new AppSettings());

        Assert.Equal(ChargeKind.Full, estimate!.Kind);
        Assert.Equal("Full", estimate.DurationText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(500)]
    public void Returns_nothing_unless_charging(int batteryWatts) =>
        Assert.Null(ChargeEstimator.Estimate(Snapshot(soc: 50, batteryWatts: batteryWatts), new AppSettings()));

    [Fact]
    public void Dashboard_shows_voltage_current_and_charge_time()
    {
        var snapshot = Snapshot(soc: 50, batteryWatts: -1000) with { BatteryCurrentAmps = -19.53 };
        var charge = ChargeEstimator.Estimate(snapshot, new AppSettings());

        var state = DashboardPresentation.Create(snapshot, null, null, charge);

        Assert.Equal("51.2 V", state.BatteryVoltage);
        Assert.Equal($"{19.5:0.0} A", state.BatteryCurrent);
        Assert.Equal("~2 h 33 m", state.ChargeTime);
        Assert.Contains("Charging slows", state.ChargeHint);
    }

    [Fact]
    public void Dashboard_blanks_grid_voltage_and_current_when_the_grid_is_off()
    {
        var onGrid = Snapshot(soc: 50, batteryWatts: 400) with
        {
            GridVoltageVolts = 230.4,
            GridCurrentAmps = 1.8
        };
        var offGrid = onGrid with { GridConnected = false };

        var connected = DashboardPresentation.Create(onGrid, null, null);
        var disconnected = DashboardPresentation.Create(offGrid, null, null);

        Assert.Equal("230.4 V", connected.GridVoltage);
        Assert.Equal($"{1.8:0.0} A", connected.GridCurrent);
        Assert.Equal("", disconnected.GridVoltage);
        Assert.Equal("", disconnected.GridCurrent);
        Assert.Equal("51.2 V", disconnected.BatteryVoltage);
    }

    [Fact]
    public void Dashboard_has_no_charge_time_when_not_charging()
    {
        var snapshot = Snapshot(soc: 50, batteryWatts: 400);

        var state = DashboardPresentation.Create(
            snapshot, null, null, ChargeEstimator.Estimate(snapshot, new AppSettings()));

        Assert.Equal("", state.ChargeTime);
    }

    private static InverterSnapshot Snapshot(int soc, int batteryWatts) =>
        new(soc, 51.2, batteryWatts, 0, 400, true, DateTimeOffset.UnixEpoch);
}
