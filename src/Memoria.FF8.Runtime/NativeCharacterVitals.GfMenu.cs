using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Combines equipped party abilities with the party-wide GF passive abilities.</summary>
    public byte GetEncounterAbilityFlags()
    {
        Validate();
        byte flags = 0;
        foreach (byte row in CombatParty.Where(row => row != 255))
        {
            for (int slot = 0; slot < 4; slot++)
            {
                byte ability = (byte)(Records[row].Junction.Abilities >> (8 * slot));
                if (ability is >= 78 and < 83 && !IsGuardianForcePassiveAbility(ability))
                {
                    flags |= MenuDefinitions.PartyAbilityFlags[ability - 78];
                }
            }
        }
        if (HasActivePassiveAbility(79))
        {
            flags |= MenuDefinitions.PartyAbilityFlags[1];
        }
        return flags;
    }

    /// <summary>Managed abilities by identity, with native display sorting and no original row capacity limit.</summary>
    public IReadOnlyList<NativeGuardianForceAbility> GuardianForceAbilities(int id)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        NativeGuardianForceVital gf = GuardianForces[id];
        SortedDictionary<int, NativeGuardianForceAbility> result = [];
        ImmutableArray<GuardianForceAbilityTemplate> templates = ResolveGuardianForceTemplates()[id];
        Dictionary<int, GuardianForceAbilityProgress>? ledger = gf.Progression?.Abilities.ToDictionary(entry => entry.Ability);
        HashSet<int> active = templates.Select(entry => entry.Ability).ToHashSet();
        for (int ability = 1; ability < MenuDefinitions.AbilityPoints.Length; ability++)
        {
            bool learned = ledger is null ? gf.LearnedBank.Has(ability) : ledger.TryGetValue(ability, out GuardianForceAbilityProgress? progress) && progress.Learned && !progress.Forgotten && (active.Contains(ability) || progress.ItemTaught);
            if (learned)
            {
                result.Add(ability, new(ability, true, -1, 0, 0));
            }
        }
        int level = NativeStatDefinitions.Level(gf.Experience, Definitions.GuardianForces[id]);
        for (int index = 0; index < templates.Length; index++)
        {
            GuardianForceAbilityTemplate entry = templates[index];
            int nativeIndex = Array.FindIndex(MenuDefinitions.Learning[id].ToArray(), rule => rule.Ability == entry.Ability);
            GuardianForceAbilityProgress? progress = ledger?.GetValueOrDefault(entry.Ability);
            bool forgotten = ledger is null ? nativeIndex >= 0 && (gf.ForgottenAbilities & (1u << nativeIndex)) != 0 : progress?.Forgotten == true;
            bool eligible = entry.SupportedCondition && level >= entry.RequiredLevel && (entry.PrerequisiteAbility is null || result.TryGetValue(entry.PrerequisiteAbility.Value, out NativeGuardianForceAbility? prerequisite) && prerequisite.Learned) && (entry.ExcludedAbility is null || !result.TryGetValue(entry.ExcludedAbility.Value, out NativeGuardianForceAbility? excluded) || !excluded.Learned);
            if (eligible && !forgotten && !result.ContainsKey(entry.Ability))
            {
                int points = ledger is null ? nativeIndex >= 0 ? gf.AbilityProgress[nativeIndex] : 0 : progress?.EarnedPoints ?? 0;
                result.Add(entry.Ability, new(entry.Ability, false, index, points, entry.RequiredPoints));
            }
        }
        List<NativeGuardianForceAbility> display = templates.Where(entry => result.ContainsKey(entry.Ability)).Select(entry => result[entry.Ability]).ToList();
        foreach (NativeGuardianForceAbility extra in result.Values.Where(entry => !active.Contains(entry.Ability)))
        {
            int position = display.FindIndex(entry => entry.Ability > extra.Ability);
            display.Insert(position < 0 ? display.Count : position, extra);
        }
        return display;
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
        NativeGuardianForceVital gf = next[id];
        next[id] = gf with { Learning = checked((byte)ability), LastProjection = gf.LastProjection is null ? null : gf.LastProjection with { Learning = checked((byte)ability) } };
        return this with { GuardianForces = next };
    }

    /// <summary>497010 saturates the active target's progress byte;4A6680 learns it and selects the first available kernel learning entry.</summary>
    public NativeCharacterVitals AwardGuardianForceAbilityPoints(int id, ushort points)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        if (points == 0 || (GuardianForces[id].Flags & 1) == 0)
        {
            return this;
        }
        NativeCharacterVitals result = ApplyGuardianForceTemplates(ResolveGuardianForceTemplates());
        NativeGuardianForceVital gf = result.GuardianForces[id];
        NativeGuardianForceAbility? target = result.GuardianForceAbilities(id).FirstOrDefault(entry => entry.Ability == gf.Learning && !entry.Learned);
        if ((gf.Flags & 1) == 0 || points == 0 || target is null || gf.Learning == 0)
        {
            return result;
        }
        int earned = Math.Min(255, target.Progress + points);
        GuardianForceAbilityProgress[] ledger = gf.Progression!.Abilities.ToArray();
        int index = Array.FindIndex(ledger, entry => entry.Ability == target.Ability);
        ledger[index] = ledger[index] with { EarnedPoints = earned, Learned = earned >= target.RequiredPoints };
        result.GuardianForces[id] = gf with
        {
            MoveFindDisabled = gf.MoveFindDisabled && (target.Ability != 79 || !ledger[index].Learned),
            Progression = gf.Progression with { Abilities = ledger.ToImmutableArray() }
        };
        result.GuardianForces[id] = result.SynchronizeGuardianForce(id);
        return result.RebuildParty();
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
        if ((uint)slot >= count || ability != 0 && (!learned.Contains(ability) || IsGuardianForcePassiveAbility(ability) || (command ? ability is < 20 or >= 39 : ability is < 39 or >= 83)))
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
        records[row] = records[row] with { HitPoints = ClampHitPoints(records[row].HitPoints, MagicMaximumHitPoints(records[row])) };
        return this with { Records = records };
    }

    public NativeCharacterVitals SetJunctionMagic(int row, int slot, int spell, bool reassign = false)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        if ((uint)slot >= 19 || (uint)spell >= Definitions.MagicHitPoints.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Magic junction requires one of the nineteen native slots and an actual kernel spell.");
        }
        if (!JunctionMagicSlotAvailable(row, slot) || spell != 0 && GetMagicCount(row, spell) == 0)
        {
            throw new InvalidOperationException("Magic junction requires its learned GF slot ability and a stocked spell.");
        }
        NativeCharacterVitals result = MagicJunctionPolicy.Assign(this, row, slot, spell, reassign);
        return result.RebuildMenuRow(row);
    }

    public NativeCharacterVitals ReassignGuardianForceJunction(int row, int id, int expectedOwner)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, 8u);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)expectedOwner, 8u);
        ushort mask = (ushort)(1 << id);
        if (row == expectedOwner || (Records[expectedOwner].Junction.GuardianForces & mask) == 0 || Records.Where((record, index) => index != expectedOwner).Any(record => (record.Junction.GuardianForces & mask) != 0))
        {
            throw new InvalidOperationException("GF reassignment requires the confirmed current owner and a different destination character.");
        }
        return ToggleGuardianForceJunction(expectedOwner, id).ToggleGuardianForceJunction(row, id);
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
        records[row] = records[row] with { HitPoints = ClampHitPoints(records[row].HitPoints, MagicMaximumHitPoints(records[row])) };
        return result.RebuildParty();
    }
}
