using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record BattleRosterMutation(BattleActorReference Source, ImmutableArray<BattleRosterAllocationRequest> Allocations)
{
    internal abstract record Operation(BattleActorReference Source)
    {
        internal sealed record Barrier(BattleActorReference Source) : Operation(Source);
        internal sealed record Remove(BattleActorReference Source, BattlePreviousTarget Target) : Operation(Source);
        internal sealed record Allocate(BattleActorReference Source, byte SceneSlot, NativeBattleModelSlot? OriginalSlot, bool ActivateAfterLoading) : Operation(Source);
        internal sealed record Activate(BattleActorReference Source, BattlePreviousTarget Target) : Operation(Source);
        internal sealed record ActivateLastAllocation(BattleActorReference Source) : Operation(Source);
        internal sealed record DeathRelease(BattleActorReference Source) : Operation(Source);
    }
}

internal sealed record BattleRosterAllocationRequest(NativeBattleEnemyAllocation Allocation, BattleRosterSlot Slot, NativeBattleModelSlot? OriginalSlot, bool ReplaceOccupant);

internal sealed record BattleRosterMutationResult(ImmutableArray<BattleRosterAllocation> Allocations);

internal sealed record BattleRosterAllocation(BattleEnemyActorReference Actor, BattleEnemyActorModel Model, BattleRosterSlot Slot, NativeBattleModelSlot? OriginalSlot, int Generation);

public sealed partial class BattleSession
{
    private BattleRosterMutationResult ApplyRosterMutation(BattleRosterMutation mutation)
    {
        if (FindTargetActor(mutation.Source) is null || mutation.Allocations.IsDefaultOrEmpty ||
            mutation.Allocations.Select(request => request.Slot).Distinct().Count() != mutation.Allocations.Length)
        {
            throw new InvalidDataException("Roster allocation requires a current source and distinct destination slots.");
        }
        foreach (BattleRosterAllocationRequest request in mutation.Allocations)
        {
            if (request.Slot.Side != BattleSide.Enemies || request.Slot.Position >= ExpandedBattleFormation.MaximumEnemies ||
                request.OriginalSlot is { } original && original.Offset != NativeBattleSlots.TryEncode(request.Slot) ||
                IsEncounterRetirementPending(request.Slot) ||
                !request.ReplaceOccupant && enemies.Any(actor => actor.Slot == request.Slot && actor.IsActive))
            {
                throw new InvalidOperationException("Roster allocation conflicts with an occupied, retiring or unsupported destination slot.");
            }
        }

        var added = ImmutableArray.CreateBuilder<BattleRosterAllocation>(mutation.Allocations.Length);
        foreach (BattleRosterAllocationRequest request in mutation.Allocations)
        {
            foreach (BattleCombatant previous in enemies.Where(actor => actor.Slot == request.Slot).ToArray())
            {
                if (previous.IsActive)
                {
                    ReleaseNativeParticipant(previous);
                }
                previous.BindRosterSlot(null);
            }
            NativeBattleEnemyAllocation allocation = request.Allocation;
            BattleCombatant next = CreateNativeAllocation(allocation);
            nativeAllocations.Add(allocation);
            next.BindRosterSlot(request.Slot, bindNative: request.OriginalSlot.HasValue);
            BattleEnemyActorReference actor = new(next.Id);
            added.Add(new(actor, new BattleEnemyActorModel(actor), request.Slot, request.OriginalSlot, allocation.Generation));
        }
        return new(added.MoveToImmutable());
    }
}
