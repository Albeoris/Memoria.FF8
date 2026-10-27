using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[JsonConverter(typeof(NativeBattleGfAnimationWorkspace.Converter))]
public sealed partial record NativeBattleGfAnimationWorkspace : INativeBattleStorageNode
{
    private readonly NativeBattleEffectArena arena;
    private readonly bool snapshot;
    private readonly ImmutableArray<byte> data;

    public NativeBattleGfAnimationWorkspace(ImmutableArray<byte> Data)
    {
        if (Data.IsDefault || Data.Length == 0 || Data.Length > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Native GF animation requires its bounded retained workspace bytes.");
        }
        arena = NativeBattleEffectArena.CreateUnknown(Data.Length).Write(0, Data.AsSpan());
    }

    private NativeBattleGfAnimationWorkspace(NativeBattleEffectArena arena, bool snapshot = false, ImmutableArray<byte> data = default)
    {
        this.arena = arena;
        this.snapshot = snapshot;
        this.data = data;
    }

    [JsonRequired]
    public ImmutableArray<byte> Data => snapshot ? data : throw new InvalidOperationException("Animation workspace bytes require an explicit detached snapshot.");
    [JsonIgnore]
    public int Length => arena.LogicalLength;
    bool INativeBattleStorageNode.IsSnapshot => snapshot;

    public static NativeBattleGfAnimationWorkspace Borrow(NativeBattleModulePoolTask task, int offset, int length) => new(task.BorrowPayloadArena(offset, length));

    public static NativeBattleGfAnimationWorkspace Borrow(NativeBattleEffectArena arena, int offset, int length)
    {
        arena.RequireWritable();
        if (offset < 0 || length <= 0 || offset > arena.LogicalLength - length)
        {
            throw new InvalidDataException("Native animation cache leaves its physical storage window.");
        }
        return new(NativeBattleEffectArena.BorrowWorking(arena.Working, checked(arena.WorkingOffset + offset), length));
    }

    public NativeBattleGfAnimationWorkspace WriteByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        arena.Write(offset, bytes);
        return this;
    }

    public NativeBattleGfAnimationWorkspace WriteWord(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        arena.Write(offset, bytes);
        return this;
    }

    public NativeBattleGfAnimationWorkspace WriteDword(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        arena.Write(offset, bytes);
        return this;
    }

    public byte[] Read(int offset, int length) => arena.Read(offset, length);
    public void ReadInto(int offset, Span<byte> destination) => arena.ReadInto(offset, destination);
    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known) => arena.ReadWithKnownness(offset, destination, known);

    public NativeBattleModulePoolTask CopyTo(NativeBattleModulePoolTask task, int offset, bool retainUnknown = true)
    {
        NativeBattleEffectArena destination = task.BorrowPayloadArena(offset, Length);
        if (SameStorage(destination))
        {
            return task;
        }
        Span<byte> bytes = stackalloc byte[256];
        Span<bool> known = stackalloc bool[256];
        using NativeBattleGfSourceReader? observed = !snapshot ? NativeBattleGfAnimationSource.Borrow(0, arena, 0, Length).OpenReader() : null;
        for (int first = 0; first < Length; first += bytes.Length)
        {
            int count = Math.Min(bytes.Length, Length - first);
            if (observed is not null)
            {
                observed.ReadWithKnownness(first, bytes[..count], known[..count]);
            }
            else
            {
                arena.ReadWithKnownness(first, bytes[..count], known[..count]);
            }
            if (retainUnknown)
            {
                task.WriteRetainedBytes(offset + first, bytes[..count]);
            }
            else
            {
                task.WriteBytes(offset + first, bytes[..count]);
            }
        }
        return task;
    }

    public NativeBattleEffectArena CopyTo(NativeBattleEffectArena destination, int offset)
    {
        destination.RequireWritable();
        if (offset < 0 || offset > destination.LogicalLength - Length)
        {
            throw new InvalidDataException("Native animation cache publication leaves its physical destination.");
        }
        NativeBattleEffectArena range = NativeBattleEffectArena.BorrowWorking(destination.Working, checked(destination.WorkingOffset + offset), Length);
        if (SameStorage(range))
        {
            return destination;
        }
        using NativeBattleEffectArenaWriter writer = new(range);
        CopyBytes(writer);
        writer.Freeze();
        return destination;
    }

    private bool SameStorage(NativeBattleEffectArena other) => !snapshot && !((INativeBattleStorageNode)other).IsSnapshot && ReferenceEquals(arena.Working, other.Working) && arena.WorkingOffset == other.WorkingOffset && Length == other.LogicalLength;

    private void CopyStateFrom(NativeBattleGfAnimationWorkspace source)
    {
        if (!SameStorage(source.arena))
        {
            using NativeBattleEffectArenaWriter writer = new(arena);
            source.CopyBytes(writer);
            writer.Freeze();
        }
    }

    private void CopyBytes(NativeBattleEffectArenaWriter writer)
    {
        Span<byte> bytes = stackalloc byte[256];
        Span<bool> known = stackalloc bool[256];
        using NativeBattleGfSourceReader? observed = !snapshot ? NativeBattleGfAnimationSource.Borrow(0, arena, 0, Length).OpenReader() : null;
        for (int first = 0; first < Length; first += bytes.Length)
        {
            int count = Math.Min(bytes.Length, Length - first);
            if (observed is not null)
            {
                observed.ReadWithKnownness(first, bytes[..count], known[..count]);
            }
            else
            {
                arena.ReadWithKnownness(first, bytes[..count], known[..count]);
            }
            writer.WriteWithKnownness(first, bytes[..count], known[..count]);
        }
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        NativeBattleEffectArena detached;
        if (arena.Transaction is not null || arena.Working.HasCheckpoint || NativeBattleEffectArenaTransaction.HasActive(arena.Working))
        {
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, Length);
            ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
            for (int first = 0; first < known.Length;)
            {
                if (known[first])
                {
                    first++;
                    continue;
                }
                int end = first + 1;
                while (end < known.Length && !known[end])
                {
                    end++;
                }
                unknown.Add(new(first, end - first));
                first = end;
            }
            ImmutableDictionary<int, NativeBattleResourcePointer?> pointers = arena.EnumerateResourcePointers().ToImmutableDictionary();
            detached = new([new(0, ImmutableCollectionsMarshal.AsImmutableArray(bytes))], unknown.ToImmutable(), pointers.Count == 0 ? null : pointers, Length);
        }
        else
        {
            detached = context.Map(arena);
        }
        ImmutableArray<NativeBattleEffectMemoryRegion> regions = detached.Regions;
        ImmutableArray<byte> payload = regions.Length == 1 && regions[0].Offset == 0 && regions[0].Data.Length == Length ? regions[0].Data : ImmutableCollectionsMarshal.AsImmutableArray(detached.ReadSnapshot(0, Length).Data);
        return new NativeBattleGfAnimationWorkspace(detached, true, payload);
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => new NativeBattleGfAnimationWorkspace(context.Map(arena));

    private sealed class MutationScope : IDisposable
    {
        private readonly NativeBattleEffectArenaTransaction? transaction;
        public NativeBattleGfAnimationWorkspace View { get; }

        public MutationScope(NativeBattleGfAnimationWorkspace source)
        {
            source.arena.RequireWritable();
            if (source.arena.Transaction is null && !source.arena.Working.HasCheckpoint && !NativeBattleEffectArenaTransaction.HasActive(source.arena.Working))
            {
                transaction = new(source.arena);
            }
            View = new(NativeBattleEffectArena.BorrowWorking(source.arena.Working, source.arena.WorkingOffset, source.Length));
        }

        public void PublishPrefix() => transaction?.Freeze();
        public void Dispose() => transaction?.Dispose();
    }

    private sealed class CacheBuffer(NativeBattleGfAnimationWorkspace workspace)
    {
        public int Int(int offset) => workspace.arena.ReadInt32(offset);
        public uint UInt(int offset) => workspace.arena.ReadUInt32(offset);
        public short Short(int offset) => workspace.arena.ReadInt16(offset);
        public ushort UShort(int offset) => workspace.arena.ReadUInt16(offset);
        public void Int(int offset, int value) => UInt(offset, unchecked((uint)value));
        public void UInt(int offset, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            workspace.arena.Write(offset, bytes);
        }
        public void Short(int offset, short value) => UShort(offset, unchecked((ushort)value));
        public void UShort(int offset, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            workspace.arena.Write(offset, bytes);
        }
        public void Clear(int length)
        {
            Span<byte> zero = stackalloc byte[256];
            zero.Clear();
            for (int first = 0; first < length; first += zero.Length)
            {
                workspace.arena.Write(first, zero[..Math.Min(zero.Length, length - first)]);
            }
        }
    }

    private sealed record SnapshotData([property: JsonRequired] ImmutableArray<byte> Data)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownRegions { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers { get; init; }
    }

    public sealed class Converter : JsonConverter<NativeBattleGfAnimationWorkspace>
    {
        public override NativeBattleGfAnimationWorkspace Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            SnapshotData value = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new InvalidDataException("Native animation workspace snapshot is missing.");
            if (value.Data.IsDefault || value.Data.Length == 0 || value.Data.Length > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Native animation workspace snapshot has an invalid physical extent.");
            }
            NativeBattleEffectArena frozen = new([new(0, value.Data)], value.UnknownRegions ?? [], value.ResourcePointers, value.Data.Length);
            frozen.Validate();
            return new(frozen, true, value.Data);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleGfAnimationWorkspace value, JsonSerializerOptions options)
        {
            if (!value.snapshot)
            {
                throw new InvalidOperationException("Native animation workspace serialization requires an explicit detached snapshot.");
            }
            JsonSerializer.Serialize(writer, new SnapshotData(value.data)
            {
                UnknownRegions = value.arena.UnknownRegions.IsEmpty ? null : value.arena.UnknownRegions,
                ResourcePointers = value.arena.ResourcePointers is { Count: > 0 } pointers ? pointers : null
            }, options);
        }
    }
}
