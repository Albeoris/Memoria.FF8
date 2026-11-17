using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly record struct AiTargetSelection(BattleTargetRequest Request, BattlePreviousTarget? PreviousTarget, bool MissingOriginalModel = false);

    private AiTargetSelection EmptyAiTargets => new(BattleTargetRequest.None, null);

    private AiTargetSelection ResolveAiTargets(BattleCombatant actor, byte selector)
    {
        // Opcode4 uses action selectors; subject5 conditions use their own operand meanings.
        return selector switch
        {
            200 => SelectAiActor(actor),
            201 => PickAiSide(BattleSide.Allies),
            202 => PickAiSide(BattleSide.Enemies),
            203 => SelectAiActors(actor.LastAttackerId is { } attacker ? [GetActor(attacker)] : []),
            204 => SelectAiGroup(BattleTargetSide.Allies),
            205 => SelectAiGroup(BattleTargetSide.Enemies),
            206 => SelectAiGroup(BattleTargetSide.Both),
            207 => PickAiSide(BattleSide.Enemies, actor),
            208 => SelectAiGroup(BattleTargetSide.Allies, random: true),
            209 => ReadAiLastAllocation(actor) switch
            {
                NativeBattleTargetRequestAdapter.OriginalPreviousOperand original => SelectAiActorOperand(original.Operand, actor),
                BattlePreviousTarget.Actor captured => SelectAiActors([GetActor(captured.Reference.ActorId)]),
                _ => EmptyAiTargets
            },
            >= 220 and <= 227 when actor.AiActorReferences.TryGetValue(selector - 220, out NativeBattleAiActorValue target) => SelectAiActors([GetActor(target.ActorId)]),
            >= 220 and <= 227 => SelectAiActorOperand(unchecked((int)actor.AiVariables[selector - 220]), actor),
            _ => ResolveExpandedModelTarget(actor, selector)
        };
    }

    private AiTargetSelection SelectAiActor(BattleCombatant actor)
    {
        return SelectAiActors([actor]);
    }

    private AiTargetSelection SelectDirectAiActor(BattleCombatant actor, int operand)
    {
        return NativeBattleSlots.Contains(operand) ? SelectAiActorOperand(operand, actor) : EmptyAiTargets;
    }

    private AiTargetSelection SelectAiActorOperand(int operand, BattleCombatant? caller = null)
    {
        (NativeBattleTargetRequestAdapter.OriginalWordProjection projection, bool allowsKo) = NativeBattleTargetRequestAdapter.DecodeAiActorOperand(operand);
        if (caller is not null && ReadScopedEnemySource(caller) is not null && projection.RequiresOriginalSlots && projection.Operands.Slots is [var slot] && slot.Side == BattleSide.Enemies)
        {
            return FindSourceOriginalActor(caller, NativeBattleSlots.Encode(slot)) is { } donor ? SelectAiActor(donor) : EmptyAiTargets;
        }
        return SelectOriginalAiTargets(projection, allowsKo);
    }

    private AiTargetSelection SelectOriginalAiTargets(NativeBattleTargetRequestAdapter.OriginalWordProjection source, bool allowsKnockedOutAllies = false)
    {
        BattleTargetRequest request = NativeBattleTargetRequestAdapter.CreateOriginalRequest(source, BattleTargetEligibility.SavedStatus, allowsKnockedOutAllies);
        return new(request, NativeBattleTargetRequestAdapter.CapturePrevious(source, allowsKnockedOutAllies));
    }

    private AiTargetSelection SelectAiGroup(BattleTargetSide side, bool random = false)
    {
        return SelectOriginalAiTargets(NativeBattleTargetRequestAdapter.OriginalGroup(side) with { Random = random });
    }

    private AiTargetSelection FilterAiTargets(byte allTargets, byte group, byte comparison, byte criterion)
    {
        // 486E70 computes extrema before applying allocation/death and group filtering.
        BattleCombatant[] population = combatants.Where(HasAiRosterSlot).ToArray();
        var filter = NativeAiTargetFilter(population, group, comparison, criterion);
        BattleCombatant[] candidates = population.Where(filter.Eligible).ToArray();
        if (allTargets == 0)
        {
            return PickAiTarget(candidates, filter.Side);
        }

        BattleTargetSide side = candidates.Any(candidate => candidate.Side == BattleSide.Enemies) ? BattleTargetSide.Enemies : BattleTargetSide.Allies;
        int? firstOriginalSlot = candidates.Select(candidate => candidate.NativeActorIndex).Min();
        BattlePreviousTarget previous = firstOriginalSlot is int original
            ? new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(original)
            : candidates.FirstOrDefault() is { } first
                ? new BattlePreviousTarget.Actor(TargetActorReference(first))
                : new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(15);
        return new(new(new BattleTargetSelection.SideGroup(side), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), previous);
    }

    private AiTargetSelection PickAiSide(BattleSide side, BattleCombatant? exclude = null)
    {
        BattleCombatant[] roster = Roster(side).Where(HasAiRosterSlot).ToArray();
        BattleCombatant[] living = roster.Where(candidate => (NativeSavedStatusBits(candidate) & 1) == 0).ToArray();
        if (exclude is not null && living.Length == 1)
        {
            return SelectAiActor(exclude);
        }
        if (exclude is null && !roster.Any(candidate => (side != BattleSide.Allies || candidate.IsActive) && (NativeSavedStatusBits(candidate) & 5) == 0))
        {
            return side == BattleSide.Allies ? EmptyAiTargets : SelectAiActorOperand(NativeBattleSlots.AllyCount);
        }
        return PickAiTarget(living.Where(candidate => candidate != exclude), side);
    }

    private AiTargetSelection PickAiTarget(IEnumerable<BattleCombatant> candidates, BattleSide? side = null)
    {
        BattleCombatant[] available = candidates.ToArray();
        if (available.Length == 0)
        {
            throw new NotSupportedException("Native random target selection has no eligible physical slot and would not terminate.");
        }
        return SelectAiActor(SampleNativeTarget(available, side));
    }

    private BattleCombatant SampleNativeTarget(BattleCombatant[] available, BattleSide? side)
    {
        int count = side switch
        {
            BattleSide.Allies => NativeBattleSlots.AllyCount,
            BattleSide.Enemies => NativeBattleSlots.EnemyCount,
            _ => NativeBattleSlots.Count
        };
        int first = side == BattleSide.Enemies ? NativeBattleSlots.AllyCount : 0;
        BattleCombatant[] extensions = combatants.Where(candidate => candidate.Slot is not null && candidate.NativeActorIndex is null && (side is null || candidate.Side == side))
            .OrderBy(candidate => candidate.Side).ThenBy(candidate => candidate.Slot!.Value.Position).ToArray();
        var slots = new BattleCombatant?[count + extensions.Length];
        foreach (BattleCombatant candidate in available)
        {
            int position = candidate.NativeActorIndex is int original ? original - first : count + Array.IndexOf(extensions, candidate);
            slots[position] = candidate;
        }
        while (true)
        {
            if (slots[NextRandomByte() % slots.Length] is { } selected)
            {
                return selected;
            }
        }
    }

    private static AiTargetSelection SelectAiActors(IEnumerable<BattleCombatant> candidates, BattleSide? randomSide = null)
    {
        ImmutableArray<BattleActorReference> actors = candidates.Select(TargetActorReference).ToImmutableArray();
        if (actors.IsEmpty)
        {
            return new(BattleTargetRequest.None, null);
        }
        BattleTargetSelection selection = randomSide is BattleSide side ? new BattleTargetSelection.RandomOnSide(side) : new BattleTargetSelection.Explicit(actors);
        return new(new(selection, BattleTargetEligibility.AiAvailable, false, BattleTargetReplacement.None), new BattlePreviousTarget.Actor(actors[0]));
    }

    private BattleTargetRequest ApplyOriginalAiTargetFlags(AiTargetSelection selected, byte flags, bool allowsKnockedOutAllies = false)
    {
        BattleTargetRequest request = selected.Request;
        if (request.OriginalProjection is not { } original)
        {
            throw new InvalidDataException("An original AI command requires its deferred target operands.");
        }
        return NativeBattleTargetRequestAdapter.CreateOriginalRequest(original with { Random = original.Random || (flags & 2) != 0 }, request.Eligibility,
            request.AllowsKnockedOutAllies || allowsKnockedOutAllies || (flags & 1) != 0);
    }

    private static (BattleSide? Side, Func<BattleCombatant, bool> Eligible) NativeAiTargetFilter(BattleCombatant[] actors, byte group, byte comparison, byte criterion)
    {
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
        return (group == 200 ? BattleSide.Allies : group == 201 ? BattleSide.Enemies : null, candidate => InGroup(candidate) && Eligible(candidate));
    }

    private static bool HasNativeRosterSlot(BattleCombatant actor) => actor.NativeActorIndex.HasValue;

    private bool HasAiRosterSlot(BattleCombatant actor) => actor.Slot is not null;

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
