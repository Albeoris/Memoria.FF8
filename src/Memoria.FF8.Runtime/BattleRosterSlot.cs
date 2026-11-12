namespace Memoria.FF8.Runtime;

internal enum BattleSide
{
    Allies,
    Enemies
}

internal readonly record struct BattleRosterSlot
{
    private BattleRosterSlot(BattleSide side, int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Side = side;
        Position = position;
    }

    public BattleSide Side { get; }
    public int Position { get; }

    public static BattleRosterSlot Ally(int position) => new(BattleSide.Allies, position);
    public static BattleRosterSlot Enemy(int position) => new(BattleSide.Enemies, position);
}

internal static class NativeBattleSlots
{
    public const int AllyCount = 3;
    public const int EnemyCount = 4;
    public const int Count = AllyCount + EnemyCount;
    public const int Unassigned = -1;

    public static bool Contains(int index) => (uint)index < Count;

    public static BattleRosterSlot Decode(int index)
    {
        return index switch
        {
            >= 0 and < AllyCount => BattleRosterSlot.Ally(index),
            >= AllyCount and < Count => BattleRosterSlot.Enemy(index - AllyCount),
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Outside the native battle actor bank.")
        };
    }

    public static int? TryEncode(BattleRosterSlot slot)
    {
        return slot.Side switch
        {
            BattleSide.Allies when slot.Position < AllyCount => slot.Position,
            BattleSide.Enemies when slot.Position < EnemyCount => AllyCount + slot.Position,
            _ => null
        };
    }

    public static int Encode(BattleRosterSlot? slot)
    {
        return slot is null ? Unassigned : TryEncode(slot.Value) ?? throw new NotSupportedException($"Battle slot {slot} has no native actor index.");
    }
}
