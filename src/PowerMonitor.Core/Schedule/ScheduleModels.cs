namespace PowerMonitor.Core.Schedule;

/// <summary>Kyiv wall-clock helpers. Schedules are written in Kyiv time, whatever zone this PC uses.</summary>
public static class KyivTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    /// <summary>The instant <paramref name="minutes"/> after midnight of <paramref name="day"/> on the Kyiv wall clock (1440 is the next midnight).</summary>
    public static DateTimeOffset At(DateOnly day, int minutes)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddMinutes(minutes);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }

    public static DateOnly DayOf(DateTimeOffset moment) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, Zone).DateTime);

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "FLE Standard Time", "Europe/Kyiv", "Europe/Kiev" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}

/// <summary>A power-off interval from a schedule. <see cref="Possible"/> means the operator only says it may happen.</summary>
public readonly record struct ScheduledOutage(DateTimeOffset Start, DateTimeOffset End, bool Possible = false)
{
    public TimeSpan Duration => End - Start;
}

public enum DayStatus
{
    /// <summary>Hourly schedule applies; <see cref="DaySchedule.Outages"/> lists the outages.</summary>
    Applies,
    NoOutages,
    /// <summary>Emergency outages replace the schedule, so the hours are not known.</summary>
    Emergency,
    Unknown
}

/// <summary>The schedule an operator published for one calendar day, for the group of the user's address.</summary>
public sealed record DaySchedule(
    DateOnly Day,
    DateTimeOffset? UpdatedAt,
    DayStatus Status,
    IReadOnlyList<ScheduledOutage> Outages)
{
    /// <summary>Same content, ignoring when the operator stamped it. Used to detect a changed schedule.</summary>
    public bool SameAs(DaySchedule other) =>
        Day == other.Day && Status == other.Status && Outages.SequenceEqual(other.Outages);
}

/// <summary>The source answered, but not in a form we trust, or did not answer.</summary>
public sealed class ScheduleSourceException : Exception
{
    public ScheduleSourceException(string message)
        : base(message)
    {
    }

    public ScheduleSourceException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public readonly record struct AddressStep(string Prompt, string Example, bool IsHouse);

/// <summary>One answer to an address step. Both fields come from the operator's directory and are untrusted.</summary>
public readonly record struct AddressChoice(string Id, string Label);

public interface IScheduleProvider
{
    /// <summary>Days the operator has published for <paramref name="group"/>, today and tomorrow at most.</summary>
    Task<IReadOnlyList<DaySchedule>> FetchAsync(string group, CancellationToken cancellationToken);
}

public interface IAddressLookup
{
    /// <summary>Steps of an address search, for example street then house.</summary>
    IReadOnlyList<AddressStep> Steps { get; }

    /// <summary>Choices for step <c>path.Count</c> matching <paramref name="query"/>, best first.</summary>
    Task<IReadOnlyList<AddressChoice>> OptionsAsync(
        IReadOnlyList<string> path,
        string query,
        CancellationToken cancellationToken);

    /// <summary>The group (for example "19.1") of the address named by <paramref name="path"/>, or null when it is in none.</summary>
    Task<string?> ResolveGroupAsync(IReadOnlyList<string> path, CancellationToken cancellationToken);
}

/// <summary>An operator whose schedule the app can read.</summary>
public sealed record ScheduleOperator(
    string Id,
    string Name,
    Func<ScheduleHttp, IScheduleProvider> CreateProvider,
    Func<ScheduleHttp, IAddressLookup> CreateLookup)
{
    public const string DefaultId = "dtek-kem";

    public static IReadOnlyList<ScheduleOperator> All { get; } =
    [
        new(
            DefaultId,
            "Kyiv city (DTEK KEM)",
            http => new YasnoProvider(http, YasnoProvider.KyivRegion, YasnoProvider.KyivDso),
            http => new YasnoAddressLookup(http, YasnoProvider.KyivRegion, YasnoProvider.KyivDso))
    ];

    public static ScheduleOperator? Find(string? id) =>
        All.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
}
