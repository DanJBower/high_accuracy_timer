namespace HighAccuracyTimers;

public static class TimeSpanUtilties
{
    public static TimeSpan FromHz(double hz)
    {
        return TimeSpan.FromSeconds(1.0 / hz);
    }
}

public class HighAccuracyTimerEventArgs : EventArgs
{
    public TimeSpan TimeStamp { get; }
    public int RemainingExecutions { get; }
}


public interface HighAccuracyTimer : IDisposable
{
    int StopAfter { get; set; }
    TimeSpan Rate { get; set; }
    event EventHandler<HighAccuracyTimerEventArgs> Elapsed;

    void Start();
    void Stop();
}

public class HighAccuracyWindowsTimer : HighAccuracyTimer
{
    private readonly Lock _lock = new();

    public HighAccuracyWindowsTimer(bool AutoStart = true)
    {
        Start();
    }

    public int StopAfter
    {
        get;
        set
        {
            lock (_lock)
            {
                field = value;
                _remainingExecutions = value;
            }
        }
    }

    public TimeSpan Rate
    {
        get;
        set
        {
            lock (_lock)
            {
                field = value;
            }
        }
    }

    public event EventHandler<HighAccuracyTimerEventArgs> Elapsed;

    private int _remainingExecutions;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
    }

    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        Stop();

        if (disposing)
        {
            // TODO: dispose managed state (managed objects)
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        _disposedValue = true;
    }

    ~HighAccuracyWindowsTimer()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
