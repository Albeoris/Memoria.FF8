namespace Memoria.FF8.Runtime;

/// <summary>Shared entry gauge policy using each participant's current speed.</summary>
public static class BattleInitialAtb
{
    public static int Calculate(int speed, byte randomByte)
    {
        int percent = (speed >> 2) + (randomByte & 0x7F) - 34;
        return Math.Clamp(percent, 0, 100) * (BattleSession.AtbMaximum / 100);
    }
}
