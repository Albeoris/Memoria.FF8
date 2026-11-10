namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public static bool IsGuardianForcePassiveAbility(int ability)
    {
        return ability is >= 79 and <= 81;
    }

    private static bool GuardianForcePassiveAbilityDisabled(NativeGuardianForceVital gf, int ability)
    {
        return ability switch
        {
            79 => gf.MoveFindDisabled,
            80 => gf.EncHalfDisabled,
            81 => gf.EncNoneDisabled,
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
            79 => gf with { MoveFindDisabled = !enabled },
            80 => gf with { EncHalfDisabled = !enabled },
            81 => gf with { EncNoneDisabled = !enabled },
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
        return this with { GuardianForces = next };
    }
}
