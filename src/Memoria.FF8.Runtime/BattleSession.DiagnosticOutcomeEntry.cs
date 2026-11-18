namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void AcknowledgeDiagnosticOutcomeEntry(NativeBattleDiagnosticOutcomeEntry receipt, long? actionToken)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidDataException("Native47DF60 diagnostic process entry requires its bound presentation.");
        if (actionToken != receipt.Invocation?.Token || receipt.Invocation is null || receipt.Invocation.Provenance != BattlePresentationInvocationProvenance.ExternalDiagnostic ||
            Outcome != BattleOutcome.Active || expandedHostedTerminal is not null)
        {
            throw new InvalidDataException("Native47DF60 diagnostic process entry does not match the current external action.");
        }
        process.ValidateDiagnosticOutcomeEntry(receipt);
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
                    throw new InvalidDataException("Native47DF60 diagnostic process entry changed its occupied physical status projection.");
                }
                occupied[index] = actor;
            }
        }
        process.AcknowledgeDiagnosticOutcomeEntry(receipt);
        foreach (BattleCombatant? actor in occupied)
        {
            if (actor is not null)
            {
                SetNativeAiStatus(actor, 47, 0);
            }
        }
    }
}
