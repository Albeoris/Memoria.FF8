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
    private ImmutableArray<EncounterArrivalState> encounterArrivals = [];

    public ImmutableArray<EncounterArrivalState> CaptureEncounterArrivals() => encounterArrivals;

    public bool EncounterArrivalComplete(string actorId) => encounterArrivals.All(state => state.Track.ActorId != actorId || state.Complete);

    public void RestoreEncounterArrivals(ImmutableArray<EncounterArrivalState> states)
    {
        if (states.IsDefault || states.Select(state => state.Track.ActorId).Distinct(StringComparer.Ordinal).Count() != states.Length)
        {
            throw new InvalidDataException("Arrival snapshot requires distinct actor identities and an initialized track collection.");
        }
        foreach (EncounterArrivalState state in states)
        {
            _ = state.Sample;
            if (state.ElapsedCallbacks < 0)
            {
                throw new InvalidDataException("Arrival snapshot has a negative source clock.");
            }
        }
        encounterArrivals = states;
    }

    public void StartEncounterArrival(IReadOnlyList<EncounterArrivalTrack> tracks)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Battle arrivals require their bound presentation.");
        process.StartEncounterArrival(tracks);
        encounterArrivals = process.CaptureEncounterArrivals();
    }
}
