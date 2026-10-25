namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeSlotsRoll> nativeSlotsRolls = new(StringComparer.Ordinal);

    public BattleNativeSlotsRoll? GetNativeSlotsRoll(string actorId)
    {
        _ = GetActor(actorId);
        return nativeSlotsRolls.GetValueOrDefault(actorId);
    }

    public BattleNativeSlotsRoll RollNativeSlots(string actorId)
    {
        RequireNoPendingSlotsDeal();
        BattleCombatant actor = GetActor(actorId);
        byte crisis = GetNativeLimitLevel(actorId);
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || !actor.IsReady || actor.NativeLimit is not { Command: 16 } rules || crisis == 0)
        {
            throw new InvalidOperationException("Slots roll requires Selphie's ready owned Limit selection.");
        }
        if (rules.SlotsSelectionBytes.IsDefaultOrEmpty || rules.SlotsSpellBytes.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Slots roll requires its actual contiguous kernel26/27 tables.");
        }
        BattleNativeSlotsRoll roll = GenerateNativeSlotsRoll(rules, actor.Level, crisis);
        nativeSlotsRolls[actorId] = roll;
        return roll;
    }

    private BattleNativeSlotsRoll GenerateNativeSlotsRoll(BattleNativeLimitDefinition rules, int level, byte crisis)
    {
        BattleNativeSlotsRoll roll = NativeBattleSlotsRules.Roll(rules, level, crisis, (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte());
        _ = FindInstalledNativeAction(16, roll.MagicId);
        return roll;
    }

    private void QueueNativeSlots(BattleCombatant actor, BattleNativeAction action, byte sequence, byte? crisis)
    {
        ValidateNativeActionContract(action);
        if (action.ActionId >= 51 && definition.LimitProgress?.SpecialMagicFlags is null)
        {
            throw new NotSupportedException("Special Slots magic requires the actual saved learned BYTE1CFE771 owner.");
        }
        BattleNativeSlotsRoll roll = nativeSlotsRolls[actor.Id];
        if (roll.Repetitions is < 1 or > 3)
        {
            throw new NotSupportedException("Native Slots input exceeds its three physical target WORDs.");
        }
        ushort mask = NativeAutomaticActionTargetMask(action.TargetFlags!.Value);
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 7 && (mask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleAttackCommand command = new(actor.Id, target.Id, BattleAbility.EnemyAbility, action.ActionId) { NativeQueuedCommand = 16, NativeAction = action, NativeTargetMask = mask, NativeSequence = sequence, NativeSlots = roll };
        EnqueueNativePacket(actor, command, action.Name ?? "Slots", playerInput: true);
    }

    private void ResolveNativeSlotsGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleNativeSlotsRoll slots = command.NativeSlots ?? throw new InvalidDataException("Native Slots group lacks its accepted roll.");
        BattleNativeAction native = command.NativeAction ?? throw new InvalidDataException("Native Slots group lacks its installed magic.");
        BattleActionDefinition action = new(command.Ability, command.ActionId, native.Name ?? "Slots", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        NativeBattleCommandLocation location = nativeDispatchCommandLocation ?? throw new InvalidDataException("Native Slots requires its selected physical command entry.");
        NativeBattleCommandPacket packet = nativeCommandBanks.Banks[location.Bank].Packets[2 * location.Slot];
        for (int repeat = 0; repeat < slots.Repetitions; repeat++)
        {
            ushort mask = SelectNativeSlotsTargetMask(native, packet.ReadWord(6 + 2 * repeat));
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 7 && (mask & (1 << candidate.NativeIndex)) != 0) ?? GetActor(command.TargetId);
            //485610 submits one104 for the group; the effect consumes the following headers itself.
            if (!ResolveInstalledNativeAction(actor, target, command with { NativeSlots = null, NativeTargetMask = mask }, action, queueGraphics: repeat == 0))
            {
                break;
            }
        }
    }

    private ushort SelectNativeSlotsTargetMask(BattleNativeAction action, ushort originalMask)
    {
        byte flags = action.TargetFlags!.Value;
        if ((flags & 0x30) != 0x10)
        {
            return originalMask;
        }
        bool players = (flags & 0x40) == 0;
        bool healing = players && action.AttackType is 3 or 4 or 21 or 25 or 32;
        bool statuses = action.AttackType is not (3 or 4 or 5 or 6 or 13 or 16 or 21 or 25 or 32) && (action.Statuses0 != 0 || action.Statuses1 != 0);
        bool damaging = !players && action.AttackType is 2 or 8 or 11 or 15 or 20 or 22 or 26 or 33;
        if (!healing && !statuses && !damaging)
        {
            return originalMask;
        }
        IEnumerable<BattleCombatant> candidates = combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7 && (candidate.NativeIndex < 3) == players && candidate.IsAlive && candidate.IsActive && !candidate.Untargetable);
        BattleCombatant? target = candidates.OrderBy(candidate => damaging && ModifyNativeElement(candidate, action, 100, false) < 0 ? 1 : 0)
            .ThenBy(candidate => statuses ? NativeSlotsStatusPriority(candidate, action, !players) : 0)
            .ThenBy(candidate => healing ? candidate.CurrentHp : 0)
            .ThenBy(candidate => (originalMask & (1 << candidate.NativeIndex)) != 0 ? 0 : 1)
            .ThenBy(candidate => candidate.NativeIndex)
            .FirstOrDefault();
        // A selected target must not retain the random-target modifier, which would replace it again.
        return target is null ? originalMask : (ushort)((originalMask & 0x4000) | (1 << target.NativeIndex));
    }

    private static int NativeSlotsStatusPriority(BattleCombatant candidate, BattleNativeAction action, bool considerImmunity)
    {
        if (considerImmunity && action.StatusAttack != 255 && !NativeSlotsHasSusceptibleStatus(candidate, action.Statuses0, action.Statuses1))
        {
            return 2;
        }
        ushort missingSaved = (ushort)(action.Statuses0 & ~NativeSavedStatusBits(candidate) & 0x7F);
        uint missingBattle = action.Statuses1 & ~NativeBattleStatusBits(candidate);
        if (missingSaved == 0 && missingBattle == 0)
        {
            return 1;
        }
        if (!considerImmunity || action.StatusAttack == 255)
        {
            return 0;
        }
        return NativeSlotsHasSusceptibleStatus(candidate, missingSaved, missingBattle) ? 0 : 2;
    }

    private static bool NativeSlotsHasSusceptibleStatus(BattleCombatant candidate, ushort saved, uint battle)
    {
        for (int bit = 0; bit < 40; bit++)
        {
            bool included = bit < 7 ? (saved & (1 << bit)) != 0 : bit >= 8 && (battle & (1u << (bit - 8))) != 0;
            if (included && GetNativeStatusResistance(candidate, bit) < 200)
            {
                return true;
            }
        }
        return false;
    }
}
