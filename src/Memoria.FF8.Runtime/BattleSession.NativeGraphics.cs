namespace Memoria.FF8.Runtime;

/// <summary>Executable-owned48D200 command252/id1 packet;MagicId is opcode30's BYTE operand, not a kernel action index.</summary>
public sealed record BattleNativeGraphicCommand(byte MagicId);

public sealed partial class BattleSession
{
    private void QueueNativeGraphicCommand(BattleCombatant actor, byte magicId, byte sequence, AiTargetSelection selected)
    {
        actor.PreviousAiTarget = selected.PreviousTarget;
        // 487DF0 reads EnemyAttack[id1] bit80 for graphics command 252.
        BattleTargetRequest request = ApplyOriginalAiTargetFlags(selected, 0, (NativeGraphicsTargetFlags() & 0x80) != 0);
        var originalSlots = request.OriginalProjection!.Operands.Slots;
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.Slot is { } slot && originalSlots.Contains(slot)) ?? actor;
        BattleAttackCommand command = new(actor.Id, target.Id, request, BattleAbility.EnemyAbility, 1) { NativeAiCallbackStage = nativeAiCallbackStage, NativeQueuedCommand = 252, NativeSequence = sequence, NativeGraphic = new(magicId) };
        EnqueueNativePacket(actor, command, $"native graphics252/1 effect{magicId}");
    }

    private byte NativeGraphicsTargetFlags()
    {
        return (byte)(FindInstalledNativeAction(8, 1).AttackFlags & 0x80);
    }

    private BattleAttackCommand PrepareNativeEventCommand(BattleCombatant actor, BattleAttackCommand command)
    {
        if ((nativeAiDepth != 0 || nativeCallbackTail is not null || nativeProductionContext is not null) && command.NativeRenzokuken is null)
        {
            command = PrepareNativeDrawCommand(actor, command);
            command = PrepareNativeSpecialCommand(actor, command);
            command = PrepareIndependentGuardianItem(actor, command);
            PrepareNativeLimitCommand(command);
            command = ProduceNativeQueuedCommand(actor, GetActor(command.TargetId), command);
        }
        return command;
    }

    private void EnqueueNativePacket(BattleCombatant actor, BattleAttackCommand command, string name, bool playerInput = false, BattleTargetRequest? diagnosticSelection = null)
    {
        if (CaptureEncounterCounterCommand(actor, command, name, playerInput, diagnosticSelection))
        {
            return;
        }
        command = PrepareNativeEventCommand(actor, command);
        NativeBattleCommandBanks? input = playerInput ? PrepareNativeInputCommand(command) : null;
        PublishNativePacket(actor, command, name, input, diagnosticSelection);
    }

    private void PublishNativePacket(BattleCombatant actor, BattleAttackCommand command, string name, NativeBattleCommandBanks? input, BattleTargetRequest? diagnosticSelection = null)
    {
        StopTurnAdvance();
        //47DDA0 completes initialization, reaction and periodic events without consuming the actor's gauge.
        if (nativeProductionContext?.CompletionCallback != 0x47DDA0)
        {
            actor.Atb = 0;
        }
        actor.IsBusy = true;
        if (input is not null)
        {
            nativeCommandBanks = input;
        }
        else
        {
            actions.Enqueue(command);
        }
        Log("Queued", actor.Id, command.TargetId, command.ActionId, $"{actor.Name}: {name} queued.");
        ObserveDiagnosticCounterCommand(actor, command, diagnosticSelection);
        if ((nativeAiDepth != 0 || flushingEncounterCounterCommands) && !IsNativeReturnDamageAction(command.NativeAction))
        {
            CompleteNativeAiReturnDamage(actor, NativeCommandPresentation(command));
        }
        FlushEncounterCounterCommands();
    }
}
