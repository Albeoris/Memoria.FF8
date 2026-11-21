using System.Diagnostics;

namespace Memoria.FF7.Runtime;

/// <summary>One cooperative work allowance shared by all detached child searches.</summary>
internal sealed class Ff7NativePlanningQuantum(int operationLimit = 8, TimeSpan? wallBudget = null)
{
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly TimeSpan budget = wallBudget ?? TimeSpan.FromMilliseconds(8);
    internal int Operations { get; private set; }

    internal bool TryBeginOperation()
    {
        if (Operations >= operationLimit || Operations > 0 && Stopwatch.GetElapsedTime(started) >= budget)
        {
            return false;
        }
        Operations++;
        return true;
    }
}

internal sealed class Ff7NativeRouteProbe
{
    internal string? ExpectedNavigationKey { get; init; }
    internal Ff7AutoPlayTarget[] NavigationPrerequisites { get; init; } = [];
    internal Ff7AutoPlayNativeRoute? Result { get; set; }
    internal Func<Ff7Session, Ff7AutoPlayObservation, bool>? SourceRelease { get; init; }
    internal string? Progress { get; set; }
    internal Action<Ff7AutoPlayRouteStep, Ff7NativeTickObservation>? QueuedLineWaitObserved { get; init; }
}

internal sealed class Ff7NativePlanningCursor(IEnumerator<Memoria.AutoPlay.AutoPlayPlanStep?> search) : IDisposable
{
    internal bool Completed { get; private set; }
    internal long Operations { get; private set; }
    internal Memoria.AutoPlay.AutoPlayPlanStep? Result { get; private set; }

    internal void Resume(Ff7NativePlanningQuantum quantum)
    {
        while (!Completed && quantum.TryBeginOperation())
        {
            Operations++;
            if (!search.MoveNext())
            {
                Completed = true;
                Dispose();
            }
            else if (search.Current is { } result)
            {
                Result = result;
                Completed = true;
                Dispose();
            }
        }
    }

    public void Dispose()
    {
        search.Dispose();
    }
}
