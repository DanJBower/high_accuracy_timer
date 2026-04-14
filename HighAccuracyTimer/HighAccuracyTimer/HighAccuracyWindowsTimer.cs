using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HighAccuracyTimers;

public class HighAccuracyWindowsTimer : HighAccuracyTimer
{
    private readonly Lock _lock = new();
    private readonly EventWaitHandle _cancelSignal = new(initialState: false, EventResetMode.ManualReset);
    private readonly SafeWaitHandle _timerHandle;

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
    }

    public ValueTask WaitAsync(TimeSpan dueIn, CancellationToken cancellationToken = default)
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
                return ValueTask.CompletedTask;
            }

            using var cancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static state => ((HighAccuracyWindowsTimer)state!).SignalCancellation(), this)
                : default;

            ArmTimer(dueIn);

            var waitResult = WaitForTimerOrCancellation();
            if (waitResult == NativeMethods.WAIT_OBJECT_0 + 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return ValueTask.CompletedTask;
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
        lock (_lock)
        {
            alreadyDisposed = _disposed;
            _disposed = true;
        }

        if (alreadyDisposed)
        {
            return;
        }

        SignalCancellation();
        _cancelSignal.Dispose();
        _timerHandle.Dispose();
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
        }
    }

    private void SignalCancellation()
    {
        _cancelSignal.Set();

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

    private uint WaitForTimerOrCancellation()
    {
        bool timerHandleReferenced = false;
        bool cancelHandleReferenced = false;

        try
        {
            _timerHandle.DangerousAddRef(ref timerHandleReferenced);
            _cancelSignal.SafeWaitHandle.DangerousAddRef(ref cancelHandleReferenced);

            IntPtr[] handles =
            [
                _timerHandle.DangerousGetHandle(),
                _cancelSignal.SafeWaitHandle.DangerousGetHandle(),
            ];

            var waitResult = NativeMethods.WaitForMultipleObjects(
                nCount: (uint)handles.Length,
                lpHandles: handles,
                bWaitAll: false,
                dwMilliseconds: NativeMethods.INFINITE);

            return waitResult switch
            {
                NativeMethods.WAIT_OBJECT_0 => waitResult,
                NativeMethods.WAIT_OBJECT_0 + 1 => waitResult,
                NativeMethods.WAIT_FAILED => throw CreateWin32Exception("Waiting on the timer source failed."),
                _ => throw new InvalidOperationException($"Unexpected wait result: 0x{waitResult:X8}."),
            };
        }
        finally
        {
            if (cancelHandleReferenced)
            {
                _cancelSignal.SafeWaitHandle.DangerousRelease();
            }

            if (timerHandleReferenced)
            {
                _timerHandle.DangerousRelease();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Win32Exception CreateWin32Exception(string message)
    {
        return new(Marshal.GetLastWin32Error(), message);
    }

    private static class NativeMethods
    {
        internal const uint SYNCHRONIZE = 0x00100000;
        internal const uint TIMER_MODIFY_STATE = 0x0002;
        internal const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
        internal const uint WAIT_OBJECT_0 = 0x00000000;
        internal const uint WAIT_FAILED = 0xFFFFFFFF;
        internal const uint INFINITE = 0xFFFFFFFF;

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
        internal static extern uint WaitForMultipleObjects(
            uint nCount,
            IntPtr[] lpHandles,
            [MarshalAs(UnmanagedType.Bool)] bool bWaitAll,
            uint dwMilliseconds);
    }
}
