namespace PowerMonitor.Core.Presentation;

public readonly record struct OutageEvent(
    DateTimeOffset Start,
    DateTimeOffset? End,
    bool StartKnown = true,
    bool EndKnown = true)
{
    public bool IsOngoing => End is null;

    public TimeSpan Duration(DateTimeOffset now) => (End ?? now) - Start;

    /// <summary>
    /// How long to show. The span is the recorded start through the recorded end, including when
    /// both times are only bounds. "Over" means it is still going and began while this PC was off.
    /// "Under" means it ended while this PC was off, so it is shorter than the span.
    /// </summary>
    public string DurationLabel(DateTimeOffset now)
    {
        var span = Duration(now);
        var text = span.TotalHours >= 1
            ? $"{(int)span.TotalHours} h {span.Minutes} m"
            : $"{Math.Max(1, (int)span.TotalMinutes)} m";
        if (End is null && !StartKnown)
            return $"Over {text}";
        if (End is not null && StartKnown && !EndKnown)
            return $"Under {text}";
        return text;
    }
}

/// <summary>
/// Tracks grid-loss events (start, end, duration) from a stream of grid-connected observations.
/// </summary>
public sealed class OutageLog
{
    public const int MaxEvents = 500;

    private readonly List<OutageEvent> _events;
    private bool? _lastConnected;
    private DateTimeOffset? _lastAt;

    public OutageLog(
        IEnumerable<OutageEvent>? seed = null,
        DateTimeOffset? lastSeenAt = null,
        bool? lastGridConnected = null)
    {
        _events = seed?.ToList() ?? [];
        _lastAt = lastSeenAt;
        _lastConnected = lastGridConnected;
        // A log saved by an older version has no cursor. The last event is the best bound we have.
        if (_lastConnected is null && _events.Count > 0)
        {
            var last = _events[^1];
            if (last.IsOngoing)
            {
                _lastConnected = false;
                _lastAt ??= last.Start;
            }
            else if (last.End is { } end)
            {
                _lastConnected = true;
                _lastAt ??= end;
            }
        }
    }

    public IReadOnlyList<OutageEvent> Events => _events;

    public DateTimeOffset? LastSeenAt => _lastAt;

    public bool? LastGridConnected => _lastConnected;

    /// <returns>true if a new outage started or an open one closed, so the caller should refresh the list.</returns>
    public bool Observe(bool gridConnected, DateTimeOffset at, TimeSpan? pollInterval = null)
    {
        if (_lastAt is { } previous && at < previous)
            return false;

        var unwatched = _lastAt is { } seen && at - seen > UnwatchedAfter(pollInterval ?? TimeSpan.FromSeconds(10));
        var changed = false;

        if (!gridConnected && _lastConnected is null or true)
        {
            var sawTheDrop = _lastConnected == true && !unwatched;
            _events.Add(new OutageEvent(at, null, StartKnown: sawTheDrop));
            changed = true;
        }
        else if (gridConnected && _lastConnected == false && _events.Count > 0 && _events[^1].IsOngoing)
        {
            _events[^1] = _events[^1] with { End = at, EndKnown = !unwatched };
            changed = true;
        }

        _lastConnected = gridConnected;
        _lastAt = at;

        if (_events.Count > MaxEvents)
            _events.RemoveRange(0, _events.Count - MaxEvents);

        return changed;
    }

    /// <summary>
    /// A gap longer than this means the app was not watching, so a grid change in between
    /// did not happen at the moment of the next reading.
    /// </summary>
    public static TimeSpan UnwatchedAfter(TimeSpan pollInterval)
    {
        var scaled = pollInterval * 3;
        var floor = TimeSpan.FromMinutes(1);
        return scaled > floor ? scaled : floor;
    }
}
