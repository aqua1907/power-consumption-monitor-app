namespace PowerMonitor.Core.Estimate;

public enum BackupKind
{
    Duration,
    Idle,
    Empty
}

public sealed record BackupEstimate(
    BackupKind Kind,
    TimeSpan? Duration,
    string Label,
    decimal RemainingWh,
    int LoadWatts)
{
    public string DurationText => Kind == BackupKind.Idle
        ? "Idle"
        : Format(Duration ?? TimeSpan.Zero);

    public string Hint
    {
        get
        {
            var energy = $"{RemainingWh / 1000m:0.0} kWh above reserve";
            return Kind == BackupKind.Idle
                ? $"Load is under 50 W. {energy}."
                : $"{Label}. {energy}.";
        }
    }

    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        var totalMinutes = (int)time.TotalMinutes;
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours <= 0 ? $"{minutes} m" : $"{hours} h {minutes:00} m";
    }
}
