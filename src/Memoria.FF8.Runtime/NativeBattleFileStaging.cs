using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Observed bytes at B6D08C's shared199B018 resource staging address. Unobserved tails stay unknown.</summary>
public sealed record NativeBattleFileStaging([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    public const uint Address = 0x199B018;
    public static NativeBattleFileStaging Unknown() => new([]);

    public void Validate() => NativeKnownByteRanges.Validate(KnownRanges);

    public byte[] Read(uint address, int length) => NativeKnownByteRanges.Read(KnownRanges, Offset(address), length);

    public NativeBattleFileStaging Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return new(NativeKnownByteRanges.Write(KnownRanges, Offset(address), bytes));
    }

    public ImmutableArray<byte> ReadDat(uint address)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(address, 4));
        if (count is < 1 or > 16)
        {
            throw new NotSupportedException("Native staging DAT requires its reached bounded section directory.");
        }
        int length = BinaryPrimitives.ReadInt32LittleEndian(Read(checked(address + 4u + (uint)count * 4), 4));
        if (length < 8 + count * 4)
        {
            throw new InvalidDataException("Native staging DAT declares an extent shorter than its directory.");
        }
        ImmutableArray<byte> bytes = Read(address, length).ToImmutableArray();
        _ = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        return bytes;
    }

    private static int Offset(uint address)
    {
        if (address < Address || address - Address > int.MaxValue)
        {
            throw new NotSupportedException("Native staging access requires an owned original address at or after199B018.");
        }
        return checked((int)(address - Address));
    }
}
