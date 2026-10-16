namespace Memoria.FF8.Runtime;

public static class NativeBattleFormation
{
    /// <summary>47DBA0's occupied party positions from authenticated signed WORD tableB81030.</summary>
    public static NativeBattleVector PartyPosition(int slot, int count)
    {
        if (count is < 1 or > 3 || slot < 0 || slot >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return count switch
        {
            1 => new(0, 0, 1800),
            2 => new(slot == 0 ? (short)-1000 : (short)1000, 0, 1800),
            _ => new(checked((short)((slot - 1) * 1200)), 0, slot == 1 ? (short)1500 : (short)2100)
        };
    }
}
