using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A retained mutable ACT owner; catalog animation sources remain immutable values.</summary>
internal sealed record NativeBattleOwnedGfAnimationSource : NativeBattleGfAnimationSource, INativeBattleStorageNode, INativeBattleArenaStorageNode
{
    private readonly NativeBattleEffectArena storage;
    private readonly bool snapshot;
    private readonly ImmutableArray<byte> payload;

    private NativeBattleOwnedGfAnimationSource(uint address, NativeBattleEffectArena storage) : base(address, NativeBattleEffectArena.BorrowWorking(storage.Working, storage.WorkingOffset, storage.LogicalLength))
    {
        this.storage = Arena!;
    }

    private NativeBattleOwnedGfAnimationSource(uint address, NativeBattleEffectArena storage, ImmutableArray<byte> payload) : base(address, payload)
    {
        this.storage = storage;
        this.payload = payload;
        snapshot = true;
    }

    public override ImmutableArray<byte> Data
    {
        get => snapshot ? payload : throw new InvalidOperationException("Retained ACT bytes require an explicit detached snapshot.");
        init => throw new InvalidOperationException("Retained ACT storage requires explicit bounded stores.");
    }
    internal override NativeBattleEffectArena? FrozenArena => snapshot ? storage : null;
    bool INativeBattleStorageNode.IsSnapshot => snapshot;
    NativeBattleEffectArena INativeBattleArenaStorageNode.SnapshotArena => snapshot ? storage : throw new InvalidOperationException("Arena alias inspection requires a frozen ACT source.");

    internal static NativeBattleOwnedGfAnimationSource Own(NativeBattleGfAnimationSource source)
    {
        if (source is NativeBattleOwnedGfAnimationSource owned)
        {
            if (owned.snapshot)
            {
                throw new InvalidOperationException("Frozen ACT storage must be restored before mutation.");
            }
            return owned;
        }
        if (source.Arena is { } arena)
        {
            return new(source.Address, NativeBattleEffectArena.BorrowWorking(arena.Working, checked(arena.WorkingOffset + source.Offset), source.Length));
        }
        if (source.Staging is not null || source.IsDefault || source.Length <= 0 || source.Length > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Retained ACT initialization requires its actual bounded catalog bytes or physical bank.");
        }
        return new(source.Address, NativeBattleEffectArena.CreateUnknown(source.Length).Write(0, source.Data.AsSpan()));
    }

    internal void RequireStorage(NativeBattleEffectArena destination, int offset)
    {
        destination.RequireRange(offset, Length);
        if (snapshot || !ReferenceEquals(storage.Working, destination.Working) || storage.WorkingOffset != checked(destination.WorkingOffset + offset))
        {
            throw new InvalidOperationException("Retained ACT publication requires its actual physical bank authority.");
        }
    }

    internal Mutation BeginMutation() => new(this);

    internal sealed class Mutation : IDisposable
    {
        private readonly NativeBattleEffectArenaTransaction? transaction;
        internal NativeBattleEffectArena View { get; }

        internal Mutation(NativeBattleOwnedGfAnimationSource source)
        {
            source.storage.RequireWritable();
            if (source.storage.Transaction is null && !source.storage.Working.HasCheckpoint && !NativeBattleEffectArenaTransaction.HasActive(source.storage.Working))
            {
                transaction = new(source.storage);
            }
            View = NativeBattleEffectArena.BorrowWorking(source.storage.Working, source.storage.WorkingOffset, source.Length);
        }

        internal void Commit() => transaction?.Freeze();
        public void Dispose() => transaction?.Dispose();
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        NativeBattleEffectArena frozen = context.Map(storage);
        ImmutableArray<NativeBattleEffectMemoryRegion> ranges = frozen.Regions;
        ImmutableArray<byte> bytes = ranges.Length == 1 && ranges[0].Offset == 0 && ranges[0].Data.Length == Length ? ranges[0].Data : ImmutableCollectionsMarshal.AsImmutableArray(frozen.ReadSnapshot(0, Length).Data);
        return new NativeBattleOwnedGfAnimationSource(Address, frozen, bytes);
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => new NativeBattleOwnedGfAnimationSource(Address, context.Map(storage));

    private sealed record SnapshotData(uint Address, ImmutableArray<byte> Data)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public NativeBattleStorageReference? StorageReference { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownRegions { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers { get; init; }
    }

    public sealed class Converter : JsonConverter<NativeBattleGfAnimationSource>
    {
        public override NativeBattleGfAnimationSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            SnapshotData value = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new InvalidDataException("Retained ACT snapshot is missing.");
            if (value.Data.IsDefault || value.Data.Length <= 0 || value.Data.Length > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Retained ACT snapshot has an invalid physical extent.");
            }
            NativeBattleEffectArena frozen = new([new(0, value.Data)], value.UnknownRegions ?? [], value.ResourcePointers, value.Data.Length);
            frozen.Validate();
            if (value.StorageReference is not null)
            {
                frozen = frozen.WithStorageReference(value.StorageReference);
            }
            return new NativeBattleOwnedGfAnimationSource(value.Address, frozen, value.Data);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleGfAnimationSource value, JsonSerializerOptions options)
        {
            if (value is not NativeBattleOwnedGfAnimationSource && value.Arena is null && value.Staging is null)
            {
                JsonSerializer.Serialize(writer, new SnapshotData(value.Address, value.Data), options);
                return;
            }
            if (value is not NativeBattleOwnedGfAnimationSource owned || !owned.snapshot)
            {
                throw new InvalidOperationException("Retained ACT serialization requires an explicit detached owned snapshot.");
            }
            JsonSerializer.Serialize(writer, new SnapshotData(owned.Address, owned.payload)
            {
                StorageReference = owned.storage.StorageReference,
                UnknownRegions = owned.storage.UnknownRegions.IsEmpty ? null : owned.storage.UnknownRegions,
                ResourcePointers = owned.storage.ResourcePointers is { Count: > 0 } pointers ? pointers : null
            }, options);
        }
    }
}
