namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ushort ResolveNativeAiTargetMask(BattleCombatant actor, byte selector)
    {
        //487DF0 stores the lowWORD of these physical-slot masks; model selectors include dead slots.
        switch (selector)
        {
            case 200:
                return NativeAiIndexMask(actor.EncodeNativeSlot());
            case 201:
                if (!NativeTargetActors().Any(candidate => candidate.Side == BattleSide.Allies && candidate.IsActive && (NativeSavedStatusBits(candidate) & 5) == 0))
                {
                    return 0;
                }
                return PickNativeAiTarget(BattleSide.Allies, candidate => (NativeSavedStatusBits(candidate) & 1) == 0);
            case 202:
                if (!NativeTargetActors().Any(candidate => candidate.Side == BattleSide.Enemies && (NativeSavedStatusBits(candidate) & 5) == 0))
                {
                    return 8;
                }
                return PickNativeAiTarget(BattleSide.Enemies, candidate => (NativeSavedStatusBits(candidate) & 1) == 0);
            case 203:
                return NativeAiIndexMask(NativeConditionLastAttackerIndex(actor));
            case 204:
                return 0x8007;
            case 205:
                return 0x80F8;
            case 206:
                return 0x80FF;
            case 207:
                if (NativeTargetActors().Count(candidate => candidate.Side == BattleSide.Enemies && (NativeSavedStatusBits(candidate) & 1) == 0) == 1)
                {
                    return NativeAiIndexMask(actor.EncodeNativeSlot());
                }
                return PickNativeAiTarget(BattleSide.Enemies, candidate => candidate.EncodeNativeSlot() != actor.EncodeNativeSlot() && (NativeSavedStatusBits(candidate) & 1) == 0);
            case 208:
                return 0xA007;
            case 209:
                return NativeAiIndexMask(nativeLastAllocatedIndex);
            case >= 220 and <= 227:
                return NativeAiIndexMask(unchecked((int)actor.AiVariables[selector - 220]));
            default:
                return NativeAiIndexMask(NativeConditionActorIndex(selector));
        }
    }

    private ushort FilterNativeAiTargetMask(byte allTargets, byte group, byte comparison, byte criterion)
    {
        //486E70 computes extrema over the physical group, then applies allocation/death and group filtering.
        BattleCombatant[] actors = NativeTargetActors().ToArray();
        BattleCombatant[] range = actors.Where(candidate => (group == 200 ? candidate.Side == BattleSide.Allies : group != 201 || candidate.Side == BattleSide.Enemies) && (NativeSavedStatusBits(candidate) & 1) == 0).ToArray();
        Func<BattleCombatant, bool> matches;
        if (criterion <= 9 || criterion is >= 16 and <= 42 or 46 or 47)
        {
            matches = candidate => criterion < 16 ? (NativeSavedStatusBits(candidate) & (1u << criterion)) != 0 : (NativeBattleStatusBits(candidate) & (1u << (criterion - 16))) != 0;
        }
        else if (criterion is 200 or 201)
        {
            matches = candidate => ((candidate.NativeCombatantFlags & 0x100) != 0) == (criterion == 201);
        }
        else if (criterion is 203 or 204)
        {
            //487168/4871A5 use signed JGE/JLE, unlike the unsigned HP condition helper48A680.
            int extreme = criterion == 203 ? Math.Max(0, range.Select(candidate => candidate.CurrentHp).DefaultIfEmpty(0).Max()) : range.Where(candidate => candidate.CurrentHp != 0).Select(candidate => candidate.CurrentHp).DefaultIfEmpty(int.MaxValue).Min();
            matches = candidate => candidate.CurrentHp == extreme;
        }
        else if (criterion is >= 205 and <= 220)
        {
            bool minimum = criterion >= 213;
            int row = criterion - (minimum ? 213 : 205);
            int extreme = minimum ? range.Select(candidate => NativeAiTargetStat(candidate, row)).DefaultIfEmpty(255).Min() : range.Select(candidate => NativeAiTargetStat(candidate, row)).DefaultIfEmpty(0).Max();
            matches = candidate => NativeAiTargetStat(candidate, row) == extreme;
        }
        else if (criterion is >= 221 and <= 236)
        {
            bool minimum = criterion >= 229;
            int row = criterion - (minimum ? 229 : 221);
            int extreme = minimum ? range.Select(candidate => candidate.ElementDefenses[row]).DefaultIfEmpty(65535).Min() : range.Select(candidate => candidate.ElementDefenses[row]).DefaultIfEmpty(0).Max();
            matches = candidate => candidate.ElementDefenses[row] == extreme;
        }
        else
        {
            throw new NotSupportedException($"Native target-filter criterion {criterion} depends on undefined retained scratch state.");
        }
        bool Eligible(BattleCombatant candidate)
        {
            return candidate.IsActive && (criterion == 0 || (NativeSavedStatusBits(candidate) & 1) == 0) && matches(candidate) != (comparison == 3);
        }
        bool InGroup(BattleCombatant candidate)
        {
            return group == 200 ? candidate.Side == BattleSide.Allies : group == 201 ? candidate.Side == BattleSide.Enemies : NativeModelId(candidate) == group;
        }
        if (allTargets != 0)
        {
            ushort mask = 0x8000;
            foreach (BattleCombatant candidate in actors.Where(candidate => InGroup(candidate) && Eligible(candidate)))
            {
                mask |= NativeAiIndexMask(candidate.EncodeNativeSlot());
            }
            return mask;
        }
        return PickNativeAiTarget(group == 200 ? BattleSide.Allies : group == 201 ? BattleSide.Enemies : null, candidate => InGroup(candidate) && Eligible(candidate));
    }

    private static bool HasNativeRosterSlot(BattleCombatant actor) =>
        actor.Slot is { } slot && NativeBattleSlots.TryEncode(slot).HasValue;

    private IEnumerable<BattleCombatant> NativeTargetActors()
    {
        return combatants.Where(HasNativeRosterSlot);
    }

    private ushort PickNativeAiTarget(BattleSide? side, Func<BattleCombatant, bool> eligible)
    {
        HashSet<BattleRosterSlot> candidates = NativeTargetActors().Where(candidate => (side is null || candidate.Side == side) && eligible(candidate)).Select(candidate => candidate.Slot!.Value).ToHashSet();
        int count = side switch
        {
            BattleSide.Allies => NativeBattleSlots.AllyCount,
            BattleSide.Enemies => NativeBattleSlots.EnemyCount,
            _ => NativeBattleSlots.Count
        };
        int first = side == BattleSide.Enemies ? NativeBattleSlots.AllyCount : 0;
        if (candidates.Count == 0)
        {
            throw new NotSupportedException("Native random target selection has no eligible physical slot and would not terminate.");
        }
        while (true)
        {
            int index = first + NextRandomByte() % count;
            if (candidates.Contains(NativeBattleSlots.Decode(index)))
            {
                return NativeAiIndexMask(index);
            }
        }
    }

    private static ushort NativeAiIndexMask(int index)
    {
        return unchecked((ushort)(1u << index));
    }

    private int NativeAiTargetStat(BattleCombatant actor, int row)
    {
        return row switch
        {
            0 => EffectiveStat(actor, BattleStatId.Strength),
            1 => EffectiveStat(actor, BattleStatId.Vitality),
            2 => actor.Magic,
            3 => EffectiveStat(actor, BattleStatId.Spirit),
            4 => actor.Speed,
            5 => actor.Luck,
            6 => actor.Evasion,
            7 => actor.Hit,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }
}
