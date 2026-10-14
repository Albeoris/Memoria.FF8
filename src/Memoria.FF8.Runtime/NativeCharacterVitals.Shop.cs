namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>4C2B40 unions IDs92..115 among each acquiredGF's first22 learned abilities, as4ACB70(a3=0).</summary>
    public uint ShopAbilityMask()
    {
        Validate();
        uint result = 0;
        foreach (NativeGuardianForceVital guardianForce in GuardianForces)
        {
            if ((guardianForce.Flags & 1) == 0)
            {
                continue;
            }
            int learned = 0;
            for (int ability = 0; ability < 128 && learned < 22; ability++)
            {
                if (guardianForce.LearnedBank.Has(ability))
                {
                    learned++;
                    if (ability is >= 92 and < 116)
                    {
                        result |= 1u << (ability - 92);
                    }
                }
            }
        }
        return result;
    }
}
