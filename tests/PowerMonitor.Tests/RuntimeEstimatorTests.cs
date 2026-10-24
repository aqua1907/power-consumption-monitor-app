using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Tests;

public class RuntimeEstimatorTests
{
    [Fact]
    public void Estimates_backup_time_from_one_pack_and_apartment_load()
    {
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 72, load: 840, grid: true), new AppSettings());

        Assert.Equal(BackupKind.Duration, estimate.Kind);
        Assert.Equal(3174.4m, estimate.RemainingWh);
        Assert.Equal("3 h 46 m", estimate.DurationText);
        Assert.Equal("If the grid dropped now", estimate.Label);
        Assert.Contains("If the grid dropped now", estimate.Hint);
    }

    [Fact]
    public void Uses_a_backup_label_when_the_grid_is_off()
    {
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 72, load: 840, grid: false), new AppSettings());

        Assert.Equal("Backup time remaining", estimate.Label);
        Assert.Equal("3 h 46 m", estimate.DurationText);
    }

    [Fact]
    public void Treats_a_load_under_50_watts_as_idle()
    {
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 72, load: 49, grid: true), new AppSettings());

        Assert.Equal(BackupKind.Idle, estimate.Kind);
        Assert.Equal("Idle", estimate.DurationText);
        Assert.Contains("under 50 W", estimate.Hint);
    }

    [Fact]
    public void Estimates_a_time_at_50_watts()
    {
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 72, load: 50, grid: false), new AppSettings());

        Assert.Equal(BackupKind.Duration, estimate.Kind);
    }

    [Fact]
    public void Uses_the_magnitude_of_a_negative_load()
    {
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 72, load: -840, grid: true), new AppSettings());

        Assert.Equal("3 h 46 m", estimate.DurationText);
    }

    [Fact]
    public void Reports_empty_at_the_reserve()
    {
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 10, load: 840, grid: false), new AppSettings());

        Assert.Equal(BackupKind.Empty, estimate.Kind);
        Assert.Equal(0m, estimate.RemainingWh);
        Assert.Equal("0 m", estimate.DurationText);
    }

    [Fact]
    public void Scales_energy_by_pack_count()
    {
        var settings = new AppSettings { PackCount = 2 };
        var estimate = RuntimeEstimator.Estimate(Snapshot(soc: 72, load: 840, grid: true), settings);

        Assert.Equal(6348.8m, estimate.RemainingWh);
    }

    private static InverterSnapshot Snapshot(int soc, int load, bool grid) =>
        new(soc, 51.2, -100, 0, load, grid, DateTimeOffset.UnixEpoch);
}
