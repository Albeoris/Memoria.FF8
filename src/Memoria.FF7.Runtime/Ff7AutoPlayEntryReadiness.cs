namespace Memoria.FF7.Runtime;

/// <summary>Waits for native field entry eligibility to remain stable across distinct completed ticks.</summary>
internal sealed class Ff7AutoPlayEntryReadiness
{
    private long? generation;
    private long? pendingTick;
    private long? countedTick;
    private string? pendingWitness;
    private int unstableTicks;
    private bool ready;

    internal bool IsReady(long generation)
    {
        return this.generation == generation && ready;
    }

    internal bool Observe(long generation, long tick, string witness, bool canAccept)
    {
        if (this.generation != generation)
        {
            this.generation = generation;
            pendingTick = null;
            countedTick = null;
            pendingWitness = null;
            unstableTicks = 0;
            ready = false;
        }
        if (ready)
        {
            return true;
        }
        if (!canAccept)
        {
            pendingWitness = null;
            pendingTick = null;
            return false;
        }
        if (pendingWitness == witness && pendingTick is long previous && tick > previous)
        {
            ready = true;
            return true;
        }
        pendingWitness = witness;
        pendingTick = tick;
        if (countedTick != tick)
        {
            countedTick = tick;
            unstableTicks++;
            if (unstableTicks >= Ff7AutoPlayNativeReadiness.ObservationLimit)
            {
                throw new NotSupportedException("Native field entry eligibility did not stabilize within its bounded observations.");
            }
        }
        return false;
    }
}
