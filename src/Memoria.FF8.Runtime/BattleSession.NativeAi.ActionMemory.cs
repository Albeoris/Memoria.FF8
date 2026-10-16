namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void RecordNativeActionMemory(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation action)
    {
        if (action.NativeAction is { } resolved)
        {
            byte commandByte = action.Ability == BattleAbility.Draw ? (byte)6 : resolved.Command;
            StoreNativeActionMemory(actor, recipient, action.Ability, commandByte, resolved.ActionId, resolved.Element, (byte)(resolved.AttackFlags & 3));
            return;
        }
        if (action.Ability is not (BattleAbility.Attack or BattleAbility.EnemyAbility))
        {
            BattleActionDefinition definition = FindAction(action.Ability == BattleAbility.Draw ? BattleAbility.Magic : action.Ability, action.ActionId);
            if (definition.NativeAction is { } native)
            {
                byte commandByte = action.Ability == BattleAbility.Draw ? (byte)6 : native.Command;
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
            NativeBattleAiDefinition ai = actor.NativeAi ?? throw new InvalidDataException("Native enemy action memory requires the installed ability owner.");
            NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + action.ActionId];
            command = ability.Command;
            id = ability.Id;
            if (ability.Command == 2)
            {
                BattleActionDefinition spell = FindAction(BattleAbility.Magic, ability.Id);
                element = spell.Element;
                attackType = spell.NativeAttackFlags is byte flags ? (byte)(flags & 3) : null;
            }
            else
            {
                element = ability.Element;
                attackType = ability.NativeAttackFlags is byte flags ? (byte)(flags & 3) : null;
            }
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
        recipient.LastNativeAction = new(attackType, checked((byte)NativeModelId(actor)), checked((byte)command), unchecked((byte)id), checked((byte)element), checked((byte)actor.NativeIndex));
        recipient.LastAttackerId = actor.Id;
        recipient.LastAttackerAbility = ability;
    }
}
