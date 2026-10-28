using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The bounded temporary reservation shared by services within one native callback.</summary>
[JsonConverter(typeof(NativeBattleWaterTemporaryFrame.Converter))]
public sealed record NativeBattleWaterTemporaryFrame : INativeBattleStorageNode, INativeBattleArenaStorageNode, INativeBattleReadWindowSource
{
    private const int Capacity = 0x180;
    private readonly NativeBattleEffectArena storage;
    private readonly bool snapshot;
    private readonly ImmutableArray<byte> frozenBytes;
    private readonly ImmutableArray<bool> frozenKnown;
    private readonly PrivateWrite? privateWrite;

    public NativeBattleWaterTemporaryFrame(uint Address, ImmutableArray<byte> Bytes, ImmutableArray<bool> Known)
    {
        RequireShape(Bytes, Known);
        this.Address = Address;
        storage = NativeBattleEffectArena.CreateUnknown(Capacity);
        storage.Working.WriteWithKnownness(0, Bytes.AsSpan(), Known.AsSpan());
    }

    private NativeBattleWaterTemporaryFrame(uint address, NativeBattleEffectArena storage)
    {
        Address = address;
        this.storage = storage;
    }

    private NativeBattleWaterTemporaryFrame(uint address, NativeBattleEffectArena storage, PrivateWrite privateWrite) : this(address, storage)
    {
        this.privateWrite = privateWrite;
    }

    private NativeBattleWaterTemporaryFrame(uint address, NativeBattleEffectArena storage, ImmutableArray<byte> bytes, ImmutableArray<bool> known) : this(address, storage)
    {
        snapshot = true;
        frozenBytes = bytes;
        frozenKnown = known;
    }

    public uint Address { get; init; }
    [JsonIgnore] public int Length => Capacity;
    [JsonIgnore] public bool IsSnapshot => snapshot;
    [JsonIgnore] public ImmutableArray<byte> Bytes => snapshot ? frozenBytes : throw new InvalidOperationException("Native temporary bytes require an explicit detached snapshot.");
    [JsonIgnore] public ImmutableArray<bool> Known => snapshot ? frozenKnown : throw new InvalidOperationException("Native temporary knownness requires an explicit detached snapshot.");
    bool INativeBattleStorageNode.IsSnapshot => snapshot;
    NativeBattleEffectArena? INativeBattleArenaStorageNode.SnapshotArena => snapshot ? storage : throw new InvalidOperationException("Temporary alias inspection requires a frozen snapshot.");

    public void Validate()
    {
        privateWrite?.RequireActive();
        if (storage.LogicalLength != Capacity)
        {
            throw new InvalidDataException("Native temporary access requires its actual bounded reservation.");
        }
        storage.Validate();
    }

    private void RequireRange(int offset, int length)
    {
        privateWrite?.RequireActive();
        if (offset < 0 || length < 0 || offset > Capacity - length)
        {
            throw new InvalidDataException("Native temporary access requires its actual bounded reservation.");
        }
    }

    public byte ReadByte(int offset)
    {
        Span<byte> bytes = stackalloc byte[1];
        ReadInto(offset, bytes);
        return bytes[0];
    }

    public ushort ReadUInt16(int offset)
    {
        Span<byte> bytes = stackalloc byte[2];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public uint ReadUInt32(int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public void ReadInto(int offset, Span<byte> destination)
    {
        RequireRange(offset, destination.Length);
        if (privateWrite is null)
        {
            storage.ReadInto(offset, destination);
            return;
        }
        Span<byte> bytes = stackalloc byte[destination.Length];
        Span<bool> known = stackalloc bool[destination.Length];
        ReadWithKnownness(offset, bytes, known);
        if (known.Contains(false))
        {
            throw new NotSupportedException("Native temporary read intersects unresolved physical bytes.");
        }
        bytes.CopyTo(destination);
    }

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        RequireRange(offset, destination.Length);
        if (privateWrite is null)
        {
            storage.ReadWithKnownness(offset, destination, known);
        }
        else
        {
            privateWrite.ReadWithKnownness(offset, destination, known);
        }
    }

    public bool IsKnown(int offset, int length)
    {
        RequireRange(offset, length);
        Span<byte> bytes = stackalloc byte[length];
        Span<bool> known = stackalloc bool[length];
        ReadWithKnownness(offset, bytes, known);
        return !known.Contains(false);
    }

    public NativeBattleWaterTemporaryFrame Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        storage.Write(offset, bytes);
        return this;
    }

    public NativeBattleWaterTemporaryFrame WriteWithKnownness(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        RequireRange(offset, bytes.Length);
        storage.RequireWritable();
        if (storage.Transaction is { } transaction)
        {
            transaction.WriteWithKnownness(offset, bytes, known);
        }
        else
        {
            storage.Working.WriteWithKnownness(storage.WorkingOffset + offset, bytes, known);
        }
        return this;
    }

    public NativeBattleWaterTemporaryFrame WriteByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        return Write(offset, bytes);
    }

    public NativeBattleWaterTemporaryFrame WriteUInt16(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Write(offset, bytes);
    }

    public NativeBattleWaterTemporaryFrame WriteUInt32(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Write(offset, bytes);
    }

    public NativeBattleWaterTemporaryFrame MarkUnknown(int offset, int length)
    {
        RequireRange(offset, length);
        storage.MarkUnknown(offset, length);
        return this;
    }

    public NativeBattleWaterTemporaryFrame SetKnownness(int offset, ReadOnlySpan<bool> known)
    {
        RequireRange(offset, known.Length);
        Span<byte> bytes = stackalloc byte[known.Length];
        Span<bool> previous = stackalloc bool[known.Length];
        ReadWithKnownness(offset, bytes, previous);
        return WriteWithKnownness(offset, bytes, known);
    }

    public NativeBattleWaterTemporaryFrame Copy(int sourceOffset, int destinationOffset, int length)
    {
        RequireRange(sourceOffset, length);
        RequireRange(destinationOffset, length);
        Span<byte> bytes = stackalloc byte[length];
        Span<bool> known = stackalloc bool[length];
        ReadWithKnownness(sourceOffset, bytes, known);
        return WriteWithKnownness(destinationOffset, bytes, known);
    }

    internal PrivateWrite BeginPrivateWrite() => new(this);

    internal sealed class PrivateWrite : IDisposable
    {
        private readonly NativeBattleWaterTemporaryFrame source;
        private readonly NativeBattleEffectArenaTransaction transaction;
        private readonly NativeBattleReadWindow baseline;
        private bool published;
        private bool disposed;

        internal PrivateWrite(NativeBattleWaterTemporaryFrame source)
        {
            source.Validate();
            this.source = source;
            baseline = source.storage.Working.OpenReadWindow(source, source.storage.WorkingOffset, Capacity, Capacity);
            try
            {
                transaction = NativeBattleEffectArenaTransaction.CreatePrivateChild(source.storage);
                View = new(source.Address, transaction.View, this);
            }
            catch
            {
                baseline.Dispose();
                throw;
            }
        }

        internal NativeBattleWaterTemporaryFrame View { get; }

        internal NativeBattleWaterTemporaryFrame Publish()
        {
            RequireActive();
            Span<byte> bytes = stackalloc byte[Capacity];
            Span<bool> known = stackalloc bool[Capacity];
            Span<byte> current = stackalloc byte[Capacity];
            Span<bool> currentKnown = stackalloc bool[Capacity];
            View.ReadWithKnownness(0, bytes, known);
            source.ReadWithKnownness(0, current, currentKnown);
            for (int offset = 0; offset < Capacity;)
            {
                if (bytes[offset] == current[offset] && known[offset] == currentKnown[offset])
                {
                    offset++;
                    continue;
                }
                int first = offset++;
                while (offset < Capacity && (bytes[offset] != current[offset] || known[offset] != currentKnown[offset]))
                {
                    offset++;
                }
                transaction.WriteWithKnownness(first, bytes[first..offset], known[first..offset]);
            }
            transaction.Freeze();
            published = true;
            return source;
        }

        internal void RequireActive()
        {
            if (disposed || published)
            {
                throw new ObjectDisposedException(nameof(PrivateWrite));
            }
        }

        internal void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
        {
            baseline.ReadWithKnownness(offset, destination, known);
            for (int index = 0; index < destination.Length;)
            {
                if (!transaction.Overlay.HasPrivateByte(offset + index))
                {
                    index++;
                    continue;
                }
                int first = index++;
                while (index < destination.Length && transaction.Overlay.HasPrivateByte(offset + index))
                {
                    index++;
                }
                transaction.ReadWithKnownness(offset + first, destination[first..index], known[first..index]);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            try
            {
                transaction.Dispose();
            }
            finally
            {
                baseline.Dispose();
            }
        }
    }

    private INativeBattleReadWindowSource ReadWindowSource => storage.Transaction is { } transaction ? transaction.Overlay : storage.Working;
    long INativeBattleReadWindowSource.ReadWindowGeneration => ReadWindowSource.ReadWindowGeneration;
    void INativeBattleReadWindowSource.ValidateReadWindow(long generation) => ReadWindowSource.ValidateReadWindow(generation);
    void INativeBattleReadWindowSource.SeedReadWindow(NativeBattleReadWindowLease lease) => ReadWindowSource.SeedReadWindow(lease);
    bool INativeBattleReadWindowSource.ObservesLayer(INativeBattleReadWindowSource layer) => ReadWindowSource.ObservesLayer(layer);

    void INativeBattleReadWindowSource.ReadWindowByte(int absoluteOffset, out byte value, out bool known)
    {
        Span<byte> bytes = stackalloc byte[1];
        Span<bool> flags = stackalloc bool[1];
        ReadWithKnownness(absoluteOffset - storage.WorkingOffset, bytes, flags);
        value = bytes[0];
        known = flags[0];
    }

    void INativeBattleReadWindowSource.ReadWindowWithKnownness(int absoluteOffset, Span<byte> destination, Span<bool> known) => ReadWithKnownness(absoluteOffset - storage.WorkingOffset, destination, known);

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        Validate();
        NativeBattleEffectArena frozen;
        byte[] bytes;
        bool[] known;
        if (privateWrite is null)
        {
            frozen = context.Map(storage);
            (bytes, known) = frozen.ReadSnapshot(0, Capacity);
        }
        else
        {
            bytes = new byte[Capacity];
            known = new bool[Capacity];
            ReadWithKnownness(0, bytes, known);
            frozen = FrozenStorage(ImmutableCollectionsMarshal.AsImmutableArray(bytes), ImmutableCollectionsMarshal.AsImmutableArray(known));
        }
        return new NativeBattleWaterTemporaryFrame(Address, frozen, ImmutableCollectionsMarshal.AsImmutableArray(bytes), ImmutableCollectionsMarshal.AsImmutableArray(known));
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => new NativeBattleWaterTemporaryFrame(Address, context.Map(storage));

    private static void RequireShape(ImmutableArray<byte> bytes, ImmutableArray<bool> known)
    {
        if (bytes.IsDefault || known.IsDefault || bytes.Length != Capacity || known.Length != Capacity)
        {
            throw new InvalidDataException("Native temporary storage requires its complete bounded data and knownness.");
        }
    }

    private static NativeBattleEffectArena FrozenStorage(ImmutableArray<byte> bytes, ImmutableArray<bool> known)
    {
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        for (int offset = 0; offset < Capacity;)
        {
            if (known[offset])
            {
                offset++;
                continue;
            }
            int first = offset++;
            while (offset < Capacity && !known[offset])
            {
                offset++;
            }
            unknown.Add(new(first, offset - first));
        }
        return new([new(0, bytes)], unknown.ToImmutable(), null, Capacity);
    }

    private sealed record SnapshotData(uint Address, ImmutableArray<byte> Bytes, ImmutableArray<bool> Known, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeBattleStorageReference? StorageReference = null);

    public sealed class Converter : JsonConverter<NativeBattleWaterTemporaryFrame>
    {
        public override NativeBattleWaterTemporaryFrame Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            SnapshotData data = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new InvalidDataException("Native temporary snapshot is missing.");
            RequireShape(data.Bytes, data.Known);
            NativeBattleEffectArena frozen = FrozenStorage(data.Bytes, data.Known);
            if (data.StorageReference is not null)
            {
                frozen = frozen.WithStorageReference(data.StorageReference);
            }
            return new(data.Address, frozen, data.Bytes, data.Known);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleWaterTemporaryFrame value, JsonSerializerOptions options)
        {
            if (!value.snapshot)
            {
                throw new InvalidOperationException("Native temporary serialization requires an explicit detached snapshot.");
            }
            JsonSerializer.Serialize(writer, new SnapshotData(value.Address, value.frozenBytes, value.frozenKnown, value.storage.StorageReference), options);
        }
    }
}
