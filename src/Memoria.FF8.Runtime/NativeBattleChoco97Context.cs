using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97Context([property: JsonRequired] ImmutableArray<byte> Data)
{
    [JsonIgnore]
    internal ImmutableDictionary<BattleEnemyModelOwner, ushort> HiddenEnemies { get; init; } = ImmutableDictionary<BattleEnemyModelOwner, ushort>.Empty;

    public static NativeBattleChoco97Context Clear72A350() => new(new byte[0x54].ToImmutableArray());

    public static NativeBattleChoco97Context Initialize72A350(BattleModelPool<BattleEnemyModelOwner> enemies)
    {
        byte[] context = new byte[0x54];
        BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(4), 2048);
        NativeBattleVector[] positions = enemies.Entries.Where(entry => (entry.State.Flags0 & 2) != 0).Select(entry => (entry.State.Transform ?? throw new NotSupportedException("Native Choco centroid requires its active enemy positions.")).Position28).ToArray();
        if (positions.Length != 0)
        {
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(8), unchecked((short)((positions.Min(position => (int)position.X) + positions.Max(position => (int)position.X)) / 2)));
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(12), unchecked((short)((positions.Min(position => (int)position.Z) + positions.Max(position => (int)position.Z)) / 2)));
        }
        return new(context.ToImmutableArray());
    }

    public void Validate()
    {
        if (Data.IsDefault || Data.Length != 0x54)
        {
            throw new InvalidDataException("Native Choco requires its complete original context owner.");
        }
    }

    public ushort Word(int offset)
    {
        Validate();
        return BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan().Slice(offset, 2));
    }

    public NativeBattleChoco97Context WriteWord(int offset, ushort value)
    {
        Validate();
        byte[] data = Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
        return this with { Data = data.ToImmutableArray() };
    }
}
