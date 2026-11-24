namespace PowerMonitor.Core.Schedule;

/// <summary>A day whose schedule is new or different from the one stored before.</summary>
public sealed record DayChange(DaySchedule Schedule, DaySchedule? Previous)
{
    public bool IsPublished => Previous is null;
}

/// <summary>Merging, lookups and change detection across days of one group.</summary>
public static class ScheduleTimeline
{
    /// <summary>Days after today a schedule may cover. Anything further is a broken answer.</summary>
    public const int DaysAhead = 2;

    /// <summary>Sorts outages and joins the ones that overlap or touch. A definite outage wins over a possible one.</summary>
    public static IReadOnlyList<ScheduledOutage> Merge(IEnumerable<ScheduledOutage> outages)
    {
        var all = outages.ToList();
        var definite = Join(all.Where(o => !o.Possible));
        var possible = Join(all.Where(o => o.Possible)).SelectMany(o => Cut(o, definite));
        return definite.Concat(possible).OrderBy(o => o.Start).ThenBy(o => o.End).ToList();
    }

    /// <summary>Outages across days: 22:00-24:00 plus the next day's 00:00-02:00 become one.</summary>
    public static IReadOnlyList<ScheduledOutage> Outages(IEnumerable<DaySchedule> days, bool includePossible = false)
    {
        var merged = Merge(days.SelectMany(d => d.Outages));
        return includePossible ? merged : merged.Where(o => !o.Possible).ToList();
    }

    public static ScheduledOutage? Current(IEnumerable<DaySchedule> days, DateTimeOffset now)
    {
        foreach (var outage in Outages(days))
        {
            if (outage.Start <= now && now < outage.End)
                return outage;
        }

        return null;
    }

    public static ScheduledOutage? Next(IEnumerable<DaySchedule> days, DateTimeOffset now)
    {
        foreach (var outage in Outages(days))
        {
            if (outage.Start > now)
                return outage;
        }

        return null;
    }

    /// <summary>What changed between the stored days and a fresh fetch. A day the fetch lacks is not reported.</summary>
    public static IReadOnlyList<DayChange> Diff(
        IReadOnlyDictionary<DateOnly, DaySchedule> stored,
        IEnumerable<DaySchedule> fetched)
    {
        var changes = new List<DayChange>();
        foreach (var day in fetched.OrderBy(d => d.Day))
        {
            if (!stored.TryGetValue(day.Day, out var previous))
                changes.Add(new DayChange(day, null));
            else if (!previous.SameAs(day))
                changes.Add(new DayChange(day, previous));
        }

        return changes;
    }

    /// <summary>Keeps days from <paramref name="today"/> to <see cref="DaysAhead"/> after it.</summary>
    public static IEnumerable<DaySchedule> Trim(IEnumerable<DaySchedule> days, DateOnly today)
    {
        var last = today.AddDays(DaysAhead);
        return days.Where(d => d.Day >= today && d.Day <= last);
    }

    private static List<ScheduledOutage> Join(IEnumerable<ScheduledOutage> outages)
    {
        var joined = new List<ScheduledOutage>();
        foreach (var outage in outages.OrderBy(o => o.Start).ThenBy(o => o.End))
        {
            if (joined.Count > 0 && outage.Start <= joined[^1].End)
            {
                if (outage.End > joined[^1].End)
                    joined[^1] = joined[^1] with { End = outage.End };
            }
            else
            {
                joined.Add(outage);
            }
        }

        return joined;
    }

    private static List<ScheduledOutage> Cut(ScheduledOutage outage, List<ScheduledOutage> definite)
    {
        var pieces = new List<ScheduledOutage> { outage };
        foreach (var d in definite)
        {
            var rest = new List<ScheduledOutage>();
            foreach (var piece in pieces)
            {
                if (d.End <= piece.Start || d.Start >= piece.End)
                {
                    rest.Add(piece);
                    continue;
                }

                if (piece.Start < d.Start)
                    rest.Add(piece with { End = d.Start });
                if (d.End < piece.End)
                    rest.Add(piece with { Start = d.End });
            }

            pieces = rest;
        }

        return pieces;
    }
}
