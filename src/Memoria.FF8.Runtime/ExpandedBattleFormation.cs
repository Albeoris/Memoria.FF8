namespace Memoria.FF8.Runtime;

/// <summary>Placement for battle rosters without an authored position table.</summary>
public static class ExpandedBattleFormation
{
    public const int MaximumSideSize = NativeCharacterVitals.ExtendedRecordCount;

    public static NativeBattleVector Position(int slot, int count, bool isAlly)
    {
        if (count is < 1 or > MaximumSideSize)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        if (slot < 0 || slot >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        // Rear and front positions occupy the gaps between the four middle positions.
        int rowSlot = slot < 4 ? slot : slot == 10 ? 0 : (slot - 4) % 3;
        int rowCapacity = slot < 4 ? Math.Min(4, count) : slot == 10 ? 1 : 3;
        int x = (2 * rowSlot - rowCapacity + 1) * 600;
        int distance = slot switch
        {
            < 4 => 1800,
            < 7 => 2700,
            < 10 => 900,
            _ => 3600
        };
        return new(checked((short)x), 0, checked((short)(isAlly ? distance : -distance)));
    }
}
