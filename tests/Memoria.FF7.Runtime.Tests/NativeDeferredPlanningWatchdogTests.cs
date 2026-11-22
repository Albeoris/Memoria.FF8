using Memoria.FF7.Runtime;

internal static class NativeDeferredPlanningWatchdogTests
{
    internal static void Run()
    {
        Ff7AutoPlayProgressWatchdog watchdog = new();
        Check(watchdog.Observe("native-source"), "The initial native progress state was not observed.");
        for (int callback = 1; callback < Ff7AutoPlayProgressWatchdog.IdleStepLimit; callback++)
        {
            Check(!watchdog.Observe("native-source"), "Repeated native callbacks fabricated progress.");
        }
        for (int poll = 0; poll < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 3; poll++)
        {
            Check(!watchdog.Observe("native-source", countIdleStep: false), "Deferred planning fabricated native progress.");
        }
        ExpectTimeout(() => watchdog.Observe("native-source"));
        watchdog.Reset();
        Check(watchdog.Observe("native-source", countIdleStep: false), "A fresh lifecycle failed to register its initial source without charging a callback.");
        for (int callback = 1; callback < Ff7AutoPlayProgressWatchdog.IdleStepLimit; callback++)
        {
            Check(!watchdog.Observe("native-source"), "Reset changed repeated-state semantics.");
        }
        Check(watchdog.Observe("actual-next-state"), "Actual changed native work failed to renew progress after deferred planning.");
        for (int callback = 1; callback < Ff7AutoPlayProgressWatchdog.IdleStepLimit; callback++)
        {
            watchdog.Observe("actual-next-state");
        }
        ExpectTimeout(() => watchdog.Observe("actual-next-state"));
        CheckSemanticObservationMarker();
        Console.WriteLine("Deferred planning preserves native watchdog debt without spending or resetting callback limits.");
    }

    private static void CheckSemanticObservationMarker()
    {
        Ff7AutoPlaySemanticWatchdog semantic = new();
        Check(semantic.HasPendingNativeObservation, "The initial native source was not eligible for observation.");
        Check(semantic.Observe("source", "frontier") && !semantic.HasPendingNativeObservation, "A semantic observation did not consume exactly its pending native marker.");
        for (int poll = 0; poll < Ff7AutoPlayProgressWatchdog.IdleStepLimit * 2; poll++)
        {
            Check(!semantic.Observe("source", "frontier") && !semantic.HasPendingNativeObservation, "Planning polls created native observations without a callback.");
        }
        semantic.NativeAdvanced();
        Check(semantic.HasPendingNativeObservation, "Actual native advancement did not restore observation eligibility.");
        Check(!semantic.Observe("source", "frontier") && !semantic.HasPendingNativeObservation, "A repeated native callback bypassed normal semantic progress accounting.");
        semantic.Reset();
        Check(semantic.HasPendingNativeObservation && semantic.Observe("source", "frontier"), "A normal lifecycle reset failed to admit a fresh initial observation.");
    }

    private static void ExpectTimeout(Action action)
    {
        bool timedOut = false;
        try
        {
            action();
        }
        catch (TimeoutException)
        {
            timedOut = true;
        }
        Check(timedOut, "Deferred planning erased real native idle debt or changed the existing callback limit.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
