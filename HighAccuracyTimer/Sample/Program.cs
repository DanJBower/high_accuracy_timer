
using HighAccuracyTimers;
using System.Diagnostics;

#pragma warning disable CA1859

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

using HighAccuracyTimer fiftyMsHighAccuracyTimer = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = fiftyMsRate,
};
fiftyMsHighAccuracyTimer.Elapsed += (sender, args) => { LogEvent(fiftyMsHighAccuracyTimerExecutions, args.RemainingExecutions); };

using HighAccuracyTimer sixtyFpsHighAccuracyTimer = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = sixtyFpsRate,
    StopAfter = 600,
};
sixtyFpsHighAccuracyTimer.Elapsed += (sender, args) => { LogEvent(sixtyFpsHighAccuracyTimerExecutions, args.RemainingExecutions); };

using HighAccuracyTimer oneSecondHighAccuracyTimer = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = oneSecondRate,
};
oneSecondHighAccuracyTimer.Elapsed += (sender, args) => { LogEvent(oneSecondHighAccuracyTimerExecutions, args.RemainingExecutions); };
oneSecondHighAccuracyTimer.Elapsed += (sender, args) => { Console.WriteLine($"Running time: {args.TimeStamp}"); };

using HighAccuracyTimer oneMsHighAccuracyTimer = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = oneMsRate,
    StopAfter = 2000,
};
oneMsHighAccuracyTimer.Elapsed += (sender, args) => { LogEvent(oneMsHighAccuracyTimerExecutions, args.RemainingExecutions); };

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

fiftyMsHighAccuracyTimer.Start();
sixtyFpsHighAccuracyTimer.Start();
oneSecondHighAccuracyTimer.Start();
oneMsHighAccuracyTimer.Start();

fiftyMsTimersTimer.Start();
sixtyFpsTimersTimer.Start();
oneSecondTimersTimer.Start();
oneMsTimersTimer.Start();

StartThreadingTimer(fiftyMsThreadingTimer, fiftyMsRate);
StartThreadingTimer(sixtyFpsThreadingTimer, sixtyFpsRate);
StartThreadingTimer(oneSecondThreadingTimer, oneSecondRate);
StartThreadingTimer(oneMsThreadingTimer, oneMsRate);

await Task.Delay(20100);

fiftyMsHighAccuracyTimer.Stop();
sixtyFpsHighAccuracyTimer.Stop();
oneSecondHighAccuracyTimer.Stop();
oneMsHighAccuracyTimer.Stop();

fiftyMsTimersTimer.Stop();
sixtyFpsTimersTimer.Stop();
oneSecondTimersTimer.Stop();
oneMsTimersTimer.Stop();

StopThreadingTimer(fiftyMsThreadingTimer);
StopThreadingTimer(sixtyFpsThreadingTimer);
StopThreadingTimer(oneSecondThreadingTimer);
StopThreadingTimer(oneMsThreadingTimer);

periodicTimerCancellationTokenSource.Cancel();
await Task.WhenAll(periodicTimerTasks);

foreach (var logs in allLogs)
{
    WriteLogFile(logs);
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

void LogEvent(List<string> logs, int executionsRemaining)
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

        logs.Add($"* Timestamp: {FormatElapsedForLog(elapsed)} - Since Last: {FormatElapsedForLog(timeSinceLastLog)} - Target Time: {FormatElapsedForLog(targetElapsed)} - Total Drift: {FormatElapsedForLog(totalDrift)} - Remaining Executions: {executionsRemaining}");
    }
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

void StartThreadingTimer(System.Threading.Timer timer, TimeSpan interval)
{
    timer.Change(interval, interval);
}

void StopThreadingTimer(System.Threading.Timer timer)
{
    timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
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

sealed class TimerLogState(TimeSpan interval)
{
    public long IntervalTicks { get; } = interval.Ticks;
    public int ExecutionCount { get; set; }
    public long PreviousElapsedTicks { get; set; }
}
