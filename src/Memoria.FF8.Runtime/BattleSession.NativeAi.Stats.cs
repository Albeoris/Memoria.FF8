namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static void SetNativeAiStatMultiplier(BattleCombatant actor, int index, int multiplier)
    {
        //487DF0 opcode40 writes oneU8 in the six-byte multiplier bank, then48C1C0 recomputes statistics.
        actor.SetNativeStatMultiplier(index, checked((byte)multiplier));
    }

    private static void RestoreNativeStatMultipliers(BattleCombatant actor, byte[] multipliers)
    {
        if (multipliers is null || multipliers.Length != 6)
        {
            throw new InvalidDataException("Native battle participant requires all six statistic multipliers.");
        }
        for (int index = 0; index < multipliers.Length; index++)
        {
            if (multipliers[index] != 10)
            {
                actor.SetNativeStatMultiplier(index, multipliers[index]);
            }
            else
            {
                actor.NativeStatMultipliers[index] = 10;
            }
        }
    }
}
