namespace Memoria.FF8.Runtime;

public enum BattleNativePlayerReaction
{
    Counter,
    AutoPotion,
    AngeloRush,
    AngeloRecover,
    AngeloReverse,
    AngeloPeriodic,
    AutomaticGuardian,
    PhoenixRevival
}

public sealed partial class BattleSession
{
    private void InterpretNativePlayerReaction(BattleCombatant actor, int stage)
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
        //48B5F0 clears B98/B99; B9E retains its physical byte until a primary commit.
        byte attacker = actor.LastNativeAction?.AttackerIndex ?? throw new NotSupportedException("Native player reactions require the retained attacker BYTE B98.");
        if (attacker >= 3 && (flags & 4) != 0)
        {
            byte attackClass = actor.LastNativeAction?.AttackType ?? throw new NotSupportedException("Native Counter requires the received attack's original class byte.");
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
        queued = PrepareNativeEventCommand(actor, queued);
        if (nativeProductionContext is not null && queued.NativeHeaderIndex is int headerIndex)
        {
            byte lane = nativeActionScratch.EventLane ?? throw new NotSupportedException("Native483400 requires its current event batch BYTE B0E.");
            if (lane == 0)
            {
                //483400 clears the last published header's BYTE17 before queuing its intermediate48D1F0 callback.
                nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(headerIndex, 1);
                if (NativeGraphics is { } graphics)
                {
                    NativeGraphics = QueueNativeDescriptor(graphics, 10, 0x47DD90);
                }
            }
        }
        //483400 bypasses4847F0: keep ATB and Defend/Charged, and do not run the compelled player-input selector.
        actor.IsBusy = true;
        actions.Enqueue(queued);
        Log("Queued", actor.Id, target.Id, id, $"{actor.Name}: {native.Name ?? reaction.ToString()} queued.");
    }

    private void ValidateNativePlayerReaction(BattleCombatant actor, BattleAbility ability, BattleNativeAction? native, BattleNativePlayerReaction? reaction)
    {
        if (reaction is null)
        {
            return;
        }
        uint? flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags;
        byte learned = definition.LimitProgress?.AngeloFlags ?? 0;
        bool supportOwner = reaction is not (BattleNativePlayerReaction.AngeloPeriodic or BattleNativePlayerReaction.AutomaticGuardian or BattleNativePlayerReaction.PhoenixRevival) || nativeSupport is { EntryInitialized: true };
        bool valid = actor.IsPlayer && supportOwner && (reaction switch
        {
            BattleNativePlayerReaction.Counter => flags.HasValue && ability == BattleAbility.Attack && native?.Command == 1 && native.ActionId == 0 && (flags & 4) != 0,
            BattleNativePlayerReaction.AutoPotion => flags.HasValue && ability == BattleAbility.Item && native?.Command == 4 && native.ActionId is 1 or 2 or 3 or 4 or 5 or 9 && (flags & 0x40000) != 0,
            BattleNativePlayerReaction.AngeloRush => (learned & 1) != 0 && ability == BattleAbility.EnemyAbility && actor.ModelId == 4 && native?.Command == 240 && native.ActionId == 11,
            BattleNativePlayerReaction.AngeloRecover => (learned & 2) != 0 && ability == BattleAbility.EnemyAbility && actor.ModelId == 4 && native?.Command == 240 && native.ActionId == 12,
            BattleNativePlayerReaction.AngeloReverse => (learned & 4) != 0 && ability == BattleAbility.EnemyAbility && actor.ModelId == 4 && native?.Command == 240 && native.ActionId == 13,
            BattleNativePlayerReaction.AngeloPeriodic => ability == BattleAbility.EnemyAbility && native?.Command == 240 && native.ActionId is 12 or 13 or 14 && (learned & (native.ActionId == 12 ? 2 : native.ActionId == 13 ? 4 : 8)) != 0,
            BattleNativePlayerReaction.AutomaticGuardian => ability == BattleAbility.EnemyAbility && native?.Command == 245 && native.ActionId is 0 or 1 or 7 or 8 or 9 or 10,
            BattleNativePlayerReaction.PhoenixRevival => ability == BattleAbility.EnemyAbility && native?.Command == 0 && native.ActionId == 8,
            _ => false
        });
        if (!valid)
        {
            throw new InvalidDataException("Saved player reaction contradicts its native command and equipped passive ability.");
        }
    }
}
