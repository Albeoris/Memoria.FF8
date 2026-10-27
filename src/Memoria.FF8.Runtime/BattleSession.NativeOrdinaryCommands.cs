namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void OnNativePlayerCommandSubmitted(BattleCombatant actor, byte command)
    {
        if (actor.IsPlayer)
        {
            StopTurnAdvance();
        }
        if (actor.IsPlayer && command is not (0 or 254))
        {
            RemoveNativeUiReady(actor);
        }
        //4847F0 clears Defend/Charged when accepting an ordinary player command;0 and254 bypass this producer.
        if (actor.IsPlayer && command is not (0 or 254))
        {
            actor.NativeStatus1 &= 0xFF77FFFF;
        }
    }

    private void ResolveNativeLevelChange(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleNativeAction native)
    {
        if ((recipient.NativeCombatantFlags & 0x8000) != 0 || native.StatusAttack <= NextRandomByte())
        {
            Log("Miss", actor.Id, recipient.Id, 0, $"{native.Name}: level change failed.");
            return;
        }
        int previousLevel = recipient.Level;
        int missingHp = recipient.MaximumHp - recipient.CurrentHp;
        int level = Math.Clamp(native.Power * recipient.Level / 8, 1, 100);
        int difference = native.AttackType == 16 ? level - previousLevel : previousLevel - level;
        string digits = NativeBattleGlobalText(11);
        if (digits.Length < 11)
        {
            throw new InvalidDataException("Native level message requires its original separator and ten digits.");
        }
        //495330 renders only the low five decimal digits of its unsigned DWORD argument.
        string number = string.Concat((unchecked((uint)difference) % 100000).ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => digits[digit - '0' + 1]));
        string text = recipient.Name + NativeBattleGlobalText(88) + NativeBattleGlobalText(native.AttackType == 16 ? 89 : 95) + digits[0] + number;
        recipient.BindNativeLevel(level);
        recipient.CurrentHp = native.AttackType == 16 ? recipient.MaximumHp - missingHp : Math.Min(recipient.CurrentHp, recipient.MaximumHp);
        QueueNativeSpecialText(actor, recipient, text);
        RecordNativeActionMemory(actor, recipient, command);
        Log("LevelChanged", actor.Id, recipient.Id, Math.Abs(level - previousLevel), $"{recipient.Name}: level {previousLevel} → {level}.");
        DispatchNativeActionCounter(recipient);
    }

    private void CompleteNativeOrdinaryCommand(BattleCombatant actor, BattleActionPresentation action)
    {
        if ((action.NativeQueuedCommand ?? action.NativeAction?.Command) != 31 || (NativeSavedStatusBits(actor) & 1) != 0)
        {
            return;
        }
        //4840E0 queues0/id7;48FE20 setsEject and494410 commitsHP0/KO before493840 removes the party record.
        actor.NativeStatus1 |= 0x10000;
        ApplyDamage(null, actor, actor.CurrentHp, "Kamikaze", false, bypassDamageLimit: true);
        actor.NativeStatus1 &= ~0x10000u;
        actor.NativeStatus0 &= 0xFFDF;
        guardianForceCasts.Remove(actor.Id);
        actor.NativeStatus1 &= 0x7FFFFFFF;
        ReleaseNativeParticipant(actor);
        actor.NativeCombatantFlags = 0;
    }

    private void CompleteNativeOrdinaryCommandCost(BattleCombatant actor, BattleActionPresentation action)
    {
        if ((action.NativeQueuedCommand ?? action.NativeAction?.Command) is not (28 or 243) || (NativeSavedStatusBits(actor) & 1) != 0)
        {
            return;
        }
        //484180 queues0/id6 before poison, even after a missed Darkside attack. No formula/status RNG is consumed.
        int damage = actor.MaximumHp / 10;
        if (unchecked((uint)damage) >= unchecked((uint)actor.CurrentHp) && actor.NativeAi is not null && (actor.NativeCombatantFlags & 0x20) != 0)
        {
            StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 0, 6, 0, 3);
        }
        ApplyDamage(null, actor, damage, "Darkside cost", false, bypassDamageLimit: true);
    }

    private static ushort NextNativePlayerDeathCount(BattleCombatant actor)
    {
        ushort current = actor.NativeDeathCount ?? throw new NotSupportedException("Native player KO requires its actual saved death counter WORD146.");
        return unchecked((ushort)(current + 1));
    }
}
