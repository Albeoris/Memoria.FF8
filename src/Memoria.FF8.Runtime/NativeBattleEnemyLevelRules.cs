namespace Memoria.FF8.Runtime;

/// <summary>Native48BBD0 level dispatch; original random bytes are consumed by its caller.</summary>
public static class NativeBattleEnemyLevelRules
{
    public static int SelectBand(int level, byte mediumLevel, byte highLevel) => level < mediumLevel ? 0 : level < highLevel ? 1 : 2;

    public static int RandomByteCount(byte sceneLevel) => sceneLevel <= 100 || sceneLevel == 254 ? 0 : sceneLevel is 251 or 253 ? 2 : 1;

    public static int Resolve(byte sceneLevel, ReadOnlySpan<byte> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll = null)
    {
        return ResolveOriginalParty(sceneLevel, presentPartyLevels, originalBattleRoll, secondBattleRoll, 3);
    }

    public static int ResolveExpanded(byte sceneLevel, ReadOnlySpan<byte> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll = null)
    {
        return ResolveOriginalParty(sceneLevel, presentPartyLevels, originalBattleRoll, secondBattleRoll, NativeCharacterVitals.ExtendedRecordCount);
    }

    private static int ResolveOriginalParty(byte sceneLevel, ReadOnlySpan<byte> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll, int capacity)
    {
        Span<int> levels = stackalloc int[Math.Min(presentPartyLevels.Length, capacity + 1)];
        for (int index = 0; index < levels.Length; index++)
        {
            levels[index] = presentPartyLevels[index];
        }
        return ResolveForParty(sceneLevel, levels, originalBattleRoll, secondBattleRoll, capacity, GameplayLevelRules.Default);
    }

    public static int ResolveExpanded(byte sceneLevel, ReadOnlySpan<int> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll, GameplayLevelRules rules)
    {
        return ResolveForParty(sceneLevel, presentPartyLevels, originalBattleRoll, secondBattleRoll, NativeCharacterVitals.ExtendedRecordCount, rules);
    }

    private static int ResolveForParty(byte sceneLevel, ReadOnlySpan<int> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll, int capacity, GameplayLevelRules rules)
    {
        int maximum = rules.MaximumEnemyLevel;
        if (sceneLevel <= 100)
        {
            return Math.Min(sceneLevel, maximum);
        }
        if (sceneLevel == 252)
        {
            return Math.Min(originalBattleRoll % 100 + 1, maximum);
        }
        if (RandomByteCount(sceneLevel) == 2 && secondBattleRoll is null)
        {
            throw new InvalidDataException("Native enemy level code requires its second original battle random byte.");
        }
        if (presentPartyLevels.Length < 1 || presentPartyLevels.Length > capacity)
        {
            throw new InvalidDataException("Native enemy level requires the actual present battle party levels within its roster capacity.");
        }
        int sum = 0;
        foreach (int level in presentPartyLevels)
        {
            if (level < 1 || level > rules.MaximumCharacterLevel)
            {
                throw new InvalidDataException("Present battle party levels exceed the supported character level range.");
            }
            sum += level;
        }
        int average = sum / presentPartyLevels.Length + rules.AdditionalPartyLevel;
        if (sceneLevel == 254)
        {
            return Math.Min(maximum, average);
        }
        int varied = Math.Clamp(average + ((originalBattleRoll & 1) != 0 ? average / 5 : average / -5), 1, maximum);
        if (sceneLevel <= 200)
        {
            return Math.Min(sceneLevel - 100, varied);
        }
        if (sceneLevel <= 250)
        {
            return Math.Min(maximum, varied + sceneLevel - 200);
        }
        return sceneLevel switch
        {
            251 => Math.Clamp(average + ((originalBattleRoll & 1) != 0 ? secondBattleRoll!.Value & 3 : -(secondBattleRoll!.Value & 3)), 1, Math.Min(65, maximum)),
            253 => Math.Max(1, secondBattleRoll!.Value % varied),
            255 => varied,
            _ => throw new InvalidDataException("Invalid native level dispatch.")
        };
    }
}
