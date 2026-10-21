using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Memoria.FF8;

/// <summary>Paces the display host without accumulating missed presentation deadlines.</summary>
internal sealed class PresentationPacer : IDisposable
{
    private const uint HighResolution = 2;
    private const uint TimerAccess = 0x00100002;
    private const uint Infinite = uint.MaxValue;
    private SafeWaitHandle? timer;
    private double nextDeadline;
    private int activeFrameLimit;
    private bool disposed;

    public void WaitForNextFrame(int frameLimit)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (frameLimit <= 0)
        {
            Reset();
            return;
        }
        long now = Stopwatch.GetTimestamp();
        double period = Math.Max(1, Stopwatch.Frequency / (double)frameLimit);
        if (activeFrameLimit != frameLimit || nextDeadline == 0)
        {
            activeFrameLimit = frameLimit;
            nextDeadline = now + period;
        }
        if (now >= nextDeadline)
        {
            nextDeadline = AdvanceDeadline(nextDeadline, now, period);
            return;
        }
        SafeWaitHandle handle = Timer();
        while (now < nextDeadline)
        {
            long dueTime = -Math.Max(1, checked((long)Math.Ceiling((nextDeadline - now) * TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
            if (!SetWaitableTimerEx(handle, in dueTime, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            uint result = WaitForSingleObject(handle, Infinite);
            if (result == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (result != 0)
            {
                throw new InvalidOperationException($"The presentation timer returned unexpected wait result 0x{result:X8}.");
            }
            now = Stopwatch.GetTimestamp();
        }
        nextDeadline = AdvanceDeadline(nextDeadline, now, period);
    }

    internal static double AdvanceDeadline(double deadline, double now, double period)
    {
        // A small overshoot must not permanently lower the selected display rate.
        double next = deadline + period;
        return next > now ? next : now + period;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        nextDeadline = 0;
        activeFrameLimit = 0;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        timer?.Dispose();
        disposed = true;
    }

    private SafeWaitHandle Timer()
    {
        if (timer is not null)
        {
            return timer;
        }
        SafeWaitHandle created = CreateWaitableTimerEx(IntPtr.Zero, null, HighResolution, TimerAccess);
        if (created.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            created.Dispose();
            throw new Win32Exception(error);
        }
        timer = created;
        return created;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateWaitableTimerEx(IntPtr attributes, string? name, uint flags, uint desiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimerEx(SafeWaitHandle timer, in long dueTime, int period, IntPtr completionRoutine, IntPtr completionArgument, IntPtr wakeContext, uint tolerableDelay);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
}
