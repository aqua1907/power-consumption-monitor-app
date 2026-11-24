using PowerMonitor.Core.Settings;

namespace PowerMonitor.Core.Schedule;

/// <summary>Decides which schedule notifications are due. Each fires once per outage.</summary>
public sealed class ScheduleAlerts
{
    /// <summary>How long after its scheduled start the "outage started" notice may still fire.</summary>
    public static readonly TimeSpan StartedWindow = TimeSpan.FromMinutes(15);

    private readonly HashSet<(DateTimeOffset Start, bool Started)> _sent = [];

    public void Reset() => _sent.Clear();

    /// <summary>Notifications due at <paramref name="now"/>: one before a scheduled outage and one when it starts.</summary>
    public IReadOnlyList<string> Check(IEnumerable<DaySchedule> days, DateTimeOffset now, AppSettings settings)
    {
        var messages = new List<string>();
        if (!settings.ScheduleActive)
            return messages;

        var lead = TimeSpan.FromMinutes(Math.Clamp(
            settings.ScheduleLeadMinutes,
            AppSettings.MinScheduleLeadMinutes,
            AppSettings.MaxScheduleLeadMinutes));
        _sent.RemoveWhere(s => s.Start < now.AddDays(-1));

        foreach (var outage in ScheduleTimeline.Outages(days))
        {
            if (now < outage.Start && outage.Start - now <= lead && _sent.Add((outage.Start, false)))
            {
                messages.Add(
                    $"Scheduled outage {ScheduleText.Range(outage)} starts in {ScheduleText.Span(outage.Start - now, ceiling: true)}.");
            }
            else if (now >= outage.Start && now < outage.End && now - outage.Start < StartedWindow &&
                     _sent.Add((outage.Start, true)))
            {
                messages.Add($"Scheduled outage started. Power is expected back at {ScheduleText.Clock(outage.End)}.");
            }
        }

        return messages;
    }

    /// <summary>One message for the days that were published or changed, or null when there is nothing to say.</summary>
    public static string? DescribeChanges(IReadOnlyList<DayChange> changes, DateOnly today)
    {
        var lines = new List<string>();
        foreach (var change in changes)
        {
            var schedule = change.Schedule;
            if (schedule.Status == DayStatus.Unknown)
                continue;

            var label = schedule.Day == today ? "Today's"
                : schedule.Day == today.AddDays(1) ? "Tomorrow's"
                : $"{schedule.Day:MMM d}";
            var what = change.IsPublished ? "outage schedule is out" : "outage schedule changed";
            var detail = schedule.Status switch
            {
                DayStatus.Emergency => "emergency outages apply, no fixed hours",
                DayStatus.NoOutages => "no outages scheduled",
                _ => ScheduleTimeline.Merge(schedule.Outages.Where(o => !o.Possible)) is { Count: > 0 } outages
                    ? string.Join(", ", outages.Select(ScheduleText.Range))
                    : "no outages scheduled"
            };
            lines.Add($"{label} {what}: {detail}.");
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }
}

/// <summary>Short text for schedules, in this PC's time zone.</summary>
public static class ScheduleText
{
    public static string Clock(DateTimeOffset moment) => moment.ToLocalTime().ToString("HH:mm");

    public static string Range(ScheduledOutage outage) => $"{Clock(outage.Start)}–{Clock(outage.End)}";

    /// <summary>"1 h 20 m" or "35 m". <paramref name="ceiling"/> rounds a partial minute up, for a countdown.</summary>
    public static string Span(TimeSpan span, bool ceiling = false)
    {
        var minutes = (int)(ceiling ? Math.Ceiling(span.TotalMinutes) : Math.Floor(span.TotalMinutes));
        minutes = Math.Max(minutes, 1);
        return minutes >= 60 ? $"{minutes / 60} h {minutes % 60} m" : $"{minutes} m";
    }

    /// <summary>A few words for the tray tooltip, or null when no outage is current or coming.</summary>
    public static string? Brief(IReadOnlyCollection<DaySchedule> days, DateTimeOffset now)
    {
        if (ScheduleTimeline.Current(days, now) is { } current)
            return $"Outage until {Clock(current.End)}";
        if (ScheduleTimeline.Next(days, now) is { } next)
            return $"Next outage {Range(next)}";
        return null;
    }

    /// <summary>The line the flyout shows: what is happening now or next.</summary>
    public static string Line(IReadOnlyCollection<DaySchedule> days, DateTimeOffset now)
    {
        if (ScheduleTimeline.Current(days, now) is { } current)
            return $"Scheduled outage until {Clock(current.End)} · {Span(current.End - now)} left";

        if (ScheduleTimeline.Next(days, now) is { } next)
        {
            var sameDay = next.Start.ToLocalTime().Date == now.ToLocalTime().Date;
            var range = sameDay ? Range(next) : $"tomorrow {Range(next)}";
            return $"Next scheduled outage {range} · in {Span(next.Start - now)}";
        }

        var today = KyivTime.DayOf(now);
        if (days.Any(d => d.Day == today && d.Status == DayStatus.Emergency))
            return "Emergency outages today, no fixed schedule";
        return days.Count == 0 ? "Waiting for the outage schedule" : "No more outages scheduled";
    }
}
