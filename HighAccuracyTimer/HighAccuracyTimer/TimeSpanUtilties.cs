namespace HighAccuracyTimers;

public static class TimeSpanUtilties
{
    public static TimeSpan FromHz(double hz)
    {
        return TimeSpan.FromSeconds(1.0 / hz);
    }
}
