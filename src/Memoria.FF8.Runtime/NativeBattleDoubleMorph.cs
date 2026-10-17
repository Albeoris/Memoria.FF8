using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The current163FFD0 mesh;8D57C0 replaces XYZ and preserves every other original byte.</summary>
public sealed record NativeBattleDoubleMorph([property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        if (Bytes.IsDefault || Bytes.Length < 8)
        {
            throw new InvalidDataException("Native Double morph lacks its current163FFD0 mesh bytes.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(4));
        if (count < 0 || count > 4096 || BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan()) != 8 + count * 8 || 8L + count * 8L > Bytes.Length)
        {
            throw new InvalidDataException("Native Double morph vertex directory exceeds its current destination.");
        }
    }

    public NativeBattleDoubleMorph Morph8D57C0(ImmutableArray<byte> first, ImmutableArray<byte> second, short ratio)
    {
        Validate();
        if (first.IsDefault || first.Length < 8)
        {
            throw new InvalidDataException("Native8D57C0 requires its original first shape count.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(first.AsSpan().Slice(4));
        return new(NativeBattleGfMorphGeometry.Morph45E9D0(first, 8, second, 8, count, ratio, Bytes, 8));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoubleMorph? DoubleMorph { get; init; }
}
