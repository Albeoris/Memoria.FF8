using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record EncounterArrivalState(EncounterArrivalTrack Track, NativeBattleSequenceState ResumeSequence, long ElapsedCallbacks = 0, long? LastCallback = null, bool Complete = false)
{
    public EncounterArrivalSample Sample => Track.Sample(checked(Track.StartedAtCallback + ElapsedCallbacks));

    public EncounterArrivalState Advance(long callback, bool paused)
    {
        if (callback < LastCallback)
        {
            throw new InvalidDataException("Arrival callback cannot move backwards.");
        }
        if (Complete || LastCallback == callback)
        {
            return this;
        }
        if (paused)
        {
            return this with { LastCallback = callback };
        }
        if (Sample.Complete)
        {
            return this with { LastCallback = callback, Complete = true };
        }
        return this with { LastCallback = callback, ElapsedCallbacks = checked(ElapsedCallbacks + (LastCallback.HasValue ? 1 : 0)) };
    }
}

public sealed partial class BattleSession
{
    public ImmutableArray<EncounterArrivalState> CaptureEncounterArrivals() => Presentation?.CaptureEncounterArrivals() ?? [];

    public bool EncounterArrivalComplete(string actorId) => Presentation?.EncounterArrivalComplete(actorId) ?? true;

    public void RestoreEncounterArrivals(ImmutableArray<EncounterArrivalState> states)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Battle arrivals require their bound presentation.");
        process.RestoreEncounterArrivals(states);
    }

    public void StartEncounterArrival(IReadOnlyList<EncounterArrivalTrack> tracks)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Battle arrivals require their bound presentation.");
        process.StartEncounterArrival(tracks);
    }
}
