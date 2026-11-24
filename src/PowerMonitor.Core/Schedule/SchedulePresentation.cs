using System.Globalization;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Core.Schedule;

/// <summary>One outage on a day's bar and in its list.</summary>
/// <param name="StartFraction">Where it starts on the day's bar, 0 at midnight and 1 at the next midnight (Kyiv).</param>
/// <param name="Note">"Now · 1 h 5 m left" or "Next · in 1 h 20 m", or null.</param>
public sealed record ScheduleSegment(
    double StartFraction,
    double EndFraction,
    bool Possible,
    bool IsCurrent,
    string Range,
    string Duration,
    string? Note);

/// <summary>One day card of the Schedule tab.</summary>
/// <param name="NowFraction">Where "now" falls on the bar. Set for today only.</param>
public sealed record ScheduleDayView(
    DateOnly Day,
    string Title,
    string Status,
    IReadOnlyList<ScheduleSegment> Segments,
    double? NowFraction,
    string Total);

/// <summary>Everything the flyout's Schedule tab shows.</summary>
/// <param name="Available">False when no schedule is selected, which disables the tab.</param>
/// <param name="Notice">A failure or "waiting" message, or null.</param>
public sealed record SchedulePresentation(
    bool Available,
    string Header,
    string Updated,
    string? Notice,
    IReadOnlyList<ScheduleDayView> Days)
{
    public static readonly SchedulePresentation Unavailable = new(false, "", "", null, []);

    /// <summary>Today and tomorrow of the saved group, as bars and lists.</summary>
    public static SchedulePresentation Create(
        AppSettings settings,
        IReadOnlyCollection<DaySchedule> days,
        DateTimeOffset now,
        string? error,
        DateTimeOffset? lastFetched)
    {
        if (!settings.ScheduleActive)
            return Unavailable;

        var header = string.IsNullOrWhiteSpace(settings.ScheduleAddressLabel)
            ? $"Group {settings.ScheduleGroup}"
            : $"{settings.ScheduleAddressLabel} · group {settings.ScheduleGroup}";
        var updated = lastFetched is { } at ? $"Updated {ScheduleText.Clock(at)}" : "";
        var notice = error is not null ? $"Could not refresh: {error}"
            : days.Count == 0 ? "Waiting for the outage schedule"
            : null;

        var today = KyivTime.DayOf(now);
        var outages = ScheduleTimeline.Outages(days, includePossible: true);
        var next = ScheduleTimeline.Next(days, now);
        var views = new[]
        {
            DayView(today, "Today", days, outages, next, now),
            DayView(today.AddDays(1), "Tomorrow", days, outages, next, now)
        };
        return new SchedulePresentation(true, header, updated, notice, views);
    }

    private static ScheduleDayView DayView(
        DateOnly day,
        string name,
        IReadOnlyCollection<DaySchedule> days,
        IReadOnlyList<ScheduledOutage> outages,
        ScheduledOutage? next,
        DateTimeOffset now)
    {
        var schedule = days.FirstOrDefault(d => d.Day == day);
        var start = KyivTime.At(day, 0);
        var end = KyivTime.At(day.AddDays(1), 0);
        var length = (end - start).TotalSeconds;

        var segments = new List<ScheduleSegment>();
        var total = TimeSpan.Zero;
        foreach (var outage in outages)
        {
            var from = outage.Start > start ? outage.Start : start;
            var to = outage.End < end ? outage.End : end;
            if (to <= from)
                continue;

            var clipped = outage with { Start = from, End = to };
            var current = !outage.Possible && outage.Start <= now && now < outage.End;
            string? note = null;
            if (current)
                note = $"Now · {ScheduleText.Span(outage.End - now)} left";
            else if (!outage.Possible && next is { } n && n.Start == outage.Start)
                note = $"Next · in {ScheduleText.Span(outage.Start - now)}";

            segments.Add(new ScheduleSegment(
                (from - start).TotalSeconds / length,
                (to - start).TotalSeconds / length,
                outage.Possible,
                current,
                ScheduleText.Range(clipped),
                outage.Possible ? "possible" : ScheduleText.Span(to - from),
                note));
            if (!outage.Possible)
                total += to - from;
        }

        var status = schedule?.Status switch
        {
            null => "Not published yet",
            DayStatus.Emergency => "Emergency outages, no fixed hours",
            DayStatus.Unknown => "Schedule unavailable",
            _ => segments.Count == 0 ? "No outages" : "Schedule applies"
        };
        var title = $"{name} · {day.ToString("ddd, MMM d", CultureInfo.CurrentCulture)}";
        double? nowFraction = day == KyivTime.DayOf(now)
            ? Math.Clamp((now - start).TotalSeconds / length, 0, 1)
            : null;
        var totalText = total > TimeSpan.Zero ? $"Off {ScheduleText.Span(total)} in total" : "";
        return new ScheduleDayView(day, title, status, segments, nowFraction, totalText);
    }
}
