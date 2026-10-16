namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeExamTimerState? nativeConditionTimer;
    private long nativeConditionTimerFrame;

    private bool EvaluateNativeAiCondition(BattleCombatant actor, int subject, int selector, int comparison, int value)
    {
        long left;
        long right = value;
        if (subject is >= 80 and <= 87)
        {
            left = persistentAiVariables?[subject - 80] ?? throw new NotSupportedException("Native battle AI requires its executed persistent DWORD bank80..87.");
        }
        else if (subject is >= 96 and <= 103)
        {
            left = nativeAiVariables[subject - 96];
        }
        else if (subject is >= 220 and <= 227)
        {
            left = ReadNativeActorVariable(FindNativeConditionActor(actor, selector), subject);
        }
        else
        {
            switch (subject)
            {
                case 0:
                    BattleCombatant hpActor = FindNativeConditionActor(actor, selector);
                    RequireNativeAllocationStatistics(hpActor);
                    left = unchecked((uint)hpActor.CurrentHp);
                    right = NativeConditionHpThreshold(hpActor, value);
                    break;
                case 1:
                    return NativeConditionGroup(selector).Any(candidate => candidate.IsActive && (NativeSavedStatusBits(candidate) & 1) == 0 && CompareNativeCondition(unchecked((uint)candidate.CurrentHp), comparison, NativeConditionHpThreshold(candidate, value)));
                case 2:
                    if (selector == 0)
                    {
                        throw new InvalidDataException("Native battle AI random condition divides by zero.");
                    }
                    left = NextRandomByte() % selector;
                    break;
                case 3:
                    left = definition.EncounterId ?? throw new NotSupportedException("Native battle AI formation condition requires its installed encounter identity.");
                    break;
                case 4:
                    return CompareNativeConditionBit(NativeAiStatusPresent(FindNativeConditionActor(actor, selector), value), comparison);
                case 5:
                    return EvaluateNativeAiGroupStatus(selector, comparison, value);
                case 6:
                    left = NativeConditionGroup(selector).Count(candidate => (NativeSavedStatusBits(candidate) & 1) == 0);
                    break;
                case 7:
                    BattleCombatant levelActor = FindNativeConditionActor(actor, selector);
                    RequireNativeAllocationStatistics(levelActor);
                    left = levelActor.Level;
                    break;
                case 8:
                    return CompareNativeConditionBit(combatants.Any(candidate => candidate.NativeIndex is >= 0 and < 7 && NativeModelId(candidate) == value), comparison);
                case 9:
                    return CompareNativeConditionBit(combatants.Any(candidate => candidate.NativeIndex is >= 0 and < 7 && (NativeSavedStatusBits(candidate) & 1) == 0 && NativeModelId(candidate) == value), comparison);
                case 10:
                    return EvaluateNativeActionCondition(actor, selector, comparison, value);
                case 11:
                case 12:
                case 13:
                    //487DF0 deliberately consumes these condition records without branching.
                    return true;
                case 14:
                    left = actor.NativeAi?.LevelBand ?? throw new InvalidDataException("Native battle AI level-band condition requires its installed program.");
                    break;
                case 15:
                    return CompareNativeConditionBit(combatants.Any(candidate => candidate.NativeIndex == value && candidate.IsActive && (NativeSavedStatusBits(candidate) & 1) == 0), comparison);
                case 16:
                    BattleCombatant[] party = combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 3 && candidate.IsActive && (NativeSavedStatusBits(candidate) & 1) == 0).ToArray();
                    if (party.Any(candidate => candidate.NativeCharacterFlag100 is null))
                    {
                        throw new NotSupportedException("Native party characteristic condition requires the original kernel character gender flag.");
                    }
                    return CompareNativeConditionBit(party.Any(candidate => ((candidate.NativeCombatantFlags & 0x100) != 0) == (value != 202)), comparison);
                case 17:
                    return CompareNativeConditionBit(actor.DrawGuardianForces.Count > 0, comparison);
                case 18:
                    byte flags = CurrentNativeSavedBattleFlags();
                    byte mask = unchecked((byte)(1u << value));
                    if ((mask & definition.NativeSavedBattleFlagsKnownMask) != mask)
                    {
                        throw new NotSupportedException("Native saved battle flag condition requires an explicit owner for the queried bit.");
                    }
                    return CompareNativeConditionBit((flags & mask) != 0, comparison);
                case 19:
                    return CompareNativeConditionBit(NativeConditionTimerIsZero(), comparison);
                case 20:
                    return combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 3 && candidate.IsActive && (NativeSavedStatusBits(candidate) & 1) == 0).All(candidate => NativeAiStatusPresent(candidate, value));
                default:
                    //The native default condition table deliberately consumes these subjects without branching.
                    return true;
            }
        }
        return CompareNativeCondition(left, comparison, right);
    }

    private bool NativeConditionTimerIsZero()
    {
        if (definition.NativeExamTimerAtEntry is { } entry)
        {
            NativeExamTimerState current = nativeConditionTimer ?? entry;
            long remaining = Frame - nativeConditionTimerFrame;
            while (remaining > 0)
            {
                int callbacks = (int)Math.Min(remaining, 30000);
                current = current.AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks);
                remaining -= callbacks;
            }
            nativeConditionTimer = current;
            nativeConditionTimerFrame = Frame;
            bool[] alternatives = current.Alternatives.Select(alternative => alternative.Remaining == 0).Distinct().ToArray();
            return alternatives.Length == 1 ? alternatives[0] : throw new NotSupportedException("Native battle timer condition is ambiguous under its retained clock alternatives.");
        }
        return definition.NativeExamTimerZero ?? throw new NotSupportedException("Native battle timer condition requires the current canonical exam timer owner.");
    }

    private bool EvaluateNativeActionCondition(BattleCombatant actor, int selector, int comparison, int value)
    {
        long left;
        long right = value;
        if (selector == 2)
        {
            left = actor.AiTurnCount;
        }
        else if (selector == 203)
        {
            left = NativeConditionLastAttackerIndex(actor);
            right = value == 200 ? actor.NativeIndex : NativeConditionActorIndex(value);
        }
        else
        {
            NativeBattleLastAction? last = actor.LastNativeAction;
            if (selector == 0)
            {
                //48B5F0/48BBD0 clearB99 even before any action reaches this actor.
                left = last is null ? 0 : last.AttackType ?? throw new NotSupportedException("Native last-action attack type requires the original action flags.");
            }
            else
            {
                if (last is null)
                {
                    throw new NotSupportedException("Native last-action condition requires an executed action metadata owner.");
                }
                if (selector == 5)
                {
                    return CompareNativeConditionBit((last.ElementMask & (1u << value)) != 0, comparison);
                }
                left = selector switch
                {
                    1 => last.AttackerModelId,
                    3 => last.Command,
                    4 => last.ActionId,
                    _ => throw new NotSupportedException($"Native battle AI action condition selector {selector} is unsupported.")
                };
            }
        }
        return CompareNativeCondition(left, comparison, right);
    }

    private BattleCombatant FindNativeConditionActor(BattleCombatant actor, int selector)
    {
        int index = selector switch
        {
            200 => actor.NativeIndex,
            203 => NativeConditionLastAttackerIndex(actor),
            >= 220 and <= 227 => checked((int)ReadNativeActorVariable(actor, selector)),
            _ => NativeConditionActorIndex(selector)
        };
        return combatants.FirstOrDefault(candidate => candidate.NativeIndex == index) ?? throw new NotSupportedException($"Native battle AI condition actor index {index} has no retained allocation.");
    }

    private int NativeConditionLastAttackerIndex(BattleCombatant actor)
    {
        //Allocation explicitly clearsB98 to0. Older saves may still own the precise attacker identity.
        return actor.LastNativeAction?.AttackerIndex ?? (actor.LastAttackerId is string id ? byId[id].NativeIndex : 0);
    }

    private int NativeConditionActorIndex(int model)
    {
        return combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7).OrderBy(candidate => candidate.NativeIndex).FirstOrDefault(candidate => NativeModelId(candidate) == model)?.NativeIndex ?? 255;
    }

    private IEnumerable<BattleCombatant> NativeConditionGroup(int selector)
    {
        //48A830 scans physical slots0..2 or3..6; model selectors inspect exactly the same seven slots.
        return combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7 && (selector == 200 ? candidate.NativeIndex < 3 : selector == 201 ? candidate.NativeIndex >= 3 : NativeModelId(candidate) == selector));
    }

    private static long NativeConditionHpThreshold(BattleCombatant actor, int value)
    {
        return value is >= 1 and <= 9 ? value * (actor.MaximumHp / 10) : value == 10 ? actor.MaximumHp >> 2 : value;
    }

    private static bool CompareNativeCondition(long left, int comparison, long right)
    {
        return comparison switch
        {
            0 => left == right,
            1 => left < right,
            2 => left > right,
            3 => left != right,
            4 => left <= right,
            5 => left >= right,
            _ => false
        };
    }

    private static bool CompareNativeConditionBit(bool present, int comparison)
    {
        return comparison switch
        {
            0 => present,
            3 => !present,
            _ => false
        };
    }
}
