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
    private const uint WaitTimeout = 258;
    private const uint MaximumWaitMilliseconds = 50;
    private SafeWaitHandle? timer;
    private double nextDeadline;
    private int activeFrameLimit;
    private long updateStarted;
    private int requestedFrameLimit;
    private bool awaitingPresentation;
    private bool pacedThisUpdate;
    private bool disposed;
    private bool observeTiming;
    private long observedCalls;
    private long observedTimerCalls;
    private long observedTimerTicks;
    private long observedResets;
    private long invalidObservations;
    private int observedFrameLimit;

    internal void ObserveTiming(bool enabled) => observeTiming = enabled;
    internal PresentationPacerObservation TimingObservation => new(observeTiming, observedCalls, observedTimerCalls, observedTimerTicks, observedResets, observedFrameLimit, invalidObservations);


    internal void BeginUpdate(int frameLimit, long started)
    {
        pacedThisUpdate = awaitingPresentation;
        if (pacedThisUpdate)
        {
            // The platform can suppress both BeginDraw and EndDraw. Keep the same deadline while polling.
            WaitForDeadline(requestedFrameLimit > 0 ? requestedFrameLimit : 30, updateStarted, started, default);
        }
        updateStarted = started;
        requestedFrameLimit = frameLimit;
        awaitingPresentation = true;
    }

    internal void RequestFrameLimit(int frameLimit) => requestedFrameLimit = frameLimit;

    internal void WaitBeforeDraw(long now)
    {
        if (!awaitingPresentation || pacedThisUpdate)
        {
            return;
        }
        WaitForDeadline(requestedFrameLimit, updateStarted, now, default);
        pacedThisUpdate = true;
    }

    internal void CompletePresentedFrame() => awaitingPresentation = false;

    public void WaitForNextFrame(int frameLimit, CancellationToken cancellationToken = default)
    {
        long now = Stopwatch.GetTimestamp();
        WaitForDeadline(frameLimit, now, now, cancellationToken);
    }

    private void WaitForDeadline(int frameLimit, long frameStarted, long now, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (observeTiming)
        {
            observedCalls++;
            observedFrameLimit = frameLimit;
        }
        if (frameLimit <= 0)
        {
            Reset();
            return;
        }
        double period = Math.Max(1, Stopwatch.Frequency / (double)frameLimit);
        if (activeFrameLimit != frameLimit || nextDeadline == 0)
        {
            activeFrameLimit = frameLimit;
            nextDeadline = frameStarted + period;
        }
        if (now >= nextDeadline)
        {
            nextDeadline = AdvanceDeadline(nextDeadline, now, period);
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            now = WaitWindows(nextDeadline, now, cancellationToken);
        }
        else
        {
            while (now < nextDeadline)
            {
                double milliseconds = (nextDeadline - now) * 1000 / Stopwatch.Frequency;
                TimeSpan delay = TimeSpan.FromMilliseconds(Math.Clamp(milliseconds, 1, MaximumWaitMilliseconds));
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.WaitHandle.WaitOne(delay);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                else
                {
                    Thread.Sleep(delay);
                }
                now = Stopwatch.GetTimestamp();
            }
        }
        nextDeadline = AdvanceDeadline(nextDeadline, now, period);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private long WaitWindows(double nextDeadline, long now, CancellationToken cancellationToken)
    {
        SafeWaitHandle handle = Timer();
        while (now < nextDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long dueTime = -Math.Max(1, checked((long)Math.Ceiling((nextDeadline - now) * TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
            if (!SetWaitableTimerEx(handle, in dueTime, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            long waitStarted = observeTiming ? Stopwatch.GetTimestamp() : 0;
            uint result = WaitForSingleObject(handle, MaximumWaitMilliseconds);
            if (observeTiming)
            {
                long waitEnded = Stopwatch.GetTimestamp();
                observedTimerCalls++;
                if (waitEnded < waitStarted || observedTimerTicks > long.MaxValue - (waitEnded - waitStarted))
                {
                    invalidObservations++;
                }
                else
                {
                    observedTimerTicks += waitEnded - waitStarted;
                }
            }
            if (result == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (result != 0 && result != WaitTimeout)
            {
                throw new InvalidOperationException($"The presentation timer returned unexpected wait result 0x{result:X8}.");
            }
            now = Stopwatch.GetTimestamp();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return now;
    }

    internal static double AdvanceDeadline(double deadline, double now, double period)
    {
        // A small overshoot must not permanently lower the selected display rate.
        double next = deadline + period;
        return next > now ? next : next + (Math.Floor((now - next) / period) + 1) * period;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (observeTiming)
        {
            observedResets++;
        }
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

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
