using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Local model rows selected for graphics presentation.</summary>
public readonly record struct NativeBattleGraphicsActorSet
{
    private const uint NativeMask = (1u << NativeBattleGraphicsModelAddress.NativeRowCount) - 1;
    private const uint GraphicsMask = (1u << NativeBattleGraphicsModelAddress.MaximumRowCount) - 1;

    [JsonConstructor]
    public NativeBattleGraphicsActorSet(uint bits)
    {
        if ((bits & ~GraphicsMask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bits));
        }
        Bits = bits;
    }

    public uint Bits { get; }
    [JsonIgnore]
    public int Count => BitOperations.PopCount(Bits);
    [JsonIgnore]
    public bool IsEmpty => Bits == 0;
    [JsonIgnore]
    public int HighestRow => IsEmpty ? throw new InvalidOperationException("An empty set has no graphics row.") : BitOperations.Log2(Bits);

    /// <summary>Projects only the seven original physical bits for native serialization.</summary>
    [JsonIgnore]
    public ushort NativePhysicalBits => (ushort)(Bits & NativeMask);

    public static NativeBattleGraphicsActorSet FromNative(ushort mask)
    {
        if ((mask & ~NativeMask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mask));
        }
        return new(mask);
    }

    public bool Contains(int row) => (uint)row < NativeBattleGraphicsModelAddress.MaximumRowCount && (Bits & (1u << row)) != 0;
    public bool IsSingle(int row) => Count == 1 && Contains(row);
    public NativeBattleGraphicsActorSet Add(int row) => new(Bits | RowBit(row));
    public NativeBattleGraphicsActorSet Remove(int row) => new(Bits & ~RowBit(row));

    public IEnumerable<int> EnumerateRows()
    {
        uint remaining = Bits;
        while (remaining != 0)
        {
            int row = BitOperations.TrailingZeroCount(remaining);
            yield return row;
            remaining &= remaining - 1;
        }
    }

    public void ValidateCapacity(int actorCount)
    {
        if (actorCount is < 1 or > NativeBattleGraphicsModelAddress.MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(actorCount));
        }
        if ((Bits >> actorCount) != 0)
        {
            throw new InvalidDataException("The graphics actor set contains a row outside the current bank.");
        }
    }

    private static uint RowBit(int row)
    {
        if ((uint)row >= NativeBattleGraphicsModelAddress.MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        return 1u << row;
    }
}
