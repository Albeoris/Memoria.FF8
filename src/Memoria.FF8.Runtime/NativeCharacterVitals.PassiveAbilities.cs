namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    private const int FirstPartyAbility = 78;
    private const int LastPartyAbility = 82;

    public static bool IsGuardianForcePassiveAbility(int ability)
    {
        return ability is >= FirstPartyAbility and <= LastPartyAbility;
    }

    private static bool GuardianForcePassiveAbilityDisabled(NativeGuardianForceVital gf, int ability)
    {
        return ability switch
        {
            78 => gf.AlertDisabled,
            79 => gf.MoveFindDisabled,
            80 => gf.EncHalfDisabled,
            81 => gf.EncNoneDisabled,
            82 => gf.RareItemDisabled,
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
    }

    public bool GuardianForcePassiveAbilityEnabled(int id, int ability)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)id, 16u);
        if (!IsGuardianForcePassiveAbility(ability))
        {
            throw new ArgumentOutOfRangeException(nameof(ability), "The ability is not a supported GF passive.");
        }
        NativeGuardianForceVital gf = GuardianForces[id];
        return (gf.Flags & 1) != 0 && gf.HasLearnedAbility(ability) && !GuardianForcePassiveAbilityDisabled(gf, ability);
    }

    public bool HasActivePassiveAbility(int ability)
    {
        if (!IsGuardianForcePassiveAbility(ability))
        {
            throw new ArgumentOutOfRangeException(nameof(ability), "The ability is not a supported GF passive.");
        }
        Validate();
        return HasLivingGuardianForceWithPassive(ability);
    }

    private bool HasLivingGuardianForceWithPassive(int ability)
    {
        return GuardianForces.Any(gf => (gf.Flags & 1) != 0 && gf.HitPoints > 0 && gf.HasLearnedAbility(ability) && !GuardianForcePassiveAbilityDisabled(gf, ability));
    }

    public NativeCharacterVitals SetGuardianForcePassiveAbilityEnabled(int id, int ability, bool enabled)
    {
        GuardianForcePassiveAbilityEnabled(id, ability);
        NativeGuardianForceVital gf = GuardianForces[id];
        if ((gf.Flags & 1) == 0 || !gf.HasLearnedAbility(ability))
        {
            throw new InvalidOperationException("Only an acquired GF's learned passive can be toggled.");
        }
        NativeGuardianForceVital[] next = GuardianForces.ToArray();
        next[id] = ability switch
        {
            78 => gf with { AlertDisabled = !enabled },
            79 => gf with { MoveFindDisabled = !enabled },
            80 => gf with { EncHalfDisabled = !enabled },
            81 => gf with { EncNoneDisabled = !enabled },
            82 => gf with { RareItemDisabled = !enabled },
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
        return this with { GuardianForces = next };
    }

    /// <summary>Clears former party ability slots while retaining every other assignment and its slot.</summary>
    internal NativeCharacterVitals WithoutPartyAbilityJunctions()
    {
        NativeCharacterVital[]? records = null;
        for (int row = 0; row < Records.Length; row++)
        {
            NativeCharacterVital record = Records[row];
            uint abilities = record.Junction.Abilities;
            for (int shift = 0; shift < 32; shift += 8)
            {
                if (IsGuardianForcePassiveAbility((byte)(abilities >> shift)))
                {
                    abilities &= ~(255u << shift);
                }
            }
            if (abilities != record.Junction.Abilities)
            {
                records ??= Records.ToArray();
                records[row] = record with { Junction = record.Junction with { Abilities = abilities } };
            }
        }
        return records is null ? this : this with { Records = records };
    }
}
