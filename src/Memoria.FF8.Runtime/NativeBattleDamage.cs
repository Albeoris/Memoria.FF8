namespace Memoria.FF8.Runtime;

/// <summary>Integer operation order from original492C40,491AD0,493280; RNG byte is supplied by the battle owner.</summary>
public static class NativeBattleDamage
{
    public static int Physical(int strength, int vitality, int power, int randomByte)
    {
        return (randomByte % 33 + 240) * (power * ((265 - vitality) * (strength + strength * strength / 16) / 256) / 16) / 256;
    }

    public static int Magic(int magic, int spirit, int power, int randomByte, bool enemy)
    {
        int value = (randomByte % 33 + 240) * (power * ((265 - spirit) * (power + magic) / 4) / 256) / 256;
        return enemy ? value >> 1 : value;
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
