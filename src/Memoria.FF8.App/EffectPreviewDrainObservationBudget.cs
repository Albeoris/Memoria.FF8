namespace Memoria.FF8;

internal sealed class EffectPreviewDrainObservationBudget
{
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
