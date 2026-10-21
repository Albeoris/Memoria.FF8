namespace Memoria.FF8.Runtime;

/// <summary>Integer operation order from original492C40,491AD0,493280; RNG byte is supplied by the battle owner.</summary>
public static class NativeBattleDamage
{
    public static int Element(int damage, byte elementMask, int defense, bool physical, int elementPercent)
    {
        if (elementMask == 0)
        {
            return damage;
        }
        return physical ? damage + damage * elementPercent * (800 - defense) / 10_000 : damage * (900 - defense) / 100;
    }

    public static int MagicMitigation(int damage, byte attackFlags, uint statuses)
    {
        if ((attackFlags & 3) == 1 && (statuses & 0x40) != 0)
        {
            damage >>= 1;
        }
        if ((statuses & 0x80000) != 0)
        {
            damage >>= 1;
        }
        return damage;
    }

    public static bool PhysicalHits(int hit, int luck, int evasion, int targetLuck, ushort savedStatus, uint targetStatus, int randomByte)
    {
        if ((targetStatus & 9) != 0 || hit == 255)
        {
            return true;
        }
        if ((savedStatus & 8) != 0)
        {
            hit >>= 2;
        }
        int chance = 255 * Math.Max(0, hit + (luck >> 1) - evasion - targetLuck) / 100;
        return chance != 0 && chance >= randomByte;
    }

    public static bool PhysicalCritical(int critical, int luck, int randomByte)
    {
        int chance = critical + luck;
        return chance != 0 && chance >= randomByte;
    }

    public static int Physical(int strength, int vitality, int power, int randomByte)
    {
        return (randomByte % 33 + 240) * (power * ((265 - vitality) * (strength + strength * strength / 16) / 256) / 16) / 256;
    }

    public static int Magic(int magic, int spirit, int power, int randomByte, bool enemy, BattleDifficulty difficulty = BattleDifficulty.Normal)
    {
        int value = (randomByte % 33 + 240) * (power * ((265 - spirit) * (power + magic) / 4) / 256) / 256;
        return enemy && difficulty == BattleDifficulty.Normal ? value >> 1 : value;
    }

    public static int GuardianForce(int spirit, int power, int level, int levelCoefficient, int constantPower, int damagePercent, int randomByte, int boostPercent = 100)
    {
        return (randomByte % 33 + 240) * ((damagePercent + 100) * (boostPercent * (power * ((265 - spirit) * (levelCoefficient * level / 10 + power + constantPower) / 8) / 256) / 100) / 100) / 256;
    }

    public static int Cure(int magic, int power, int randomByte)
    {
        return power * (randomByte % 33 + 240) * ((power + magic) / 2) / 256;
    }
}
