namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void AcknowledgeActionOutcomeEntry(NativeBattleActionOutcomeEntry receipt, long? actionToken)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidDataException("Native47DF60 action process entry requires its bound presentation.");
        if (actionToken != receipt.Invocation?.Token || receipt.Invocation is null || Outcome != BattleOutcome.Active || expandedHostedTerminal is not null)
        {
            throw new InvalidDataException("Native47DF60 action process entry does not match the current battle action.");
        }
        if (receipt.Invocation.Provenance == BattlePresentationInvocationProvenance.Gameplay &&
            (CurrentExpandedHostedInvocation is not { Command: { NativeAiCallbackStage: 3, NativeAction: { Command: 8, ActionId: 356, MagicId: 77, KernelSection: 3, KernelIndex: 356 } }, Header: { Source: BattleEnemyActorModel source, Command: 8, Action: 356, Sequence: 18, Effect: 77 } } current ||
                current.HeaderReference != receipt.Invocation.Header || source != receipt.Source || current.Command.ActorId != source.Actor.ActorId ||
                current.Command.TargetId != source.Actor.ActorId || GetActor(source.Actor.ActorId).ModelId != 126))
        {
            throw new InvalidDataException("Native47DF60 action process entry requires its current authored death action.");
        }
        process.ValidateActionOutcomeEntry(receipt);
        _ = RequireCanonicalGraphicsActorId(receipt.Source);
        BattleCombatant?[] occupied = new BattleCombatant?[3];
        for (int index = 0; index < 3; index++)
        {
            BattleCombatant? actor = allies.SingleOrDefault(candidate => candidate.NativeActorIndex == index);
            if (actor is not null)
            {
                NativeBattlePhysicalStatusDword before = receipt.BeforeStatuses.Rows[index];
                NativeBattlePhysicalStatusDword after = receipt.AfterStatuses.Rows[index];
                if (before.KnownMask != uint.MaxValue || before.KnownBits != actor.NativeStatus1 || after.KnownMask != uint.MaxValue || after.KnownBits != (actor.NativeStatus1 & 0x7FFFFFFF) ||
                    process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)index))) != ReadPresentationModelOwner(actor.Id))
                {
                    throw new InvalidDataException("Native47DF60 action process entry changed its occupied physical status projection.");
                }
                occupied[index] = actor;
            }
        }
        process.AcknowledgeActionOutcomeEntry(receipt);
        foreach (BattleCombatant? actor in occupied)
        {
            if (actor is not null)
            {
                SetNativeAiStatus(actor, 47, 0);
            }
        }
    }
}
