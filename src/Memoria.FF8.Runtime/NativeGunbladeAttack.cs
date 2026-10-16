namespace Memoria.FF8.Runtime;

/// <summary>Original48F480 damage and48FE10 trigger acceptance, independent of ordinary critical hits.</summary>
public static class NativeGunbladeAttack
{
    public static bool TriggerSucceeded(short timing)
    {
        return timing >= 20;
    }

    public static int Damage(int strength, int vitality, int power, int randomByte, short timing)
    {
        return (randomByte % 33 + 240) * (timing / 20 + 2) * (4 * power * ((265 - vitality) * (strength + strength * strength / 16) / 256) / 128) / 256;
    }
}
