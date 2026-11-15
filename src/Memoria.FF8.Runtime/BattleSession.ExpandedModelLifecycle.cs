namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleModelRoster RequireExpandedLifecycleRoster()
    {
        return expandedPresentationProcess?.Graphics.ActorModels.Roster ?? throw new InvalidOperationException("Expanded model lifecycle requires its attached presentation roster.");
    }

    private void AcknowledgeExpandedModelRelease(BattleModelRoster roster, NativeBattleModelParticipantReleased receipt)
    {
        BattleCombatant actor = RequireExpandedLifecycleActor(roster, receipt);
        actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
        actor.NativeStatus0 |= 1;
    }

    private void AcknowledgeExpandedModelSuspension(BattleModelRoster roster, NativeBattleModelParticipantSuspended receipt)
    {
        BattleCombatant actor = RequireExpandedLifecycleActor(roster, receipt);
        actor.NativeCombatantFlags |= 0x40;
        actor.NativeStatus0 |= 1;
        ObserveEncounterNativeTerminal(actor, receipt.Sequence, receipt.ActiveHeader, receipt.Owner);
    }

    private BattleCombatant RequireExpandedLifecycleActor(BattleModelRoster roster, NativeBattleModelLifecycleReceipt receipt)
    {
        if (!UsesExpandedRoster || !roster.Contains(receipt.Owner) || !StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(receipt.Owner), receipt.ActorId) ||
            !byId.TryGetValue(receipt.ActorId, out BattleCombatant? actor) || actor.Slot is null || RequirePresentationModelOwner(roster, actor) != receipt.Owner)
        {
            throw new InvalidDataException("Expanded model lifecycle receipt does not belong to its current allocated actor.");
        }
        return actor;
    }
}
