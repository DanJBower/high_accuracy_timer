# High Accuracy Timer

An event based timer in C# that runs closer to realtime than the built in Timer. Inbuilt C# timers run with an accuracy of 12-15ms (according to other things only). This project aims for sub ms accuracy.

Currently only supports Windows. Windows implementation uses the WaitableTimerExW functions with the CREATE_WAITABLE_TIMER_HIGH_RESOLUTION flag to be as accurate as possible.

## Usage sample

    using HighAccuracyTimer timer = new HighAccuracyWindowsTimer()
    {
        Rate = TimeSpan.FromMilliseconds(50),
        StopAfter = 50,
    };

    timer.Elapsed += (sender, args) =>
    {
        // Code to run when timer is triggered
    };
