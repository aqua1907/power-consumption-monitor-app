namespace PowerMonitor.Core.Presentation;

public readonly record struct PowerSample(
    DateTimeOffset At,
    int BatteryWatts,
    int LoadWatts);

public sealed class PowerHistory
{
    private readonly int _capacity;
    private readonly Queue<PowerSample> _samples;

    public PowerHistory(int capacity = 180)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity), "History needs at least two samples.");

        _capacity = capacity;
        _samples = new Queue<PowerSample>(capacity);
    }

    public IReadOnlyList<PowerSample> Samples => _samples.ToArray();
    public int Capacity => _capacity;

    public void Add(PowerSample sample)
    {
        if (_samples.Count > 0 && sample.At < _samples.Last().At)
            throw new ArgumentException("Samples must be added in chronological order.", nameof(sample));

        _samples.Enqueue(sample);
        while (_samples.Count > _capacity)
            _samples.Dequeue();
    }

    public void Clear() => _samples.Clear();
}
