namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleCombatant RequireEmittingHostedActor(NativeBattleModelLifecycleServices services, NativeBattleModelLifecycleReceipt receipt)
    {
        if (!services.Facts.TryGetValue(receipt.Owner, out NativeBattleModelActorFacts? facts) ||
            !StringComparer.Ordinal.Equals(facts.ActorId, receipt.ActorId) || !byId.TryGetValue(receipt.ActorId, out BattleCombatant? actor) ||
            CreatePresentationModelOwner(actor) != receipt.Owner)
        {
            throw new InvalidDataException("Expanded model lifecycle receipt does not match its emitting frame and allocation identity.");
        }
        return actor;
    }

    private void AcknowledgeHostedNativeActivation(NativeBattleModelLifecycleServices services, NativeBattleModelParticipantActivated receipt)
    {
        BattleCombatant actor = RequireEmittingHostedActor(services, receipt);
        if (receipt.OriginalSlot is { } slot)
        {
            actor = (slot.Side == actor.Side ? FindAllocatedNativeActor(slot.Offset) : null)
                ?? throw new InvalidDataException("Hosted native activation lost its current logical slot allocation.");
        }
        StopTurnAdvance();
        actor.NativeCombatantFlags &= ~0xC2u;
        QueueExpandedAiCallback(actor, 0);
        Log("Activated", actor.Id, null, 0, $"{actor.Name} enters battle.");
    }

    private void ApplyHostedAllocationStatistics(NativeBattleModelLifecycleServices services, NativeBattleAllocationStatisticsRequested receipt)
    {
        BattleCombatant actor = RequireEmittingHostedActor(services, receipt);
        if (services.Allocation is not { } allocation || allocation.Owner != receipt.Owner || allocation.SceneSlot != receipt.SceneSlot ||
            nativeScriptState.AllocationControl?.SceneSlot != receipt.SceneSlot || !ReferenceEquals(ResolveLastNativeAllocation(), actor))
        {
            throw new InvalidDataException("Hosted allocation statistics do not match the current native allocation globals.");
        }
        InitializeNativeAllocatedStatistics(actor);
    }

    private void AcknowledgeExpandedModelRelease(BattleModelRoster roster, NativeBattleModelParticipantReleased receipt)
    {
        ApplyExpandedModelRelease(RequireExpandedLifecycleActor(roster, receipt));
    }

    private void ApplyExpandedModelRelease(BattleCombatant actor)
    {
        actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
        actor.NativeStatus0 |= 1;
        // The completion receipt in this frame can restore a candidate before the next logical tick prunes the queue.
        CancelIneligibleExpandedAiCallbacks(actor);
    }

    private void AcknowledgeExpandedModelSuspension(BattleModelRoster roster, NativeBattleModelParticipantSuspended receipt)
    {
        ApplyExpandedModelSuspension(RequireExpandedLifecycleActor(roster, receipt), receipt);
    }

    private void ApplyExpandedModelSuspension(BattleCombatant actor, NativeBattleModelParticipantSuspended receipt)
    {
        actor.NativeCombatantFlags |= 0x40;
        actor.NativeStatus0 |= 1;
        CancelIneligibleExpandedAiCallbacks(actor);
        ObserveEncounterNativeTerminal(actor, receipt.Sequence, receipt.ActiveHeader, receipt.Owner);
    }

    private BattleCombatant RequireExpandedLifecycleActor(BattleModelRoster roster, NativeBattleModelLifecycleReceipt receipt)
    {
        if (!roster.Contains(receipt.Owner) || !StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(receipt.Owner), receipt.ActorId) ||
            !byId.TryGetValue(receipt.ActorId, out BattleCombatant? actor) || actor.Slot is null || RequirePresentationModelOwner(roster, actor) != receipt.Owner)
        {
            throw new InvalidDataException("Expanded model lifecycle receipt does not belong to its current allocated actor.");
        }
        return actor;
    }
}
