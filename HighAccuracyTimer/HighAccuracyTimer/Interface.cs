namespace HighAccuracyTimers;

public sealed record SchedulerOptions
{
    public required TimeSpan Period { get; init; }

    /// <summary>
    /// How many iterations to run for.
    ///
    /// Null for indefinite or positive value.
    /// Counts scheduled slots, not delivered callbacks.
    /// Overruns will reduce the number of actual executions
    /// </summary>
    public long? StopAfterScheduledTicks { get; init; }
}

public interface HighAccuracyTimer : IDisposable
{
    ValueTask WaitAsync(TimeSpan dueIn, CancellationToken cancellationToken = default);
    ValueTask CancelAsync(CancellationToken cancellationToken = default);
}

public readonly record struct ScheduledTick(
    long Sequence,
    long SkippedCount,
    TimeSpan Period,
    TimeSpan ScheduledOffset,
    TimeSpan ActualOffset,
    TimeSpan SincePreviousDelivery,
    TimeSpan Drift,
    long? RemainingScheduledTicksAtDispatch);

public interface Scheduler : IAsyncDisposable
{
    TimeSpan Period { get; }

    bool IsRunning { get; }
    long? RemainingScheduledTicks { get; }

    void SetRemainingScheduledTicks(long? value);

    /// <summary>
    /// Actual elapses that happened
    /// </summary>
    long DeliveredTickCount { get; }

    /// <summary>
    /// Number of scheduled elapses consumed, including skipped ones.
    /// </summary>
    long ScheduledTickCount { get; }

    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask<ScheduledTick?> WaitForNextTickAsync(
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ScheduledTick> GetTicksAsync(
        CancellationToken cancellationToken = default);
}

public delegate ValueTask TickSubscriber(
    ScheduledTick tick,
    CancellationToken cancellationToken);

public interface DispatcherSubscription : IDisposable, IAsyncDisposable
{
    bool IsSubscribed { get; }
}

public interface Dispatcher : IAsyncDisposable
{
    DispatcherSubscription Subscribe(TickSubscriber subscriber);
    Task DispatchAsync(CancellationToken cancellationToken = default);
}
