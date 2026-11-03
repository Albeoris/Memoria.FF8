using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record EncounterSpellChoice(string Spell, int ElementIndex, bool Reflectable, int Weight = 1);
public sealed record EncounterTargetChoice(string AllocationId, bool Reflect);
public enum EncounterOffensiveDelivery
{
    Direct,
    AlliedReflector,
    Dispel
}
public sealed record EncounterOffensiveChoice(EncounterOffensiveDelivery Delivery, string TargetId, string Spell);

/// <summary>Selection reads observations, never concealed combatant defenses. Random draws belong to the battle owner.</summary>
public static class EncounterActionSelection
{
    public static EncounterOffensiveChoice? Select(ImmutableArray<EncounterSpellChoice> spells, IEnumerable<EncounterTargetChoice> opponents, IEnumerable<EncounterTargetChoice> allies, BattleScanKnowledge knowledge, BattleObserverSide observer, bool avoidReflect, Func<int, int> nextIndex)
    {
        ArgumentNullException.ThrowIfNull(nextIndex);
        if (spells.IsDefaultOrEmpty || spells.Any(spell => string.IsNullOrWhiteSpace(spell.Spell) || spell.ElementIndex is < 0 or > 7 || spell.Weight is < 1 or > 255) || spells.Sum(spell => spell.Weight) > 256 || spells.Select(spell => spell.Spell).Distinct(StringComparer.Ordinal).Count() != spells.Length)
        {
            throw new InvalidDataException("An elemental policy requires distinct spells and their canonical element indices.");
        }
        EncounterTargetChoice[] targets = StableTargets(opponents);
        if (targets.Length == 0)
        {
            return null;
        }
        EncounterSpellChoice[] pool = spells.OrderBy(spell => spell.Spell, StringComparer.Ordinal).ToArray();
        if (avoidReflect)
        {
            EncounterTargetChoice[] direct = targets.Where(target => !target.Reflect).ToArray();
            if (direct.Length != 0)
            {
                targets = direct;
            }
            else
            {
                EncounterTargetChoice[] reflectors = StableTargets(allies).Where(target => target.Reflect).ToArray();
                if (reflectors.Length == 0)
                {
                    return new(EncounterOffensiveDelivery.Dispel, Choose(targets, nextIndex).AllocationId, "Dispel");
                }
                EncounterSpellChoice[] reflectable = pool.Where(spell => spell.Reflectable).ToArray();
                if (reflectable.Length == 0)
                {
                    throw new InvalidDataException("An allied-reflector policy requires a spell verified as reflectable.");
                }
                string reflector = Choose(reflectors, nextIndex).AllocationId;
                return new(EncounterOffensiveDelivery.AlliedReflector, reflector, Choose(reflectable, nextIndex).Spell);
            }
        }
        List<(string Target, string Spell, decimal Multiplier)> informed = [];
        foreach (EncounterTargetChoice target in targets)
        {
            BattleScanObservation? observation = knowledge.Find(observer, target.AllocationId);
            if (observation is null)
            {
                continue;
            }
            foreach (EncounterSpellChoice spell in pool)
            {
                BattleKnownAffinity affinity = observation.Affinities[spell.ElementIndex];
                if (!affinity.Absorbs && affinity.DamageMultiplier >= 1)
                {
                    informed.Add((target.AllocationId, spell.Spell, affinity.DamageMultiplier));
                }
            }
        }
        if (informed.Count != 0)
        {
            decimal maximum = informed.Max(pair => pair.Multiplier);
            var selected = Choose(informed.Where(pair => pair.Multiplier == maximum).ToArray(), nextIndex);
            return new(EncounterOffensiveDelivery.Direct, selected.Target, selected.Spell);
        }
        string fallbackTarget = Choose(targets, nextIndex).AllocationId;
        return new(EncounterOffensiveDelivery.Direct, fallbackTarget, pool[ChooseWeightedIndex(pool.Select(spell => spell.Weight).ToImmutableArray(), nextIndex)].Spell);
    }

    internal static int ChooseWeightedIndex(ImmutableArray<int> weights, Func<int, int> nextIndex)
    {
        if (weights.IsDefaultOrEmpty || weights.Any(weight => weight is < 1 or > 255) || weights.Sum() > 256)
        {
            throw new InvalidDataException("A spell pool requires positive integer weights in 1–255 totaling at most 256.");
        }
        int total = weights.Sum();
        int draw = nextIndex(total);
        if ((uint)draw >= total)
        {
            throw new InvalidDataException("Battle RNG returned an index outside the weighted spell pool.");
        }
        for (int index = 0; index < weights.Length; index++)
        {
            if (draw < weights[index])
            {
                return index;
            }
            draw -= weights[index];
        }
        throw new InvalidDataException("Weighted spell selection did not reach a valid interval.");
    }

    private static EncounterTargetChoice[] StableTargets(IEnumerable<EncounterTargetChoice> targets)
    {
        EncounterTargetChoice[] result = targets.OrderBy(target => target.AllocationId, StringComparer.Ordinal).ToArray();
        if (result.Any(target => string.IsNullOrWhiteSpace(target.AllocationId)) || result.Select(target => target.AllocationId).Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidDataException("Policy candidates require distinct live allocation identities.");
        }
        return result;
    }

    private static T Choose<T>(T[] values, Func<int, int> nextIndex)
    {
        int index = nextIndex(values.Length);
        if ((uint)index >= values.Length)
        {
            throw new InvalidDataException("Battle RNG returned an index outside the candidate set.");
        }
        return values[index];
    }
}

/// <summary>Only successful action admission advances a policy; contacts and repeat casts do not.</summary>
public sealed record EncounterActionCursor(ulong CommittedTurns = 0)
{
    public ulong NextOwnTurn => CommittedTurns == ulong.MaxValue ? ulong.MaxValue : CommittedTurns + 1;

    public EncounterActionCursor Admit()
    {
        return new(NextOwnTurn);
    }
}
