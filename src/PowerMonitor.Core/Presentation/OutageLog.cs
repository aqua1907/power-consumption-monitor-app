namespace PowerMonitor.Core.Presentation;

public readonly record struct OutageEvent(DateTimeOffset Start, DateTimeOffset? End)
{
    public bool IsOngoing => End is null;

    public TimeSpan Duration(DateTimeOffset now) => (End ?? now) - Start;
}

/// <summary>
/// Tracks grid-loss events (start, end, duration) from a stream of grid-connected observations.
/// </summary>
public sealed class OutageLog
{
    public const int MaxEvents = 500;

    private readonly List<OutageEvent> _events;
    private bool? _lastConnected;

    public OutageLog(IEnumerable<OutageEvent>? seed = null)
    {
        _events = seed?.ToList() ?? [];
        // If the saved log ended mid-outage, pick up where it left off instead of double-counting.
        if (_events.Count > 0 && _events[^1].IsOngoing)
            _lastConnected = false;
    }

    public IReadOnlyList<OutageEvent> Events => _events;

    /// <returns>true if a new outage started or an open one closed, so the caller should persist.</returns>
    public bool Observe(bool gridConnected, DateTimeOffset at)
    {
        var changed = false;

        if (!gridConnected && _lastConnected is null or true)
        {
            _events.Add(new OutageEvent(at, null));
            changed = true;
        }
        else if (gridConnected && _lastConnected == false && _events.Count > 0 && _events[^1].IsOngoing)
        {
            _events[^1] = _events[^1] with { End = at };
            changed = true;
        }

        _lastConnected = gridConnected;

        if (_events.Count > MaxEvents)
            _events.RemoveRange(0, _events.Count - MaxEvents);

        return changed;
    }
}
