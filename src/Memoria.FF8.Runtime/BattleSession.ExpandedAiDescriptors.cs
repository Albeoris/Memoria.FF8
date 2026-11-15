namespace Memoria.FF8.Runtime;

public enum BattleExpandedAiDescriptorKind
{
    Barrier,
    Remove,
    Allocate,
    Activate,
    DeathRelease
}

public sealed record BattleExpandedAiDescriptor(BattleExpandedAiDescriptorKind Kind, int Operand = 0, int PhysicalIndex = 0, bool Activate = false);

public sealed partial class BattleSession
{
    private bool HasExpandedAiDescriptors => actions.Any(command => command.ExpandedAiDescriptor is not null);

    private void QueueExpandedAiDescriptor(BattleCombatant actor, BattleExpandedAiDescriptor operation)
    {
        if (!UsesOriginalEnemySlots)
        {
            throw new NotSupportedException("Expanded AI graphics ordering requires original enemy slots.");
        }
        StopTurnAdvance();
        actions.Enqueue(new(actor.Id, actor.Id, BattleTargetRequest.None, BattleAbility.EnemyAbility) { ExpandedAiDescriptor = operation });
    }

    private void ValidateExpandedAiDescriptor(BattleAttackCommand command)
    {
        BattleExpandedAiDescriptor operation = command.ExpandedAiDescriptor!;
        bool valid = operation.Kind switch
        {
            BattleExpandedAiDescriptorKind.Barrier => operation is { Operand: 0, PhysicalIndex: 0, Activate: false },
            BattleExpandedAiDescriptorKind.Remove => (operation.Operand == 200 || (uint)operation.Operand < NativeBattleSlots.EnemyCount) && operation is { PhysicalIndex: 0, Activate: false },
            BattleExpandedAiDescriptorKind.Allocate => (uint)operation.Operand < NativeBattleSlots.FormationSlotCount && (operation.PhysicalIndex == 0 || operation.PhysicalIndex is >= NativeBattleSlots.AllyCount and < NativeBattleSlots.Count),
            BattleExpandedAiDescriptorKind.Activate => (operation.Operand == 209 || NativeBattleSlots.Contains(operation.Operand)) && operation is { PhysicalIndex: 0, Activate: false },
            BattleExpandedAiDescriptorKind.DeathRelease => operation is { Operand: 0, PhysicalIndex: 0, Activate: false } && GetActor(command.ActorId).NativeAi?.DeathSequenceReleasesAllocation == true,
            _ => false
        };
        BattleAttackCommand expected = new(command.ActorId, command.ActorId, BattleTargetRequest.None, BattleAbility.EnemyAbility) { ExpandedAiDescriptor = operation };
        if (!UsesOriginalEnemySlots || !valid || !command.MagicCasts.IsEmpty || (command with { MagicCasts = expected.MagicCasts }) != expected || GetActor(command.ActorId).NativeAi is null)
        {
            throw new InvalidDataException("Invalid expanded AI graphics descriptor or source actor.");
        }
    }

    private void DispatchExpandedAiDescriptor(BattleAttackCommand command, BattleExpandedAiDescriptor operation)
    {
        BattleCombatant caller = GetActor(command.ActorId);
        switch (operation.Kind)
        {
            case BattleExpandedAiDescriptorKind.Barrier:
                // TODO(TD-009): The expanded host serializes graphical work in this FIFO. An action retains the head until its displayed effect and model sequence drain.
                break;
            case BattleExpandedAiDescriptorKind.Remove:
                BattleCombatant removed = operation.Operand == 200 ? caller : FindNativeActor(operation.Operand + NativeBattleSlots.AllyCount);
                ReleaseNativeParticipant(removed);
                break;
            case BattleExpandedAiDescriptorKind.Allocate:
                AllocateNativeEnemy(caller, operation.Operand, operation.PhysicalIndex, operation.Activate);
                break;
            case BattleExpandedAiDescriptorKind.Activate:
                ActivateNativeParticipant(FindActiveActivationActor(operation.Operand == 209 ? nativeLastAllocatedTarget : new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(operation.Operand)));
                break;
            case BattleExpandedAiDescriptorKind.DeathRelease:
                if (caller.IsActive && (NativeSavedStatusBits(caller) & 1) != 0)
                {
                    ReleaseNativeParticipant(caller);
                }
                break;
        }
    }
}
