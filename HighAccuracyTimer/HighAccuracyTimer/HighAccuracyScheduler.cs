using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HighAccuracyTimers;

public sealed class HighAccuracyScheduler : Scheduler
{
    private readonly Lock _lock = new();
    private readonly HighAccuracyTimer _timer;

    private bool _disposed;
    private bool _isRunning;
    private bool _waitInProgress;
    private long _startTimestamp;
    private long _deliveredTickCount;
    private long _scheduledTickCount;
    private long? _configuredRemainingScheduledTicks;
    private long? _remainingScheduledTicks;
    private TimeSpan? _previousDeliveryOffset;
    private TaskCompletionSource<bool> _waitCompletedSource = CreateCompletedWaitSource();

    public HighAccuracyScheduler(HighAccuracyTimer timer, SchedulerOptions options)
    {
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        ArgumentNullException.ThrowIfNull(options);

        if (options.Period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The period must be greater than zero.");
        }

        if (options.StopAfterScheduledTicks is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "StopAfterScheduledTicks must be null or greater than zero.");
        }

        Period = options.Period;
        _configuredRemainingScheduledTicks = options.StopAfterScheduledTicks;
        _remainingScheduledTicks = options.StopAfterScheduledTicks;
    }

    public TimeSpan Period { get; }

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

    public long? RemainingScheduledTicks
    {
        get
        {
            lock (_lock)
            {
                return _remainingScheduledTicks;
            }
        }
    }

    public long DeliveredTickCount
    {
        get
        {
            lock (_lock)
            {
                return _deliveredTickCount;
            }
        }
    }

    public long ScheduledTickCount
    {
        get
        {
            lock (_lock)
            {
                return _scheduledTickCount;
            }
        }
    }

    public void SetRemainingScheduledTicks(long? value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Remaining scheduled ticks must be null or zero or greater.");
        }

        bool shouldCancelTimer = false;
        Task? waitCompletionTask = null;

        lock (_lock)
        {
            ThrowIfDisposed();

            _configuredRemainingScheduledTicks = value;
            _remainingScheduledTicks = value;

            if (_isRunning && value == 0)
            {
                _isRunning = false;
                shouldCancelTimer = _waitInProgress;
                waitCompletionTask = _waitCompletedSource.Task;
            }
        }

        if (shouldCancelTimer)
        {
            _timer.CancelAsync().GetAwaiter().GetResult();
            waitCompletionTask!.GetAwaiter().GetResult();
        }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            ThrowIfDisposed();
            if (_isRunning)
            {
                throw new InvalidOperationException("The scheduler is already running.");
            }

            if (_waitInProgress)
            {
                throw new InvalidOperationException("The scheduler cannot be started while a wait is in progress.");
            }

            _isRunning = _configuredRemainingScheduledTicks != 0;
            _startTimestamp = Stopwatch.GetTimestamp();
            _deliveredTickCount = 0;
            _scheduledTickCount = 0;
            _remainingScheduledTicks = _configuredRemainingScheduledTicks;
            _previousDeliveryOffset = null;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool shouldCancelTimer;
        Task waitCompletionTask;
        lock (_lock)
        {
            ThrowIfDisposed();
            _isRunning = false;
            shouldCancelTimer = _waitInProgress;
            waitCompletionTask = _waitCompletedSource.Task;
        }

        if (shouldCancelTimer)
        {
            await _timer.CancelAsync(cancellationToken).ConfigureAwait(false);
            await waitCompletionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
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

            await _timer.WaitAsync(nextTick.Value.DueIn, cancellationToken).ConfigureAwait(false);

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
        await StopAsync();

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
            _waitCompletedSource = CreatePendingWaitSource();
        }
    }

    private void EndWait()
    {
        lock (_lock)
        {
            _waitInProgress = false;
            _waitCompletedSource.TrySetResult(true);
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

            if (_remainingScheduledTicks == 0)
            {
                _isRunning = false;
                return null;
            }

            var elapsedTicks = Stopwatch.GetElapsedTime(_startTimestamp).Ticks;
            var latestDueSequence = Math.Max(1L, elapsedTicks / Period.Ticks);
            var nextSequence = Math.Max(_scheduledTickCount + 1, latestDueSequence);

            if (_remainingScheduledTicks is long remaining)
            {
                var maxSequence = checked(_scheduledTickCount + remaining);
                if (nextSequence > maxSequence)
                {
                    _scheduledTickCount = maxSequence;
                    _remainingScheduledTicks = 0;
                    _isRunning = false;
                    return null;
                }
            }

            var skippedCount = nextSequence - (_scheduledTickCount + 1);
            var scheduledOffsetTicks = checked(nextSequence * Period.Ticks);
            var dueInTicks = scheduledOffsetTicks - elapsedTicks;

            return new PlannedTick(
                Sequence: nextSequence,
                SkippedCount: skippedCount,
                ScheduledOffset: TimeSpan.FromTicks(scheduledOffsetTicks),
                DueIn: dueInTicks > 0 ? TimeSpan.FromTicks(dueInTicks) : TimeSpan.Zero);
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

            _scheduledTickCount = sequence;
            _deliveredTickCount++;
            _previousDeliveryOffset = actualOffset;

            long? remainingScheduledTicksAtDispatch = null;
            if (_remainingScheduledTicks is long remaining)
            {
                var consumedScheduledTicks = skippedCount + 1;
                var updatedRemaining = remaining - consumedScheduledTicks;
                _remainingScheduledTicks = updatedRemaining;
                remainingScheduledTicksAtDispatch = updatedRemaining;

                if (updatedRemaining == 0)
                {
                    _isRunning = false;
                }
            }

            return new ScheduledTick(
                Sequence: sequence,
                SkippedCount: skippedCount,
                Period: Period,
                ScheduledOffset: scheduledOffset,
                ActualOffset: actualOffset,
                SincePreviousDelivery: sincePreviousDelivery,
                Drift: actualOffset - scheduledOffset,
                RemainingScheduledTicksAtDispatch: remainingScheduledTicksAtDispatch);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static TaskCompletionSource<bool> CreateCompletedWaitSource()
    {
        var source = CreatePendingWaitSource();
        source.SetResult(true);
        return source;
    }

    private static TaskCompletionSource<bool> CreatePendingWaitSource()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly record struct PlannedTick(
        long Sequence,
        long SkippedCount,
        TimeSpan ScheduledOffset,
        TimeSpan DueIn);
}
