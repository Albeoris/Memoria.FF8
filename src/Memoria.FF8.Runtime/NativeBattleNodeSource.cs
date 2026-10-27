using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A retained node bank sharing its native bounded physical authority.</summary>
public sealed record NativeBattleNodeSource : INativeBattleStorageNode
{
    private readonly NativeBattleEffectArena storage;
    private readonly bool snapshot;
    private readonly ImmutableArray<byte> payload;

    public NativeBattleNodeSource(ImmutableArray<byte> data)
    {
        if (data.IsDefaultOrEmpty || data.Length > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Native node storage requires its actual bounded bytes.");
        }
        storage = NativeBattleEffectArena.CreateUnknown(data.Length).Write(0, data.AsSpan());
    }

    private NativeBattleNodeSource(NativeBattleEffectArena storage)
    {
        this.storage = storage;
    }

    private NativeBattleNodeSource(NativeBattleEffectArena storage, ImmutableArray<byte> payload)
    {
        this.storage = storage;
        this.payload = payload;
        snapshot = true;
    }

    [JsonIgnore] public int Length => storage.LogicalLength;
    [JsonIgnore] public ImmutableArray<byte> Data => snapshot ? payload : throw new InvalidOperationException("Native node bytes require an explicit detached snapshot.");
    [JsonIgnore] internal NativeBattleEffectArena Arena => !snapshot ? storage : throw new InvalidOperationException("Frozen node storage must be restored before mutation.");
    bool INativeBattleStorageNode.IsSnapshot => snapshot;

    internal static NativeBattleNodeSource Borrow(NativeBattleEffectArena arena, int offset, int length)
    {
        arena.RequireWritable();
        arena.RequireRange(offset, length);
        return new(NativeBattleEffectArena.BorrowWorking(arena.Working, checked(arena.WorkingOffset + offset), length));
    }

    public byte ReadByte(int offset) => storage.ReadByte(offset);
    public ushort ReadUInt16(int offset) => storage.ReadUInt16(offset);
    public uint ReadUInt32(int offset) => storage.ReadUInt32(offset);
    public void ReadInto(int offset, Span<byte> destination) => storage.ReadInto(offset, destination);
    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known) => storage.ReadWithKnownness(offset, destination, known);

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        NativeBattleEffectArena frozen = context.Map(storage);
        ImmutableArray<NativeBattleEffectMemoryRegion> ranges = frozen.Regions;
        ImmutableArray<byte> bytes = ranges.Length == 1 && ranges[0].Offset == 0 && ranges[0].Data.Length == Length ? ranges[0].Data : ImmutableCollectionsMarshal.AsImmutableArray(frozen.ReadSnapshot(0, Length).Data);
        return new NativeBattleNodeSource(frozen, bytes);
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => new NativeBattleNodeSource(context.Map(storage));

    private sealed record SnapshotData(ImmutableArray<byte> Data, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownRegions, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers);

    public sealed class Converter : JsonConverter<NativeBattleNodeSource>
    {
        public override NativeBattleNodeSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            SnapshotData value;
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                value = new(JsonSerializer.Deserialize<ImmutableArray<byte>>(ref reader, options), [], null);
            }
            else
            {
                value = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new InvalidDataException("Native node snapshot is missing.");
            }
            if (value.Data.IsDefaultOrEmpty || value.Data.Length > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Native node snapshot has an invalid physical extent.");
            }
            NativeBattleEffectArena frozen = new([new(0, value.Data)], value.UnknownRegions is { IsDefault: false } ranges ? ranges : [], value.ResourcePointers, value.Data.Length);
            frozen.Validate();
            return new(frozen, value.Data);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleNodeSource value, JsonSerializerOptions options)
        {
            if (!value.snapshot)
            {
                throw new InvalidOperationException("Native node serialization requires an explicit detached snapshot.");
            }
            ImmutableArray<NativeBattleEffectUnknownRegion> unknown = value.storage.UnknownRegions;
            ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers = value.storage.ResourcePointers;
            if (unknown.IsEmpty && (pointers is null || pointers.IsEmpty))
            {
                JsonSerializer.Serialize(writer, value.payload, options);
                return;
            }
            JsonSerializer.Serialize(writer, new SnapshotData(value.payload, unknown.IsEmpty ? null : unknown, pointers is { IsEmpty: true } ? null : pointers), options);
        }
    }
}
