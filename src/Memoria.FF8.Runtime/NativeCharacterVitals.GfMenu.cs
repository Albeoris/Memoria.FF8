using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>495EF0/495530 OR all three combat members' equipped party-ability kernel flags, including KO members.</summary>
    public byte GetEncounterAbilityFlags()
    {
        Validate();
        byte flags = 0;
        foreach (byte row in CombatParty.Where(row => row != 255))
        {
            for (int slot = 0; slot < 4; slot++)
            {
                byte ability = (byte)(Records[row].Junction.Abilities >> (8 * slot));
                if (ability is >= 78 and < 83)
                {
                    flags |= MenuDefinitions.PartyAbilityFlags[ability - 78];
                }
            }
        }
        return flags;
    }

    /// <summary>Original4ACB70: learned IDs first (limit22), then level and prerequisite unlock passes; output sorted by ability ID.</summary>
    public IReadOnlyList<NativeGuardianForceAbility> GuardianForceAbilities(int id)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        NativeGuardianForceVital gf = GuardianForces[id];
        SortedDictionary<int, NativeGuardianForceAbility> result = [];
        for (int ability = 0; ability < 128 && result.Count < 22; ability++)
        {
            if (gf.LearnedBank.Has(ability))
            {
                result.Add(ability, new NativeGuardianForceAbility(ability, true, -1, 0, 0));
            }
        }
        ImmutableArray<NativeGuardianForceLearningRule> rules = MenuDefinitions.Learning[id];
        int level = NativeStatDefinitions.Level(gf.Experience, Definitions.GuardianForces[id]);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 0; index < rules.Length && result.Count < 22; index++)
            {
                NativeGuardianForceLearningRule rule = rules[index];
                bool eligible = pass == 0 ? rule.Requirement <= 100 && level >= rule.Requirement : rule.Requirement is >= 101 and <= 121 && result.TryGetValue(rules[rule.Requirement - 101].Ability, out NativeGuardianForceAbility? prerequisite) && prerequisite.Learned && (rule.Exclusion == 255 || !result.TryGetValue(rules[rule.Exclusion].Ability, out NativeGuardianForceAbility? excluded) || !excluded.Learned);
                if (eligible && !result.ContainsKey(rule.Ability) && (gf.ForgottenAbilities & (1u << index)) == 0)
                {
                    if (rule.Ability >= MenuDefinitions.AbilityPoints.Length)
                    {
                        throw new NotSupportedException("GF learning requires its actual kernel AP requirement.");
                    }
                    result.Add(rule.Ability, new NativeGuardianForceAbility(rule.Ability, false, index, gf.AbilityProgress[index], MenuDefinitions.AbilityPoints[rule.Ability]));
                }
            }
        }
        return result.Values.ToArray();
    }

    public NativeCharacterVitals SelectGuardianForceLearning(int id, int ability)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        if ((GuardianForces[id].Flags & 1) == 0 || !GuardianForceAbilities(id).Any(entry => entry.Ability == ability && !entry.Learned))
        {
            throw new InvalidOperationException("Only an acquired GF's available unlearned ability can be selected.");
        }
        NativeGuardianForceVital[] next = GuardianForces.ToArray();
        next[id] = next[id] with { Learning = checked((byte)ability) };
        return this with { GuardianForces = next };
    }

    /// <summary>497010 saturates the active target's progress byte;4A6680 learns it and selects the first available kernel learning entry.</summary>
    public NativeCharacterVitals AwardGuardianForceAbilityPoints(int id, ushort points)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        NativeGuardianForceVital gf = GuardianForces[id];
        NativeGuardianForceAbility? target = GuardianForceAbilities(id).FirstOrDefault(entry => entry.Ability == gf.Learning && !entry.Learned);
        if ((gf.Flags & 1) == 0 || points == 0 || target is null || gf.Learning == 0)
        {
            return this;
        }
        int progress = target.Progress + points;
        NativeGuardianForceVital[] next = GuardianForces.ToArray();
        gf = gf with { AbilityProgress = gf.AbilityProgress.SetItem(target.LearningIndex, (byte)Math.Min(progress, 255)) };
        if (progress >= target.RequiredPoints)
        {
            int ability = target.Ability;
            gf = gf with { LearnedBank = ability < 64 ? gf.LearnedBank with { Low = gf.LearnedBank.Low | (1UL << ability) } : gf.LearnedBank with { High = gf.LearnedBank.High | (1UL << (ability - 64)) } };
        }
        next[id] = gf;
        NativeCharacterVitals result = this with { GuardianForces = next };
        if (progress >= target.RequiredPoints)
        {
            HashSet<int> available = result.GuardianForceAbilities(id).Where(entry => !entry.Learned).Select(entry => entry.Ability).ToHashSet();
            byte learning = MenuDefinitions.Learning[id].FirstOrDefault(rule => rule.Ability != 0 && available.Contains(rule.Ability))?.Ability ?? 0;
            next[id] = gf with { Learning = learning };
        }
        return result;
    }

    public IReadOnlySet<int> JunctionLearnedAbilities(int row)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        return Records[row].Junction.GuardianForceAssignments.SelectMany(id => GuardianForceAbilities(id).Where(entry => entry.Learned).Select(entry => entry.Ability)).ToHashSet();
    }

    public int JunctionAbilitySlots(int row)
    {
        IReadOnlySet<int> learned = JunctionLearnedAbilities(row);
        return learned.Contains(19) ? 4 : learned.Contains(18) ? 3 : 2;
    }

    public bool JunctionMagicSlotAvailable(int row, int slot)
    {
        IReadOnlySet<int> learned = JunctionLearnedAbilities(row);
        return slot switch
        {
            >= 0 and < 9 => learned.Contains(slot + 1),
            9 => learned.Contains(10),
            10 => learned.Contains(11),
            >= 11 and < 15 => slot - 11 < (learned.Contains(15) ? 4 : learned.Contains(14) ? 2 : learned.Contains(12) ? 1 : 0),
            >= 15 and < 19 => slot - 15 < (learned.Contains(17) ? 4 : learned.Contains(16) ? 2 : learned.Contains(13) ? 1 : 0),
            _ => false
        };
    }

    public NativeCharacterVitals SetJunctionAbility(int row, int slot, int ability, bool command)
    {
        IReadOnlySet<int> learned = JunctionLearnedAbilities(row);
        int count = command ? 3 : JunctionAbilitySlots(row);
        if ((uint)slot >= count || ability != 0 && (!learned.Contains(ability) || (command ? ability is < 20 or >= 39 : ability is < 39 or >= 83)))
        {
            throw new InvalidOperationException("Junction choice requires an unlocked slot and a learned ability from an assigned GF.");
        }
        NativeCharacterVital[] records = Records.ToArray();
        NativeCharacterJunction junction = records[row].Junction;
        uint packed = command ? junction.Commands : junction.Abilities;
        for (int index = 0; index < 4; index++)
        {
            if (ability != 0 && (byte)(packed >> (8 * index)) == ability)
            {
                packed &= ~(255u << (8 * index));
            }
        }
        packed = (packed & ~(255u << (8 * slot))) | ((uint)ability << (8 * slot));
        junction = command ? junction with { Commands = packed } : junction with { Abilities = packed };
        records[row] = records[row] with { Junction = junction };
        records[row] = records[row] with { HitPoints = ClampHitPoints(records[row].HitPoints, Definitions.CharacterMaximum(records[row])) };
        return this with { Records = records };
    }

    public NativeCharacterVitals SetJunctionMagic(int row, int slot, int spell)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        if ((uint)slot >= 19 || (uint)spell >= Definitions.MagicHitPoints.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Magic junction requires one of the nineteen native slots and an actual kernel spell.");
        }
        if (!JunctionMagicSlotAvailable(row, slot) || spell != 0 && !Records[row].MagicInventory.Any(stock => stock.SpellId == spell && stock.Count > 0))
        {
            throw new InvalidOperationException("Magic junction requires its learned GF slot ability and a stocked spell.");
        }
        NativeCharacterVital[] records = Records.ToArray();
        byte[] junctions = records[row].Junction.MagicJunctions.ToArray();
        for (int index = 0; index < 19; index++)
        {
            if (spell != 0 && junctions[index] == spell)
            {
                junctions[index] = 0;
            }
        }
        junctions[slot] = checked((byte)spell);
        records[row] = records[row] with { Junction = records[row].Junction with { MagicJunctions = junctions } };
        records[row] = records[row] with { HitPoints = ClampHitPoints(records[row].HitPoints, Definitions.CharacterMaximum(records[row])) };
        return this with { Records = records };
    }

    public NativeCharacterVitals ToggleGuardianForceJunction(int row, int id)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        if ((GuardianForces[id].Flags & 1) == 0)
        {
            throw new InvalidOperationException("An unacquired GF cannot be junctioned.");
        }
        ushort mask = (ushort)(1 << id);
        bool remove = (Records[row].Junction.GuardianForces & mask) != 0;
        if (Records.Where((record, index) => index != row).Any(record => (record.Junction.GuardianForces & mask) != 0))
        {
            throw new InvalidOperationException("Remove this GF from its current character before junctioning it to another character.");
        }
        NativeCharacterVital[] records = Records.ToArray();
        records[row] = records[row] with { Junction = records[row].Junction with { GuardianForces = remove ? (ushort)(records[row].Junction.GuardianForces & ~mask) : (ushort)(records[row].Junction.GuardianForces | mask) } };
        NativeCharacterVitals result = this with { Records = records };
        IReadOnlySet<int> learned = result.JunctionLearnedAbilities(row);
        NativeCharacterJunction junction = records[row].Junction;
        uint commands = junction.Commands;
        uint abilities = junction.Abilities;
        for (int slot = 0; slot < 4; slot++)
        {
            if (!learned.Contains((byte)(commands >> (8 * slot))))
            {
                commands &= ~(255u << (8 * slot));
            }
            if (slot >= result.JunctionAbilitySlots(row) || !learned.Contains((byte)(abilities >> (8 * slot))))
            {
                abilities &= ~(255u << (8 * slot));
            }
        }
        // Magic junction removal depends on the same learned GF junction abilities.
        // Retaining inaccessible assignments would leave a saved bonus after its GF was removed.
        byte[] magic = junction.MagicJunctions.ToArray();
        for (int slot = 0; slot < 19; slot++)
        {
            if (!result.JunctionMagicSlotAvailable(row, slot))
            {
                magic[slot] = 0;
            }
        }
        if (remove && junction.GuardianForces == 0)
        {
            Array.Clear(magic);
        }
        records[row] = records[row] with { Junction = junction with { Commands = commands, Abilities = abilities, MagicJunctions = magic } };
        records[row] = records[row] with { HitPoints = ClampHitPoints(records[row].HitPoints, Definitions.CharacterMaximum(records[row])) };
        return result;
    }
}
