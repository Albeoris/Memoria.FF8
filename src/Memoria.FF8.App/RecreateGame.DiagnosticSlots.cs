using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly List<DiagnosticSlotsAdmissionReceipt> diagnosticSlotsAdmissions = [];

    private void AdmitDiagnosticSlots(BattleSession active, DiagnosticBattleDriver driver, BattleDiagnosticLimitSelectionReceipt selection)
    {
        DiagnosticSlotsSource source = world!.DiagnosticReceipt!.SlotsSource ?? throw new InvalidDataException("Slots admission lacks its initialized original source.");
        DiagnosticSlotsDeclaration declaration = diagnosticDriverRecipe!.SlotsAdmission!;
        DiagnosticSlotsObservation start = diagnosticTelemetry!.SlotsStart ?? throw new InvalidDataException("Slots admission lacks its independent scored-window start.");
        if (diagnosticSlotsAdmissions.Count != driver.Execution - 1 || diagnosticSlotsAdmissions.Count >= 2)
        {
            throw new InvalidOperationException("Slots admission cannot replace or repeat an execution receipt.");
        }
        DiagnosticSlotsAdmissionReceipt receipt = new(driver.Execution, source.Fingerprint, declaration, diagnosticDriverRecipe.Input, start, diagnosticDriverRecipe.LimitSelection!, selection);
        diagnosticSlotsAdmissions.Add(receipt);
        DiagnosticSlotsAdmission admission = new(source, receipt);
        List<DiagnosticSlotsRollReceipt> rolls = [];
        DiagnosticSlotsDoReceipt? accepted = null;
        try
        {
            for (int attempt = 1; attempt <= declaration.MaximumRolls; attempt++)
            {
                DiagnosticSlotsObservation before = DiagnosticSlotsObservations.Observe(active, source.BattleInstanceId, source.ActorId, source);
                if (before.Seconds - start.Seconds > declaration.MaximumSeconds)
                {
                    throw new NotSupportedException("The declared ordinary Slots roll deadline expired.");
                }
                BattleNativeSlotsRoll result = active.RollNativeSlots(source.ActorId);
                DiagnosticSlotsObservation after = DiagnosticSlotsObservations.Observe(active, source.BattleInstanceId, source.ActorId, source);
                DiagnosticSlotsRollReceipt roll = new(attempt, before, after, result);
                rolls.Add(roll);
                admission.Rolled(roll);
                if (admission.Matched)
                {
                    DiagnosticSlotsObservation beforeDo = DiagnosticSlotsObservations.Observe(active, source.BattleInstanceId, source.ActorId, source);
                    ApplyBattleCommand(diagnosticDriverRecipe.Input);
                    DiagnosticSlotsObservation afterDo = DiagnosticSlotsObservations.Observe(active, source.BattleInstanceId, source.ActorId, source);
                    BattleAttackCommand command = active.QueuedActions.Single(candidate => candidate.ActorId == source.ActorId && candidate.NativeQueuedCommand == 16);
                    BattleNativeAction native = command.NativeAction ?? throw new InvalidDataException("Slots Do lost its actual native action.");
                    if (command.Ability != BattleAbility.EnemyAbility || command.ActionId != declaration.MagicAction || native.KernelSection != 1 || native.KernelIndex != declaration.MagicAction || !native.RawRecord.AsSpan().SequenceEqual(source.ActionRecord.AsSpan()))
                    {
                        throw new InvalidDataException("Slots Do did not publish the initialized native command action.");
                    }
                    accepted = new(beforeDo, afterDo, command.ActorId, command.TargetId, checked((byte)(active.ObserveDiagnosticCombatants().Single(candidate => candidate.Id == command.TargetId).NativeIndex ?? throw new InvalidDataException("Slots target lost its physical index."))), command.NativeQueuedCommand!.Value, native.ActionId, native.MagicId, command.NativeTargetMask, command.NativeSlots ?? throw new InvalidDataException("Slots Do lost its consumed owned result."));
                    admission.Did(accepted);
                    return;
                }
            }
            throw new NotSupportedException("Bounded ordinary Slots rolls did not produce the declared spell.");
        }
        finally
        {
            // Preserve every observed genuine roll, including a failing validation, without reconstructing the native battle.
            diagnosticSlotsAdmissions[^1] = receipt with { Rolls = rolls.ToArray(), Do = accepted };
        }
    }
}
