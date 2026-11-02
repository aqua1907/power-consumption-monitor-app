using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Tests;

public sealed class OutageLogTests
{
    [Fact]
    public void Grid_drop_then_recovery_records_one_closed_event()
    {
        var log = new OutageLog();
        var start = DateTimeOffset.UtcNow;

        log.Observe(true, start);
        var opened = log.Observe(false, start.AddMinutes(1));
        var closed = log.Observe(true, start.AddMinutes(16));

        Assert.True(opened);
        Assert.True(closed);
        var outage = Assert.Single(log.Events);
        Assert.False(outage.IsOngoing);
        Assert.Equal(TimeSpan.FromMinutes(15), outage.Duration(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Ongoing_outage_stays_open_until_grid_returns()
    {
        var log = new OutageLog();
        var start = DateTimeOffset.UtcNow;

        log.Observe(true, start);
        log.Observe(false, start.AddMinutes(1));
        log.Observe(false, start.AddMinutes(2));

        var outage = Assert.Single(log.Events);
        Assert.True(outage.IsOngoing);
    }

    [Fact]
    public void Starting_already_without_grid_opens_an_event_without_a_prior_reading()
    {
        var log = new OutageLog();
        var now = DateTimeOffset.UtcNow;

        var changed = log.Observe(false, now);

        Assert.True(changed);
        Assert.True(Assert.Single(log.Events).IsOngoing);
    }

    [Fact]
    public void Seeding_with_an_open_event_does_not_duplicate_it()
    {
        var now = DateTimeOffset.UtcNow;
        var log = new OutageLog([new OutageEvent(now.AddMinutes(-5), null)]);

        var changed = log.Observe(false, now);

        Assert.False(changed);
        Assert.Single(log.Events);
    }

    [Fact]
    public void Seeding_with_an_open_event_closes_it_once_grid_returns()
    {
        var now = DateTimeOffset.UtcNow;
        var log = new OutageLog([new OutageEvent(now.AddMinutes(-5), null)]);

        var changed = log.Observe(true, now);

        Assert.True(changed);
        Assert.False(Assert.Single(log.Events).IsOngoing);
    }
}
