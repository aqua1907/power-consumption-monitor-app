using PowerMonitor.Core.Presentation;
using PowerMonitor.Core.Schedule;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Tests;

public class ScheduleTests
{
    private static readonly DateOnly Day = new(2026, 11, 24);

    private static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) =>
        KyivTime.At(Day.AddDays(dayOffset), hour * 60 + minute);

    private static ScheduledOutage Out(int fromHour, int toHour, int dayOffset = 0, bool possible = false) =>
        new(At(fromHour, 0, dayOffset), At(toHour, 0, dayOffset), possible);

    private static DaySchedule Applies(int dayOffset, params ScheduledOutage[] outages) =>
        new(Day.AddDays(dayOffset), null, DayStatus.Applies, outages);

    private static AppSettings Active(int lead = 30) => new()
    {
        ScheduleEnabled = true,
        ScheduleGroup = "20.1",
        ScheduleLeadMinutes = lead
    };

    [Fact]
    public void Merges_touching_outages_across_midnight_and_lets_definite_win()
    {
        var days = new[]
        {
            Applies(0, Out(22, 24)),
            Applies(1, new ScheduledOutage(At(0, 0, 1), At(2, 0, 1)))
        };

        var merged = Assert.Single(ScheduleTimeline.Outages(days));
        Assert.Equal(At(22), merged.Start);
        Assert.Equal(At(2, 0, 1), merged.End);

        var mixed = ScheduleTimeline.Merge([Out(10, 14), Out(12, 16, possible: true)]);
        Assert.Equal(2, mixed.Count);
        Assert.Equal(Out(10, 14), mixed[0]);
        Assert.Equal(Out(14, 16, possible: true), mixed[1]);
    }

    [Fact]
    public void Finds_the_current_and_next_outage_and_ignores_possible_ones()
    {
        var days = new[] { Applies(0, Out(2, 4), Out(12, 14), Out(16, 18, possible: true)) };

        Assert.Equal(Out(2, 4), ScheduleTimeline.Current(days, At(3)));
        Assert.Null(ScheduleTimeline.Current(days, At(4)));
        Assert.Equal(Out(12, 14), ScheduleTimeline.Next(days, At(4)));
        Assert.Null(ScheduleTimeline.Next(days, At(14)));
    }

    [Fact]
    public void Diff_reports_new_and_changed_days_but_not_missing_or_identical_ones()
    {
        var stored = new Dictionary<DateOnly, DaySchedule>
        {
            [Day] = Applies(0, Out(2, 4)),
            [Day.AddDays(1)] = Applies(1, Out(6, 8))
        };

        var changes = ScheduleTimeline.Diff(stored,
        [
            Applies(0, Out(2, 4)) with { UpdatedAt = At(9) },
            Applies(1, Out(6, 9)),
            Applies(2, Out(1, 2))
        ]);

        Assert.Equal(2, changes.Count);
        Assert.False(changes[0].IsPublished);
        Assert.Equal(Day.AddDays(1), changes[0].Schedule.Day);
        Assert.True(changes[1].IsPublished);
    }

    [Fact]
    public void Archive_keeps_days_a_fetch_lacks_ignores_days_too_far_ahead_and_prunes()
    {
        var archive = new ScheduleArchive("dtek-kem", "20.1");
        var first = archive.Merge([Applies(0, Out(2, 4)), Applies(1, Out(6, 8)), Applies(9, Out(1, 2))], Day);
        Assert.Equal(2, first.Count);
        Assert.Equal(2, archive.Days.Count);

        var second = archive.Merge([Applies(1, Out(6, 8))], Day);
        Assert.Empty(second);
        Assert.Equal(2, archive.Days.Count);

        archive.Prune(At(12, 0, 40));
        Assert.Empty(archive.Days);
    }

    [Fact]
    public void Archive_forgets_everything_when_the_group_changes()
    {
        var archive = new ScheduleArchive("dtek-kem", "20.1");
        archive.Merge([Applies(0, Out(2, 4))], Day);
        archive.Observe(At(1), TimeSpan.FromSeconds(10));

        archive.Rebind("dtek-kem", "20.1");
        Assert.Single(archive.Days);

        archive.Rebind("dtek-kem", "5.1");
        Assert.Empty(archive.Days);
        Assert.Empty(archive.Watched);
        Assert.True(archive.BelongsTo("dtek-kem", "5.1"));
    }

    [Fact]
    public void Archive_records_watched_spans_and_starts_a_new_one_after_a_gap()
    {
        var archive = new ScheduleArchive("dtek-kem", "20.1");
        var poll = TimeSpan.FromSeconds(10);

        archive.Observe(At(1, 0), poll);
        archive.Observe(At(1, 0).AddSeconds(10), poll);
        archive.Observe(At(1, 0).AddSeconds(20), poll);
        archive.Observe(At(3, 0), poll);

        Assert.Equal(2, archive.Watched.Count);
        Assert.Equal(TimeSpan.FromSeconds(20), archive.Watched[0].Duration);
        Assert.Equal(TimeSpan.FromSeconds(20), archive.WatchedWithin(At(0), At(2)));
        Assert.Equal(TimeSpan.FromSeconds(5), archive.WatchedWithin(At(1, 0).AddSeconds(15), At(1, 0).AddSeconds(30)));
    }

    [Fact]
    public void Store_round_trips_and_survives_a_corrupt_file()
    {
        var path = TempPath("schedule.json");
        var archive = new ScheduleArchive("dtek-kem", "20.1");
        archive.Merge([Applies(0, Out(2, 4), Out(12, 14, possible: true))], Day);
        archive.Observe(At(1), TimeSpan.FromSeconds(10));

        ScheduleStore.Save(archive, path);
        var loaded = ScheduleStore.Load(path);

        Assert.True(loaded.BelongsTo("dtek-kem", "20.1"));
        var day = Assert.Single(loaded.Days.Values);
        Assert.True(day.SameAs(archive.Days[Day]));
        Assert.Single(loaded.Watched);

        File.WriteAllText(path, "{not json");
        Assert.Empty(ScheduleStore.Load(path).Days);
        Assert.Empty(ScheduleStore.Load(TempPath("missing.json")).Days);
    }

    [Fact]
    public void Warns_once_before_an_outage_and_once_when_it_starts()
    {
        var alerts = new ScheduleAlerts();
        var days = new[] { Applies(0, Out(12, 14)) };
        var settings = Active(lead: 30);

        Assert.Empty(alerts.Check(days, At(11, 0), settings));

        var soon = Assert.Single(alerts.Check(days, At(11, 40), settings));
        Assert.Contains(ScheduleText.Range(Out(12, 14)), soon);
        Assert.Contains("20 m", soon);
        Assert.Empty(alerts.Check(days, At(11, 45), settings));

        var started = Assert.Single(alerts.Check(days, At(12, 1), settings));
        Assert.Contains(ScheduleText.Clock(At(14)), started);
        Assert.Empty(alerts.Check(days, At(12, 5), settings));
    }

    [Fact]
    public void Does_not_announce_an_outage_that_started_long_ago_or_when_the_schedule_is_off()
    {
        var days = new[] { Applies(0, Out(12, 14)) };

        Assert.Empty(new ScheduleAlerts().Check(days, At(13, 0), Active()));
        Assert.Empty(new ScheduleAlerts().Check(days, At(11, 40), Active() with { ScheduleEnabled = false }));
        Assert.Empty(new ScheduleAlerts().Check(days, At(11, 40), Active() with { ScheduleGroup = "" }));
        Assert.Empty(new ScheduleAlerts().Check(
            [Applies(0, Out(12, 14, possible: true))], At(11, 40), Active()));
    }

    [Fact]
    public void Describes_published_and_changed_days()
    {
        var published = new DayChange(Applies(1, Out(2, 4), Out(12, 14)), null);
        var changed = new DayChange(Applies(0, Out(6, 8)), Applies(0, Out(5, 8)));
        var none = new DayChange(new DaySchedule(Day.AddDays(2), null, DayStatus.NoOutages, []), null);
        var unknown = new DayChange(new DaySchedule(Day, null, DayStatus.Unknown, []), null);

        var text = ScheduleAlerts.DescribeChanges([published, changed, none, unknown], Day);

        Assert.NotNull(text);
        Assert.Contains("Tomorrow's outage schedule is out", text);
        Assert.Contains(ScheduleText.Range(Out(12, 14)), text);
        Assert.Contains("Today's outage schedule changed", text);
        Assert.Contains("no outages scheduled", text);
        Assert.Equal(3, text.Split(Environment.NewLine).Length);
        Assert.Null(ScheduleAlerts.DescribeChanges([unknown], Day));
    }

    [Fact]
    public void Flyout_line_says_what_is_happening_now_or_next()
    {
        var days = new[] { Applies(0, Out(12, 14)), Applies(1, Out(2, 4, dayOffset: 1)) };

        Assert.Contains("until", ScheduleText.Line(days, At(13)));
        Assert.StartsWith("Next scheduled outage", ScheduleText.Line(days, At(11)));
        Assert.Contains("tomorrow", ScheduleText.Line(days, At(15)));
        Assert.Equal("No more outages scheduled", ScheduleText.Line([Applies(0, Out(2, 4))], At(15)));
        Assert.Equal("Waiting for the outage schedule", ScheduleText.Line([], At(15)));
        Assert.Null(ScheduleText.Brief([Applies(0, Out(2, 4))], At(15)));
        Assert.StartsWith("Outage until", ScheduleText.Brief(days, At(13)));
    }

    private static OutageEvent Actual(int fromHour, int fromMinute, int toHour, int toMinute) =>
        new(At(fromHour, fromMinute), At(toHour, toMinute));

    private static ScheduleArchive ArchiveWith(params ScheduledOutage[] outages)
    {
        var archive = new ScheduleArchive("dtek-kem", "20.1");
        archive.Merge([Applies(0, outages)], Day);
        return archive;
    }

    private static void Watch(ScheduleArchive archive, int fromHour, int toHour)
    {
        var poll = TimeSpan.FromSeconds(10);
        for (var at = At(fromHour); at <= At(toHour); at = at.AddSeconds(10))
            archive.Observe(at, poll);
    }

    [Fact]
    public void Comparison_calls_a_close_outage_on_time_and_a_far_one_shifted()
    {
        var archive = ArchiveWith(Out(2, 4), Out(12, 14));
        Watch(archive, 0, 16);
        var comparison = ScheduleComparer.Compare(
            archive,
            [Actual(2, 5, 4, 10), Actual(12, 40, 14, 0)],
            At(18));

        Assert.Equal(2, comparison.Rows.Count);
        var late = comparison.Rows[0];
        Assert.Equal(ComparisonResult.Shifted, late.Result);
        Assert.Equal(TimeSpan.FromMinutes(40), late.StartDelta);
        Assert.Equal(TimeSpan.Zero, late.EndDelta);
        var onTime = comparison.Rows[1];
        Assert.Equal(ComparisonResult.OnTime, onTime.Result);
        Assert.Equal(TimeSpan.FromMinutes(5), onTime.StartDelta);
        Assert.Equal(TimeSpan.FromMinutes(10), onTime.EndDelta);
        Assert.Equal(1, comparison.Summary.OnTime);
        Assert.Equal(1, comparison.Summary.Shifted);
        Assert.Equal(TimeSpan.FromHours(4), comparison.Summary.ScheduledTime);
        Assert.Contains("1 on time", ComparisonText.Summary(comparison.Summary));
        Assert.StartsWith("Started 40 m late", ComparisonText.Result(late));
    }

    [Fact]
    public void Comparison_separates_a_missed_outage_from_one_this_app_could_not_see()
    {
        var archive = ArchiveWith(Out(2, 4), Out(12, 14));
        Watch(archive, 11, 15);

        var comparison = ScheduleComparer.Compare(archive, [], At(18));

        Assert.Equal(ComparisonResult.DidNotHappen, comparison.Rows[0].Result);
        Assert.Equal(ComparisonResult.NoData, comparison.Rows[1].Result);
        Assert.Equal(TimeSpan.FromHours(2), comparison.Summary.ScheduledTime);
        Assert.Equal(1, comparison.Summary.NoData);
    }

    [Fact]
    public void Comparison_flags_a_real_outage_on_a_scheduled_day_that_no_schedule_covers()
    {
        var archive = ArchiveWith(Out(2, 4));
        var comparison = ScheduleComparer.Compare(
            archive,
            [Actual(2, 0, 4, 0), Actual(20, 0, 21, 0)],
            At(23));

        var row = comparison.Rows[0];
        Assert.Equal(ComparisonResult.Unscheduled, row.Result);
        Assert.Null(row.Scheduled);
        Assert.Equal("Not scheduled", ComparisonText.Scheduled(row));
        Assert.Equal(1, comparison.Summary.Unscheduled);
        Assert.Equal(TimeSpan.FromHours(3), comparison.Summary.ActualTime);

        // A day the app has no schedule for says nothing about "unscheduled".
        var elsewhere = new OutageEvent(At(10, 0, -5), At(11, 0, -5));
        Assert.DoesNotContain(
            ScheduleComparer.Compare(archive, [elsewhere], At(23)).Rows,
            r => r.Result == ComparisonResult.Unscheduled);
    }

    [Fact]
    public void Comparison_handles_an_ongoing_outage_and_unknown_start()
    {
        var archive = ArchiveWith(Out(12, 16));
        var ongoing = new OutageEvent(At(12, 0), null);
        var comparison = ScheduleComparer.Compare(archive, [ongoing], At(13));

        var row = Assert.Single(comparison.Rows);
        Assert.Equal(ComparisonResult.InProgress, row.Result);
        Assert.Null(row.ActualEnd);
        Assert.EndsWith("ongoing", ComparisonText.Actual(row));

        var before = new OutageEvent(At(12, 50), At(16, 0), StartKnown: false);
        var unknownStart = Assert.Single(ScheduleComparer.Compare(archive, [before], At(18)).Rows);
        Assert.Null(unknownStart.StartDelta);
        Assert.Equal(ComparisonResult.OnTime, unknownStart.Result);
        Assert.StartsWith("Before", ComparisonText.Actual(unknownStart));
    }

    [Fact]
    public void Comparison_ignores_scheduled_outages_that_have_not_started()
    {
        var archive = ArchiveWith(Out(20, 22));

        Assert.Empty(ScheduleComparer.Compare(archive, [], At(12)).Rows);
    }

    private static string TempPath(string name) =>
        Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"), name);
}
