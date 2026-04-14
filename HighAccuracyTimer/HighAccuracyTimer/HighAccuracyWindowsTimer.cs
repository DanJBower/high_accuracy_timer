using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HighAccuracyTimers;

public class HighAccuracyWindowsTimer : HighAccuracyTimer
{
    private readonly Lock _lock = new();
    private readonly EventWaitHandle _cancelSignal = new(initialState: false, EventResetMode.ManualReset);
    private readonly SafeWaitHandle _timerHandle;
    private readonly EventWaitHandle _timerSignal = new(initialState: false, EventResetMode.ManualReset);
    private readonly ManualResetEventSlim _waitCompleted = new(initialState: true);

    private bool _disposed;
    private bool _waitInProgress;

    public HighAccuracyWindowsTimer()
    {
        _timerHandle = NativeMethods.CreateWaitableTimerExW(
            lpTimerAttributes: IntPtr.Zero,
            lpTimerName: null,
            dwFlags: NativeMethods.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
            dwDesiredAccess: NativeMethods.SYNCHRONIZE | NativeMethods.TIMER_MODIFY_STATE);

        if (_timerHandle.IsInvalid)
        {
            throw CreateWin32Exception("Failed to create a high-resolution waitable timer.");
        }

        _timerSignal.SafeWaitHandle = DuplicateHandle(_timerHandle);
    }

    public async ValueTask WaitAsync(TimeSpan dueIn, CancellationToken cancellationToken = default)
    {
        if (dueIn < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(dueIn), "The due time must be zero or positive.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        BeginWait();

        try
        {
            if (dueIn == TimeSpan.Zero)
            {
                return;
            }

            using var cancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static state => ((HighAccuracyWindowsTimer)state!).SignalCancellation(), this)
                : default;

            ArmTimer(dueIn);

            var waitResult = await WaitForTimerOrCancellationAsync().ConfigureAwait(false);
            if (waitResult == WaitResult.Cancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            EndWait();
        }
    }

    public ValueTask CancelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool shouldCancel;
        lock (_lock)
        {
            ThrowIfDisposed();
            shouldCancel = _waitInProgress;
        }

        if (!shouldCancel)
        {
            return ValueTask.CompletedTask;
        }

        SignalCancellation();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        bool alreadyDisposed;
        bool waitInProgress;
        lock (_lock)
        {
            alreadyDisposed = _disposed;
            waitInProgress = _waitInProgress;
            _disposed = true;
        }

        if (alreadyDisposed)
        {
            return;
        }

        SignalCancellation();

        if (waitInProgress)
        {
            _waitCompleted.Wait();
        }

        _cancelSignal.Dispose();
        _timerSignal.Dispose();
        _timerHandle.Dispose();
        _waitCompleted.Dispose();
    }

    private void BeginWait()
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            if (_waitInProgress)
            {
                throw new InvalidOperationException("Only one wait may be active on a timer source at a time.");
            }

            _waitInProgress = true;
            _waitCompleted.Reset();
            _cancelSignal.Reset();
        }
    }

    private void EndWait()
    {
        lock (_lock)
        {
            _waitInProgress = false;
            if (!_disposed)
            {
                _cancelSignal.Reset();
            }

            _waitCompleted.Set();
        }
    }

    private void SignalCancellation()
    {
        try
        {
            _cancelSignal.Set();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        if (!_timerHandle.IsClosed && !_timerHandle.IsInvalid)
        {
            _ = NativeMethods.CancelWaitableTimer(_timerHandle);
        }
    }

    private void ArmTimer(TimeSpan dueIn)
    {
        var relativeDueTime = -dueIn.Ticks;
        if (!NativeMethods.SetWaitableTimerEx(
                hTimer: _timerHandle,
                lpDueTime: in relativeDueTime,
                lPeriod: 0,
                pfnCompletionRoutine: IntPtr.Zero,
                lpArgToCompletionRoutine: IntPtr.Zero,
                wakeContext: IntPtr.Zero,
                tolerableDelay: 0)
            )
        {
            throw CreateWin32Exception("Failed to arm the waitable timer.");
        }
    }

    private Task<WaitResult> WaitForTimerOrCancellationAsync()
    {
        var state = new AsyncWaitState();

        state.TimerRegistration = ThreadPool.RegisterWaitForSingleObject(
            waitObject: _timerSignal,
            callBack: static (waitState, timedOut) =>
            {
                if (timedOut)
                {
                    ((AsyncWaitState)waitState!).TrySetException(new InvalidOperationException("The timer wait unexpectedly timed out."));
                    return;
                }

                ((AsyncWaitState)waitState!).TrySetResult(WaitResult.Timer);
            },
            state: state,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: true);

        state.CancelRegistration = ThreadPool.RegisterWaitForSingleObject(
            waitObject: _cancelSignal,
            callBack: static (waitState, timedOut) =>
            {
                if (timedOut)
                {
                    ((AsyncWaitState)waitState!).TrySetException(new InvalidOperationException("The cancellation wait unexpectedly timed out."));
                    return;
                }

                ((AsyncWaitState)waitState!).TrySetResult(WaitResult.Cancellation);
            },
            state: state,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: true);

        state.OnRegistrationsReady();
        return state.Task;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Win32Exception CreateWin32Exception(string message)
    {
        return new(Marshal.GetLastWin32Error(), message);
    }

    private static SafeWaitHandle DuplicateHandle(SafeWaitHandle sourceHandle)
    {
        var currentProcess = NativeMethods.GetCurrentProcess();
        if (!NativeMethods.DuplicateHandle(
                hSourceProcessHandle: currentProcess,
                hSourceHandle: sourceHandle,
                hTargetProcessHandle: currentProcess,
                lpTargetHandle: out var duplicatedHandle,
                dwDesiredAccess: 0,
                bInheritHandle: false,
                dwOptions: NativeMethods.DUPLICATE_SAME_ACCESS))
        {
            throw CreateWin32Exception("Failed to duplicate the waitable timer handle.");
        }

        return duplicatedHandle;
    }

    private enum WaitResult
    {
        Timer,
        Cancellation,
    }

    private sealed class AsyncWaitState
    {
        private readonly TaskCompletionSource<WaitResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _completed;
        private int _registrationsReady;

        public RegisteredWaitHandle? TimerRegistration { get; set; }
        public RegisteredWaitHandle? CancelRegistration { get; set; }

        public Task<WaitResult> Task => _completion.Task;

        public void OnRegistrationsReady()
        {
            Volatile.Write(ref _registrationsReady, 1);
            if (Volatile.Read(ref _completed) != 0)
            {
                UnregisterAll();
            }
        }

        public void TrySetResult(WaitResult result)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            {
                return;
            }

            _completion.TrySetResult(result);
            if (Volatile.Read(ref _registrationsReady) != 0)
            {
                UnregisterAll();
            }
        }

        public void TrySetException(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            {
                return;
            }

            _completion.TrySetException(exception);
            if (Volatile.Read(ref _registrationsReady) != 0)
            {
                UnregisterAll();
            }
        }

        private void UnregisterAll()
        {
            TimerRegistration?.Unregister(waitObject: null);
            CancelRegistration?.Unregister(waitObject: null);
        }
    }

    private static class NativeMethods
    {
        internal const uint SYNCHRONIZE = 0x00100000;
        internal const uint TIMER_MODIFY_STATE = 0x0002;
        internal const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
        internal const uint DUPLICATE_SAME_ACCESS = 0x00000002;

        [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeWaitHandle CreateWaitableTimerExW(
            IntPtr lpTimerAttributes,
            string? lpTimerName,
            uint dwFlags,
            uint dwDesiredAccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWaitableTimerEx(
            SafeWaitHandle hTimer,
            in long lpDueTime,
            int lPeriod,
            IntPtr pfnCompletionRoutine,
            IntPtr lpArgToCompletionRoutine,
            IntPtr wakeContext,
            uint tolerableDelay);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CancelWaitableTimer(SafeWaitHandle hTimer);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateHandle(
            IntPtr hSourceProcessHandle,
            SafeWaitHandle hSourceHandle,
            IntPtr hTargetProcessHandle,
            out SafeWaitHandle lpTargetHandle,
            uint dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            uint dwOptions);
    }
}
