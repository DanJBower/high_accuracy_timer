
using HighAccuracyTimers;
using System.Collections.Concurrent;
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
TimeSpan sixtyFpsRate = TimeSpanUtilties.FromHz(60);
ConcurrentDictionary<List<string>, long> previousLogElapsedTicks = new();

using HighAccuracyTimer fiftyMsHighAccuracyTimer = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromMilliseconds(50),
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
    Rate = TimeSpan.FromSeconds(1),
};
oneSecondHighAccuracyTimer.Elapsed += (sender, args) => { LogEvent(oneSecondHighAccuracyTimerExecutions, args.RemainingExecutions); };
oneSecondHighAccuracyTimer.Elapsed += (sender, args) => { Console.WriteLine($"Running time: {args.TimeStamp}"); };

using HighAccuracyTimer oneMsHighAccuracyTimer = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromMilliseconds(1),
    StopAfter = 2000,
};
oneMsHighAccuracyTimer.Elapsed += (sender, args) => { LogEvent(oneMsHighAccuracyTimerExecutions, args.RemainingExecutions); };

using var fiftyMsTimersTimer = CreateTimersTimer(TimeSpan.FromMilliseconds(50), fiftyMsTimersTimerExecutions);
using var sixtyFpsTimersTimer = CreateTimersTimer(sixtyFpsRate, sixtyFpsTimersTimerExecutions);
using var oneSecondTimersTimer = CreateTimersTimer(TimeSpan.FromSeconds(1), oneSecondTimersTimerExecutions);
using var oneMsTimersTimer = CreateTimersTimer(TimeSpan.FromMilliseconds(1), oneMsTimersTimerExecutions);

await using var fiftyMsThreadingTimer = CreateThreadingTimer(TimeSpan.FromMilliseconds(50), fiftyMsThreadingTimerExecutions);
await using var sixtyFpsThreadingTimer = CreateThreadingTimer(sixtyFpsRate, sixtyFpsThreadingTimerExecutions);
await using var oneSecondThreadingTimer = CreateThreadingTimer(TimeSpan.FromSeconds(1), oneSecondThreadingTimerExecutions);
await using var oneMsThreadingTimer = CreateThreadingTimer(TimeSpan.FromMilliseconds(1), oneMsThreadingTimerExecutions);

using CancellationTokenSource periodicTimerCancellationTokenSource = new();

startTime = Stopwatch.GetTimestamp();

Task[] periodicTimerTasks =
[
    RunPeriodicTimerAsync(TimeSpan.FromMilliseconds(50), fiftyMsPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
    RunPeriodicTimerAsync(sixtyFpsRate, sixtyFpsPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
    RunPeriodicTimerAsync(TimeSpan.FromSeconds(1), oneSecondPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
    RunPeriodicTimerAsync(TimeSpan.FromMilliseconds(1), oneMsPeriodicTimerExecutions, periodicTimerCancellationTokenSource.Token),
];

fiftyMsHighAccuracyTimer.Start();
sixtyFpsHighAccuracyTimer.Start();
oneSecondHighAccuracyTimer.Start();
oneMsHighAccuracyTimer.Start();

fiftyMsTimersTimer.Start();
sixtyFpsTimersTimer.Start();
oneSecondTimersTimer.Start();
oneMsTimersTimer.Start();

StartThreadingTimer(fiftyMsThreadingTimer, TimeSpan.FromMilliseconds(50));
StartThreadingTimer(sixtyFpsThreadingTimer, sixtyFpsRate);
StartThreadingTimer(oneSecondThreadingTimer, TimeSpan.FromSeconds(1));
StartThreadingTimer(oneMsThreadingTimer, TimeSpan.FromMilliseconds(1));

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
    var elapsedTicks = elapsed.Ticks;
    var formattedElapsed = FormatElapsedForLog(elapsed);
    lock (logs)
    {
        previousLogElapsedTicks.TryGetValue(logs, out var previousElapsedTicks);
        var timeSinceLastLog = TimeSpan.FromTicks(elapsedTicks - previousElapsedTicks);
        previousLogElapsedTicks[logs] = elapsedTicks;

        logs.Add($"* Timestamp: {formattedElapsed} - Since Last: {FormatElapsedForLog(timeSinceLastLog)} - Remaining Executions: {executionsRemaining}");
    }
}

string FormatElapsedForLog(TimeSpan elapsed)
{
    var totalSeconds = (long)elapsed.TotalSeconds;
    var milliseconds = elapsed.Milliseconds;
    var subMillisecondTicks = elapsed.Ticks % TimeSpan.TicksPerMillisecond;
    return $"{totalSeconds:00}.{milliseconds:000}_{subMillisecondTicks:0000}";
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
