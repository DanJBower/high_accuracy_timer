using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HighAccuracyTimers;

public sealed class HighAccuracyBasicScheduler : BasicScheduler
{
    private readonly Lock _lock = new();
    private readonly HighAccuracyTimer _timer;

    private bool _disposed;
    private bool _isRunning;
    private bool _waitInProgress;
    private long _sequence;
    private long _startTimestamp;
    private TimeSpan? _previousDeliveryOffset;

    public HighAccuracyBasicScheduler(HighAccuracyTimer timer, BasicSchedulerOptions options)
    {
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        ArgumentNullException.ThrowIfNull(options);

        if (options.Period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The period must be greater than zero.");
        }

        Period = options.Period;
        AutoReset = options.AutoReset;
    }

    public TimeSpan Period { get; }
    public bool AutoReset { get; }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            ThrowIfDisposed();
            if (_waitInProgress)
            {
                throw new InvalidOperationException("The scheduler cannot be started while a wait is in progress.");
            }

            _isRunning = true;
            _sequence = 0;
            _startTimestamp = Stopwatch.GetTimestamp();
            _previousDeliveryOffset = null;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask CancelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool shouldCancelTimer;
        lock (_lock)
        {
            ThrowIfDisposed();
            _isRunning = false;
            shouldCancelTimer = _waitInProgress;
        }

        if (shouldCancelTimer)
        {
            await _timer.CancelAsync(cancellationToken);
        }
    }

    public async ValueTask<ScheduledTick?> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeginWait();

        try
        {
            var nextTick = PlanNextTick();
            if (nextTick is null)
            {
                return null;
            }

            await _timer.WaitAsync(nextTick.Value.DueIn, cancellationToken);

            return CompleteWait(
                sequence: nextTick.Value.Sequence,
                skippedCount: nextTick.Value.SkippedCount,
                scheduledOffset: nextTick.Value.ScheduledOffset);
        }
        finally
        {
            EndWait();
        }
    }

    public async IAsyncEnumerable<ScheduledTick> GetTicksAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var tick = await WaitForNextTickAsync(cancellationToken);
            if (tick is null)
            {
                yield break;
            }

            yield return tick.Value;
        }
    }

    public ValueTask DisposeAsync()
    {
        bool alreadyDisposed;
        lock (_lock)
        {
            alreadyDisposed = _disposed;
        }

        if (alreadyDisposed)
        {
            return ValueTask.CompletedTask;
        }

        return DisposeCoreAsync();
    }

    private async ValueTask DisposeCoreAsync()
    {
        await CancelAsync();

        lock (_lock)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }

    private void BeginWait()
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            if (_waitInProgress)
            {
                throw new InvalidOperationException("Only one wait may be active on the scheduler at a time.");
            }

            _waitInProgress = true;
        }
    }

    private void EndWait()
    {
        lock (_lock)
        {
            _waitInProgress = false;
        }
    }

    private PlannedTick? PlanNextTick()
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            if (!_isRunning)
            {
                return null;
            }

            var elapsedTicks = Stopwatch.GetElapsedTime(_startTimestamp).Ticks;
            var latestDueSequence = Math.Max(1, elapsedTicks / Period.Ticks);
            var sequence = Math.Max(_sequence + 1, latestDueSequence);
            var skippedCount = sequence - (_sequence + 1);
            var scheduledOffset = TimeSpan.FromTicks(sequence * Period.Ticks);
            var dueIn = scheduledOffset - TimeSpan.FromTicks(elapsedTicks);

            return new PlannedTick(
                Sequence: sequence,
                SkippedCount: skippedCount,
                ScheduledOffset: scheduledOffset,
                DueIn: dueIn > TimeSpan.Zero ? dueIn : TimeSpan.Zero);
        }
    }

    private ScheduledTick? CompleteWait(long sequence, long skippedCount, TimeSpan scheduledOffset)
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                return null;
            }

            var actualOffset = Stopwatch.GetElapsedTime(_startTimestamp);
            var sincePreviousDelivery = _previousDeliveryOffset is null
                ? actualOffset
                : actualOffset - _previousDeliveryOffset.Value;

            _sequence = sequence;
            _previousDeliveryOffset = actualOffset;

            if (!AutoReset)
            {
                _isRunning = false;
            }

            return new ScheduledTick(
                Sequence: sequence,
                SkippedCount: skippedCount,
                Period: Period,
                ScheduledOffset: scheduledOffset,
                ActualOffset: actualOffset,
                SincePreviousDelivery: sincePreviousDelivery,
                Drift: actualOffset - scheduledOffset,
                RemainingScheduledTicksAtDispatch: null);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private readonly record struct PlannedTick(
        long Sequence,
        long SkippedCount,
        TimeSpan ScheduledOffset,
        TimeSpan DueIn);
}
