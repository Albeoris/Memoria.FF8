using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained physical bytes and explicit loader-zero provenance in shared field/battle memory.</summary>
public sealed partial record NativeBattleFileStaging([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges) : IJsonOnSerializing
{
    public const uint Address = 0x199B018;
    // Prior snapshots encode offsets from199B018. Rebasing changes coordinates while preserving every absolute byte.
    public uint BaseAddress { get; init; } = Address;
    [JsonIgnore]
    internal NativeBattleFileStagingTransaction? Transaction { get; init; }
    public static NativeBattleFileStaging Unknown() => new([]);

    void IJsonOnSerializing.OnSerializing()
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException("Native staging must publish its private transaction before serialization.");
        }
    }

    public void Validate()
    {
        if (Transaction is { } transaction)
        {
            transaction.RequireActive();
            return;
        }
        if (BaseAddress > Address)
        {
            throw new InvalidDataException("Native shared staging origin cannot exclude its original battle resource address.");
        }
        NativeKnownByteRanges.Validate(KnownRanges, checked((int)Math.Min(int.MaxValue, (ulong)uint.MaxValue - BaseAddress + 1)));
        ValidateLoaderZeroRanges();
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
        if (Transaction is not null)
        {
            return this with { BaseAddress = lowerAddress };
        }
        int shift = checked((int)(BaseAddress - lowerAddress));
        return this with { KnownRanges = KnownRanges.Select(range => range with { Offset = checked(range.Offset + shift) }).ToImmutableArray(), BaseAddress = lowerAddress };
    }

    public byte[] Read(uint address, int length)
    {
        int offset = Offset(address);
        if (length < 0 || offset > int.MaxValue - length)
        {
            throw new InvalidDataException("Native retained memory access exceeds its offset representation.");
        }
        if (Transaction is { } transaction)
        {
            return transaction.Read(address, length);
        }
        if (!IntersectsLoaderZero(address, length))
        {
            return NativeKnownByteRanges.Read(KnownRanges, offset, length);
        }
        if (!HasKnownStorage(address, length))
        {
            throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
        }
        (byte[] data, _) = ReadSnapshotStorage(address, length);
        return data;
    }

    public NativeBattleFileStaging Write(uint address, ReadOnlySpan<byte> bytes)
    {
        NativeBattleFileStaging current = address < BaseAddress ? Rebase(address) : this;
        if (current.Transaction is { } transaction)
        {
            transaction.Write(address, bytes);
            return current.InvalidateResourcePointers(address, bytes.Length);
        }
        // Bound copy-on-write storage so scalar scratch stores do not clone adjacent resource images.
        return (current with { KnownRanges = NativeKnownByteRanges.Write(current.KnownRanges, current.Offset(address), bytes, blockLength: 4096) }).InvalidateResourcePointers(address, bytes.Length);
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
        ImmutableArray<byte> bytes = NativeBattleTelemetry.Immutable(1042, Read(address, length));
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
