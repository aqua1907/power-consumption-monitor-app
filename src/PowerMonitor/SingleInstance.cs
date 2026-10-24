namespace PowerMonitor;

internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\DeyeBackupTrayMonitor";
    private const string EventName = @"Local\DeyeBackupTrayMonitor.Show";
    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent)
    {
        _mutex = mutex;
        ShowEvent = showEvent;
    }

    public EventWaitHandle ShowEvent { get; }

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(false, MutexName);
        try
        {
            if (!mutex.WaitOne(0))
            {
                mutex.Dispose();
                SignalShow();
                return null;
            }
        }
        catch (AbandonedMutexException)
        {
        }

        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return new SingleInstance(mutex, showEvent);
    }

    public void Dispose()
    {
        ShowEvent.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
    }

    private static void SignalShow()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(EventName);
            showEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }
}
