namespace HighAccuracyTimers;

public sealed class HighAccuracyDispatcher : Dispatcher
{
    private readonly Lock _lock = new();
    private readonly Scheduler _scheduler;
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly Dictionary<long, TickSubscriber> _subscribers = [];

    private bool _disposed;
    private bool _dispatchInProgress;
    private long _nextSubscriptionId;
    private Task? _dispatchTask;

    public HighAccuracyDispatcher(Scheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public DispatcherSubscription Subscribe(TickSubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (_lock)
        {
            ThrowIfDisposed();

            var subscriptionId = ++_nextSubscriptionId;
            _subscribers.Add(subscriptionId, subscriber);
            return new Subscription(this, subscriptionId);
        }
    }

    public async Task DispatchAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? linkedCancellationTokenSource = null;
        Task dispatchTask;

        lock (_lock)
        {
            ThrowIfDisposed();
            if (_dispatchInProgress)
            {
                throw new InvalidOperationException("Only one dispatch loop may be active at a time.");
            }

            _dispatchInProgress = true;
            linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCancellationTokenSource.Token);
            dispatchTask = DispatchCoreAsync(linkedCancellationTokenSource.Token);
            _dispatchTask = dispatchTask;
        }

        try
        {
            await dispatchTask.ConfigureAwait(false);
        }
        finally
        {
            linkedCancellationTokenSource.Dispose();

            lock (_lock)
            {
                _dispatchInProgress = false;
                if (ReferenceEquals(_dispatchTask, dispatchTask))
                {
                    _dispatchTask = null;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? dispatchTask;
        CancellationTokenSource? disposeCancellationTokenSource = null;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _subscribers.Clear();
            dispatchTask = _dispatchTask;
            disposeCancellationTokenSource = _disposeCancellationTokenSource;
        }

        disposeCancellationTokenSource.Cancel();

        if (dispatchTask is not null)
        {
            try
            {
                await dispatchTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (disposeCancellationTokenSource.IsCancellationRequested)
            {
            }
        }

        disposeCancellationTokenSource.Dispose();
    }

    private async Task DispatchCoreAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var tick = await _scheduler.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            if (tick is null)
            {
                return;
            }

            var subscribers = GetSubscriberSnapshot();
            if (subscribers.Length == 0)
            {
                continue;
            }

            var dispatchTasks = new Task[subscribers.Length];
            for (var i = 0; i < subscribers.Length; i++)
            {
                dispatchTasks[i] = InvokeSubscriberAsync(
                    subscribers[i],
                    tick.Value,
                    cancellationToken);
            }

            await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
        }
    }

    private TickSubscriber[] GetSubscriberSnapshot()
    {
        lock (_lock)
        {
            return [.. _subscribers.Values];
        }
    }

    private bool IsSubscribed(long subscriptionId)
    {
        lock (_lock)
        {
            return !_disposed && _subscribers.ContainsKey(subscriptionId);
        }
    }

    private void Unsubscribe(long subscriptionId)
    {
        lock (_lock)
        {
            _subscribers.Remove(subscriptionId);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Task InvokeSubscriberAsync(
        TickSubscriber subscriber,
        ScheduledTick tick,
        CancellationToken cancellationToken)
    {
        try
        {
            return subscriber(tick, cancellationToken).AsTask();
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class Subscription : DispatcherSubscription
    {
        private readonly HighAccuracyDispatcher _dispatcher;
        private readonly long _subscriptionId;
        private int _disposed;

        public Subscription(HighAccuracyDispatcher dispatcher, long subscriptionId)
        {
            _dispatcher = dispatcher;
            _subscriptionId = subscriptionId;
        }

        public bool IsSubscribed =>
            Volatile.Read(ref _disposed) == 0 &&
            _dispatcher.IsSubscribed(_subscriptionId);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _dispatcher.Unsubscribe(_subscriptionId);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
