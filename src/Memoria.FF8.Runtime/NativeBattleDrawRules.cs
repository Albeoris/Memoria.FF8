namespace Memoria.FF8.Runtime;

public static class NativeBattleDrawRules
{
    public static NativeCharacterVitals Resolve(NativeCharacterVitals vitals, int row, int spell, int quantity, bool cast, out MagicStockMutation? mutation)
    {
        if (spell is < 1 or > 56 || quantity is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(spell));
        }
        mutation = null;
        if (cast)
        {
            return vitals;
        }
        NativeCharacterVitals next = vitals.ChangeMagicStock(row, spell, quantity, MagicOperationReason.DrawStock, out MagicStockMutation result);
        mutation = result;
        return next;
    }

    public static int Quantity(int actorLevel, int sourceLevel, int magic, int spellResistance, int slotResistance, int randomByte)
    {
        return Math.Clamp(((((actorLevel - sourceLevel + 10) >> 1) - spellResistance + (randomByte & 31) + 1 + magic) / 5) - slotResistance, 0, 9);
    }
}
