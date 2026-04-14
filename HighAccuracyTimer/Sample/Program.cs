
using HighAccuracyTimers;
using System.Diagnostics;

#pragma warning disable CA1859

// TODO Create lists for oneSecond and sixtyFps periodic, timers, and threading timers
// TODO Make all list names consistent
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

using HighAccuracyTimer fiftyMs = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromMilliseconds(50),
};
fiftyMs.Elapsed += (sender, args) => { LogEvent(fiftyMsExecutions, args.RemainingExecutions); };

using HighAccuracyTimer sixtyFps = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpanUtilties.FromHz(60),
};
sixtyFps.Elapsed += (sender, args) => { LogEvent(sixtyFpsExecutions, args.RemainingExecutions); };

using HighAccuracyTimer oneSecond = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromSeconds(1),
    StopAfter = 10,
};
oneSecond.Elapsed += (sender, args) => { LogEvent(oneSecondExecutions, args.RemainingExecutions); };

using HighAccuracyTimer oneMs = new HighAccuracyWindowsTimer(AutoStart: false)
{
    Rate = TimeSpan.FromMilliseconds(1),
    StopAfter = 2000,
};
oneMs.Elapsed += (sender, args) => { LogEvent(oneMsExecutions, args.RemainingExecutions); };

var fiftyMsTimersTimer = new System.Timers.Timer(TimeSpan.FromMilliseconds(50))
{
    Enabled = false,
};
fiftyMsTimersTimer.Elapsed += (sender, args) => { LogEvent(oneMsExecutions, -1); };

// TODO Set up rest of timers, do not let them auto start. Set remaining executions to -1 if timer does not have that concept

startTime = Stopwatch.GetTimestamp();
// TODO Manually start all timers
fiftyMs.Start();

await Task.Delay(20100);

// Set up rest of log files
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
