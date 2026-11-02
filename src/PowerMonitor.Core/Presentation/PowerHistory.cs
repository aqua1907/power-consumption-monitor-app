namespace PowerMonitor.Core.Presentation;

public readonly record struct PowerSample(
    DateTimeOffset At,
    int BatteryWatts,
    int LoadWatts);

public sealed class PowerHistory
{
    /// <summary>Longest range the chart can show; also the default retention window.</summary>
    public static readonly TimeSpan MaxRetention = TimeSpan.FromHours(24);

    private readonly TimeSpan _maxAge;
    private readonly List<PowerSample> _samples = [];

    public PowerHistory(TimeSpan? maxAge = null)
    {
        _maxAge = maxAge ?? MaxRetention;
        if (_maxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxAge), "History needs a positive retention window.");
    }

    public IReadOnlyList<PowerSample> Samples => _samples;

    public void Add(PowerSample sample)
    {
        if (_samples.Count > 0 && sample.At < _samples[^1].At)
            throw new ArgumentException("Samples must be added in chronological order.", nameof(sample));

        _samples.Add(sample);

        var cutoff = sample.At - _maxAge;
        var staleCount = 0;
        while (staleCount < _samples.Count && _samples[staleCount].At < cutoff)
            staleCount++;
        if (staleCount > 0)
            _samples.RemoveRange(0, staleCount);
    }

    /// <summary>Returns only the samples within [now - range, now], newest-ordered input assumed.</summary>
    public IReadOnlyList<PowerSample> Window(DateTimeOffset now, TimeSpan range)
    {
        var cutoff = now - range;
        var start = 0;
        while (start < _samples.Count && _samples[start].At < cutoff)
            start++;

        return start == 0 ? _samples : _samples.GetRange(start, _samples.Count - start);
    }

    public void Clear() => _samples.Clear();
}
