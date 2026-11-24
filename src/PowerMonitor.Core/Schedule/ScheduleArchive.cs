using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Core.Schedule;

/// <summary>A stretch of time the app was reading the grid state, so a missing outage in it is real.</summary>
public readonly record struct WatchSpan(DateTimeOffset From, DateTimeOffset To)
{
    public TimeSpan Duration => To - From;
}

/// <summary>
/// The schedules this app has seen for one group, kept for <see cref="RetentionDays"/>. Operators publish
/// only today and tomorrow, so comparing a past outage to its schedule needs our own copy.
/// It also records when the app was watching the grid.
/// </summary>
public sealed class ScheduleArchive
{
    public const int RetentionDays = 30;

    private readonly SortedDictionary<DateOnly, DaySchedule> _days;
    private readonly List<WatchSpan> _watched;

    public ScheduleArchive(
        string operatorId = "",
        string group = "",
        IEnumerable<DaySchedule>? days = null,
        IEnumerable<WatchSpan>? watched = null)
    {
        OperatorId = operatorId;
        Group = group;
        _days = new SortedDictionary<DateOnly, DaySchedule>(
            (days ?? []).GroupBy(d => d.Day).ToDictionary(g => g.Key, g => g.Last()));
        _watched = (watched ?? []).OrderBy(w => w.From).ToList();
    }

    public string OperatorId { get; private set; }

    public string Group { get; private set; }

    public IReadOnlyDictionary<DateOnly, DaySchedule> Days => _days;

    public IReadOnlyList<WatchSpan> Watched => _watched;

    public bool BelongsTo(string operatorId, string group) =>
        OperatorId == operatorId && Group == group;

    /// <summary>Empties the archive when the address (and so the group) changes: another group's hours say nothing here.</summary>
    public void Rebind(string operatorId, string group)
    {
        if (BelongsTo(operatorId, group))
            return;

        OperatorId = operatorId;
        Group = group;
        _days.Clear();
        _watched.Clear();
    }

    /// <summary>
    /// Stores a fresh fetch and returns what changed. Days the fetch lacks stay as they were, so a
    /// temporarily empty answer neither wipes history nor announces the schedule again.
    /// </summary>
    public IReadOnlyList<DayChange> Merge(IEnumerable<DaySchedule> fetched, DateOnly today)
    {
        var fresh = ScheduleTimeline.Trim(fetched, today).ToList();
        var changes = ScheduleTimeline.Diff(_days, fresh);
        foreach (var day in fresh)
            _days[day.Day] = day;
        return changes;
    }

    /// <summary>Notes that the grid state was read at <paramref name="at"/>.</summary>
    public void Observe(DateTimeOffset at, TimeSpan pollInterval)
    {
        if (_watched.Count > 0)
        {
            var last = _watched[^1];
            if (at < last.To)
                return;
            if (at - last.To <= OutageLog.UnwatchedAfter(pollInterval))
            {
                _watched[^1] = last with { To = at };
                return;
            }
        }

        _watched.Add(new WatchSpan(at, at));
    }

    /// <summary>Drops days and spans older than <see cref="RetentionDays"/>.</summary>
    public void Prune(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-RetentionDays);
        var cutoffDay = KyivTime.DayOf(cutoff);
        foreach (var day in _days.Keys.Where(d => d < cutoffDay).ToList())
            _days.Remove(day);
        _watched.RemoveAll(w => w.To < cutoff);
    }

    /// <summary>How much of <paramref name="from"/> to <paramref name="to"/> the app was watching.</summary>
    public TimeSpan WatchedWithin(DateTimeOffset from, DateTimeOffset to)
    {
        var total = TimeSpan.Zero;
        foreach (var span in _watched)
        {
            var start = span.From > from ? span.From : from;
            var end = span.To < to ? span.To : to;
            if (end > start)
                total += end - start;
        }

        return total;
    }
}
