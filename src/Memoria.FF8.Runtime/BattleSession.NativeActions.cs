namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleActionPresentation? resolvingNativeAction;
    private BattleNativeAction FindInstalledNativeAction(byte command, ushort id, byte? crisis = null)
    {
        BattleNativeAction[] matches = definition.NativeActions.Where(action => action.Command == command && action.ActionId == id && (action.CrisisLevel is null || action.CrisisLevel == crisis)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new NotSupportedException($"Native command {command}/{id} does not identify one installed action for crisis {crisis}.");
    }

    private BattleNativeAction FindNativeCommandAction(BattleCombatant actor, byte command, ushort id, byte? crisis = null)
    {
        if (command is 1 or 12 or 28 && id == 0)
        {
            BattleNativeAction attack = actor.NativeAttack ?? throw new NotSupportedException("Native physical command requires its character-owned equipped weapon and junction metadata.");
            BattleNativeCommandBinding? binding = actor.NativeCommands.FirstOrDefault(binding => binding.Command == command);
            return attack with { Command = command, DispatchCommand = command, Name = binding?.Name ?? attack.Name, TargetFlags = binding?.TargetFlags ?? attack.TargetFlags };
        }
        return FindInstalledNativeAction(command, id, crisis);
    }

    private void QueueNativeCommand(BattleCombatant actor, byte command, ushort id, byte sequence, ushort targetMask, byte? crisis = null)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, command, id, crisis);
        ValidateNativeActionContract(native);
        actor.NativePreviousTargetMask = targetMask;
        if (command is 2 or 4)
        {
            targetMask |= (ushort)(((native.TargetFlags.GetValueOrDefault() & 1) != 0 ? 0x4000 : 0) | ((native.TargetFlags.GetValueOrDefault() & 2) != 0 ? 0x2000 : 0));
        }
        else if (command == 8 && (native.AttackFlags & 0x80) != 0)
        {
            targetMask |= 0x4000;
        }
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 16 && (targetMask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleAttackCommand queued = new(actor.Id, target.Id, BattleAbility.EnemyAbility, id) { NativeQueuedCommand = command, NativeAction = native, NativeSequence = sequence, NativeTargetMask = targetMask };
        actor.Atb = 0;
        actor.IsBusy = true;
        actions.Enqueue(queued);
        Log("Queued", actor.Id, target.Id, id, $"{actor.Name}: {native.Name ?? $"native command {command}/{id}"} queued.");
    }

    private static bool IsNativeRevive(BattleActionDefinition action)
    {
        return action.NativeAction?.AttackType is 5 or 6 || action.Effect == BattleActionEffect.Revive;
    }

    private bool ActionHasValidTarget(string targetId, BattleAbility ability, int actionId, BattleNativeAction? native, ushort targetMask)
    {
        bool revive = native?.AttackType is 5 or 6 || native is null && IsRevive(ability, actionId);
        if (native is not null)
        {
            if ((targetMask & 0x1FFF) == 0)
            {
                return true;
            }
            int selected = targetMask & 0x1FFF;
            if ((targetMask & 0xA000) == 0 && combatants.Any(candidate => candidate.IsActive && !candidate.Untargetable && candidate.NativeIndex is >= 0 and < 7 && (selected & (1 << candidate.NativeIndex)) != 0 && ((NativeSavedStatusBits(candidate) & 1) == 0 || (targetMask & 0x4000) != 0 && candidate.NativeIndex < 3)))
            {
                return true;
            }
            if (nativeScriptState.DisableAutomaticTargetReplacement && (targetMask & 0xA000) == 0)
            {
                return combatants.Any(candidate => candidate.NativeIndex is >= 0 and < 7 && (selected & (1 << candidate.NativeIndex)) != 0);
            }
            return combatants.Any(candidate => candidate.IsActive && !candidate.Untargetable && candidate.NativeIndex is >= 0 and < 7 && (selected == 255 || (selected <= 7) == (candidate.NativeIndex < 3)) && ((NativeSavedStatusBits(candidate) & 1) == 0 || (targetMask & 0x4000) != 0 && candidate.NativeIndex < 3));
        }
        if (targetMask != 0)
        {
            return combatants.Any(candidate => candidate.NativeIndex is >= 0 and < 16 && (targetMask & (1 << candidate.NativeIndex)) != 0 && (candidate.IsAlive || revive && candidate.IsActive));
        }
        return byId[targetId].IsAlive || revive;
    }

    private static void ValidateNativeActionContract(BattleNativeAction action)
    {
        if (action.AttackType is not (0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 32 or 33 or 34 or 35 or 36))
        {
            throw new NotSupportedException($"Native action {action.Command}/{action.ActionId} attack type {action.AttackType} requires its original invocation or persistent effect owner.");
        }
        if (action.HitCount == 0 || action.RawRecord.IsDefault || action.SupplementalRecord.IsDefault)
        {
            throw new InvalidDataException("Native action lacks its effective hit count or source records.");
        }
    }

    private void ResolveInstalledNativeAction(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleActionDefinition action)
    {
        BattleNativeAction native = action.NativeAction!;
        ValidateNativeActionContract(native);
        if (actor.IsPlayer && native.Command is 2 or 6 or 254)
        {
            nativeAffinityAction = native;
        }
        BattleCombatant? firstReflector = null;
        for (int hit = 0; hit < native.HitCount; hit++)
        {
            BattleCombatant[] targets;
            if (command.NativeAction is not null)
            {
                targets = ResolveNativeActionMask(command.NativeTargetMask);
            }
            else if ((native.TargetFlags.GetValueOrDefault() & 2) != 0)
            {
                BattleCombatant? randomTarget = ChooseNativeRandomActionTarget(target.IsPlayer);
                targets = randomTarget is null ? [] : [randomTarget];
            }
            else if (action.AllTargets)
            {
                targets = combatants.Where(candidate => candidate.IsActive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer && (IsNativeRevive(action) ? candidate.CurrentHp == 0 : candidate.IsAlive)).OrderBy(candidate => candidate.NativeIndex).ToArray();
            }
            else
            {
                targets = target.IsAlive || IsNativeRevive(action) ? [target] : [];
            }
            foreach (BattleCombatant recipient in targets)
            {
                if (hit == 0 && (command.NativeQueuedCommand ?? native.Command) == 12)
                {
                    ResolveNativeMug(actor, recipient);
                }
                if (NativeActionReflects(recipient, native))
                {
                    firstReflector ??= recipient;
                    RecordNativeActionMemory(actor, recipient, command);
                    DispatchNativeActionCounter(recipient);
                    Log("Reflect", actor.Id, recipient.Id, 0, $"{recipient.Name} reflects {action.Name}.");
                    continue;
                }
                BattleActionPresentation resolved = command.NativeAction is null ? command with { NativeAction = native } : command;
                BattleActionPresentation? previous = resolvingNativeAction;
                resolvingNativeAction = resolved;
                try
                {
                    ResolveInstalledNativeHit(actor, recipient, resolved, action, native);
                }
                finally
                {
                    resolvingNativeAction = previous;
                }
            }
        }
        if (firstReflector is not null)
        {
            ResolveNativeReflection(actor, firstReflector, command, native);
        }
    }

    private BattleCombatant[] ResolveNativeActionMask(ushort targetMask)
    {
        int selected = targetMask & 0x1FFF;
        int modifiers = targetMask >> 13;
        if ((modifiers & 1) != 0)
        {
            BattleCombatant? randomTarget = ChooseNativeRandomActionTarget(selected <= 7);
            selected = randomTarget is null ? 0 : 1 << randomTarget.NativeIndex;
            modifiers &= ~4;
        }
        int available = 0;
        foreach (BattleCombatant candidate in combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7 && candidate.IsActive && !candidate.Untargetable))
        {
            if ((NativeSavedStatusBits(candidate) & 1) == 0 || (modifiers & 2) != 0 && candidate.NativeIndex < 3)
            {
                available |= 1 << candidate.NativeIndex;
            }
        }
        int result;
        if ((modifiers & 4) != 0)
        {
            result = selected == 255 ? available : available & (selected <= 7 ? 7 : 248);
        }
        else
        {
            result = available & selected;
            if (result == 0 && nativeScriptState.DisableAutomaticTargetReplacement)
            {
                result = selected;
            }
            else if (result == 0 && selected != 0)
            {
                int side = available & (selected <= 7 ? 7 : 248);
                int shifted = selected;
                for (int index = 0; index < 8 && result == 0; index++)
                {
                    shifted <<= 1;
                    result = side & shifted;
                }
                shifted = selected;
                for (int index = 0; index < 8 && result == 0; index++)
                {
                    shifted >>= 1;
                    result = side & shifted;
                }
            }
        }
        return combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7 && (result & (1 << candidate.NativeIndex)) != 0).OrderBy(candidate => candidate.NativeIndex).ToArray();
    }

    private BattleCombatant? ChooseNativeRandomActionTarget(bool players)
    {
        if (!combatants.Any(candidate => candidate.IsPlayer == players && candidate.IsActive && (NativeSavedStatusBits(candidate) & 5) == 0 && (players ? candidate.NativeIndex is >= 0 and < 3 : candidate.NativeIndex is >= 3 and < 7)))
        {
            return null;
        }
        // Native486DC0/486E00 reject dead physical indexes, rather than drawing uniformly over a compacted living list.
        while (true)
        {
            int index = players ? NextRandomByte() % 3 : (NextRandomByte() & 3) + 3;
            BattleCombatant? selected = combatants.FirstOrDefault(candidate => candidate.NativeIndex == index && candidate.IsPlayer == players && candidate.IsActive && (NativeSavedStatusBits(candidate) & 1) == 0);
            if (selected is not null)
            {
                return selected;
            }
        }
    }

    private void ResolveInstalledNativeHit(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native)
    {
        if (TryResolveNativeSpecialHit(actor, recipient, command, native))
        {
            return;
        }
        if (native.AttackType == 24)
        {
            ResolveMoogleDance(actor, recipient, command);
            return;
        }
        ushort saved = NativeSavedStatusBits(recipient);
        uint statuses = NativeBattleStatusBits(recipient);
        if (native.AttackType is 13 or 16)
        {
            if ((recipient.NativeCombatantFlags & 0x8000) != 0 || native.StatusAttack <= NextRandomByte())
            {
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: level change failed.");
                return;
            }
            int previousLevel = recipient.Level;
            int missingHp = recipient.MaximumHp - recipient.CurrentHp;
            int level = Math.Clamp(native.Power * recipient.Level / 8, 1, 100);
            recipient.BindNativeLevel(level);
            recipient.CurrentHp = native.AttackType == 16 ? recipient.MaximumHp - missingHp : Math.Min(recipient.CurrentHp, recipient.MaximumHp);
            RecordNativeActionMemory(actor, recipient, command);
            Log("LevelChanged", actor.Id, recipient.Id, Math.Abs(level - previousLevel), $"{recipient.Name}: level {previousLevel} → {level}.");
            DispatchNativeActionCounter(recipient);
            return;
        }
        bool bypass = (native.Statuses1 & 0x4000000) != 0;
        bool physical = native.AttackType is 1 or 7 or 9 or 10 or 18 or 34 or 36;
        bool curative = native.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32;
        if (physical && !bypass && ((saved & 4) != 0 || (statuses & 0x180800) != 0))
        {
            Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is immune.");
            return;
        }
        int damage;
        bool applyStatuses = true;
        if (native.AttackType is 5 or 6)
        {
            if ((saved & 0x40) != 0)
            {
                curative = false;
                damage = ModifyNativeMagicDamage(recipient, native, NativeBattleDamage.Magic(actor.Magic, recipient.Spirit, native.Power, NextRandomByte(), !actor.IsPlayer));
            }
            else if ((saved & 1) != 0 || recipient.CurrentHp == 0)
            {
                ApplyNativeActionStatuses(actor, recipient, native with { Statuses0 = 1, Statuses1 = 0 }, true);
                int revived = native.AttackType == 6 ? recipient.MaximumHp : Math.Max(1, recipient.MaximumHp / 8);
                recipient.CurrentHp = revived;
                RecordNativeActionMemory(actor, recipient, command);
                Log("Healing", actor.Id, recipient.Id, revived, $"{action.Name}: {recipient.Name} revived.");
                DispatchNativeActionCounter(recipient);
                return;
            }
            else
            {
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is not KO.");
                return;
            }
        }
        else if (physical)
        {
            if (native.AttackType != 10 && !NativePhysicalActionHits(actor, recipient, native))
            {
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {actor.Name} missed.");
                return;
            }
            bool critical = native.AttackType != 10 && NativePhysicalCritical(actor, native);
            int vitality = (statuses & 0x1000000) != 0 || native.AttackType == 36 ? 0 : recipient.Defense;
            damage = native.AttackType switch
            {
                7 => (recipient.NativeCombatantFlags & 0x10000) != 0 ? 0 : native.Power * recipient.CurrentHp / 16,
                18 => 5 * actor.MaximumHp,
                34 => recipient.IsPlayer ? native.Power * recipient.NativeKillCount : 0,
                10 => NativeGunbladeAttack.Damage(actor.Attack, vitality, native.Power, NextRandomByte(), ResolveGunbladeTiming(actor, recipient, command)),
                _ => NativeBattleDamage.Physical(actor.Attack, vitality, native.Power, NextRandomByte())
            };
            if ((statuses & 0x20) != 0)
            {
                damage >>= 1;
            }
            if ((statuses & 0x800000) != 0)
            {
                damage *= 2;
            }
            if (critical)
            {
                damage *= 2;
            }
            if ((saved & 0x40) != 0)
            {
                damage >>= 1;
            }
            damage = ModifyNativeElement(recipient, native, damage, true);
        }
        else
        {
            int spirit = (statuses & 0x1000000) != 0 || native.AttackType is 15 or 22 ? 0 : recipient.Spirit;
            bool magic = native.AttackType is 2 or 8 or 11 or 15 or 20 or 22 or 26 or 33;
            bool magicCure = native.AttackType is 3 or 21;
            bool fixedDamage = native.AttackType is 27 or 28 or 29 or 35;
            bool earthImmune = (native.Element & 8) != 0 && (statuses & 0x2000) != 0;
            if (magic && !bypass && ((saved & 5) != 0 || (statuses & 0x200800) != 0 || earthImmune) || magicCure && !bypass && ((saved & 1) != 0 || (statuses & 0x200000) != 0 || earthImmune) || fixedDamage && ((saved & 4) != 0 || (statuses & 0x800) != 0))
            {
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is immune.");
                return;
            }
            switch (native.AttackType)
            {
                case 0:
                case 12:
                case 14:
                    damage = 0;
                    applyStatuses = false;
                    if (native.AttackType == 12)
                    {
                        ResolveNativeScan(actor, recipient);
                    }
                    break;
                case 3:
                    damage = -NativeBattleDamage.Cure(actor.Magic, native.Power, NextRandomByte());
                    break;
                case 4:
                case 25:
                case 32:
                    if ((native.Hit ?? 255) <= NextRandomByte() % 100 + 1)
                    {
                        Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} was unaffected.");
                        return;
                    }
                    damage = native.AttackType == 32 ? -native.Power * recipient.MaximumHp / 16 : native.AttackType == 25 ? -(actor.MaximumHp - actor.CurrentHp) : -50 * native.Power;
                    applyStatuses = native.StatusAttack > NextRandomByte() % 100 + 1;
                    break;
                case 21:
                    damage = -native.Power * recipient.MaximumHp / 16;
                    break;
                case 8:
                    damage = (recipient.NativeCombatantFlags & 0x10000) != 0 ? 0 : native.Power * recipient.CurrentHp / 16;
                    break;
                case 20:
                    damage = (recipient.NativeCombatantFlags & 0x10000) != 0 ? 0 : action.Level * recipient.MaximumHp / ((native.PowerModifier ?? 0) - (native.LevelModifier ?? 0) + 100);
                    break;
                case 11:
                case 15:
                    damage = NativeBattleDamage.GuardianForce(spirit, native.Power, action.Level, native.LevelModifier ?? 0, native.PowerModifier ?? 0, action.DamagePercent, NextRandomByte());
                    break;
                case 26:
                    if (recipient.Level % (native.Hit ?? 255) != 0)
                    {
                        Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: level condition failed.");
                        return;
                    }
                    damage = NativeBattleDamage.Magic(actor.Magic, spirit, native.Power, NextRandomByte(), !actor.IsPlayer);
                    break;
                case 27:
                    applyStatuses = false;
                    damage = 100 * native.Power - (native.Hit ?? 255);
                    break;
                case 28:
                    applyStatuses = false;
                    damage = Math.Max(0, recipient.CurrentHp - 1);
                    break;
                case 29:
                    applyStatuses = false;
                    damage = 1000 * (native.Power * action.Level / 1000 + 1);
                    break;
                case 35:
                    applyStatuses = false;
                    damage = 1;
                    break;
                case 33:
                    uint counter = definition.NativeElapsedCounter ?? throw new NotSupportedException("Native attack type33 requires its field elapsed counter DWORD at battle entry.");
                    damage = unchecked((int)(native.Power * (counter / 1000)));
                    break;
                default:
                    damage = NativeBattleDamage.Magic(actor.Magic, spirit, native.Power, NextRandomByte(), !actor.IsPlayer);
                    break;
            }
            if (curative)
            {
                if (native.AttackType is 3 or 21 && (statuses & 0x40) != 0)
                {
                    damage >>= 1;
                }
                if ((saved & 4) != 0 && native.AttackType is 3 or 21)
                {
                    damage = 0;
                }
                if ((saved & 0x40) != 0)
                {
                    damage = -damage;
                }
            }
            else if (native.AttackType is 2 or 8 or 11 or 15 or 20 or 22 or 26 or 33)
            {
                damage = ModifyNativeMagicDamage(recipient, native, damage);
            }
        }
        if (applyStatuses)
        {
            ApplyNativeActionStatuses(actor, recipient, native with { Statuses1 = native.Statuses1 & ~0x4008000u }, curative);
        }
        if (command.DrawCast)
        {
            damage = damage * (NextRandomByte() + 10) / 150;
        }
        if (native.Command is 1 or 12 or 28 or 243 or 248 or 253 && (NativeSavedStatusBits(actor) & 0x20) != 0)
        {
            damage = damage * 3 >> 1;
        }
        if (native.Command is 28 or 243)
        {
            damage *= 3;
        }
        if (native.Command == 2 && (NativeBattleStatusBits(actor) & 0x2000000) != 0)
        {
            damage *= 5;
        }
        int maximum = (native.AttackFlags & 8) != 0 ? 60_000 : 9_999;
        damage = Math.Clamp(damage, -maximum, maximum);
        if ((NativeSavedStatusBits(recipient) & 1) != 0 && recipient.CurrentHp > 0)
        {
            damage = recipient.CurrentHp;
        }
        RecordNativeActionMemory(actor, recipient, command);
        if (damage >= 0)
        {
            ApplyDamage(actor, recipient, damage, action.Name);
        }
        else
        {
            ApplyHealing(recipient, -damage, action.Name);
            DispatchNativeActionCounter(recipient);
        }
        if (native.AttackType == 9 || (native.Statuses1 & 0x8000) != 0)
        {
            int percent = native.AttackType == 9 ? 100 : Math.Max(0, native.StatusAttack - GetNativeStatusResistance(recipient, 23));
            int drain = Math.Min(9_999, Math.Abs(damage) * percent / 100);
            bool reverseDrain = ((NativeSavedStatusBits(actor) ^ NativeSavedStatusBits(recipient)) & 0x40) != 0;
            if (drain > 0 && reverseDrain)
            {
                ApplyDamage(recipient, actor, drain, action.Name, false);
            }
            else if (drain > 0)
            {
                ApplyHealing(actor, drain, action.Name);
            }
        }
    }

    private bool NativePhysicalActionHits(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction native)
    {
        int hit = native.Hit ?? 255;
        if ((NativeBattleStatusBits(recipient) & 9) != 0 || hit == 255)
        {
            return true;
        }
        if ((NativeSavedStatusBits(actor) & 8) != 0)
        {
            hit >>= 2;
        }
        int chance = 255 * Math.Max(0, hit + (actor.Luck >> 1) - recipient.Evasion - recipient.Luck) / 100;
        int draw = NextRandomByte();
        return chance != 0 && chance >= draw;
    }

    private bool NativePhysicalCritical(BattleCombatant actor, BattleNativeAction native)
    {
        int chance = native.Critical.GetValueOrDefault() + actor.Luck;
        int draw = NextRandomByte();
        return chance != 0 && chance >= draw;
    }

    private int ModifyNativeMagicDamage(BattleCombatant recipient, BattleNativeAction native, int damage)
    {
        uint statuses = NativeBattleStatusBits(recipient);
        if ((native.AttackFlags & 3) == 1 && (statuses & 0x40) != 0)
        {
            damage >>= 1;
        }
        if ((statuses & 0x80000) != 0)
        {
            damage >>= 1;
        }
        return ModifyNativeElement(recipient, native, damage, false);
    }

    private int ModifyNativeElement(BattleCombatant recipient, BattleNativeAction native, int damage, bool physical)
    {
        if (native.Element == 0)
        {
            return damage;
        }
        int element = System.Numerics.BitOperations.TrailingZeroCount((uint)native.Element);
        int defense = (NativeSavedStatusBits(recipient) & 0x40) != 0 && element == 7 ? 700 : recipient.ElementDefenses[element];
        return physical ? damage + damage * native.ElementPercent.GetValueOrDefault() * (800 - defense) / 10_000 : damage * (900 - defense) / 100;
    }

    private void DispatchNativeActionCounter(BattleCombatant recipient)
    {
        if (recipient.NativeAi is not null)
        {
            QueueNativeReaction(recipient, 2);
            if ((recipient.NativeCombatantFlags & 0x10) != 0)
            {
                RunNativeAi(recipient, 4);
            }
        }
    }
}
