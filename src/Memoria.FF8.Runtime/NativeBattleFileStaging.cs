using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One retained staging authority; detached JSON records are explicit snapshot boundaries.</summary>
[JsonConverter(typeof(NativeBattleFileStaging.NativeBattleFileStagingJsonConverter))]
public sealed partial record NativeBattleFileStaging : INativeBattleStorageNode
{
    public const uint Address = 0x199B018;
    private NativeBattleFileStagingWorkingOwner? owner;
    private readonly bool snapshot;
    private ImmutableArray<NativeBattleEffectMemoryRegion> knownRanges;
    private uint baseAddress = Address;
    private ImmutableArray<NativeBattleLoaderZeroRegion> loaderZeroRanges = [];
    private ImmutableDictionary<uint, NativeBattleResourcePointer?> resourcePointers = ImmutableDictionary<uint, NativeBattleResourcePointer?>.Empty;
    private ImmutableArray<NativeBattleEffectMemoryRegion> retainedUnknownRanges = [];

    public NativeBattleFileStaging(ImmutableArray<NativeBattleEffectMemoryRegion> knownRanges)
    {
        this.knownRanges = knownRanges;
    }

    private NativeBattleFileStaging(NativeBattleFileStagingData data, bool snapshot)
    {
        this.snapshot = snapshot;
        knownRanges = data.KnownRanges;
        baseAddress = data.BaseAddress;
        loaderZeroRanges = data.LoaderZeroRanges;
        resourcePointers = data.ResourcePointers;
        retainedUnknownRanges = data.RetainedUnknownRanges;
        ValidateData(data);
    }

    private NativeBattleFileStaging(NativeBattleFileStagingWorkingOwner owner)
    {
        this.owner = owner;
    }

    [JsonRequired]
    public ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges
    {
        get => snapshot ? knownRanges : throw new InvalidOperationException("Native staging ranges require an explicit detached snapshot.");
        init
        {
            RequireInitializer();
            knownRanges = value;
        }
    }

    public uint BaseAddress
    {
        get => Transaction is { } transaction ? transaction.BaseAddress : owner?.BaseAddress ?? baseAddress;
        init
        {
            RequireInitializer();
            baseAddress = value;
        }
    }

    public ImmutableArray<NativeBattleEffectMemoryRegion> RetainedUnknownRanges
    {
        get => snapshot ? retainedUnknownRanges : throw new InvalidOperationException("Native retained unknown ranges require an explicit detached snapshot.");
        init
        {
            RequireInitializer();
            retainedUnknownRanges = value;
        }
    }

    [JsonIgnore]
    internal NativeBattleFileStagingTransaction? Transaction { get; init; }
    internal NativeBattleFileStagingWorkingOwner WorkingOwner => RequireOwner();
    internal object OwnerIdentity => RequireOwner();
    internal int AllocatedPageCount => RequireOwner().PageCount;
    internal long StorageVersion => RequireOwner().Version;
    public ulong KnownExtentEnd => Transaction is { } transaction ? transaction.KnownExtentEnd : snapshot ? FrozenKnownExtentEnd() : RequireOwner().KnownExtentEnd;
    bool INativeBattleStorageNode.IsSnapshot => snapshot;
    public static NativeBattleFileStaging Unknown() => new([]);

    public void Validate()
    {
        if (Transaction is { } transaction)
        {
            transaction.RequireActive();
            return;
        }
        if (owner is null)
        {
            ValidateData(ImportData());
        }
    }

    public NativeBattleFileStaging Rebase(uint lowerAddress)
    {
        RequireWritable();
        if (lowerAddress > BaseAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(lowerAddress));
        }
        if (Transaction is { } transaction)
        {
            transaction.Rebase(lowerAddress);
        }
        else
        {
            RequireOwner().Rebase(lowerAddress);
        }
        return this;
    }

    public byte[] Read(uint address, int length)
    {
        RequireOffsetRange(address, length);
        if (Transaction is { } transaction)
        {
            return transaction.Read(address, length);
        }
        if (!IsKnown(address, length))
        {
            throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
        }
        byte[] data = new byte[length];
        Span<bool> known = stackalloc bool[Math.Min(length, NativeBattleFileStagingWorkingOwner.PageLength)];
        for (int offset = 0; offset < length;)
        {
            int count = Math.Min(length - offset, known.Length);
            ReadWithKnownness(checked(address + (uint)offset), data.AsSpan(offset, count), known[..count]);
            offset += count;
        }
        return data;
    }

    public void ReadInto(uint address, Span<byte> destination)
    {
        RequireOffsetRange(address, destination.Length);
        if (!IsKnown(address, destination.Length))
        {
            throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
        }
        Span<bool> known = stackalloc bool[Math.Min(destination.Length, NativeBattleFileStagingWorkingOwner.PageLength)];
        for (int offset = 0; offset < destination.Length;)
        {
            int count = Math.Min(destination.Length - offset, known.Length);
            ReadWithKnownness(checked(address + (uint)offset), destination.Slice(offset, count), known[..count]);
            offset += count;
        }
    }

    public byte ReadByte(uint address)
    {
        Span<byte> data = stackalloc byte[1];
        ReadInto(address, data);
        return data[0];
    }

    public ushort ReadUInt16(uint address)
    {
        Span<byte> data = stackalloc byte[2];
        ReadInto(address, data);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    public short ReadInt16(uint address) => unchecked((short)ReadUInt16(address));

    public uint ReadUInt32(uint address)
    {
        Span<byte> data = stackalloc byte[4];
        ReadInto(address, data);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    public int ReadInt32(uint address) => unchecked((int)ReadUInt32(address));

    public NativeBattleFileStaging Write(uint address, ReadOnlySpan<byte> bytes)
    {
        RequireWritableRange(address, bytes.Length);
        if (address < BaseAddress)
        {
            Rebase(address);
        }
        if (bytes.IsEmpty)
        {
            return this;
        }
        if (Transaction is { } transaction)
        {
            transaction.Write(address, bytes);
        }
        else
        {
            RequireOwner().Write(address, bytes);
        }
        return this;
    }

    public NativeBattleFileStaging WriteByte(uint address, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        return Write(address, bytes);
    }

    public NativeBattleFileStaging WriteWithKnownness(uint address, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        RequireWritableRange(address, bytes.Length);
        if (bytes.Length != known.Length)
        {
            throw new ArgumentException("Native staging raw stores require their complete knownness mask.", nameof(known));
        }
        if (Transaction is null)
        {
            using NativeBattleFileStagingTransaction transaction = new(this);
            transaction.View.WriteWithKnownness(address, bytes, known);
            return transaction.Freeze(transaction.View);
        }
        if (address < BaseAddress)
        {
            Rebase(address);
        }
        Transaction.WriteRaw(address, bytes, known);
        return this;
    }

    public NativeBattleFileStaging WriteUInt16(uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    public NativeBattleFileStaging WriteUInt32(uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    public ImmutableArray<byte> ReadDat(uint address)
    {
        int count = ReadInt32(address);
        if (count is < 1 or > 16)
        {
            throw new NotSupportedException("Native staging DAT requires its reached bounded section directory.");
        }
        int length = ReadInt32(checked(address + 4u + (uint)count * 4));
        if (length < 8 + count * 4)
        {
            throw new InvalidDataException("Native staging DAT declares an extent shorter than its directory.");
        }
        ImmutableArray<byte> bytes = NativeBattleTelemetry.Immutable(1042, Read(address, length));
        _ = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        return bytes;
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException("Native staging snapshot requires its published transaction boundary.");
        }
        NativeBattleFileStagingWorkingOwner current = RequireOwner();
        return context.GetOrAddOwner(current, () => new NativeBattleFileStaging(current.Capture(), true));
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        if (!snapshot)
        {
            throw new InvalidOperationException("Native staging restore requires detached snapshot storage.");
        }
        ValidateData(ImportData());
        NativeBattleFileStagingWorkingOwner restored = context.GetOrAddOwner(this, () => new NativeBattleFileStagingWorkingOwner(ImportData()));
        return new NativeBattleFileStaging(restored);
    }

    private bool IsKnown(uint address, int length)
    {
        if (Transaction is { } transaction)
        {
            return transaction.IsKnown(address, length);
        }
        if (snapshot)
        {
            return FrozenKnown(address, length);
        }
        return RequireOwner().IsKnown(address, length);
    }

    private NativeBattleFileStagingWorkingOwner RequireOwner()
    {
        RequireWritable();
        if (owner is null)
        {
            ValidateData(ImportData());
            owner = new(ImportData());
        }
        return owner;
    }

    private void RequireWritable()
    {
        Transaction?.RequireActive();
        if (snapshot)
        {
            throw new InvalidOperationException("Native staging snapshots cannot be mutated; restore owned working storage first.");
        }
    }

    private void RequireInitializer()
    {
        if (owner is not null || Transaction is not null)
        {
            throw new InvalidOperationException("Native staging working metadata requires explicit owned mutation.");
        }
    }

    private void RequireOffsetRange(uint address, int length)
    {
        Validate();
        RequirePhysicalRange(address, length);
        if (address < BaseAddress || (ulong)address - BaseAddress + (uint)length > int.MaxValue)
        {
            throw new NotSupportedException("Native staging access requires an address within its explicit shared memory origin.");
        }
    }

    private void RequireWritableRange(uint address, int length)
    {
        RequireWritable();
        RequirePhysicalRange(address, length);
        uint origin = Math.Min(BaseAddress, address);
        if ((ulong)address - origin + (uint)length > int.MaxValue)
        {
            throw new InvalidDataException("Native retained memory access exceeds its offset representation.");
        }
    }

    internal static void RequirePhysicalRange(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new InvalidDataException("Native staging access leaves its physical address extent.");
        }
    }

    private NativeBattleFileStagingData ImportData() => new(baseAddress, knownRanges, loaderZeroRanges, resourcePointers, retainedUnknownRanges);
    private ulong FrozenKnownExtentEnd()
    {
        Validate();
        return knownRanges.IsEmpty ? baseAddress : (ulong)baseAddress + (uint)knownRanges[^1].Offset + (uint)knownRanges[^1].Data.Length;
    }

    public sealed class NativeBattleFileStagingJsonConverter : JsonConverter<NativeBattleFileStaging>
    {
        private sealed record Payload([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
        {
            public uint BaseAddress { get; init; } = Address;
            public ImmutableArray<NativeBattleLoaderZeroRegion> LoaderZeroRanges { get; init; } = [];
            public ImmutableDictionary<uint, NativeBattleResourcePointer?> ResourcePointers { get; init; } = ImmutableDictionary<uint, NativeBattleResourcePointer?>.Empty;
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public ImmutableArray<NativeBattleEffectMemoryRegion>? RetainedUnknownRanges { get; init; }
        }

        public override NativeBattleFileStaging Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Payload payload = JsonSerializer.Deserialize<Payload>(ref reader, options) ?? throw new JsonException("Native staging snapshot is null.");
            return new(new(payload.BaseAddress, payload.KnownRanges, payload.LoaderZeroRanges, payload.ResourcePointers, payload.RetainedUnknownRanges ?? []), true);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleFileStaging value, JsonSerializerOptions options)
        {
            if (!value.snapshot || value.Transaction is not null)
            {
                throw new InvalidOperationException("Native staging working storage must be captured before serialization.");
            }
            NativeBattleFileStagingData data = value.ImportData();
            ValidateData(data);
            JsonSerializer.Serialize(writer, new Payload(data.KnownRanges) { BaseAddress = data.BaseAddress, LoaderZeroRanges = data.LoaderZeroRanges, ResourcePointers = data.ResourcePointers, RetainedUnknownRanges = data.RetainedUnknownRanges.IsEmpty ? null : data.RetainedUnknownRanges }, options);
        }
    }
}
