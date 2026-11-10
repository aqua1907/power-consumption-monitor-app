using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Tests;

public sealed class OutageLogTests
{
    [Fact]
    public void Grid_drop_then_recovery_records_one_closed_event()
    {
        var log = new OutageLog();
        var start = DateTimeOffset.UtcNow;

        var poll = TimeSpan.FromHours(1);
        log.Observe(true, start, poll);
        var opened = log.Observe(false, start.AddMinutes(1), poll);
        var closed = log.Observe(true, start.AddMinutes(16), poll);

        Assert.True(opened);
        Assert.True(closed);
        var outage = Assert.Single(log.Events);
        Assert.False(outage.IsOngoing);
        Assert.True(outage.StartKnown);
        Assert.True(outage.EndKnown);
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
        var outage = Assert.Single(log.Events);
        Assert.True(outage.IsOngoing);
        Assert.False(outage.StartKnown);
    }

    [Fact]
    public void Grid_drop_while_the_pc_was_off_is_only_known_as_before_the_next_reading()
    {
        var log = new OutageLog();
        var start = DateTimeOffset.UtcNow;

        log.Observe(true, start);
        var changed = log.Observe(false, start.AddHours(8));

        Assert.True(changed);
        var outage = Assert.Single(log.Events);
        Assert.False(outage.StartKnown);
        Assert.True(outage.IsOngoing);
        Assert.StartsWith("Over ", outage.DurationLabel(start.AddHours(8)));
    }

    [Fact]
    public void Grid_return_while_the_pc_was_off_is_only_known_as_before_the_next_reading()
    {
        var start = DateTimeOffset.UtcNow;
        var log = new OutageLog([new OutageEvent(start, null)], lastSeenAt: start, lastGridConnected: false);

        var changed = log.Observe(true, start.AddHours(8));

        Assert.True(changed);
        var outage = Assert.Single(log.Events);
        Assert.True(outage.StartKnown);
        Assert.False(outage.EndKnown);
        Assert.StartsWith("Under ", outage.DurationLabel(start.AddHours(8)));
    }

    [Fact]
    public void Duration_is_the_span_when_both_times_are_only_bounds()
    {
        var start = new DateTimeOffset(2026, 11, 10, 10, 12, 30, TimeSpan.FromHours(2));
        var end = new DateTimeOffset(2026, 11, 10, 17, 28, 41, TimeSpan.FromHours(2));
        var outage = new OutageEvent(start, end, StartKnown: false, EndKnown: false);

        Assert.Equal("7 h 16 m", outage.DurationLabel(end));
    }

    [Fact]
    public void An_outage_that_starts_and_ends_while_the_pc_was_off_is_not_invented()
    {
        var log = new OutageLog();
        var start = DateTimeOffset.UtcNow;

        log.Observe(true, start);
        var changed = log.Observe(true, start.AddHours(8));

        Assert.False(changed);
        Assert.Empty(log.Events);
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

        var changed = log.Observe(true, now, TimeSpan.FromHours(1));

        Assert.True(changed);
        Assert.False(Assert.Single(log.Events).IsOngoing);
    }
}
