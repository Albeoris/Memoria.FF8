namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RecordNativeActionMemory(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation action)
    {
        //48FE20 internal0 sets DF5;494410 skips surviving full-memory writes, including a successful revival.
        if (nativeContactWorking is not null && nativeActionScratch.Command == 0 && recipient.CurrentHp != 0)
        {
            return;
        }
        if (action.Ability == BattleAbility.Draw && action.NativeDraw is { } draw && (!action.DrawCast || draw.Failed))
        {
            StoreNativeActionMemory(actor, recipient, BattleAbility.Draw, draw.Failed ? 0 : 6, draw.Failed ? 9 : draw.Selection, 0, 3);
            return;
        }
        if (action.NativeAction is { } resolved)
        {
            byte commandByte = action.NativeQueuedCommand ?? (action.Ability == BattleAbility.Draw ? (byte)6 : resolved.Command);
            StoreNativeActionMemory(actor, recipient, action.Ability, commandByte, resolved.ActionId, resolved.Element, (byte)(resolved.AttackFlags & 3));
            return;
        }
        if (action.Ability is not (BattleAbility.Attack or BattleAbility.EnemyAbility))
        {
            BattleActionDefinition definition = FindAction(action.Ability == BattleAbility.Draw ? BattleAbility.Magic : action.Ability, action.ActionId);
            if (definition.NativeAction is { } native)
            {
                byte commandByte = action.NativeQueuedCommand ?? (action.Ability == BattleAbility.Draw ? (byte)6 : native.Command);
                StoreNativeActionMemory(actor, recipient, action.Ability, commandByte, native.ActionId, native.Element, (byte)(native.AttackFlags & 3));
                return;
            }
        }
        int command;
        int id = action.ActionId;
        int element;
        byte? attackType;
        if (action.Ability == BattleAbility.Attack)
        {
            command = 1;
            id = 0;
            element = actor.ElementAttack;
            attackType = 0;
        }
        else if (action.Ability == BattleAbility.EnemyAbility)
        {
            throw new InvalidDataException("Native enemy action memory requires its exact installed command/action metadata.");
        }
        else
        {
            BattleActionDefinition ability = FindAction(action.Ability == BattleAbility.Draw ? BattleAbility.Magic : action.Ability, action.ActionId);
            command = action.Ability switch
            {
                BattleAbility.Magic => 2,
                BattleAbility.Item => 4,
                BattleAbility.Draw => 6,
                BattleAbility.GuardianForce => 254,
                _ => throw new NotSupportedException("Native action memory requires its original queued command byte.")
            };
            if (action.Ability == BattleAbility.GuardianForce)
            {
                id += 64;
            }
            element = ability.Element;
            attackType = ability.NativeAttackFlags is byte flags ? (byte)(flags & 3) : null;
        }
        //494410 publishes these target-ownedU8 fields after effect commitment and before counter/death AI.
        StoreNativeActionMemory(actor, recipient, action.Ability, command, id, element, attackType);
    }

    private void StoreNativeActionMemory(BattleCombatant actor, BattleCombatant recipient, BattleAbility ability, int command, int id, int element, byte? attackType)
    {
        if (nativeContactWorking is not null)
        {
            //494410 never writes B99, including its a9=1 secondary death path.
            attackType = recipient.LastNativeAction?.AttackType;
        }
        recipient.LastNativeAction = new(attackType, checked((byte)NativeModelId(actor)), checked((byte)command), unchecked((byte)id), checked((byte)element), checked((byte)actor.NativeIndex));
        recipient.LastAttackerId = actor.Id;
        recipient.LastAttackerAbility = ability;
    }

    private static void StoreNativeAttackClass(BattleCombatant recipient, byte attackClass)
    {
        //48FE20 writes B99 before the independently gated494410 B98/B9B/B9C/B9D/B9F memory writes.
        NativeBattleLastAction previous = recipient.LastNativeAction ?? new(null, null, null, null, null, null);
        recipient.LastNativeAction = previous with { AttackType = attackClass };
    }
}
