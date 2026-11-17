using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Portable identity of one original primitive arena; heap addresses are deliberately not invented.</summary>
public sealed record NativeBattlePrimitiveReference(string Bank, byte Buffer, int ByteOffset);
public readonly record struct NativeBattlePrimitiveLink(bool Proxy, int Index);
public readonly record struct NativeBattleAddressedOrderingRoot(uint NextAddress, int Bucket);
public sealed record NativeBattleLogicalPacket(NativeBattlePrimitiveReference Reference, ImmutableArray<uint> Words, NativeBattlePrimitiveLink Next)
{
    public byte? NextTableBuffer { get; init; }
    public bool RequiresPhysicalRead { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? AddressedNextAddress { get; init; }
    // Presentation coordinates belong to the current physical packet, including all of its proxy aliases.
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
    [JsonIgnore]
    public NativeBattlePilotGeometryReference Pilot { get; init; }
    [JsonIgnore]
    public NativeBattlePilotVisualGeometry? PilotGeometry
    {
        get => Pilot.ToGeometry();
        init => Pilot = value;
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PresentationActorId { get; init; }
    public string? TextureSourceOwner { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleModelTextureOwner? ModelTextureOwner { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScreenCoverage? ScreenCoverage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSceneCapture? SceneCapture { get; init; }
}
public sealed record NativeBattleLogicalProxy(NativeBattlePrimitiveReference Packet, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public byte? NextHigh22 { get; init; }
}
public sealed record NativeBattleLogicalCommand(NativeBattlePrimitiveReference Packet, int WordOffset, byte Opcode, ImmutableArray<uint> Words, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    private readonly NativeBattleImmutableSlice<uint> wordBacking = Words;
    private readonly NativeBattleImmutableSlice<int> depthBacking = VertexPayload4;

    internal NativeBattleLogicalCommand(NativeBattlePrimitiveReference packet, int wordOffset, byte opcode, NativeBattleImmutableSlice<uint> packetWords, int count, ushort flags20, NativeBattleImmutableSlice<int> vertexPayload4, int? moduleDraw)
        : this(packet, wordOffset, opcode, default, flags20, default, moduleDraw)
    {
        wordBacking = packetWords.Slice(wordOffset, count);
        depthBacking = vertexPayload4;
    }

    public ImmutableArray<uint> Words
    {
        get => wordBacking.ToImmutableArray();
        init => wordBacking = value;
    }

    [JsonIgnore]
    public ReadOnlySpan<uint> WordSpan => wordBacking.AsSpan();

    public ImmutableArray<int> VertexPayload4
    {
        get => depthBacking.ToImmutableArray();
        init => depthBacking = value;
    }

    internal NativeBattleImmutableSlice<int> VertexPayloadSlice => depthBacking;

    [JsonIgnore]
    public ReadOnlySpan<int> VertexPayloadSpan => depthBacking.IsDefault ? throw new InvalidDataException("Native command has no original proxy depth fields.") : depthBacking.AsSpan();

    public int? OrderingBucket { get; init; }
    internal NativeBattleImmutableSlice<BattleSceneScreenPoint>? ProjectedCoordinateSlice { get; init; }
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates
    {
        get => ProjectedCoordinateSlice?.ToImmutableArray();
        init => ProjectedCoordinateSlice = value;
    }

    [JsonIgnore]
    public ReadOnlySpan<BattleSceneScreenPoint> ProjectedCoordinateSpan => ProjectedCoordinateSlice is { } slice ? slice.AsSpan() : default;
    [JsonIgnore]
    public bool HasProjectedCoordinates => ProjectedCoordinateSlice.HasValue;
    [JsonIgnore]
    public NativeBattlePilotGeometryReference Pilot { get; init; }
    [JsonIgnore]
    public NativeBattlePilotVisualGeometry? PilotGeometry
    {
        get => Pilot.ToGeometry();
        init => Pilot = value;
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PresentationActorId { get; init; }
    public string? TextureSourceOwner { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleModelTextureOwner? ModelTextureOwner { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScreenCoverage? ScreenCoverage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSceneCapture? SceneCapture { get; init; }
}
public readonly record struct NativeBattleLogicalResolvedCommand(NativeBattleLogicalCommandReference Command, NativeBattlePrimitiveDrawMode DrawMode, byte Pass);

/// <summary>One persistent pending ordering owner, with detached legacy-shaped persistence nodes.</summary>
[JsonConverter(typeof(NativeBattleLogicalOrderingJsonConverter))]
public sealed partial record NativeBattleLogicalOrdering : INativeBattleStorageNode
{
    private readonly Owner? owner;
    private readonly NativeBattleLogicalOrderingData? snapshot;
    private readonly NativeBattleLogicalOrderingTransaction? transaction;
    private readonly Dictionary<NativeBattlePrimitiveReference, int>? snapshotIndices;
    private readonly bool snapshotDuplicateIdentity;

    public NativeBattleLogicalOrdering(ImmutableArray<NativeBattlePrimitiveLink> Table, ImmutableArray<NativeBattleLogicalProxy> Proxies, ImmutableArray<NativeBattleLogicalPacket> Packets, int? proxyCursor = null, byte? tableBuffer = null, ImmutableArray<NativeBattlePrimitiveLink>? otherTable = null)
    {
        owner = new(new(Table, Proxies, Packets, proxyCursor, tableBuffer, otherTable));
        owner.Root = this;
    }

    private NativeBattleLogicalOrdering(NativeBattleLogicalOrderingData snapshot)
    {
        this.snapshot = snapshot;
        snapshotIndices = [];
        if (!snapshot.Packets.IsDefault)
        {
            for (int index = 0; index < snapshot.Packets.Length; index++)
            {
                snapshotDuplicateIdentity |= !snapshotIndices.TryAdd(NativeBattlePrimitiveProcess.NormalizeCommonReference(snapshot.Packets[index].Reference), index);
            }
        }
    }

    internal NativeBattleLogicalOrdering(NativeBattleLogicalOrdering source, NativeBattleLogicalOrderingTransaction transaction)
    {
        owner = source.RequireOwner();
        this.transaction = transaction;
    }

    internal static NativeBattleLogicalOrdering FromSnapshot(NativeBattleLogicalOrderingData data) => new(data);
    internal NativeBattleLogicalOrderingData RequireSnapshot() => snapshot ?? throw new InvalidOperationException("Pending working ordering requires explicit capture before serialization.");
    internal NativeBattleLogicalOrderingTransaction? Transaction => transaction;
    internal bool SharesWorkingStorage(NativeBattleLogicalOrdering other) => owner is not null && ReferenceEquals(owner, other.owner);
    private Owner RequireOwner()
    {
        transaction?.RequireActive();
        return owner ?? throw new InvalidOperationException("Frozen ordering storage must be restored before mutation.");
    }
    private void RequireReadAccess() => transaction?.RequireActive();
    private static void RejectInitializer() => throw new InvalidOperationException("Ordering initializers cannot change shared ownership; use explicit ordering operations.");

    public NativeBattleOrderingList<NativeBattlePrimitiveLink> Table
    {
        get
        {
            RequireReadAccess();
            return owner is null ? NativeBattleOrderingList<NativeBattlePrimitiveLink>.FromSnapshot(snapshot!.Table) : owner.TableView(transaction?.Validation);
        }
        init => RejectInitializer();
    }
    public NativeBattleOrderingList<NativeBattleLogicalProxy> Proxies
    {
        get
        {
            RequireReadAccess();
            return owner is null ? NativeBattleOrderingList<NativeBattleLogicalProxy>.FromSnapshot(snapshot!.Proxies) : owner.ProxyView(transaction?.Validation);
        }
        init => RejectInitializer();
    }
    public NativeBattleOrderingList<NativeBattleLogicalPacket> Packets
    {
        get
        {
            RequireReadAccess();
            return owner is null ? NativeBattleOrderingList<NativeBattleLogicalPacket>.FromSnapshot(snapshot!.Packets) : owner.PacketView(transaction?.Validation);
        }
        init => RejectInitializer();
    }
    public int? ProxyCursor
    {
        get
        {
            RequireReadAccess();
            return owner?.proxyCursor ?? snapshot?.ProxyCursor;
        }
        init => RejectInitializer();
    }
    public byte? TableBuffer
    {
        get
        {
            RequireReadAccess();
            return owner?.tableBuffer ?? snapshot?.TableBuffer;
        }
        init => RejectInitializer();
    }
    public NativeBattleOrderingList<NativeBattlePrimitiveLink>? OtherTable
    {
        get
        {
            RequireReadAccess();
            return owner is null ? snapshot!.OtherTable is { } roots ? NativeBattleOrderingList<NativeBattlePrimitiveLink>.FromSnapshot(roots) : null : owner.OtherView(transaction?.Validation);
        }
        init => RejectInitializer();
    }
    [JsonIgnore]
    public bool IsSnapshot => owner is null;
    [JsonIgnore]
    internal object StorageIdentity => RequireOwner();
    [JsonIgnore]
    internal bool HasCheckpoint => RequireOwner().HasMarks;

    private static ImmutableArray<NativeBattlePrimitiveLink> EmptyRoots() => Enumerable.Range(0, NativeBattlePrimitiveOrdering.BattleTableLength).Select(index => new NativeBattlePrimitiveLink(false, index - 1)).ToImmutableArray();
    public static NativeBattleLogicalOrdering Clear(byte buffer = 0) => new(EmptyRoots(), [], [], 0, buffer, EmptyRoots());

    internal ImmutableArray<int> ReadRetainedProxyDepth(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        Owner storage = RequireOwner();
        storage.RequireOperational();
        int index = (int)Math.Min((long)storage.proxyCursor!.Value + ordinal, NativeBattlePrimitiveOrdering.ProxyCapacity);
        return index < storage.ProxyRowCount ? storage.ReadProxy(index).VertexPayload4.ToImmutableArray() : [];
    }

    public NativeBattleLogicalOrdering ResetAfterDraw45D080()
    {
        return ResetAfterDraw45D080(clearRoots: false);
    }
    internal NativeBattleLogicalOrdering ResetAfterDraw45D080(bool clearRoots)
    {
        RequireOwner().ResetAfterDraw(clearRoots);
        return this;
    }
    public NativeBattleLogicalOrdering ClearRoots45D550()
    {
        RequireOwner().ClearRoots();
        return this;
    }
    public NativeBattleLogicalOrdering ClearRange45D550(int first, int count)
    {
        RequireOwner().ClearRange(first, count);
        return this;
    }
    public NativeBattleLogicalOrdering SelectTable(byte buffer)
    {
        RequireOwner().SelectTable(buffer);
        return this;
    }
    public NativeBattleLogicalOrdering SetTableLink(int index, NativeBattlePrimitiveLink link)
    {
        RequireOwner().SetTableLink(index, link);
        return this;
    }
    public NativeBattleLogicalOrdering UpdateMetadata(int? proxyCursor, byte? tableBuffer)
    {
        RequireOwner().UpdateMetadata(proxyCursor, tableBuffer);
        return this;
    }

    public static byte[] LinkBytes(uint address) => [(byte)address, (byte)(address >> 8), (byte)(address >> 16)];
    private static uint LinkAddress(NativeBattlePrimitiveLink link, byte buffer)
    {
        if (link.Index < 0)
        {
            return uint.MaxValue;
        }
        return checked((link.Proxy ? 0x1C48828u : 0x1D8E058u + 17544u * buffer) + (link.Proxy ? 24u : 4u) * (uint)link.Index);
    }

    public NativeBattleLogicalOrdering Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null, NativeBattlePilotVisualGeometry? pilotGeometry = null, string? presentationActorId = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, NativeBattleModelTextureOwner? modelTextureOwner = null)
    {
        using Builder builder = new(this);
        builder.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner, pilotGeometry, presentationActorId, screenCoverage, sceneCapture, modelTextureOwner);
        return builder.ToImmutable();
    }

    public (NativeBattleLogicalOrdering Ordering, uint? ProxyAddress) PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, NativeBattlePilotVisualGeometry? pilotGeometry = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, NativeBattleModelTextureOwner? modelTextureOwner = null, bool retainAuthoredPayload = false)
    {
        using Builder builder = new(this);
        int index = builder.ProxyCount;
        builder.PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw, pilotGeometry, screenCoverage, sceneCapture, projectedCoordinates, modelTextureOwner, retainAuthoredPayload);
        return (builder.ToImmutable(), index < NativeBattlePrimitiveOrdering.ProxyCapacity ? 0x1C48828u + (uint)(24 * index) : null);
    }

    private static void ValidateInsertion(int tableLength, int bucket, NativeBattlePrimitiveReference reference, NativeBattleImmutableSlice<uint> words, NativeBattleImmutableSlice<int> vertexPayload4)
    {
        if ((uint)bucket >= tableLength || string.IsNullOrWhiteSpace(reference.Bank) || reference.Buffer > 1 || reference.ByteOffset < 0 || (reference.ByteOffset & 3) != 0 || words.IsDefaultOrEmpty || words.Length < 1 + (words[0] >> 24) || vertexPayload4.IsDefault || vertexPayload4.Length is not (0 or 4))
        {
            throw new InvalidDataException($"Native logical primitive insertion lacks its original bounded packet or bank identity: bucket {bucket}/{tableLength}, bank '{reference.Bank}', buffer {reference.Buffer}, byte offset {reference.ByteOffset}, words {(words.IsDefault ? -1 : words.Length)}, header {(words.IsDefaultOrEmpty ? 0 : words[0]):X8}, vertex payload {(vertexPayload4.IsDefault ? -1 : vertexPayload4.Length)}.");
        }
    }

    internal readonly record struct ModelPacketInsertion(NativeBattlePrimitiveReference Reference, object Storage, int Index, ushort Flags20, int? ModuleDraw, string? TextureSourceOwner)
    {
        internal NativeBattleModelPrimitive Primitive => Storage is NativeBattleModelPrimitiveBatch batch ? batch[Index] : (NativeBattleGeometryPrimitive)Storage;
    }
    internal readonly record struct JournalEntry(Action? Callback, ModelPacketInsertion ModelPacket = default);

    internal sealed class Delta(object identity, long expectedVersion, ImmutableArray<JournalEntry> edits)
    {
        internal object Identity { get; } = identity;
        internal long ExpectedVersion { get; } = expectedVersion;
        internal ImmutableArray<JournalEntry> Edits { get; } = edits;
        internal bool Consumed { get; set; }
    }

    /// <summary>A scoped unpublished suffix; publication accepts it without replacing the persistent owner.</summary>
    internal sealed class Builder : IDisposable
    {
        private readonly NativeBattleLogicalOrdering source;
        private readonly Owner storage;
        private readonly Checkpoint boundary;
        private readonly List<Checkpoint> callbacks = [];
        private bool disposed;

        public Builder(NativeBattleLogicalOrdering source)
        {
            this.source = source;
            storage = source.RequireOwner();
            storage.RequireOperational();
            boundary = storage.BeginMark();
        }

        public int ProxyCount
        {
            get
            {
                RequireActive();
                return storage.proxyCursor!.Value;
            }
        }
        public uint LastHeaderLink
        {
            get
            {
                RequireActive();
                return storage.lastHeaderLink;
            }
        }
        public void InvalidatePacketRange(NativeBattlePrimitiveReference reference, int length)
        {
            RequireActive();
            storage.InvalidatePacketRange(reference, length);
        }
        public void Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null, NativeBattlePilotVisualGeometry? pilotGeometry = null, string? presentationActorId = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, NativeBattleModelTextureOwner? modelTextureOwner = null)
        {
            RequireActive();
            storage.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner, pilotGeometry, presentationActorId, screenCoverage, sceneCapture, modelTextureOwner);
        }
        internal void PrependModel(NativeBattlePrimitiveReference reference, NativeBattleGeometryPrimitive primitive, ushort flags20, int? moduleDraw, string? textureSourceOwner)
        {
            RequireActive();
            storage.PrependModel(new(reference, primitive, 0, flags20, moduleDraw, textureSourceOwner));
        }
        internal void PrependModel(NativeBattlePrimitiveReference reference, NativeBattleModelPrimitiveBatch batch, int index, ushort flags20, int? moduleDraw, string? textureSourceOwner)
        {
            RequireActive();
            storage.PrependModel(new(reference, batch, index, flags20, moduleDraw, textureSourceOwner));
        }
        internal void PrependPlacedModel(NativeBattlePlacedModelPrimitives placement, int index)
        {
            RequireActive();
            storage.PrependModel(new(placement.ReadPacket(index), placement, index, placement.ReadFlags(index), placement.ModuleDraw, placement.TextureSourceOwner));
        }
        public void PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, NativeBattlePilotVisualGeometry? pilotGeometry = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, NativeBattleModelTextureOwner? modelTextureOwner = null, bool retainAuthoredPayload = false)
        {
            RequireActive();
            storage.PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw, pilotGeometry, screenCoverage, sceneCapture, projectedCoordinates, modelTextureOwner, retainAuthoredPayload);
        }
        public NativeBattleLogicalOrdering ToImmutable()
        {
            RequireActive();
            storage.Publish(boundary, callbacks);
            return source.Transaction?.View ?? storage.Root;
        }
        internal NativeBattleLogicalOrdering PublishRoot()
        {
            ToImmutable();
            return storage.Root;
        }
        internal NativeBattleLogicalOrdering PublishKnownAncestorPrefix()
        {
            RequireActive();
            storage.PublishKnownAncestorPrefix(boundary, callbacks);
            return storage.Root;
        }
        internal void BeginCheckpoint()
        {
            RequireActive();
            callbacks.Add(storage.BeginMark());
        }
        internal void CommitCheckpoint()
        {
            RequireActive();
            Checkpoint current = CurrentCallback();
            storage.CommitMark(current);
            callbacks.RemoveAt(callbacks.Count - 1);
        }
        internal void RollbackCheckpoint()
        {
            RequireActive();
            Checkpoint current = CurrentCallback();
            storage.RollbackMark(current);
            callbacks.RemoveAt(callbacks.Count - 1);
        }
        internal Delta SuspendCheckpointDelta()
        {
            RequireActive();
            Checkpoint current = CurrentCallback();
            ImmutableArray<JournalEntry> edits = storage.PendingEdits(current);
            storage.RollbackMark(current);
            callbacks.RemoveAt(callbacks.Count - 1);
            return new(storage, storage.version, edits);
        }
        internal Delta SuspendDelta()
        {
            RequireActive();
            if (callbacks.Count != 0)
            {
                throw new InvalidOperationException("Commit or roll back the callback checkpoint before suspending its ordering suffix.");
            }
            ImmutableArray<JournalEntry> edits = storage.PendingEdits(boundary);
            storage.RollbackMark(boundary, false);
            return new(storage, storage.version, edits);
        }
        internal void ResumeDelta(Delta delta)
        {
            RequireActive();
            if (!ReferenceEquals(delta.Identity, storage) || delta.ExpectedVersion != storage.version || delta.Consumed)
            {
                throw new InvalidOperationException("Ordering delta requires its unchanged original owner and accepted prefix.");
            }
            delta.Consumed = true;
            foreach (JournalEntry edit in delta.Edits)
            {
                if (edit.Callback is { } callback)
                {
                    callback();
                }
                else
                {
                    storage.PrependModel(edit.ModelPacket);
                }
            }
        }
        private Checkpoint CurrentCallback() => callbacks.Count == 0 ? throw new InvalidOperationException("Native ordering callback checkpoint is absent.") : callbacks[^1];
        private void RequireActive()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            source.RequireReadAccess();
        }
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            while (callbacks.Count != 0)
            {
                RollbackCheckpoint();
            }
            storage.RollbackMark(boundary);
            disposed = true;
        }
    }

    private sealed class Checkpoint
    {
        internal bool Automatic;
        internal int Tables;
        internal int ProxiesUndo;
        internal int PacketsUndo;
        internal int Ranges;
        internal int Indices;
        internal int Edits;
        internal List<NativeBattlePrimitiveLink> Table = null!;
        internal List<NativeBattlePrimitiveLink>? Other;
        internal PacketBank Bank = null!;
        internal long DrawGeneration;
        internal int ProxyCount;
        internal int PacketCount;
        internal int? Cursor;
        internal byte? Buffer;
        internal int Maximum;
        internal uint Header;
        internal bool TableDefault;
        internal bool OtherDefault;
        internal bool OtherPresent;
        internal bool ProxyDefault;
        internal bool PacketDefault;
        internal bool Duplicate;
    }

    private sealed partial class Owner : INativeBattleOrderingCollectionScope
    {
        internal NativeBattleLogicalOrdering Root = null!;
        private List<NativeBattlePrimitiveLink> table;
        private readonly OrderingPages<ProxySlot> proxies = new();
        private readonly ProjectedRows<NativeBattleLogicalProxy> proxyRecords;
        private readonly ProjectedRows<NativeBattleLogicalPacket> packetRecords;
        private PacketBank bank = new();
        private PacketBank? spareBank;
        private long drawGeneration;
        private OrderingPages<PacketSlot> Packets => bank.Rows;
        private int maximumPacketBytes;
        internal byte? tableBuffer;
        private List<NativeBattlePrimitiveLink>? otherTable;
        internal int? proxyCursor;
        internal uint lastHeaderLink;
        internal long version;
        private bool tableDefault;
        private bool otherTableDefault;
        private bool otherTablePresent;
        private bool proxyDefault;
        private bool packetDefault;
        private bool duplicateIdentity;
        private int operationDepth;
        private long collectionGeneration;
        private readonly List<Checkpoint> marks = [];
        private readonly List<(List<NativeBattlePrimitiveLink> Rows, int Index, NativeBattlePrimitiveLink Value)> tableUndo = [];
        private readonly OrderingPages<(int Index, ProxySlot Value)> proxyUndo = new();
        private readonly OrderingPages<(OrderingPages<PacketSlot> Rows, int Index, PacketSlot Value)> packetUndo = new();
        private readonly OrderingPages<(PacketRangeIndex Ranges, (int Offset, int Index) Value)> rangeUndo = new();
        private readonly OrderingPages<(Dictionary<NativeBattlePrimitiveReference, int> Indices, NativeBattlePrimitiveReference Key)> indexUndo = new();
        private readonly List<JournalEntry> edits = [];
        internal bool HasMarks => marks.Count != 0;
        private int ProxyCount => proxyCursor!.Value;
        private uint LastHeaderLink => lastHeaderLink;

        internal Owner(NativeBattleLogicalOrderingData source)
        {
            tableDefault = source.Table.IsDefault;
            proxyDefault = source.Proxies.IsDefault;
            packetDefault = source.Packets.IsDefault;
            table = tableDefault ? [] : source.Table.ToList();
            proxyRecords = new(() => proxies.Count, ObserveProxy);
            packetRecords = new(() => Packets.Count, ObservePacket);
            if (!proxyDefault)
            {
                foreach (NativeBattleLogicalProxy proxy in source.Proxies)
                {
                    proxies.Add(new(proxy));
                }
            }
            otherTablePresent = source.OtherTable.HasValue;
            otherTableDefault = source.OtherTable is { } savedOther && savedOther.IsDefault;
            otherTable = source.OtherTable is { } other ? other.IsDefault ? [] : other.ToList() : null;
            proxyCursor = source.ProxyCursor;
            tableBuffer = source.TableBuffer;
            if (!packetDefault)
            {
                bank.Indices.EnsureCapacity(source.Packets.Length);
                foreach (NativeBattleLogicalPacket record in source.Packets)
                {
                    PacketSlot packet = new(record);
                    int index = Packets.Count;
                    duplicateIdentity |= !bank.Indices.TryAdd(NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference), index);
                    RegisterPacketRange(packet, index);
                    Packets.Add(packet);
                }
            }
        }

        internal void RequireOperational()
        {
            if (proxyCursor is not int cursor || tableBuffer is not byte buffer)
            {
                throw new NotSupportedException("Native proxy allocation requires its retained cursor and physical OT table identity.");
            }
            if (tableDefault || proxyDefault || packetDefault || cursor < 0 || cursor > NativeBattlePrimitiveOrdering.ProxyCapacity || cursor > proxies.Count || buffer > 1)
            {
                throw new InvalidDataException("Native proxy cursor exceeds its retained physical cells.");
            }
        }
        internal NativeBattleOrderingList<NativeBattlePrimitiveLink> TableView(Action? validation) => tableDefault ? default : new(table, validation, this, collectionGeneration);
        internal NativeBattleOrderingList<NativeBattleLogicalProxy> ProxyView(Action? validation) => proxyDefault ? default : new(proxyRecords, validation, this, collectionGeneration);
        internal NativeBattleOrderingList<NativeBattleLogicalPacket> PacketView(Action? validation) => packetDefault ? default : new(packetRecords, validation, this, collectionGeneration);
        internal NativeBattleOrderingList<NativeBattlePrimitiveLink>? OtherView(Action? validation) => !otherTablePresent ? null : otherTableDefault ? default(NativeBattleOrderingList<NativeBattlePrimitiveLink>) : new(otherTable!, validation, this, collectionGeneration);
        public void ValidateBorrow(long generation)
        {
            if (generation != collectionGeneration)
            {
                throw new InvalidOperationException("Native ordering collection borrow was invalidated by a container reset.");
            }
        }
        internal int ProxyRowCount => proxies.Count;
        internal long DrawGeneration => drawGeneration;

        internal ProxySlot ReadProxy(int index) => proxies[index];

        private NativeBattleLogicalProxy ObserveProxy(int index)
        {
            ref ProxySlot slot = ref proxies.Slot(index);
            if (slot.Observation is not { } observation)
            {
                observation = slot.Capture();
                slot = slot with { Observation = observation };
            }
            return observation;
        }

        private NativeBattleLogicalPacket ObservePacket(int index)
        {
            ref PacketSlot slot = ref Packets.Slot(index);
            if (slot.Observation is not { } observation)
            {
                observation = new();
                slot = slot with { Observation = observation };
            }
            if (!slot.RequiresPhysicalRead && slot.Generation != drawGeneration)
            {
                return observation.AfterDraw ??= slot.Capture(drawGeneration);
            }
            return observation.Authored ??= slot.Capture(drawGeneration);
        }

        internal bool TryPacket(NativeBattlePrimitiveReference reference, out PacketSlot packet)
        {
            if (bank.Indices.TryGetValue(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference), out int index))
            {
                packet = Packets[index];
                return true;
            }
            packet = default;
            return false;
        }
        internal void ValidatePacketIdentityIndex()
        {
            if (duplicateIdentity)
            {
                throw new ArgumentException("Native ordering has duplicate normalized packet identities.");
            }
        }
        private void Run(Action body, Action replay)
        {
            operationDepth++;
            try
            {
                body();
                CompleteMutation(new(replay));
            }
            finally
            {
                operationDepth--;
            }
        }
        private void CompleteMutation(JournalEntry replay)
        {
            if (operationDepth == 1)
            {
                version++;
                if (HasMarks)
                {
                    edits.Add(replay);
                }
            }
        }

        internal void InvalidatePacketRange(NativeBattlePrimitiveReference reference, int length)
        {
            operationDepth++;
            try
            {
                bool changed = InvalidatePacketRangeCore(reference, length);
                if (changed && operationDepth == 1)
                {
                    version++;
                    if (HasMarks)
                    {
                        RetainInvalidation(reference, length);
                    }
                }
            }
            finally
            {
                operationDepth--;
            }
        }

        private void RetainInvalidation(NativeBattlePrimitiveReference reference, int length)
        {
            edits.Add(new(() => InvalidatePacketRange(reference, length)));
        }
        internal void Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates, string? textureSourceOwner, NativeBattlePilotVisualGeometry? pilotGeometry, string? presentationActorId, NativeBattleScreenCoverage? screenCoverage, NativeBattleSceneCapture? sceneCapture, NativeBattleModelTextureOwner? modelTextureOwner)
        {
            Run(() => PrependCore(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner, pilotGeometry, presentationActorId, screenCoverage, sceneCapture, modelTextureOwner), () => Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner, pilotGeometry, presentationActorId, screenCoverage, sceneCapture, modelTextureOwner));
        }
        internal void PrependModel(ModelPacketInsertion insertion)
        {
            operationDepth++;
            try
            {
                if (insertion.Storage is NativeBattlePlacedModelPrimitives placement)
                {
                    NativeBattleModelPrimitiveBatch batch = placement.Batch;
                    int row = insertion.Index;
                    PrependCore(batch.ReadBucket(row), insertion.Reference, batch.ReadWords(row), insertion.Flags20, batch.ReadVertexPayload4(row), insertion.ModuleDraw, batch.ReadProjectedCoordinates(row), insertion.TextureSourceOwner, batch.ReadPilot(row), batch.ReadPresentationActorId(row), batch.ReadScreenCoverage(row), batch.ReadSceneCapture(row), batch.ReadModelTextureOwner(row), placement, row);
                }
                else
                {
                    NativeBattleModelPrimitive primitive = insertion.Primitive;
                    PrependCore(primitive.Bucket, insertion.Reference, primitive.Words, insertion.Flags20, primitive.VertexPayload4, insertion.ModuleDraw, primitive.ProjectedCoordinates, insertion.TextureSourceOwner, primitive.Pilot, primitive.PresentationActorId, primitive.ScreenCoverage, primitive.SceneCapture, primitive.ModelTextureOwner);
                }
                CompleteMutation(new(null, insertion));
            }
            finally
            {
                operationDepth--;
            }
        }
        internal void PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw, NativeBattlePilotVisualGeometry? pilotGeometry, NativeBattleScreenCoverage? screenCoverage, NativeBattleSceneCapture? sceneCapture, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates, NativeBattleModelTextureOwner? modelTextureOwner, bool retainAuthoredPayload = false)
        {
            Run(() => PrependAddressedCore(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw, pilotGeometry, screenCoverage, sceneCapture, projectedCoordinates, modelTextureOwner, retainAuthoredPayload), () => PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw, pilotGeometry, screenCoverage, sceneCapture, projectedCoordinates, modelTextureOwner, retainAuthoredPayload));
        }
        private bool InvalidatePacketRangeCore(NativeBattlePrimitiveReference reference, int length)
        {
            reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
            if (length == 0 || !bank.Ranges.TryGetValue((reference.Bank, reference.Buffer), out PacketRangeIndex? ranges))
            {
                return false;
            }
            bool changed = false;
            int minimum = Math.Max(0, reference.ByteOffset - maximumPacketBytes + 1);
            int maximum = checked(reference.ByteOffset + length - 1);
            for (int slot = ranges.FirstAtOrAfter((minimum, int.MinValue)); slot != PacketRangeIndex.NoSlot; slot = ranges.Next(slot))
            {
                (int offset, int index) = ranges.ReadKey(slot);
                if (offset > maximum)
                {
                    break;
                }
                PacketSlot packet = Packets[index];
                NativeBattlePrimitiveReference physical = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
                if ((!packet.ReadsPhysicalBytes(drawGeneration) || packet.ProjectedCoordinates is not null || packet.TextureSourceOwner is not null || packet.ModelTextureOwner is { Invalidated: false } || !packet.Pilot.IsEmpty || packet.PresentationActorId is not null || packet.ScreenCoverage is not null || packet.SceneCapture is not null) && physical.Bank == reference.Bank && physical.Buffer == reference.Buffer && physical.ByteOffset < reference.ByteOffset + length && reference.ByteOffset < physical.ByteOffset + packet.Words.Length * 4)
                {
                    if (HasMarks)
                    {
                        packetUndo.Add((Packets, index, packet));
                    }
                    Packets[index] = packet with { Observation = null, RequiresPhysicalRead = true, AddressedNextAddress = null, ProjectedCoordinates = null, TextureSourceOwner = null, ModelTextureOwner = packet.ModelTextureOwner is null ? null : NativeBattleModelTextureOwner.InvalidatedOwner, Pilot = default, PresentationActorId = null, ScreenCoverage = null, SceneCapture = null, Placement = null, PlacementRow = 0 };
                    changed = true;
                }
            }
            return changed;
        }

        private void RegisterPacketRange(in PacketSlot packet, int index)
        {
            if (packet.RequiresPhysicalRead && packet.ProjectedCoordinates is null && packet.TextureSourceOwner is null && packet.ModelTextureOwner is null && packet.Pilot.IsEmpty && packet.PresentationActorId is null && packet.ScreenCoverage is null && packet.SceneCapture is null)
            {
                return;
            }
            NativeBattlePrimitiveReference reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
            var key = (reference.Bank, reference.Buffer);
            if (!bank.Ranges.TryGetValue(key, out PacketRangeIndex? ranges))
            {
                ranges = new();
                bank.Ranges.Add(key, ranges);
            }
            if (ranges.Add((reference.ByteOffset, index)) && HasMarks)
            {
                rangeUndo.Add((ranges, (reference.ByteOffset, index)));
            }
            maximumPacketBytes = Math.Max(maximumPacketBytes, checked(packet.Words.Length * 4));
        }

        private void PrependCore(int bucket, NativeBattlePrimitiveReference reference, NativeBattleImmutableSlice<uint> words, ushort flags20, NativeBattleImmutableSlice<int> vertexPayload4, int? moduleDraw = null, NativeBattleImmutableSlice<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null, NativeBattlePilotGeometryReference pilotGeometry = default, string? presentationActorId = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, NativeBattleModelTextureOwner? modelTextureOwner = null, NativeBattlePlacedModelPrimitives? placement = null, int placementRow = 0)
        {
            (List<NativeBattlePrimitiveLink> roots, int rootIndex, byte rootBuffer) = ResolveInsertionRoot(bucket);
            ValidateInsertion(roots.Count, rootIndex, reference, words, vertexPayload4);
            if (ProxyCount >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            int index = ProxyCount;
            if (rootBuffer != tableBuffer)
            {
                placement = null;
            }
            AppendProxy(reference, words, flags20, vertexPayload4, moduleDraw, roots[rootIndex], rootBuffer, LinkAddress(roots[rootIndex], rootBuffer), false, projectedCoordinates, textureSourceOwner, pilotGeometry, presentationActorId, screenCoverage, sceneCapture, modelTextureOwner, placement: placement, placementRow: placementRow);
            if (HasMarks)
            {
                tableUndo.Add((roots, rootIndex, roots[rootIndex]));
            }
            roots[rootIndex] = new(true, index);
        }

        private (List<NativeBattlePrimitiveLink> Roots, int Index, byte Buffer) ResolveInsertionRoot(int relativeBucket)
        {
            byte active = tableBuffer ?? throw new NotSupportedException("Native ordering insertion requires its actual retained table selector.");
            if ((uint)relativeBucket < table.Count)
            {
                return (table, relativeBucket, active);
            }
            const int length = NativeBattlePrimitiveOrdering.BattleTableLength;
            long physicalIndex = (long)active * length + relativeBucket;
            if (tableDefault || table.Count != length || !otherTablePresent || otherTableDefault || otherTable is not { Count: length } || active > 1 || physicalIndex < 0 || physicalIndex >= 2L * length)
            {
                throw new InvalidDataException($"Native ordering insertion leaves its two retained physical root tables: active buffer {active}, relative bucket {relativeBucket}.");
            }
            byte target = physicalIndex >= length ? (byte)1 : (byte)0;
            int index = checked((int)(physicalIndex - target * length));
            return (target == active ? table : otherTable, index, target);
        }

        private void PrependAddressedCore(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, NativeBattlePilotVisualGeometry? pilotGeometry = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, NativeBattleModelTextureOwner? modelTextureOwner = null, bool retainAuthoredPayload = false)
        {
            ValidateInsertion(1, 0, reference, words, vertexPayload4);
            if (ProxyCount >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            AppendProxy(reference, words, flags20, vertexPayload4, moduleDraw, new(false, -1), 0, previousAddress, !retainAuthoredPayload, pilotGeometry: pilotGeometry, screenCoverage: screenCoverage, sceneCapture: sceneCapture, projectedCoordinates: projectedCoordinates, modelTextureOwner: modelTextureOwner, addressedNextAddress: retainAuthoredPayload ? previousAddress : null);
        }

        private void AppendProxy(NativeBattlePrimitiveReference reference, NativeBattleImmutableSlice<uint> words, ushort flags20, NativeBattleImmutableSlice<int> vertexPayload4, int? moduleDraw, NativeBattlePrimitiveLink next, byte nextBuffer, uint previousAddress, bool physicalRead, NativeBattleImmutableSlice<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null, NativeBattlePilotGeometryReference pilotGeometry = default, string? presentationActorId = null, NativeBattleScreenCoverage? screenCoverage = null, NativeBattleSceneCapture? sceneCapture = null, NativeBattleModelTextureOwner? modelTextureOwner = null, uint? addressedNextAddress = null, NativeBattlePlacedModelPrimitives? placement = null, int placementRow = 0)
        {
            InvalidatePacketRange(reference, words.Length * 4);
            lastHeaderLink = previousAddress;
            PacketSlot packet = new(reference, words, next, drawGeneration) { NextTableBuffer = nextBuffer, RequiresPhysicalRead = physicalRead, AddressedNextAddress = addressedNextAddress, ProjectedCoordinates = projectedCoordinates, TextureSourceOwner = textureSourceOwner, ModelTextureOwner = modelTextureOwner, Pilot = pilotGeometry, PresentationActorId = presentationActorId, ScreenCoverage = screenCoverage, SceneCapture = sceneCapture, Placement = placement, PlacementRow = placementRow };
            NativeBattlePrimitiveReference identity = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
            if (bank.Indices.TryGetValue(identity, out int existing))
            {
                if (HasMarks)
                {
                    packetUndo.Add((Packets, existing, Packets[existing]));
                }
                Packets[existing] = packet;
                RegisterPacketRange(packet, existing);
            }
            else
            {
                if (HasMarks)
                {
                    indexUndo.Add((bank.Indices, identity));
                }
                bank.Indices.Add(identity, Packets.Count);
                RegisterPacketRange(packet, Packets.Count);
                Packets.Add(packet);
            }
            ProxySlot proxy = new(reference, flags20, vertexPayload4, moduleDraw, (byte)(LastHeaderLink >> 24)) { Placement = placement, PlacementRow = placementRow };
            if (ProxyCount < proxies.Count)
            {
                if (HasMarks)
                {
                    proxyUndo.Add((ProxyCount, proxies[ProxyCount]));
                }
                proxies[ProxyCount] = proxy;
            }
            else
            {
                proxies.Add(proxy);
            }
            proxyCursor++;
        }


        internal void ResetAfterDraw(bool clearRoots)
        {
            Run(() =>
            {
                bool discardPackets = clearRoots && OtherTableHasNoPackets();
                drawGeneration++;
                proxyCursor = 0;
                if (clearRoots)
                {
                    ClearRootsCore(discardPackets);
                }
            }, () => ResetAfterDraw(clearRoots));
        }

        internal void ClearRoots() => Run(() => ClearRootsCore(OtherTableHasNoPackets()), ClearRoots);

        private bool OtherTableHasNoPackets()
        {
            if (!otherTablePresent || otherTableDefault || otherTable is not { Count: NativeBattlePrimitiveOrdering.BattleTableLength })
            {
                return false;
            }
            for (int index = 0; index < otherTable.Count; index++)
            {
                if (otherTable[index] != new NativeBattlePrimitiveLink(false, index - 1))
                {
                    return false;
                }
            }
            return true;
        }

        private void ClearRootsCore(bool discardPackets)
        {
            if (table.Count != NativeBattlePrimitiveOrdering.BattleTableLength)
            {
                collectionGeneration++;
                table = EmptyRoots().ToList();
            }
            else
            {
                for (int index = 0; index < table.Count; index++)
                {
                    SetTableCore(index, new(false, index - 1));
                }
            }
            tableDefault = false;
            if (discardPackets)
            {
                collectionGeneration++;
                //A real native clear may discard these unreachable packet identities. Keep rollback containers private.
                if (HasMarks)
                {
                    bank = spareBank ?? new();
                    spareBank = null;
                }
                else
                {
                    bank.Clear();
                }
                maximumPacketBytes = 0;
                packetDefault = false;
                duplicateIdentity = false;
            }
        }
        internal void ClearRange(int first, int count)
        {
            if (tableDefault || first < 0 || count <= 0 || first > table.Count - count)
            {
                throw new InvalidDataException("Native ordering-table clear requires its actual bounded DWORD range.");
            }
            Run(() =>
            {
                for (int index = first; index < first + count; index++)
                {
                    SetTableCore(index, new(false, index == first ? -1 : index - 1));
                }
            }, () => ClearRange(first, count));
        }
        internal void SelectTable(byte buffer)
        {
            if (buffer > 1 || tableBuffer is null || !otherTablePresent && buffer != tableBuffer)
            {
                throw new NotSupportedException("Native OT bank selection requires its retained table identities.");
            }
            Run(() =>
            {
                if (buffer != tableBuffer)
                {
                    (table, otherTable) = (otherTable!, table);
                    (tableDefault, otherTableDefault) = (otherTableDefault, tableDefault);
                    otherTablePresent = true;
                    tableBuffer = buffer;
                }
            }, () => SelectTable(buffer));
        }
        private void SetTableCore(int index, NativeBattlePrimitiveLink link)
        {
            if (HasMarks)
            {
                tableUndo.Add((table, index, table[index]));
            }
            table[index] = link;
        }
        internal void SetTableLink(int index, NativeBattlePrimitiveLink link)
        {
            if (tableDefault || (uint)index >= table.Count)
            {
                throw new InvalidDataException("Native ordering-table write leaves its actual root bank.");
            }
            Run(() => SetTableCore(index, link), () => SetTableLink(index, link));
        }
        internal void UpdateMetadata(int? cursor, byte? buffer)
        {
            if (cursor is int value && (value < 0 || value > NativeBattlePrimitiveOrdering.ProxyCapacity || value > proxies.Count) || buffer > 1)
            {
                throw new InvalidDataException("Native ordering metadata leaves its retained physical allocation.");
            }
            Run(() =>
            {
                proxyCursor = cursor;
                tableBuffer = buffer;
            }, () => UpdateMetadata(cursor, buffer));
        }

        internal Checkpoint BeginMark()
        {
            Checkpoint mark = new();
            Remember(mark);
            marks.Add(mark);
            return mark;
        }
        private void Remember(Checkpoint mark)
        {
            mark.Tables = tableUndo.Count;
            mark.ProxiesUndo = proxyUndo.Count;
            mark.PacketsUndo = packetUndo.Count;
            mark.Ranges = rangeUndo.Count;
            mark.Indices = indexUndo.Count;
            mark.Edits = edits.Count;
            mark.Table = table;
            mark.Other = otherTable;
            mark.Bank = bank;
            mark.DrawGeneration = drawGeneration;
            mark.ProxyCount = proxies.Count;
            mark.PacketCount = Packets.Count;
            mark.Cursor = proxyCursor;
            mark.Buffer = tableBuffer;
            mark.Maximum = maximumPacketBytes;
            mark.Header = lastHeaderLink;
            mark.TableDefault = tableDefault;
            mark.OtherDefault = otherTableDefault;
            mark.OtherPresent = otherTablePresent;
            mark.ProxyDefault = proxyDefault;
            mark.PacketDefault = packetDefault;
            mark.Duplicate = duplicateIdentity;
        }
        private void RequireTop(Checkpoint mark)
        {
            if (marks.Count == 0 || !ReferenceEquals(marks[^1], mark))
            {
                throw new InvalidOperationException("Native ordering scopes must finish in their original nested order.");
            }
        }
        internal void CommitMark(Checkpoint mark)
        {
            RequireTop(mark);
            marks.RemoveAt(marks.Count - 1);
            if (!HasMarks)
            {
                ClearUndo();
                RecycleBank(mark.Bank);
            }
        }
        internal void Publish(Checkpoint boundary, IReadOnlyList<Checkpoint> callbacks)
        {
            int first = marks.IndexOf(boundary);
            if (first < 0 || marks.Count != first + callbacks.Count + 1 || callbacks.Where((mark, index) => !ReferenceEquals(mark, marks[first + index + 1])).Any())
            {
                throw new InvalidOperationException("Native ordering publication cannot bypass an unfinished producer scope.");
            }
            PacketBank prior = boundary.Bank;
            if (first == 0)
            {
                ClearUndo();
            }
            for (int index = first; index < marks.Count; index++)
            {
                Remember(marks[index]);
            }
            if (first == 0)
            {
                RecycleBank(prior);
            }
        }
        internal void PublishKnownAncestorPrefix(Checkpoint boundary, IReadOnlyList<Checkpoint> callbacks)
        {
            int first = marks.IndexOf(boundary);
            int end = first + callbacks.Count + 1;
            if (first < 0 || marks.Count < end || callbacks.Where((mark, index) => !ReferenceEquals(mark, marks[first + index + 1])).Any())
            {
                throw new InvalidOperationException("Ancestor ordering publication lost its native producer bookmarks.");
            }
            //The inner producer must publish first; rebasing this ancestor never accepts an unfinished inner suffix.
            for (int index = end; index < marks.Count; index++)
            {
                if (!marks[index].Automatic && !RepresentsCurrentPrefix(marks[index]))
                {
                    throw new InvalidOperationException("Ancestor ordering publication requires every intervening native producer to freeze its current prefix.");
                }
            }
            for (int index = first; index < end; index++)
            {
                Remember(marks[index]);
            }
        }
        internal ImmutableArray<JournalEntry> PendingEdits(Checkpoint mark)
        {
            RequireTop(mark);
            return edits.Skip(mark.Edits).ToImmutableArray();
        }
        internal void RollbackMark(Checkpoint mark, bool remove = true)
        {
            RequireTop(mark);
            PacketBank prior = bank;
            bool changed = tableUndo.Count != mark.Tables || proxyUndo.Count != mark.ProxiesUndo || packetUndo.Count != mark.PacketsUndo || indexUndo.Count != mark.Indices || rangeUndo.Count != mark.Ranges || edits.Count != mark.Edits || !ReferenceEquals(table, mark.Table) || !ReferenceEquals(bank, mark.Bank) || !ReferenceEquals(otherTable, mark.Other) || proxies.Count != mark.ProxyCount || Packets.Count != mark.PacketCount || drawGeneration != mark.DrawGeneration || proxyCursor != mark.Cursor || tableBuffer != mark.Buffer || maximumPacketBytes != mark.Maximum || lastHeaderLink != mark.Header;
            for (int index = indexUndo.Count - 1; index >= mark.Indices; index--)
            {
                var entry = indexUndo[index];
                entry.Indices.Remove(entry.Key);
            }
            for (int index = rangeUndo.Count - 1; index >= mark.Ranges; index--)
            {
                var entry = rangeUndo[index];
                entry.Ranges.Remove(entry.Value);
            }
            for (int index = packetUndo.Count - 1; index >= mark.PacketsUndo; index--)
            {
                var entry = packetUndo[index];
                entry.Rows[entry.Index] = entry.Value;
            }
            for (int index = proxyUndo.Count - 1; index >= mark.ProxiesUndo; index--)
            {
                var entry = proxyUndo[index];
                proxies[entry.Index] = entry.Value;
            }
            for (int index = tableUndo.Count - 1; index >= mark.Tables; index--)
            {
                var entry = tableUndo[index];
                entry.Rows[entry.Index] = entry.Value;
            }
            indexUndo.Trim(mark.Indices);
            rangeUndo.Trim(mark.Ranges);
            packetUndo.Trim(mark.PacketsUndo);
            proxyUndo.Trim(mark.ProxiesUndo);
            Trim(tableUndo, mark.Tables);
            Trim(edits, mark.Edits);
            if (!ReferenceEquals(prior, mark.Bank))
            {
                collectionGeneration++;
            }
            table = mark.Table;
            otherTable = mark.Other;
            bank = mark.Bank;
            drawGeneration = mark.DrawGeneration;
            proxies.Trim(mark.ProxyCount);
            Packets.Trim(mark.PacketCount);
            proxyCursor = mark.Cursor;
            tableBuffer = mark.Buffer;
            maximumPacketBytes = mark.Maximum;
            lastHeaderLink = mark.Header;
            tableDefault = mark.TableDefault;
            otherTableDefault = mark.OtherDefault;
            otherTablePresent = mark.OtherPresent;
            proxyDefault = mark.ProxyDefault;
            packetDefault = mark.PacketDefault;
            duplicateIdentity = mark.Duplicate;
            if (changed)
            {
                version++;
            }
            if (remove)
            {
                marks.RemoveAt(marks.Count - 1);
            }
            else
            {
                Remember(mark);
            }
            if (!HasMarks)
            {
                ClearUndo();
                RecycleBank(prior);
            }
        }

        private void RecycleBank(PacketBank prior)
        {
            // Only a published or rolled-back outer boundary can retire a bank referenced by an undo suffix.
            if (!ReferenceEquals(prior, bank) && spareBank is null)
            {
                prior.Clear();
                spareBank = prior;
            }
        }

        private static void Trim<T>(List<T> values, int count)
        {
            if (values.Count > count)
            {
                values.RemoveRange(count, values.Count - count);
            }
        }
        private void ClearUndo()
        {
            tableUndo.Clear();
            proxyUndo.Clear();
            packetUndo.Clear();
            rangeUndo.Clear();
            indexUndo.Clear();
            edits.Clear();
        }
        internal NativeBattleLogicalOrderingData Capture()
        {
            if (HasMarks)
            {
                throw new InvalidOperationException("Native ordering capture requires its committed producer boundary.");
            }
            return new(tableDefault ? default : table.ToImmutableArray(), CaptureProxies(), CapturePackets(), proxyCursor, tableBuffer, !otherTablePresent ? null : otherTableDefault ? default(ImmutableArray<NativeBattlePrimitiveLink>) : otherTable!.ToImmutableArray());
        }

        private ImmutableArray<NativeBattleLogicalProxy> CaptureProxies()
        {
            if (proxyDefault)
            {
                return default;
            }
            ImmutableArray<NativeBattleLogicalProxy>.Builder result = ImmutableArray.CreateBuilder<NativeBattleLogicalProxy>(proxies.Count);
            for (int index = 0; index < proxies.Count; index++)
            {
                ProxySlot proxy = proxies[index];
                result.Add(proxy.Observation ?? proxy.Capture());
            }
            return result.MoveToImmutable();
        }

        private ImmutableArray<NativeBattleLogicalPacket> CapturePackets()
        {
            if (packetDefault)
            {
                return default;
            }
            ImmutableArray<NativeBattleLogicalPacket>.Builder result = ImmutableArray.CreateBuilder<NativeBattleLogicalPacket>(Packets.Count);
            for (int index = 0; index < Packets.Count; index++)
            {
                PacketSlot packet = Packets[index];
                result.Add(packet.Capture(drawGeneration));
            }
            return result.MoveToImmutable();
        }
    }

    private NativeBattleLogicalOrdering(Owner storage)
    {
        owner = storage;
        storage.Root = this;
    }
    private void ValidatePacketIdentityIndex()
    {
        RequireReadAccess();
        if (owner is not null)
        {
            owner.ValidatePacketIdentityIndex();
        }
        else if (snapshotDuplicateIdentity)
        {
            throw new ArgumentException("Native ordering has duplicate normalized packet identities.");
        }
    }
    internal IEnumerable<long> ReferencedSceneReplaySources()
    {
        RequireReadAccess();
        int proxyCount = ReadProxyCount();
        int allocated = ProxyCursor ?? proxyCount;
        if ((uint)allocated > (uint)proxyCount)
        {
            throw new InvalidDataException("Scene replay references require a bounded ordering proxy cursor.");
        }
        HashSet<int> visited = [];
        Queue<int> pending = new();
        HashSet<long> sources = [];
        foreach (NativeBattlePrimitiveLink link in Table)
        {
            Enqueue(link);
        }
        if (OtherTable is { } other)
        {
            foreach (NativeBattlePrimitiveLink link in other)
            {
                Enqueue(link);
            }
        }
        for (int index = 0; index < allocated; index++)
        {
            Enqueue(new(true, index));
        }
        while (pending.TryDequeue(out int index))
        {
            if (!TryReadPacket(ReadProxy(index).Packet, out PacketSlot packet))
            {
                continue;
            }
            if (packet.SceneCapture?.ReplaySourceId is long source)
            {
                sources.Add(source);
            }
            Enqueue(packet.Next);
        }
        return sources;

        void Enqueue(NativeBattlePrimitiveLink link)
        {
            if (!link.Proxy)
            {
                return;
            }
            if ((uint)link.Index >= (uint)proxyCount)
            {
                throw new InvalidDataException("Scene replay references reached a missing ordering proxy.");
            }
            if (visited.Add(link.Index))
            {
                pending.Enqueue(link.Index);
            }
        }
    }

    private int ReadProxyCount()
    {
        return owner?.ProxyRowCount ?? (snapshot!.Proxies.IsDefault ? 0 : snapshot.Proxies.Length);
    }

    private ProxySlot ReadProxy(int index)
    {
        return owner is not null ? owner.ReadProxy(index) : new(snapshot!.Proxies[index]);
    }

    private bool TryReadPacket(NativeBattlePrimitiveReference reference, out PacketSlot packet)
    {
        RequireReadAccess();
        if (owner is not null)
        {
            return owner.TryPacket(reference, out packet);
        }
        if (snapshotIndices!.TryGetValue(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference), out int index))
        {
            packet = new(snapshot!.Packets[index]);
            return true;
        }
        packet = default;
        return false;
    }
    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        Owner current = RequireOwner();
        if (transaction is not null)
        {
            throw new InvalidOperationException("Native ordering transaction views must publish before capture.");
        }
        return context.GetOrAddOwner(current, () => FromSnapshot(current.Capture()));
    }
    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        NativeBattleLogicalOrderingData frozen = RequireSnapshot();
        Owner restored = context.GetOrAddOwner(frozen, () => new Owner(context.Map(frozen)));
        return restored.Root ?? new NativeBattleLogicalOrdering(restored);
    }

    private static NativeBattleImmutableSlice<BattleSceneScreenPoint>? MatchingProjectedCoordinates(in PacketSlot packet, int word, int count)
    {
        if (packet.ProjectedCoordinates is not { } coordinates)
        {
            return null;
        }
        byte opcode = (byte)(packet.Words[word] >> 24);
        int corners = (opcode & 8) != 0 ? 4 : 3;
        bool gouraud = (opcode & 16) != 0;
        int stride = (gouraud ? 1 : 0) + 1 + ((opcode & 4) != 0 ? 1 : 0);
        int expected = corners * stride + (gouraud ? 0 : 1);
        if ((opcode & 0xE0) != 0x20 || coordinates.IsDefault || coordinates.Length != corners || word != 1 || count != expected || packet.Words.Length != count + 1 || (packet.Words[0] >> 24) != count)
        {
            return null;
        }
        for (int corner = 0; corner < corners; corner++)
        {
            int coordinateWord = word + 1 + corner * stride;
            BattleSceneScreenPoint point = coordinates[corner];
            uint packed = unchecked((ushort)point.X) | unchecked((uint)point.Y << 16);
            if (packet.Words[coordinateWord] != packed)
            {
                return null;
            }
        }
        return coordinates;
    }

    public ImmutableArray<NativeBattleLogicalCommand> Traverse(ImmutableArray<byte> wordLengths, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null, uint? initialOrderingAddress = null, Func<uint, NativeBattleAddressedOrderingRoot>? readAddressedRoot = null)
    {
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> references = TraverseReferences(wordLengths, readPhysical, initialOrderingAddress, readAddressedRoot);
        var commands = ImmutableArray.CreateBuilder<NativeBattleLogicalCommand>(references.Length);
        foreach (ref readonly NativeBattleLogicalCommandReference command in references)
        {
            commands.Add(command.ToCommand());
        }
        return commands.MoveToImmutable();
    }

    internal NativeBattleCommandPages<NativeBattleLogicalCommandReference> TraverseReferences(ImmutableArray<byte> wordLengths, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null, uint? initialOrderingAddress = null, Func<uint, NativeBattleAddressedOrderingRoot>? readAddressedRoot = null)
    {
        if (Table.IsDefault || Table.Length != NativeBattlePrimitiveOrdering.BattleTableLength || Proxies.IsDefault || Packets.IsDefault || wordLengths.IsDefault || wordLengths.Length != 256)
        {
            throw new InvalidDataException("Native logical OT requires its original4386 roots, proxy bank and command length table.");
        }
        ValidatePacketIdentityIndex();
        int proxyCount = ReadProxyCount();
        long generation = owner?.DrawGeneration ?? 0;
        // Native OT banks and proxy links have bounded identities; addressed roots retain their full keys.
        const int rootVisits = 2 * NativeBattlePrimitiveOrdering.BattleTableLength;
        const int boundedVisits = rootVisits + NativeBattlePrimitiveOrdering.ProxyCapacity;
        Span<ulong> visited = stackalloc ulong[(boundedVisits + 63) / 64];
        visited.Clear();
        HashSet<(byte Bank, bool Proxy, int Index)>? otherVisited = null;
        NativeBattleCommandPages<NativeBattleLogicalCommandReference>.Builder commands = new();
        NativeBattlePrimitiveLink cursor = new(false, initialOrderingAddress is null ? Table.Length - 1 : -1);
        uint? addressedRoot = initialOrderingAddress;
        int bucket = Table.Length - 1;
        byte rootBuffer = TableBuffer ?? throw new NotSupportedException("Native OT traversal requires its retained table bank identity.");
        while (cursor.Index >= 0 || addressedRoot is not null)
        {
            if (addressedRoot is uint rootAddress)
            {
                if (!(otherVisited ??= []).Add((2, false, unchecked((int)rootAddress))))
                {
                    throw new InvalidDataException("Native logical OT contains an addressed-root link cycle.");
                }
                NativeBattleAddressedOrderingRoot root = (readAddressedRoot ?? throw new NotSupportedException("Native private ordering requires its actual addressed DWORD roots."))(rootAddress);
                bucket = root.Bucket;
                addressedRoot = null;
                Follow(root.NextAddress);
                continue;
            }
            int visit = -1;
            if (cursor.Proxy && (uint)cursor.Index < NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                visit = rootVisits + cursor.Index;
            }
            else if (!cursor.Proxy && rootBuffer < 2 && (uint)cursor.Index < NativeBattlePrimitiveOrdering.BattleTableLength)
            {
                visit = rootBuffer * NativeBattlePrimitiveOrdering.BattleTableLength + cursor.Index;
            }
            bool repeated;
            if (visit >= 0)
            {
                ulong bit = 1UL << (visit & 63);
                repeated = (visited[visit >> 6] & bit) != 0;
                visited[visit >> 6] |= bit;
            }
            else
            {
                repeated = !(otherVisited ??= []).Add((cursor.Proxy ? byte.MaxValue : rootBuffer, cursor.Proxy, cursor.Index));
            }
            if (repeated)
            {
                throw new InvalidDataException("Native logical OT contains a shared-packet link cycle.");
            }
            if (!cursor.Proxy)
            {
                if (cursor.Index >= Table.Length)
                {
                    throw new InvalidDataException("Native logical OT has a missing table root.");
                }
                bucket = cursor.Index;
                NativeBattleOrderingList<NativeBattlePrimitiveLink> roots = rootBuffer == TableBuffer ? Table : OtherTable ?? throw new NotSupportedException("Native OT traversal reached an unknown alternate table.");
                cursor = roots[cursor.Index];
                continue;
            }
            if (cursor.Index >= proxyCount)
            {
                throw new InvalidDataException("Native logical OT has a missing proxy row.");
            }
            ProxySlot proxy = ReadProxy(cursor.Index);
            if (!TryReadPacket(proxy.Packet, out PacketSlot packet))
            {
                throw new NotSupportedException("Native logical OT reached an unobserved original packet owner.");
            }
            uint nextAddress;
            bool readsPhysicalBytes = packet.ReadsPhysicalBytes(generation);
            if (readsPhysicalBytes)
            {
                Func<NativeBattlePrimitiveReference, int, byte[]> reader = readPhysical ?? throw new NotSupportedException("Native retained packet alias requires its current physical arena bytes.");
                uint header = BinaryPrimitives.ReadUInt32LittleEndian(reader(packet.Reference, 4));
                byte[] bytes = reader(packet.Reference, checked((1 + (int)(header >> 24)) * 4));
                uint[] words = new uint[bytes.Length / 4];
                for (int index = 0; index < words.Length; index++)
                {
                    words[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 * index));
                }
                packet = packet with { Words = ImmutableCollectionsMarshal.AsImmutableArray(words) };
                nextAddress = (header & 0xFFFFFFu) | (uint)(proxy.NextHigh22 ?? throw new NotSupportedException("Native retained proxy lacks its actual high link BYTE22.")) << 24;
            }
            else
            {
                uint address = packet.AddressedNextAddress ?? LinkAddress(packet.Next, packet.NextTableBuffer ?? throw new NotSupportedException("Native packet link lacks its original OT bank identity."));
                nextAddress = (address & 0xFFFFFFu) | (uint)(proxy.NextHigh22 ?? throw new NotSupportedException("Native proxy lacks its actual high link BYTE22.")) << 24;
            }
            int remaining = (int)(packet.Words[0] >> 24);
            int word = 1;
            while (remaining > 0)
            {
                byte opcode = (byte)(packet.Words[word] >> 24);
                int count = opcode == 0 ? 1 : Math.Max((int)wordLengths[opcode], 1);
                if (count > remaining || word + count > packet.Words.Length)
                {
                    throw new InvalidDataException($"Native logical packet ends inside an original command: bank={packet.Reference.Bank}, buffer={packet.Reference.Buffer}, offset={packet.Reference.ByteOffset:X}, physical={readsPhysicalBytes}, bucket={bucket}, word={word}, opcode={opcode:X2}, count={count}, remaining={remaining}, words={string.Join(" ", packet.Words.ToImmutableArray().Select(value => value.ToString("X8")))}.");
                }
                if (opcode != 0)
                {
                    NativeBattleImmutableSlice<BattleSceneScreenPoint>? coordinates = MatchingProjectedCoordinates(packet, word, count);
                    if (!readsPhysicalBytes && initialOrderingAddress is null && rootBuffer == TableBuffer && word == 1 && count == remaining && count == packet.Words.Length - 1 &&
                        packet.Placement is { } placement && ReferenceEquals(proxy.Placement, placement) && proxy.PlacementRow == packet.PlacementRow &&
                        coordinates.HasValue == packet.ProjectedCoordinates.HasValue)
                    {
                        commands.Add(new(placement, packet.PlacementRow, bucket));
                    }
                    else
                    {
                        NativeBattleLogicalCommand literal = new(packet.Reference, word, opcode, packet.Words, count, proxy.Flags20, proxy.VertexPayload4, proxy.ModuleDraw) { OrderingBucket = bucket, ProjectedCoordinateSlice = coordinates, TextureSourceOwner = packet.TextureSourceOwner, ModelTextureOwner = packet.ModelTextureOwner, Pilot = packet.Pilot, PresentationActorId = packet.PresentationActorId, ScreenCoverage = packet.ScreenCoverage, SceneCapture = packet.SceneCapture };
                        commands.Add(literal);
                    }
                }
                remaining -= count;
                word += count;
            }
            Follow(nextAddress);
        }
        return commands.Freeze();

        void Follow(uint nextAddress)
        {
            if ((nextAddress & 0xFFFFFFu) == 0xFFFFFFu)
            {
                cursor = new(false, -1);
            }
            else if (nextAddress >= 0x1C48828u && nextAddress < 0x1CA8828u && (nextAddress - 0x1C48828u) % 24 == 0)
            {
                cursor = new(true, (int)((nextAddress - 0x1C48828u) / 24));
            }
            else if (nextAddress >= 0x1D8E058u && nextAddress < 0x1D96968u && (nextAddress - 0x1D8E058u) % 4 == 0)
            {
                uint relative = nextAddress - 0x1D8E058u;
                rootBuffer = (byte)(relative / 17544);
                cursor = new(false, (int)(relative % 17544 / 4));
            }
            else if (readAddressedRoot is not null && nextAddress >= NativeBattleEffectNodeMemory.ArenaAddress && nextAddress - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - 4 && (nextAddress & 3) == 0)
            {
                addressedRoot = nextAddress;
                cursor = new(false, -1);
            }
            else
            {
                throw new NotSupportedException("Native packet alias produced a link outside the observed proxy and OT banks.");
            }
        }
    }
}

public sealed record NativeBattlePrimitiveTables(ImmutableArray<byte> WordLengths, ImmutableArray<byte> FlaggedCommands, ImmutableArray<uint> PreprocessingHandlers, ImmutableArray<uint> FirstHandlers, ImmutableArray<uint> SecondHandlers)
{
    public void Validate()
    {
        if (WordLengths.IsDefault || WordLengths.Length != 256 || FlaggedCommands.IsDefault || FlaggedCommands.Length != 256 || PreprocessingHandlers.IsDefault || PreprocessingHandlers.Length != 256 || FirstHandlers.IsDefault || FirstHandlers.Length != 256 || SecondHandlers.IsDefault || SecondHandlers.Length != 256)
        {
            throw new InvalidDataException("Native45D080 requires its five original256-entry command/handler tables.");
        }
    }
}

public interface INativeBattleLogicalExecutionContext
{
    uint LiveMode { get; }
    uint LiveDebug { get; }
    bool HardwareEnabled { get; }
    NativeSoftwarePrimitiveTable SoftwareTable { get; }
    NativeBattlePcTextureWindowCache? Cache { get; set; }
    void ValidateModelTextureOwner(NativeBattleLogicalCommandReference command)
    {
        if (command.ModelTextureOwner is not null)
        {
            throw new NotSupportedException("This battle primitive execution context does not support model texture ownership.");
        }
    }
    NativeBattlePcTextureWindowCache? GetTextureCache(NativeBattleLogicalCommandReference command) => Cache;
    void SetTextureCache(NativeBattleLogicalCommandReference command, NativeBattlePcTextureWindowCache? cache)
    {
        Cache = cache;
    }
    void ExecuteSoftware(NativeBattleLogicalCommandReference command);
    void ExecuteNormal(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass);
    NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode);
    NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalResolvedCommand command) => command;
    NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass, NativeBattlePcTextureWindowSelection? selection)
    {
        return RetainTexturePixels(new(command, selection is null ? mode : mode with { TextureSelection = selection }, pass));
    }
    void BeginPass(byte pass)
    {
    }
    void EndPass(byte pass)
    {
    }
    void BeginOrdering()
    {
    }
    void FlushSoftwareImage()
    {
    }
    void EndBatch();
}

public static class NativeBattleLogicalPasses
{
    public static (NativeBattlePrimitiveDrawMode Final, NativeBattleResolvedCommandList Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache = null, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        tables.Validate();
        return ExecuteAdmitted(ordering, tables, initial, drawFlags, cache, null, readPhysical);
    }

    public static (NativeBattlePrimitiveDrawMode Final, NativeBattleResolvedCommandList Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, INativeBattleLogicalExecutionContext context, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        tables.Validate();
        return ExecuteAdmitted(ordering, tables, initial, 0, context.Cache, context, readPhysical);
    }

    internal static (NativeBattlePrimitiveDrawMode Final, NativeBattleResolvedCommandList Commands, NativeBattlePcTextureWindowCache? Cache) ExecuteAdmitted(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache, INativeBattleLogicalExecutionContext? context, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical)
    {
        if (ordering.ProxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
        {
            context?.EndBatch();
            return (initial, [], context is null ? cache : context.Cache);
        }
        context?.BeginOrdering();
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> commands = ordering.TraverseReferences(tables.WordLengths, readPhysical);
        foreach (ref readonly NativeBattleLogicalCommandReference command in commands)
        {
            if (command.ModelTextureOwner is { } owner)
            {
                owner.RequireActorId();
                if (context is null)
                {
                    throw new NotSupportedException("Model-owned battle primitives require a material-aware execution context.");
                }
                context.ValidateModelTextureOwner(command);
            }
        }
        NativeBattlePrimitiveDrawMode mode = context?.SynchronizeDrawMode(initial) ?? initial;
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder executed = new();
        ImmutableArray<uint>[] passes = [tables.PreprocessingHandlers, tables.FirstHandlers, tables.SecondHandlers];
        for (byte pass = 0; pass < passes.Length; pass++)
        {
            if (pass == 1 && context?.HardwareEnabled == false)
            {
                break;
            }
            context?.BeginPass(pass);
            SetCache(BeginTexturePass(GetCache(), pass));
            foreach (ref readonly NativeBattleLogicalCommandReference command in commands)
            {
                // Captured pixels keep native consumer state while their image remains owned by the host renderer.
                byte opcode = command.Opcode;
                NativeBattleForeignSurface? foreignSurface = command.Pilot.ForeignSurface;
                bool capturedSurface = foreignSurface?.Material is NativeBattleArenaCaptureMaterial;
                if (foreignSurface is not null && !capturedSurface)
                {
                    // A host material participates in ordering and depth, but owns no native page, palette or software command.
                    bool enabled = tables.FlaggedCommands[opcode] == 0 || ((context?.LiveMode ?? drawFlags) & 1) != 0;
                    byte materialPass = (opcode & 2) != 0 ? (byte)2 : (byte)1;
                    if (pass == materialPass && enabled && passes[pass][opcode] != 0 && context?.HardwareEnabled != false)
                    {
                        NativeBattlePrimitiveDrawMode materialMode = ClearTextureSelection(mode);
                        if (materialPass == 2 && (opcode & 0xE4) == 0x24)
                        {
                            int pageWord = (opcode & 16) != 0 ? 5 : 4;
                            materialMode = materialMode with { TexturePage = (ushort)(command.WordSpan[pageWord] >> 16) };
                        }
                        executed.Add(new(command, materialMode, pass));
                    }
                    continue;
                }
                if (!capturedSurface && pass == 0 && context is not null && (context.LiveMode & 2) != 0 && context.SoftwareTable.Handler(opcode) != 0)
                {
                    context.ExecuteSoftware(command);
                    mode = context.SynchronizeDrawMode(mode);
                }
                bool category = tables.FlaggedCommands[opcode] != 0;
                uint liveMode = context?.LiveMode ?? drawFlags;
                bool hardware = pass != 0 || context?.HardwareEnabled != false;
                if (passes[pass][opcode] != 0 && (!category || (liveMode & 1) != 0 && hardware))
                {
                    mode = mode.ExecuteWordSpan(opcode, command.WordSpan);
                    mode = ClearTextureSelection(mode);
                    if (context is not null)
                    {
                        context.ExecuteNormal(command, mode, pass);
                        mode = ClearTextureSelection(context.SynchronizeDrawMode(mode));
                    }
                    NativeBattlePcTextureWindowSelection? selection = null;
                    if ((context is null ? cache : context.GetTextureCache(command)) is { } currentCache)
                    {
                        (selection, currentCache) = SelectTexture(command, mode, pass, currentCache);
                        if (context is null)
                        {
                            cache = currentCache;
                        }
                        else
                        {
                            context.SetTextureCache(command, currentCache);
                        }
                    }
                    executed.Add(context is null ? new(command, selection is null ? mode : mode with { TextureSelection = selection }, pass) : context.RetainTexturePixels(command, mode, pass, selection));
                }
                if (!capturedSurface && pass == 0 && category && (liveMode & 1) != 0 && context is not null)
                {
                    for (int delivery = 0; delivery < 2; delivery++)
                    {
                        if (context.LiveDebug != 0 && context.SoftwareTable.Handler(opcode) != 0)
                        {
                            context.ExecuteSoftware(command);
                            mode = context.SynchronizeDrawMode(mode);
                        }
                    }
                }
            }
            SetCache(EndTexturePass(GetCache(), pass));
            context?.EndPass(pass);
        }
        context?.EndBatch();
        return (mode, new(executed.Freeze()), GetCache());

        NativeBattlePcTextureWindowCache? GetCache() => context is null ? cache : context.Cache;
        void SetCache(NativeBattlePcTextureWindowCache? value)
        {
            if (context is null)
            {
                cache = value;
            }
            else
            {
                context.Cache = value;
            }
        }
    }

    /// <summary>Apply PC resource preparation and selection to an executed OT; also supports a declared cache-reset migration without advancing simulation.</summary>
    public static (NativeBattleResolvedCommandList Commands, NativeBattlePcTextureWindowCache Cache) SelectTextures(NativeBattleResolvedCommandList commands, NativeBattlePcTextureWindowCache cache)
    {
        if (commands.IsDefault)
        {
            throw new InvalidDataException("Native texture selection requires the three actual executed OT passes.");
        }
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in commands)
        {
            if (command.Pass > 2)
            {
                throw new InvalidDataException("Native texture selection requires the three actual executed OT passes.");
            }
        }
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in commands)
        {
            if (command.Command.ModelTextureOwner is not null)
            {
                throw new NotSupportedException("Standalone texture selection has no model texture bank.");
            }
        }
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder selected = new();
        for (byte pass = 0; pass < 3; pass++)
        {
            cache = BeginTexturePass(cache, pass)!;
            foreach (ref readonly NativeBattleLogicalResolvedCommand command in commands)
            {
                if (command.Pass != pass)
                {
                    continue;
                }
                NativeBattlePrimitiveDrawMode mode = ClearTextureSelection(command.DrawMode);
                (NativeBattlePcTextureWindowSelection? selection, cache) = SelectTexture(command.Command, mode, pass, cache);
                if (selection is not null)
                {
                    mode = mode with { TextureSelection = selection };
                }
                selected.Add(ReferenceEquals(command.DrawMode, mode) ? command : command with { DrawMode = mode });
            }
            cache = EndTexturePass(cache, pass)!;
        }
        return (new(selected.Freeze()), cache);
    }

    private static NativeBattlePcTextureWindowCache? BeginTexturePass(NativeBattlePcTextureWindowCache? cache, byte pass) => pass == 1 ? cache?.Prepare464BD0().Cache : cache;
    private static NativeBattlePcTextureWindowCache? EndTexturePass(NativeBattlePcTextureWindowCache? cache, byte pass) => pass == 1 ? cache?.Flush465930() : cache;

    private static (NativeBattlePcTextureWindowSelection? Selection, NativeBattlePcTextureWindowCache Cache) SelectTexture(NativeBattleLogicalCommandReference command, NativeBattlePrimitiveDrawMode mode, byte pass, NativeBattlePcTextureWindowCache cache)
    {
        if (command.Pilot.ForeignSurface is not null)
        {
            return (null, cache);
        }
        bool sprite = command.Opcode is 0x64 or 0x66;
        if ((command.Opcode & 0xE4) != 0x24 && !sprite)
        {
            return (null, cache);
        }
        ushort page = mode.TexturePage ?? throw new NotSupportedException("Native textured pass requires its executed texture page.");
        ushort clut = unchecked((ushort)(command.WordSpan[2] >> 16));
        if (pass == 0)
        {
            return (null, sprite ? cache.PreprocessSprite463120(command.Opcode, page, clut) : cache.Preprocess462F90(page, clut));
        }
        uint window = mode.TextureWindowBits ?? throw new NotSupportedException("Native465CE0 requires its retained E2 window bits.");
        var resolved = sprite ? cache.ResolveSprite462DF0Core(command.Opcode, page, clut, window) : cache.Resolve465CE0Core(page, clut, window);
        return (resolved.Selection, resolved.Cache);
    }

    private static NativeBattlePrimitiveDrawMode ClearTextureSelection(NativeBattlePrimitiveDrawMode mode)
    {
        return mode.TextureSelection is null && mode.TexturePixelSnapshot is null && mode.SceneCaptureSample is null && mode.SceneCopyPalette is null ? mode : mode with { TextureSelection = null, TexturePixelSnapshot = null, SceneCaptureSample = null, SceneCopyPalette = null };
    }
}
