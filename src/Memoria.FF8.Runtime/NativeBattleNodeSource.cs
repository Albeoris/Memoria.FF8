using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A retained node bank sharing its native bounded physical authority.</summary>
[JsonConverter(typeof(NativeBattleNodeSource.Converter))]
public sealed partial record NativeBattleNodeSource : INativeBattleStorageNode, INativeBattleArenaStorageNode
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
        NativeBattleEffectArena owner = NativeBattleEffectArena.CreateUnknown(data.Length).Write(0, data.AsSpan());
        storage = NativeBattleEffectArena.BorrowWorking(owner.Working, 0, data.Length);
    }

    private NativeBattleNodeSource(NativeBattleEffectArena storage)
    {
        this.storage = NativeBattleEffectArena.BorrowWorking(storage.Working, storage.WorkingOffset, storage.LogicalLength);
    }

    private NativeBattleNodeSource(NativeBattleEffectArena storage, ImmutableArray<byte> payload)
    {
        this.storage = storage;
        this.payload = payload;
        snapshot = true;
    }

    public static implicit operator NativeBattleNodeSource(ImmutableArray<byte> data) => new(data);
    [JsonIgnore] public bool IsDefault => false;

    internal void RequireStorage(NativeBattleEffectArena arena, int offset)
    {
        arena.RequireRange(offset, Length);
        if (!ReferenceEquals(Arena.Working, arena.Working) || Arena.WorkingOffset != checked(arena.WorkingOffset + offset))
        {
            throw new InvalidOperationException("Native node publication requires its current physical bank authority.");
        }
    }

    [JsonIgnore] public int Length => staging is null ? storage.LogicalLength : stagingLength;
    [JsonIgnore] public ImmutableArray<byte> Data => snapshot ? payload : throw new InvalidOperationException("Native node bytes require an explicit detached snapshot.");
    [JsonIgnore] internal NativeBattleEffectArena Arena => staging is not null ? throw new InvalidOperationException("Staging node storage requires its logical owner.") : !snapshot ? storage : throw new InvalidOperationException("Frozen node storage must be restored before mutation.");
    bool INativeBattleStorageNode.IsSnapshot => snapshot;
    NativeBattleEffectArena? INativeBattleArenaStorageNode.SnapshotArena => snapshot ? staging is null ? storage : null : throw new InvalidOperationException("Arena alias inspection requires a frozen node source.");

    internal static NativeBattleNodeSource Borrow(NativeBattleEffectArena arena, int offset, int length)
    {
        arena.RequireWritable();
        arena.RequireRange(offset, length);
        return new(NativeBattleEffectArena.BorrowWorking(arena.Working, checked(arena.WorkingOffset + offset), length));
    }

    public byte ReadByte(int offset) => staging is null ? storage.ReadByte(offset) : ReadStagedByte(offset);
    public ushort ReadUInt16(int offset) => staging is null ? storage.ReadUInt16(offset) : ReadStagedUInt16(offset);
    public uint ReadUInt32(int offset) => staging is null ? storage.ReadUInt32(offset) : ReadStagedUInt32(offset);
    public void ReadInto(int offset, Span<byte> destination)
    {
        if (staging is null)
        {
            storage.ReadInto(offset, destination);
        }
        else
        {
            ReadStagedInto(offset, destination);
        }
    }
    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        if (staging is null)
        {
            storage.ReadWithKnownness(offset, destination, known);
        }
        else
        {
            ReadStagedWithKnownness(offset, destination, known);
        }
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        if (staging is not null)
        {
            return CaptureStaging(context);
        }
        NativeBattleEffectArena frozen = context.Map(storage);
        ImmutableArray<NativeBattleEffectMemoryRegion> ranges = frozen.Regions;
        ImmutableArray<byte> bytes = ranges.Length == 1 && ranges[0].Offset == 0 && ranges[0].Data.Length == Length ? ranges[0].Data : ImmutableCollectionsMarshal.AsImmutableArray(frozen.ReadSnapshot(0, Length).Data);
        return new NativeBattleNodeSource(frozen, bytes);
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => staging is null ? new NativeBattleNodeSource(context.Map(storage)) : new NativeBattleNodeSource(context.Map(staging), stagingAddress, stagingLength, false);

    private sealed record SnapshotData(ImmutableArray<byte> Data, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownRegions, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeBattleStorageReference? StorageReference = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<NativeBattleTextureFileSource>? TextureFiles = null);

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
            NativeBattleEffectArena frozen = new([new(0, value.Data)], value.UnknownRegions is { IsDefault: false } ranges ? ranges : [], value.ResourcePointers, value.Data.Length) { TextureFiles = value.TextureFiles };
            frozen.Validate();
            if (value.StorageReference is not null)
            {
                frozen = frozen.WithStorageReference(value.StorageReference);
            }
            return new(frozen, value.Data);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleNodeSource value, JsonSerializerOptions options)
        {
            if (!value.snapshot)
            {
                throw new InvalidOperationException("Native node serialization requires an explicit detached snapshot.");
            }
            ImmutableArray<NativeBattleEffectUnknownRegion> unknown = value.staging is null ? value.storage.UnknownRegions : value.stagingUnknown;
            ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers = value.staging is null ? value.storage.ResourcePointers : value.stagingPointers;
            NativeBattleStorageReference? reference = value.staging is null ? value.storage.StorageReference : null;
            ImmutableArray<NativeBattleTextureFileSource>? files = value.staging is null ? value.storage.TextureFiles : null;
            if (unknown.IsEmpty && (pointers is null || pointers.IsEmpty) && reference is null && files is null)
            {
                JsonSerializer.Serialize(writer, value.payload, options);
                return;
            }
            JsonSerializer.Serialize(writer, new SnapshotData(value.payload, unknown.IsEmpty ? null : unknown, pointers is { IsEmpty: true } ? null : pointers, reference, files), options);
        }
    }
}
