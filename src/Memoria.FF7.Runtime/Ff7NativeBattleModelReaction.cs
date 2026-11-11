namespace Memoria.FF7.Runtime;

public static class Ff7NativeBattleModelReaction
{
    public static short Select(bool player, byte hurtAction, byte playerHurtIndex, IReadOnlyList<byte>? playerHurtActions, byte power, uint previousStatuses, bool missed, int numericalDamage, bool magicPoints, bool revived, bool newlyKnockedOut)
    {
        short selected = power == 0 ? (short)0x33 : player ? (short)8 : (short)2;
        if (revived)
        {
            return player ? selected : (short)0x39;
        }
        if (missed)
        {
            return player ? (short)10 : selected;
        }
        if (numericalDamage < 0)
        {
            return selected;
        }
        if (!newlyKnockedOut && (magicPoints || numericalDamage == 0))
        {
            return 0x33;
        }
        if ((previousStatuses & 0x400) != 0)
        {
            return 0x30;
        }
        if ((previousStatuses & 0x800) != 0)
        {
            return 5;
        }
        if (!player || playerHurtIndex == 0 && playerHurtActions is null)
        {
            return hurtAction;
        }
        if (playerHurtActions is null || playerHurtIndex >= playerHurtActions.Count)
        {
            throw new NotSupportedException("FFVII reaction requires its original player hurt-action selector table.");
        }
        return playerHurtActions[playerHurtIndex];
    }
}
