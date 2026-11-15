namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffectVisualPhase
{
    internal static bool IsOdd(NativeBattleModelAliases aliases, BattleModelOwner owner) => (Phase(aliases, owner) & 1) != 0;

    internal static bool IsFourthFrame(NativeBattleModelAliases aliases, BattleModelOwner owner, byte age) => ((Phase(aliases, owner) + age) & 3) == 0;

    private static int Phase(NativeBattleModelAliases aliases, BattleModelOwner owner)
    {
        if (!aliases.Roster.Contains(owner))
        {
            throw new InvalidDataException("The visual phase requires a current model owner.");
        }
        if (aliases.TryEncodeExact(owner, out byte originalOperand))
        {
            return originalOperand;
        }

        // Additional actors cycle through phases in roster order.
        int phase = 0;
        foreach (BattleModelOwner candidate in aliases.Roster.Owners)
        {
            if (candidate == owner)
            {
                return phase;
            }
            if (!aliases.TryEncodeExact(candidate, out _))
            {
                phase = (phase + 1) & 3;
            }
        }
        throw new InvalidDataException("The visual phase requires a current model owner.");
    }
}
