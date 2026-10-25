namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public static bool IsGuardianForcePassiveAbility(int ability)
    {
        return ability == 79;
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
        return (gf.Flags & 1) != 0 && gf.HasLearnedAbility(ability) && !gf.MoveFindDisabled;
    }

    public bool HasActivePassiveAbility(int ability)
    {
        if (!IsGuardianForcePassiveAbility(ability))
        {
            throw new ArgumentOutOfRangeException(nameof(ability), "The ability is not a supported GF passive.");
        }
        Validate();
        return GuardianForces.Any(gf => (gf.Flags & 1) != 0 && gf.HitPoints > 0 && gf.HasLearnedAbility(ability) && !gf.MoveFindDisabled);
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
        next[id] = gf with { MoveFindDisabled = !enabled };
        return this with { GuardianForces = next };
    }
}
