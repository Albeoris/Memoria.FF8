using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private Ff7WorldRuntime? mountedControlWorld;
    private Ff7AutoPlayWorldControlObservation? mountedRiderControl;
    private string? mountedWitnessKey;
    private bool mountedWitness;

    private void ObserveMountedRiderControl(Ff7WorldRuntime world, Dictionary<string, AutoPlayEvent> events)
    {
        if (!ReferenceEquals(mountedControlWorld, world))
        {
            mountedControlWorld = world;
            mountedWitnessKey = null;
        }
        mountedRiderControl = null;
        if (State.Active is { Kind: Ff7AutoPlayTargetKind.WorldControl, WorldControl: { } owned } && owned.Id.StartsWith("control:chocobo:", StringComparison.Ordinal))
        {
            events[owned.Id] = new(owned.Id, AutoPlayEventKind.Interaction, ConditionSignature: owned.ConditionFingerprint);
            return;
        }
        if (State.DestinationField is not ushort destination || world.ReadAutoPlayChocoboControlCandidate() is not { } candidate)
        {
            return;
        }
        bool required = worldCatalogs.Values.SelectMany(catalog => catalog.Scripts).Where(target => target.Complete).Any(target => RequiresWorldRiderPrerequisite(world.ReadAutoPlayMeshEntryForModel(target, candidate.Model), world.ReadAutoPlayMeshEntryForModel(target, candidate.RiderModel)) && world.ReadAutoPlayMeshEntryForModel(target, candidate.RiderModel).Transitions.Any(transition => transition.Operation == WorldOperation.FieldTransition && transition.Selector is ushort selector && world.ResolveFieldEntry(selector).Map == destination));
        if (!required)
        {
            return;
        }
        string key = $"{world.PresentationTick}:{candidate.ConditionFingerprint}";
        if (mountedWitnessKey != key)
        {
            mountedWitness = world.ProveAutoPlayChocoboDismount(candidate);
            mountedWitnessKey = key;
        }
        if (mountedWitness)
        {
            mountedRiderControl = candidate;
            events[candidate.Id] = new(candidate.Id, AutoPlayEventKind.Interaction, ConditionSignature: candidate.ConditionFingerprint);
        }
    }
}
