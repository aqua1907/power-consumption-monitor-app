using PowerMonitor.Core.Settings;

namespace PowerMonitor.Monitoring;

internal sealed class SettingsHolder
{
    private readonly object _gate = new();
    private AppSettings _current;

    public SettingsHolder(AppSettings current) => _current = current;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public void Update(AppSettings settings)
    {
        lock (_gate)
            _current = settings;
    }
}
