namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Unions shop capabilities from every acquired GF without a managed ability capacity limit.</summary>
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
            for (int ability = 92; ability < 116; ability++)
            {
                if (guardianForce.HasLearnedAbility(ability))
                {
                    result |= 1u << (ability - 92);
                }
            }
        }
        return result;
    }
}
