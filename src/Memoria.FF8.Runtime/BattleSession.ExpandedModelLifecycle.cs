namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsRoster RequireExpandedLifecycleRoster()
    {
        return expandedPresentationProcess?.Graphics.PresentationRoster ?? throw new InvalidOperationException("Expanded model lifecycle requires its attached presentation roster.");
    }

    private void AcknowledgeExpandedModelRelease(NativeBattleGraphicsRoster roster, NativeBattleModelParticipantReleased receipt)
    {
        BattleCombatant actor = RequireExpandedLifecycleActor(roster, receipt);
        actor.NativeCombatantFlags = (actor.NativeCombatantFlags | 0x40) & ~1u;
        actor.NativeStatus0 |= 1;
    }

    private void AcknowledgeExpandedModelSuspension(NativeBattleGraphicsRoster roster, NativeBattleModelParticipantSuspended receipt)
    {
        BattleCombatant actor = RequireExpandedLifecycleActor(roster, receipt);
        actor.NativeCombatantFlags |= 0x40;
        actor.NativeStatus0 |= 1;
        ObserveEncounterNativeTerminal(actor, receipt.Sequence, receipt.ActiveHeader, checked((byte)receipt.Row));
    }

    private BattleCombatant RequireExpandedLifecycleActor(NativeBattleGraphicsRoster roster, NativeBattleModelLifecycleReceipt receipt)
    {
        if (!UsesExpandedRoster || !roster.ContainsRow(receipt.Row) || !StringComparer.Ordinal.Equals(roster[receipt.Row].ActorId, receipt.ActorId) ||
            !byId.TryGetValue(receipt.ActorId, out BattleCombatant? actor) || actor.Slot is null || actor.IsPlayer != roster[receipt.Row].IsParty || RequireExpandedPresentationRow(roster, actor) != receipt.Row)
        {
            throw new InvalidDataException("Expanded model lifecycle receipt does not belong to its current allocated actor row.");
        }
        return actor;
    }
}
