namespace Memoria.FF8.Runtime;

/// <summary>Native48BBD0 level dispatch; original random bytes are consumed by its caller.</summary>
public static class NativeBattleEnemyLevelRules
{
    public static int RandomByteCount(byte sceneLevel) => sceneLevel <= 100 || sceneLevel == 254 ? 0 : sceneLevel is 251 or 253 ? 2 : 1;

    public static int Resolve(byte sceneLevel, ReadOnlySpan<byte> presentPartyLevels, byte originalBattleRoll, byte? secondBattleRoll = null)
    {
        if (sceneLevel <= 100)
        {
            return sceneLevel;
        }
        if (sceneLevel == 252)
        {
            return originalBattleRoll % 100 + 1;
        }
        if (RandomByteCount(sceneLevel) == 2 && secondBattleRoll is null)
        {
            throw new InvalidDataException("Native enemy level code requires its second original battle random byte.");
        }
        if (presentPartyLevels.Length is < 1 or > 3)
        {
            throw new InvalidDataException("Native enemy level requires the actual one-to-three present battle party levels.");
        }
        int sum = 0;
        foreach (byte level in presentPartyLevels)
        {
            if (level is < 1 or > 100)
            {
                throw new InvalidDataException("Native present battle party levels must be1..100.");
            }
            sum += level;
        }
        int average = sum / presentPartyLevels.Length;
        if (sceneLevel == 254)
        {
            return average;
        }
        int varied = Math.Clamp(average + ((originalBattleRoll & 1) != 0 ? average / 5 : average / -5), 1, 100);
        if (sceneLevel <= 200)
        {
            return Math.Min(sceneLevel - 100, varied);
        }
        if (sceneLevel <= 250)
        {
            return Math.Min(100, varied + sceneLevel - 200);
        }
        return sceneLevel switch
        {
            251 => Math.Clamp(average + ((originalBattleRoll & 1) != 0 ? secondBattleRoll!.Value & 3 : -(secondBattleRoll!.Value & 3)), 1, 65),
            253 => Math.Max(1, secondBattleRoll!.Value % varied),
            255 => varied,
            _ => throw new InvalidDataException("Invalid native level dispatch.")
        };
    }
}
