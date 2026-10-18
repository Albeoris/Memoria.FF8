namespace Memoria.FF8.Runtime;

/// <summary>Executable-owned48D200 command252/id1 packet;MagicId is opcode30's BYTE operand, not a kernel action index.</summary>
public sealed record BattleNativeGraphicCommand(byte MagicId);

public sealed partial class BattleSession
{
    private void QueueNativeGraphicCommand(BattleCombatant actor, byte magicId, byte sequence, ushort targetMask)
    {
        actor.NativePreviousTargetMask = targetMask;
        //487DF0 LABEL375's non-Magic/non-Item branch reads EnemyAttack[id1] bit80 even for252.
        if ((FindInstalledNativeAction(8, 1).AttackFlags & 0x80) != 0)
        {
            targetMask |= 0x4000;
        }
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 16 && (targetMask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleAttackCommand command = new(actor.Id, target.Id, BattleAbility.EnemyAbility, 1) { NativeAiCallbackStage = nativeAiCallbackStage, NativeQueuedCommand = 252, NativeSequence = sequence, NativeTargetMask = targetMask, NativeGraphic = new(magicId) };
        EnqueueNativePacket(actor, command, $"native graphics252/1 effect{magicId}");
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

    private void EnqueueNativePacket(BattleCombatant actor, BattleAttackCommand command, string name, bool playerInput = false)
    {
        command = PrepareNativeEventCommand(actor, command);
        actor.Atb = 0;
        actor.IsBusy = true;
        if (playerInput)
        {
            EnqueueNativeInputCommand(command);
        }
        else
        {
            actions.Enqueue(command);
        }
        Log("Queued", actor.Id, command.TargetId, command.ActionId, $"{actor.Name}: {name} queued.");
        if (nativeAiDepth != 0 && !IsNativeReturnDamageAction(command.NativeAction))
        {
            CompleteNativeAiReturnDamage(actor, NativeCommandPresentation(command));
        }
    }
}
