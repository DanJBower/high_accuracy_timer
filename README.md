# High Accuracy Timer

[![NuGet](https://img.shields.io/nuget/v/HighAccuracyTimers.svg)](https://www.nuget.org/packages/HighAccuracyTimers/)

An asynchronous timer in C# that runs closer to realtime than the various timers provided by .NET. Inbuilt C# timers run with an accuracy of 12-15ms (according to other things, sample project included so you can see accuracy on your PC vs my timer implementation, see log files in .exe directory). This project aims for sub ms accuracy and minimal drift.

Currently only supports Windows. Windows implementation uses the `WaitableTimerExW` functions with the `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` flag to be as accurate as possible.

Provides a:

* Windows timer source for close to realtime.
* A basic scheduler that accounts for drift and skips cycles if overruns happen to catch back up.
  * Provides `WaitForNextTickAsync` for manually control of when next timer tick is consumed.
  * Provides `GetTicksAsync` to allow for async foreach loop.
  * Timer tracks start time, but doesn't actively run in background until one of the two async functions are called for getting the next tick. Prevents unnecessary processing
* A dispatcher to easily support multiple consumers to the same timer.

## Usage sample

### Single consumer

    // Set up platform specific time source
    using var timeSource = new HighAccuracyWindowsTimer();

    // Create and start scheduler (the timer)
    await using var timer = new HighAccuracyScheduler(
        timer: timeSource,
        options: new SchedulerOptions
        {
            Period = TimeSpanUtilties.FromHz(4),
        });

    await timer.StartAsync();

    // Configure what happens on the timer
    // Must be done after timer is started or GetTicksAsync will return immediately
    var onTimerTask = Task.Run(async () =>
    {
        await foreach (var tick in timer.GetTicksAsync())
        {
            Console.WriteLine("Hello");
        }
    });

    // Do whatever else your program needs to do
    await Task.Delay(TimeSpan.FromSeconds(5));

    // Clean up
    await timer.StopAsync();
    await onTimerTask;

### Multiple consumers (using dispatcher)

    // Set up platform specific time source
    using var timeSource = new HighAccuracyWindowsTimer();

    // Create scheduler (the timer)
    await using var timer = new HighAccuracyScheduler(
        timer: timeSource,
        options: new SchedulerOptions
        {
            Period = TimeSpanUtilties.FromHz(4),
            StopAfterScheduledTicks = 20,
        });

    // Configure dispatcher and subscriptions
    await using var dispatcher = new HighAccuracyDispatcher(timer);

    // Dispose subscriptions to unsubscribe
    using var subscription1 = dispatcher.Subscribe(async (tick, ct) =>
    {
        Console.WriteLine("Hello");
    });

    using var subscription2 = dispatcher.Subscribe(async (tick, ct) =>
    {
        await Task.Delay(50, ct);
        Console.WriteLine("Hello again");
    });

    // Start scheduler
    await timer.StartAsync();

    // Start dispatcher - must be started after timer
    // Will automatically stop and clean up when scheduled number
    // of ticks has been reached
    await dispatcher.DispatchAsync();
