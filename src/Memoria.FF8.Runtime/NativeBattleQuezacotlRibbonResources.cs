using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleQuezacotlResources
{
    public NativeBattleQuezacotlRibbonResources? Ribbon { get; init; }
}

/// <summary>Original12AE7F0 pairs,12AE840 control triangles and the overlapping12B05F0/12B4570 lookup tables.</summary>
public sealed record NativeBattleQuezacotlRibbonResources(ImmutableArray<byte> Pairs80, ImmutableArray<byte> ControlTriangles, ImmutableArray<ushort> SqrtWords256, ImmutableArray<byte> DistanceBytes16384)
{
    public ImmutableArray<short>? ParticleScaleWords4096 { get; init; }

    public void Validate()
    {
        if (Pairs80.IsDefault || Pairs80.Length != 80 || ControlTriangles.IsDefault || SqrtWords256.IsDefault || SqrtWords256.Length != 256 || DistanceBytes16384.IsDefault || DistanceBytes16384.Length != 16384 || ParticleScaleWords4096 is { } particleScale && (particleScale.IsDefault || particleScale.Length != 4096))
        {
            throw new InvalidDataException("Native6C8850 requires its complete original control and lookup resources.");
        }
        int maximum = 0;
        for (int index = 0; index < 20; index++)
        {
            int offset = index * 4;
            short start = BinaryPrimitives.ReadInt16LittleEndian(Pairs80.AsSpan().Slice(offset));
            short count = BinaryPrimitives.ReadInt16LittleEndian(Pairs80.AsSpan().Slice(offset + 2));
            if (start < 0 || count < 1)
            {
                throw new InvalidDataException("Native6C8850 requires observed nonnegative control starts and nonempty chains.");
            }
            maximum = Math.Max(maximum, checked(start + count));
        }
        if (ControlTriangles.Length != checked(maximum * 24))
        {
            throw new InvalidDataException("Native6C8850 control source does not match the original pair directory extent.");
        }
        //The final128 distance bytes and the first128 square-root bytes are the same original executable addresses.
        for (int index = 0; index < 64; index++)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(DistanceBytes16384.AsSpan().Slice(0x3F80 + index * 2)) != SqrtWords256[index])
            {
                throw new InvalidDataException("Native6C8850 overlapping lookup aliases disagree.");
            }
        }
    }
}
