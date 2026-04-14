
using HighAccuracyTimers;
using System.Diagnostics;

#pragma warning disable CA1859

List<string> fiftyMsPeriodicTimerExecutions = ["50ms Period Timer:"];
List<string> fiftyMsTimersTimerExecutions = ["50ms Timers Timer:"];
List<string> fiftyMsThreadingTimerExecutions = ["50ms Threading Timer:"];
List<string> fiftyMsExecutions = ["50ms High Accuracy Timer:"];
List<string> sixtyFpsExecutions = ["60Hz High Accuracy Timer:"];
List<string> oneSecondExecutions = ["1s High Accuracy Timer:"];
List<string> oneMsPeriodicTimerExecutions = ["1ms Period Timer:"];
List<string> oneMsTimersTimerExecutions = ["1ms Timers Timer:"];
List<string> oneMsThreadingTimerExecutions = ["1ms Threading Timer:"];
List<string> oneMsExecutions = ["1ms High Accuracy Timer:"];

long startTime = 0;

using HighAccuracyTimer timer = new HighAccuracyWindowsTimer()
{
    Rate = TimeSpan.FromSeconds(1),
};

timer.Elapsed += (sender, args) =>
{
    Console.WriteLine($"Timestamp: {args.TimeStamp}");
};

using HighAccuracyTimer fiftyMs = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromMilliseconds(50),
};

fiftyMs.Elapsed += (sender, args) => { LogEvent(fiftyMsExecutions, args.RemainingExecutions); };

using HighAccuracyTimer sixtyFps = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpanUtilties.FromHz(60),
};

using HighAccuracyTimer oneSecond = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromSeconds(1),
    StopAfter = 10,
};

using HighAccuracyTimer oneMs = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromMilliseconds(1),
    StopAfter = 2000,
};

startTime = Stopwatch.GetTimestamp();
fiftyMs.Start();

await Task.Delay(20000);

WriteLogFile(fiftyMsPeriodicTimerExecutions);
WriteLogFile(fiftyMsTimersTimerExecutions);
WriteLogFile(fiftyMsThreadingTimerExecutions);
WriteLogFile(fiftyMsExecutions);
WriteLogFile(sixtyFpsExecutions);
WriteLogFile(oneSecondExecutions);
WriteLogFile(oneMsPeriodicTimerExecutions);
WriteLogFile(oneMsTimersTimerExecutions);
WriteLogFile(oneMsThreadingTimerExecutions);
WriteLogFile(oneMsExecutions);

void WriteLogFile(List<string> logs)
{
    var filePath = $"{logs[0].Replace(":", "")}.log";
    File.WriteAllLines(filePath, logs);
}

void LogEvent(List<string> logs, int executionsRemaining)
{
    var elapsed = Stopwatch.GetElapsedTime(startTime);
    logs.Add($"* Timestamp: {elapsed} - Remaining Executions: {executionsRemaining}");
}
