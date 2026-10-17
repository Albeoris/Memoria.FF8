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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleQuezacotlRibbonBank? SecondaryBank { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattleQuezacotlRibbonBank PrimaryBank => new(0x12AE7F0, 0x12AE840, Pairs80, ControlTriangles);

    public void Validate()
    {
        if (Pairs80.IsDefault || Pairs80.Length != 80 || ControlTriangles.IsDefault || SqrtWords256.IsDefault || SqrtWords256.Length != 256 || DistanceBytes16384.IsDefault || DistanceBytes16384.Length != 16384 || ParticleScaleWords4096 is { } particleScale && (particleScale.IsDefault || particleScale.Length != 4096))
        {
            throw new InvalidDataException("Native6C8850 requires its complete original control and lookup resources.");
        }
        PrimaryBank.Validate();
        SecondaryBank?.Validate();
        if (SecondaryBank is { } secondary && (secondary.PairAddress != 0x12AF800 || secondary.ControlAddress != 0x12AF828 || secondary.Pairs.Length != 40))
        {
            throw new InvalidDataException("Native Quezacotl secondary ribbon metadata must identify its original five-pair directory.");
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

    public NativeBattleQuezacotlRibbonBank ResolveBank(uint pair, uint controls)
    {
        if (PrimaryBank.Contains(pair, controls))
        {
            return PrimaryBank;
        }
        if (pair is >= 0x12AF800 and < 0x12AF828 && controls == 0x12AF828 && (pair - 0x12AF800) % 8 == 0)
        {
            return SecondaryBank ?? throw new NotSupportedException("Native6C8850 requires its authenticated5-pair12AF800/12AF828 bank; explicitly migrate the named standalone metadata.");
        }
        throw new InvalidDataException("Native6C8850 task does not identify an authenticated addressed control bank.");
    }
}

public sealed record NativeBattleQuezacotlRibbonBank(uint PairAddress, uint ControlAddress, ImmutableArray<byte> Pairs, ImmutableArray<byte> ControlTriangles)
{
    public bool Contains(uint pair, uint controls) => controls == ControlAddress && pair >= PairAddress && pair - PairAddress < Pairs.Length && (pair - PairAddress) % 8 == 0;

    public void Validate()
    {
        if (Pairs.IsDefaultOrEmpty || Pairs.Length % 8 != 0 || ControlTriangles.IsDefault)
        {
            throw new InvalidDataException("Native6C8850 requires complete addressed endpoint pairs and control triangles.");
        }
        int maximum = 0;
        for (int offset = 0; offset < Pairs.Length; offset += 4)
        {
            short start = BinaryPrimitives.ReadInt16LittleEndian(Pairs.AsSpan().Slice(offset));
            short count = BinaryPrimitives.ReadInt16LittleEndian(Pairs.AsSpan().Slice(offset + 2));
            if (start < 0 || count < 1)
            {
                throw new InvalidDataException("Native6C8850 requires nonnegative control starts and nonempty chains.");
            }
            maximum = Math.Max(maximum, checked(start + count));
        }
        if (ControlTriangles.Length != checked(maximum * 24))
        {
            throw new InvalidDataException("Native6C8850 controls disagree with the addressed pair extent.");
        }
    }
}
