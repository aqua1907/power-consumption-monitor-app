using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Core.Schedule;

public enum ComparisonResult
{
    /// <summary>Started and ended within <see cref="ScheduleComparer.OnTimeTolerance"/> of the schedule.</summary>
    OnTime,
    /// <summary>The grid went off during the scheduled time, but earlier, later or for a different length.</summary>
    Shifted,
    /// <summary>The scheduled outage is still going on, and so is the real one.</summary>
    InProgress,
    /// <summary>The app was watching the whole time and the grid stayed on.</summary>
    DidNotHappen,
    /// <summary>No real outage was seen, but the app was not watching all of that time.</summary>
    NoData,
    /// <summary>A real outage on a day with a schedule, outside every scheduled outage.</summary>
    Unscheduled
}

/// <summary>One line of the comparison: a scheduled outage, the real one that overlapped it, or an unscheduled real one.</summary>
public sealed record ComparisonRow(
    DateOnly Day,
    ScheduledOutage? Scheduled,
    DateTimeOffset? ActualStart,
    DateTimeOffset? ActualEnd,
    bool StartKnown,
    bool EndKnown,
    TimeSpan? StartDelta,
    TimeSpan? EndDelta,
    ComparisonResult Result)
{
    public DateTimeOffset SortKey => Scheduled?.Start ?? ActualStart ?? default;
}

public sealed record ComparisonSummary(
    int OnTime,
    int Shifted,
    int DidNotHappen,
    int Unscheduled,
    int NoData,
    TimeSpan ScheduledTime,
    TimeSpan ActualTime,
    int Days);

public sealed record ScheduleComparison(IReadOnlyList<ComparisonRow> Rows, ComparisonSummary Summary);

/// <summary>Words for the comparison, in this PC's time zone.</summary>
public static class ComparisonText
{
    public static string Scheduled(ComparisonRow row) =>
        row.Scheduled is { } outage ? ScheduleText.Range(outage) : "Not scheduled";

    public static string Actual(ComparisonRow row)
    {
        if (row.ActualStart is not { } start)
            return "No outage seen";

        var from = (row.StartKnown ? "" : "Before ") + ScheduleText.Clock(start);
        var to = row.ActualEnd is { } end
            ? (row.EndKnown ? "" : "before ") + ScheduleText.Clock(end)
            : "ongoing";
        return $"{from}–{to}";
    }

    public static string Result(ComparisonRow row)
    {
        switch (row.Result)
        {
            case ComparisonResult.OnTime:
                return "On time";
            case ComparisonResult.InProgress:
                return "In progress";
            case ComparisonResult.DidNotHappen:
                return "Did not happen";
            case ComparisonResult.NoData:
                return "Not watched";
            case ComparisonResult.Unscheduled:
                return "Not scheduled";
            default:
                var parts = new[] { Shift("started", row.StartDelta), Shift("ended", row.EndDelta) }
                    .Where(p => p is not null)
                    .ToList();
                return parts.Count == 0
                    ? "Different from the schedule"
                    : char.ToUpperInvariant(parts[0]![0]) + string.Join(" · ", parts)[1..];
        }
    }

    public static string Summary(ComparisonSummary s)
    {
        var parts = new List<string>();
        void Add(int count, string text)
        {
            if (count > 0)
                parts.Add($"{count} {text}");
        }

        Add(s.OnTime, "on time");
        Add(s.Shifted, "shifted");
        Add(s.DidNotHappen, "did not happen");
        Add(s.Unscheduled, "not scheduled");
        Add(s.NoData, "not watched");
        var counts = parts.Count == 0 ? "No outages to compare yet." : string.Join(", ", parts) + ".";
        return s.Days == 0
            ? counts
            : $"Over {s.Days} day{(s.Days == 1 ? "" : "s")} with a schedule: scheduled {Total(s.ScheduledTime)}, " +
              $"actual {Total(s.ActualTime)}. {counts}";
    }

    private static string Total(TimeSpan span) => span < TimeSpan.FromMinutes(1) ? "0 m" : ScheduleText.Span(span);

    private static string? Shift(string verb, TimeSpan? delta)
    {
        if (delta is not { } d)
            return null;
        if (d.Duration() < TimeSpan.FromMinutes(1))
            return $"{verb} on time";
        return $"{verb} {ScheduleText.Span(d.Duration())} {(d > TimeSpan.Zero ? "late" : "early")}";
    }
}

/// <summary>Sets what the battery log recorded against what the operator scheduled.</summary>
public static class ScheduleComparer
{
    public static readonly TimeSpan OnTimeTolerance = TimeSpan.FromMinutes(15);

    /// <summary>The share of a scheduled outage the app must have watched to call a missing outage "did not happen".</summary>
    public const double WatchedShare = 0.9;

    public static ScheduleComparison Compare(
        ScheduleArchive archive,
        IReadOnlyList<OutageEvent> actual,
        DateTimeOffset now)
    {
        var scheduled = ScheduleTimeline.Outages(archive.Days.Values)
            .Where(o => o.Start < now)
            .ToList();
        var rows = new List<ComparisonRow>();
        var matched = new HashSet<int>();
        var events = actual.Select(e => (Event: e, End: e.End ?? now)).ToList();

        foreach (var outage in scheduled)
        {
            var hits = new List<int>();
            for (var i = 0; i < events.Count; i++)
            {
                if (Overlap(events[i].Event.Start, events[i].End, outage.Start, outage.End) > TimeSpan.Zero)
                    hits.Add(i);
            }

            var day = KyivTime.DayOf(outage.Start);
            if (hits.Count == 0)
            {
                var watched = archive.WatchedWithin(outage.Start, outage.End);
                var result = watched.TotalSeconds >= outage.Duration.TotalSeconds * WatchedShare
                    ? ComparisonResult.DidNotHappen
                    : ComparisonResult.NoData;
                rows.Add(new ComparisonRow(day, outage, null, null, true, true, null, null, result));
                continue;
            }

            foreach (var i in hits)
                matched.Add(i);

            var first = events[hits[0]].Event;
            var last = events[hits[^1]].Event;
            var ongoing = last.IsOngoing;
            var startDelta = first.StartKnown ? first.Start - outage.Start : (TimeSpan?)null;
            var endDelta = !ongoing && last.EndKnown && last.End is { } end ? end - outage.End : (TimeSpan?)null;
            var stillScheduled = ongoing && now < outage.End;
            var onTime = (startDelta is null || startDelta.Value.Duration() <= OnTimeTolerance) &&
                         (endDelta is null || endDelta.Value.Duration() <= OnTimeTolerance);
            rows.Add(new ComparisonRow(
                day,
                outage,
                first.Start,
                ongoing ? null : last.End,
                first.StartKnown,
                last.EndKnown,
                startDelta,
                endDelta,
                stillScheduled ? ComparisonResult.InProgress
                    : onTime ? ComparisonResult.OnTime
                    : ComparisonResult.Shifted));
        }

        // A real outage on a day we have a schedule for, and in none of its scheduled outages.
        var knownDays = archive.Days.Keys.ToHashSet();
        var actualTime = TimeSpan.Zero;
        for (var i = 0; i < events.Count; i++)
        {
            var (e, end) = events[i];
            var day = KyivTime.DayOf(e.Start);
            if (!knownDays.Contains(day))
                continue;

            actualTime += end - e.Start;
            if (!matched.Contains(i))
            {
                rows.Add(new ComparisonRow(
                    day,
                    null,
                    e.Start,
                    e.End,
                    e.StartKnown,
                    e.EndKnown,
                    null,
                    null,
                    ComparisonResult.Unscheduled));
            }
        }

        rows.Sort((a, b) => b.SortKey.CompareTo(a.SortKey));

        var counted = rows.Where(r => r.Result != ComparisonResult.NoData).ToList();
        var summary = new ComparisonSummary(
            rows.Count(r => r.Result is ComparisonResult.OnTime or ComparisonResult.InProgress),
            rows.Count(r => r.Result == ComparisonResult.Shifted),
            rows.Count(r => r.Result == ComparisonResult.DidNotHappen),
            rows.Count(r => r.Result == ComparisonResult.Unscheduled),
            rows.Count(r => r.Result == ComparisonResult.NoData),
            counted.Where(r => r.Scheduled is not null).Aggregate(TimeSpan.Zero, (sum, r) => sum + r.Scheduled!.Value.Duration),
            actualTime,
            knownDays.Count);
        return new ScheduleComparison(rows, summary);
    }

    private static TimeSpan Overlap(
        DateTimeOffset aStart,
        DateTimeOffset aEnd,
        DateTimeOffset bStart,
        DateTimeOffset bEnd)
    {
        var start = aStart > bStart ? aStart : bStart;
        var end = aEnd < bEnd ? aEnd : bEnd;
        return end > start ? end - start : TimeSpan.Zero;
    }
}
