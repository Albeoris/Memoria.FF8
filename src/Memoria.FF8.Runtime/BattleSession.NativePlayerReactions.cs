namespace Memoria.FF8.Runtime;

public enum BattleNativePlayerReaction
{
    Counter,
    AutoPotion,
    AngeloRush,
    AngeloRecover,
    AngeloReverse
}

public sealed partial class BattleSession
{
    private void RunNativePlayerReaction(BattleCombatant actor, int stage)
    {
        if (!actor.IsActive || actor.NativePlayerStatRules is null)
        {
            return;
        }
        if (stage == 3)
        {
            RunNativePlayerDeathSupport(actor);
            return;
        }
        if (stage != 2 || (NativeSavedStatusBits(actor) & 5) != 0 || (NativeBattleStatusBits(actor) & 0x4009) != 0 || (actor.NativeCombatantFlags & 0x4000) != 0)
        {
            return;
        }
        uint flags = actor.NativePlayerStatRules.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native player reactions require the equipped character-ability flags.");
        //48B5F0 clears B98/B99; B9E has no established initialization and remains unknown until a primary commit.
        byte attacker = actor.LastNativeAction?.AttackerIndex ?? 0;
        if (attacker >= 3 && (flags & 4) != 0)
        {
            byte attackClass = actor.LastNativeAction is { } last ? last.AttackType ?? throw new NotSupportedException("Native Counter requires the received attack's original class byte.") : (byte)0;
            if (attackClass == 0)
            {
                QueueNativePlayerReaction(actor, BattleNativePlayerReaction.Counter, 1, 0, checked((ushort)(1 << attacker)));
            }
        }
        RunNativePlayerDamageSupport(actor);
        if ((flags & 0x40000) == 0 || attacker == actor.NativeIndex)
        {
            return;
        }
        byte healing = actor.LastNativeHealing ?? throw new NotSupportedException("Native Auto-Potion requires the last primary commitment's healing byte.");
        if (healing != 0 || (NativeSavedStatusBits(actor) & 0x200) == 0)
        {
            return;
        }
        int missing = actor.MaximumHp - actor.CurrentHp;
        if (missing <= 200)
        {
            return;
        }
        int[] choices = missing <= 1000 ? [1] : [3, 1, 2, 4, 5, 9];
        int item = choices.FirstOrDefault(id => nativeItems.Any(stock => stock.ItemId == id && stock.Count != 0));
        if (item != 0)
        {
            QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AutoPotion, 4, checked((ushort)item), checked((ushort)(1 << actor.NativeIndex)));
        }
    }

    private void QueueNativePlayerReaction(BattleCombatant actor, BattleNativePlayerReaction reaction, byte command, ushort id, ushort mask)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, command, id);
        ValidateNativeActionContract(native);
        actor.NativePreviousTargetMask = mask;
        if (command == 4)
        {
            mask |= (ushort)(((native.TargetFlags.GetValueOrDefault() & 1) != 0 ? 0x4000 : 0) | ((native.TargetFlags.GetValueOrDefault() & 2) != 0 ? 0x2000 : 0));
        }
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 16 && (mask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleAbility ability = reaction == BattleNativePlayerReaction.Counter ? BattleAbility.Attack : reaction == BattleNativePlayerReaction.AutoPotion ? BattleAbility.Item : BattleAbility.EnemyAbility;
        BattleAttackCommand queued = new(actor.Id, target.Id, ability, id) { NativePlayerReaction = reaction, NativeQueuedCommand = command, NativeAction = native, NativeTargetMask = mask, ReservedNativeItemSlot = command == 4 ? ConsumeNativeItem(id) : null };
        //483400 bypasses4847F0: keep ATB and Defend/Charged, and do not run the compelled player-input selector.
        actor.IsBusy = true;
        actions.Enqueue(queued);
        Log("Queued", actor.Id, target.Id, id, $"{actor.Name}: {native.Name ?? reaction.ToString()} queued.");
    }

    private static void ValidateNativePlayerReaction(BattleCombatant actor, BattleAbility ability, BattleNativeAction? native, BattleNativePlayerReaction? reaction)
    {
        if (reaction is null)
        {
            return;
        }
        uint? flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags;
        bool valid = actor.IsPlayer && flags.HasValue && (reaction switch
        {
            BattleNativePlayerReaction.Counter => ability == BattleAbility.Attack && native?.Command == 1 && native.ActionId == 0 && (flags & 4) != 0,
            BattleNativePlayerReaction.AutoPotion => ability == BattleAbility.Item && native?.Command == 4 && native.ActionId is 1 or 2 or 3 or 4 or 5 or 9 && (flags & 0x40000) != 0,
            BattleNativePlayerReaction.AngeloRush => ability == BattleAbility.EnemyAbility && actor.ModelId == 4 && native?.Command == 240 && native.ActionId == 11,
            BattleNativePlayerReaction.AngeloRecover => ability == BattleAbility.EnemyAbility && actor.ModelId == 4 && native?.Command == 240 && native.ActionId == 12,
            BattleNativePlayerReaction.AngeloReverse => ability == BattleAbility.EnemyAbility && actor.ModelId == 4 && native?.Command == 240 && native.ActionId == 13,
            _ => false
        });
        if (!valid)
        {
            throw new InvalidDataException("Saved player reaction contradicts its native command and equipped passive ability.");
        }
    }
}
