using HighAccuracyTimers;
using System.Diagnostics;

#pragma warning disable CA1859

var programStart = Stopwatch.GetTimestamp();
using System.Timers.Timer timer = new(TimeSpan.FromSeconds(1))
{
    Enabled = true,
};
timer.Elapsed += (_, _) => Console.WriteLine($"Running for: {Stopwatch.GetElapsedTime(programStart).TotalSeconds:00}s");

await RunTimerComparison();
await RunMoreComplexSample();

async Task RunMoreComplexSample()
{
    List<string> log = [];
    Console.WriteLine("Running complex sample (35s)");

    await using var scheduler = new HighAccuracyScheduler(
        new HighAccuracyWindowsTimer(),
        new SchedulerOptions
        {
            Period = TimeSpan.FromSeconds(1),
            StopAfterScheduledTicks = 4,
        });

    await using var dispatcher = new HighAccuracyDispatcher(scheduler);

    using var sub1 = dispatcher.Subscribe(async (tick, ct) =>
    {
        await Task.Delay(200, ct);
        AddLog($"Short: {tick}");
    });

    using var sub2 = dispatcher.Subscribe(async (tick, ct) =>
    {
        await Task.Delay(500, ct);
        AddLog($"Normal: {tick}");
        // throw new Exception("Test"); // Uncomment to see exceptions bubble through
    });

    await scheduler.StartAsync();
    var dispatchingTask = dispatcher.DispatchAsync();
    try
    {
        // To see exception, need to await main task but also want to do the delay
        await Task.WhenAny([dispatchingTask, Task.Delay(TimeSpan.FromSeconds(2))]);

        scheduler.SetRemainingScheduledTicks(8);

        await WaitForDelayOrDispatchAsync(dispatchingTask, TimeSpan.FromSeconds(3));

        await sub1.DisposeAsync();

        using var sub3 = dispatcher.Subscribe(async (tick, ct) =>
        {
            // Will eventually cause 2 cycles to be skipped, est (20s)
            await Task.Delay(2100, ct);
            AddLog($"Overrun causer: {tick}");
        });
        scheduler.SetRemainingScheduledTicks(null);

        await WaitForDelayOrDispatchAsync(dispatchingTask, TimeSpan.FromSeconds(30));
    }
    finally
    {
        if (scheduler.IsRunning)
        {
            await scheduler.StopAsync();
        }

        if (!dispatchingTask.IsCompleted)
        {
            await dispatchingTask;
        }
    }

    Console.WriteLine("Finished running complex sample");

    File.WriteAllLines("Complex Test.log", log);

    void AddLog(string s)
    {
        log.Add(s);
        Console.WriteLine(s);
    }
}

async Task RunTimerComparison()
{
    Console.WriteLine("Running timer comparison for 20s");
    List<string> fiftyMsHighAccuracyTimerExecutions = ["50ms High Accuracy Timer:"];
    List<string> fiftyMsPeriodicTimerExecutions = ["50ms Periodic Timer:"];
    List<string> fiftyMsTimersTimerExecutions = ["50ms Timers Timer:"];
    List<string> fiftyMsThreadingTimerExecutions = ["50ms Threading Timer:"];
    List<string> sixtyFpsHighAccuracyTimerExecutions = ["60Hz High Accuracy Timer:"];
    List<string> sixtyFpsPeriodicTimerExecutions = ["60Hz Periodic Timer:"];
    List<string> sixtyFpsTimersTimerExecutions = ["60Hz Timers Timer:"];
    List<string> sixtyFpsThreadingTimerExecutions = ["60Hz Threading Timer:"];
    List<string> oneSecondHighAccuracyTimerExecutions = ["1s High Accuracy Timer:"];
    List<string> oneSecondPeriodicTimerExecutions = ["1s Periodic Timer:"];
    List<string> oneSecondTimersTimerExecutions = ["1s Timers Timer:"];
    List<string> oneSecondThreadingTimerExecutions = ["1s Threading Timer:"];
    List<string> oneMsHighAccuracyTimerExecutions = ["1ms High Accuracy Timer:"];
    List<string> oneMsPeriodicTimerExecutions = ["1ms Periodic Timer:"];
    List<string> oneMsTimersTimerExecutions = ["1ms Timers Timer:"];
    List<string> oneMsThreadingTimerExecutions = ["1ms Threading Timer:"];

    List<string>[] allLogs =
    [
        fiftyMsHighAccuracyTimerExecutions,
        fiftyMsPeriodicTimerExecutions,
        fiftyMsTimersTimerExecutions,
        fiftyMsThreadingTimerExecutions,
        sixtyFpsHighAccuracyTimerExecutions,
        sixtyFpsPeriodicTimerExecutions,
        sixtyFpsTimersTimerExecutions,
        sixtyFpsThreadingTimerExecutions,
        oneSecondHighAccuracyTimerExecutions,
        oneSecondPeriodicTimerExecutions,
        oneSecondTimersTimerExecutions,
        oneSecondThreadingTimerExecutions,
        oneMsHighAccuracyTimerExecutions,
        oneMsPeriodicTimerExecutions,
        oneMsTimersTimerExecutions,
        oneMsThreadingTimerExecutions,
    ];

    long startTime = 0;
    TimeSpan fiftyMsRate = TimeSpan.FromMilliseconds(50);
    TimeSpan sixtyFpsRate = TimeSpanUtilties.FromHz(60);
    TimeSpan oneSecondRate = TimeSpan.FromSeconds(1);
    TimeSpan oneMsRate = TimeSpan.FromMilliseconds(1);

    Dictionary<List<string>, TimerLogState> logStates = new()
    {
        [fiftyMsHighAccuracyTimerExecutions] = new(fiftyMsRate),
        [fiftyMsPeriodicTimerExecutions] = new(fiftyMsRate),
        [fiftyMsTimersTimerExecutions] = new(fiftyMsRate),
        [fiftyMsThreadingTimerExecutions] = new(fiftyMsRate),
        [sixtyFpsHighAccuracyTimerExecutions] = new(sixtyFpsRate),
        [sixtyFpsPeriodicTimerExecutions] = new(sixtyFpsRate),
        [sixtyFpsTimersTimerExecutions] = new(sixtyFpsRate),
        [sixtyFpsThreadingTimerExecutions] = new(sixtyFpsRate),
        [oneSecondHighAccuracyTimerExecutions] = new(oneSecondRate),
        [oneSecondPeriodicTimerExecutions] = new(oneSecondRate),
        [oneSecondTimersTimerExecutions] = new(oneSecondRate),
        [oneSecondThreadingTimerExecutions] = new(oneSecondRate),
        [oneMsHighAccuracyTimerExecutions] = new(oneMsRate),
        [oneMsPeriodicTimerExecutions] = new(oneMsRate),
        [oneMsTimersTimerExecutions] = new(oneMsRate),
        [oneMsThreadingTimerExecutions] = new(oneMsRate),
    };

    await using var fiftyMsHighAccuracyScheduler = CreateScheduler(fiftyMsRate);
    await using var sixtyFpsHighAccuracyScheduler = CreateScheduler(sixtyFpsRate, 600);
    await using var oneSecondHighAccuracyScheduler = CreateScheduler(oneSecondRate, 10);
    await using var oneMsHighAccuracyScheduler = CreateScheduler(oneMsRate, 2000);

    using var fiftyMsTimersTimer = CreateTimersTimer(fiftyMsRate, fiftyMsTimersTimerExecutions);
    using var sixtyFpsTimersTimer = CreateTimersTimer(sixtyFpsRate, sixtyFpsTimersTimerExecutions);
    using var oneSecondTimersTimer = CreateTimersTimer(oneSecondRate, oneSecondTimersTimerExecutions);
    using var oneMsTimersTimer = CreateTimersTimer(oneMsRate, oneMsTimersTimerExecutions);

    await using var fiftyMsThreadingTimer = CreateThreadingTimer(fiftyMsRate, fiftyMsThreadingTimerExecutions);
    await using var sixtyFpsThreadingTimer = CreateThreadingTimer(sixtyFpsRate, sixtyFpsThreadingTimerExecutions);
    await using var oneSecondThreadingTimer = CreateThreadingTimer(oneSecondRate, oneSecondThreadingTimerExecutions);
    await using var oneMsThreadingTimer = CreateThreadingTimer(oneMsRate, oneMsThreadingTimerExecutions);

    using CancellationTokenSource periodicTimerCancellationTokenSource = new();

    startTime = Stopwatch.GetTimestamp();

    Task[] periodicTimerTasks =
    [
        RunPeriodicTimerAsync(fiftyMsRate, fiftyMsPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
        RunPeriodicTimerAsync(sixtyFpsRate, sixtyFpsPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
        RunPeriodicTimerAsync(oneSecondRate, oneSecondPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
        RunPeriodicTimerAsync(oneMsRate, oneMsPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
    ];

    await fiftyMsHighAccuracyScheduler.StartAsync();
    await sixtyFpsHighAccuracyScheduler.StartAsync();
    await oneSecondHighAccuracyScheduler.StartAsync();
    await oneMsHighAccuracyScheduler.StartAsync();

    Task[] highAccuracySchedulerTasks =
    [
        RunSchedulerAsync(fiftyMsHighAccuracyScheduler, fiftyMsHighAccuracyTimerExecutions),
        RunSchedulerAsync(sixtyFpsHighAccuracyScheduler, sixtyFpsHighAccuracyTimerExecutions),
        RunSchedulerAsync(oneSecondHighAccuracyScheduler, oneSecondHighAccuracyTimerExecutions),
        RunSchedulerAsync(oneMsHighAccuracyScheduler, oneMsHighAccuracyTimerExecutions),
    ];

    fiftyMsTimersTimer.Start();
    sixtyFpsTimersTimer.Start();
    oneSecondTimersTimer.Start();
    oneMsTimersTimer.Start();

    StartThreadingTimer(fiftyMsThreadingTimer, fiftyMsRate);
    StartThreadingTimer(sixtyFpsThreadingTimer, sixtyFpsRate);
    StartThreadingTimer(oneSecondThreadingTimer, oneSecondRate);
    StartThreadingTimer(oneMsThreadingTimer, oneMsRate);

    await Task.Delay(20100);

    await fiftyMsHighAccuracyScheduler.StopAsync();
    await sixtyFpsHighAccuracyScheduler.StopAsync();
    await oneSecondHighAccuracyScheduler.StopAsync();
    await oneMsHighAccuracyScheduler.StopAsync();

    fiftyMsTimersTimer.Stop();
    sixtyFpsTimersTimer.Stop();
    oneSecondTimersTimer.Stop();
    oneMsTimersTimer.Stop();

    StopThreadingTimer(fiftyMsThreadingTimer);
    StopThreadingTimer(sixtyFpsThreadingTimer);
    StopThreadingTimer(oneSecondThreadingTimer);
    StopThreadingTimer(oneMsThreadingTimer);

    periodicTimerCancellationTokenSource.Cancel();
    await Task.WhenAll(highAccuracySchedulerTasks);
    await Task.WhenAll(periodicTimerTasks);

    foreach (var logs in allLogs)
    {
        WriteLogFile(logs);
    }
    Console.WriteLine("Timer comparison finished. See logs for metrics");

    void LogEvent(List<string> logs, int remainingScheduledTicks)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTime);
        var logState = logStates[logs];
        lock (logs)
        {
            var elapsedTicks = elapsed.Ticks;
            var timeSinceLastLog = logState.ExecutionCount == 0
                ? elapsed
                : TimeSpan.FromTicks(elapsedTicks - logState.PreviousElapsedTicks);
            logState.ExecutionCount++;
            logState.PreviousElapsedTicks = elapsedTicks;

            var targetElapsed = TimeSpan.FromTicks(logState.ExecutionCount * logState.IntervalTicks);
            var totalDrift = elapsed - targetElapsed;

            logs.Add($"* Timestamp: {FormatElapsedForLog(elapsed)} - Since Last: {FormatElapsedForLog(timeSinceLastLog)} - Target Time: {FormatElapsedForLog(targetElapsed)} - Total Drift: {FormatElapsedForLog(totalDrift)} - Remaining Scheduled Ticks: {remainingScheduledTicks}");
        }
    }

    void LogSchedulerEvent(List<string> logs, ScheduledTick tick)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTime);
        var logState = logStates[logs];
        lock (logs)
        {
            var elapsedTicks = elapsed.Ticks;
            var timeSinceLastLog = logState.ExecutionCount == 0
                ? elapsed
                : TimeSpan.FromTicks(elapsedTicks - logState.PreviousElapsedTicks);
            logState.ExecutionCount++;
            logState.PreviousElapsedTicks = elapsedTicks;

            var targetElapsed = TimeSpan.FromTicks(logState.ExecutionCount * logState.IntervalTicks);
            var totalDrift = elapsed - targetElapsed;
            var remainingScheduledTicks = tick.RemainingScheduledTicksAtDispatch is long remaining ? remaining.ToString() : "Infinite";

            logs.Add(
                $"* Timestamp: {FormatElapsedForLog(elapsed)} - Since Last: {FormatElapsedForLog(timeSinceLastLog)} - Target Time: {FormatElapsedForLog(targetElapsed)} - Total Drift: {FormatElapsedForLog(totalDrift)}" +
                $" - Tick Sequence: {tick.Sequence}" +
                $" - Tick Skipped: {tick.SkippedCount}" +
                $" - Tick Scheduled Offset: {FormatElapsedForLog(tick.ScheduledOffset)}" +
                $" - Tick Actual Offset: {FormatElapsedForLog(tick.ActualOffset)}" +
                $" - Tick Since Previous Delivery: {FormatElapsedForLog(tick.SincePreviousDelivery)}" +
                $" - Tick Drift: {FormatElapsedForLog(tick.Drift)}" +
                $" - Remaining Scheduled Ticks: {remainingScheduledTicks}");
        }
    }

    System.Timers.Timer CreateTimersTimer(TimeSpan interval, List<string> logs)
    {
        var timer = new System.Timers.Timer(interval)
        {
            AutoReset = true,
            Enabled = false,
        };
        timer.Elapsed += (sender, args) => { LogEvent(logs, -1); };
        return timer;
    }

    System.Threading.Timer CreateThreadingTimer(TimeSpan interval, List<string> logs)
    {
        return new System.Threading.Timer(
            callback: _ => { LogEvent(logs, -1); },
            state: null,
            dueTime: Timeout.InfiniteTimeSpan,
            period: interval);
    }

    async Task RunPeriodicTimerAsync(TimeSpan interval, List<string> logs, CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                LogEvent(logs, -1);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    async Task RunSchedulerAsync(Scheduler scheduler, List<string> logs)
    {
        await foreach (var tick in scheduler.GetTicksAsync())
        {
            LogSchedulerEvent(logs, tick);
        }
    }

    void WriteLogFile(List<string> logs)
    {
        string[] snapshot;
        lock (logs)
        {
            snapshot = [.. logs];
        }

        var filePath = $"{snapshot[0].Replace(":", "")}.log";
        File.WriteAllLines(filePath, snapshot);
    }

    void StartThreadingTimer(System.Threading.Timer timer, TimeSpan interval)
    {
        timer.Change(interval, interval);
    }

    void StopThreadingTimer(System.Threading.Timer timer)
    {
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    static Scheduler CreateScheduler(TimeSpan period, long? scheduledTicks = null)
    {
        return new HighAccuracyScheduler(
            timer: new HighAccuracyWindowsTimer(),
            options: new SchedulerOptions
            {
                Period = period,
                StopAfterScheduledTicks = scheduledTicks,
            });
    }
}

static async Task WaitForDelayOrDispatchAsync(Task dispatchTask, TimeSpan delay, CancellationToken cancellationToken = default)
{
    var delayTask = Task.Delay(delay, cancellationToken);
    var completedTask = await Task.WhenAny(dispatchTask, delayTask);
    await completedTask;
}

string FormatElapsedForLog(TimeSpan elapsed)
{
    var sign = elapsed.Ticks < 0 ? "-" : "";
    var absoluteTicks = Math.Abs(elapsed.Ticks);
    var totalSeconds = absoluteTicks / TimeSpan.TicksPerSecond;
    var milliseconds = (absoluteTicks % TimeSpan.TicksPerSecond) / TimeSpan.TicksPerMillisecond;
    var subMillisecondTicks = absoluteTicks % TimeSpan.TicksPerMillisecond;
    return $"{sign}{totalSeconds:00}.{milliseconds:000}_{subMillisecondTicks:0000}";
}

sealed class TimerLogState(TimeSpan interval)
{
    public long IntervalTicks { get; } = interval.Ticks;
    public int ExecutionCount { get; set; }
    public long PreviousElapsedTicks { get; set; }
}
