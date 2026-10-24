namespace PowerMonitor.Core.Deye;

/// <summary>
/// Holds back a state of charge that moved faster than the pack can, until the next reading confirms it.
/// </summary>
public sealed class SocJumpGuard
{
    private const double SlackPercent = 5;
    private const double PercentPerMinute = 3;

    private InverterSnapshot? _accepted;
    private InverterSnapshot? _pending;

    public void Reset()
    {
        _accepted = null;
        _pending = null;
    }

    public bool Accept(InverterSnapshot reading)
    {
        if (_accepted is null || Plausible(_accepted, reading) ||
            (_pending is not null && Plausible(_pending, reading)))
        {
            _accepted = reading;
            _pending = null;
            return true;
        }

        _pending = reading;
        return false;
    }

    private static bool Plausible(InverterSnapshot from, InverterSnapshot to)
    {
        var minutes = Math.Max(0, (to.ReadAt - from.ReadAt).TotalMinutes);
        return Math.Abs(to.SocPercent - from.SocPercent) <= SlackPercent + PercentPerMinute * minutes;
    }
}
