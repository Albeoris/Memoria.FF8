namespace Memoria.FF8;

internal sealed class EffectPreviewDrainObservationBudget
{
    internal static bool Supports(ushort effect) => effect is 77 or 101 or 211;

    private bool observedLiveModule;
    private bool firstStallRecorded;
    private bool timeoutRecorded;

    internal string? Observe(bool liveModule, bool moduleReturned, bool blocked, bool timeout)
    {
        observedLiveModule |= liveModule;
        if (timeout && !timeoutRecorded)
        {
            timeoutRecorded = true;
            return "timeout";
        }
        if (!timeout && observedLiveModule && moduleReturned && blocked && !firstStallRecorded)
        {
            firstStallRecorded = true;
            return "first-post-module-stall";
        }
        return null;
    }
}
