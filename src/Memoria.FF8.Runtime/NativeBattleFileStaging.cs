using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Observed bytes in the shared field/battle resource memory. Unobserved tails stay unknown.</summary>
public sealed partial record NativeBattleFileStaging([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    public const uint Address = 0x199B018;
    // Prior snapshots encode offsets from199B018. Rebasing changes coordinates while preserving every absolute byte.
    public uint BaseAddress { get; init; } = Address;
    public static NativeBattleFileStaging Unknown() => new([]);

    public void Validate()
    {
        if (BaseAddress > Address)
        {
            throw new InvalidDataException("Native shared staging origin cannot exclude its original battle resource address.");
        }
        NativeKnownByteRanges.Validate(KnownRanges, checked((int)Math.Min(int.MaxValue, (ulong)uint.MaxValue - BaseAddress + 1)));
        ValidateResourcePointers();
    }

    public NativeBattleFileStaging Rebase(uint lowerAddress)
    {
        Validate();
        if (lowerAddress > BaseAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(lowerAddress));
        }
        if (lowerAddress == BaseAddress)
        {
            return this;
        }
        int shift = checked((int)(BaseAddress - lowerAddress));
        return this with { KnownRanges = KnownRanges.Select(range => range with { Offset = checked(range.Offset + shift) }).ToImmutableArray(), BaseAddress = lowerAddress };
    }

    public byte[] Read(uint address, int length) => NativeKnownByteRanges.Read(KnownRanges, Offset(address), length);

    public NativeBattleFileStaging Write(uint address, ReadOnlySpan<byte> bytes)
    {
        NativeBattleFileStaging current = address < BaseAddress ? Rebase(address) : this;
        return (current with { KnownRanges = NativeKnownByteRanges.Write(current.KnownRanges, current.Offset(address), bytes) }).InvalidateResourcePointers(address, bytes.Length);
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

    private int Offset(uint address)
    {
        if (address < BaseAddress || address - BaseAddress > int.MaxValue)
        {
            throw new NotSupportedException("Native staging access requires an address within its explicit shared memory origin.");
        }
        return checked((int)(address - BaseAddress));
    }
}
