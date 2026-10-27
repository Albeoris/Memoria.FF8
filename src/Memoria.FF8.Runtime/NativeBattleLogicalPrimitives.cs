using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    // Presentation coordinates belong to the current physical packet, including all of its proxy aliases.
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
    public string? TextureSourceOwner { get; init; }
}
public sealed record NativeBattleLogicalProxy(NativeBattlePrimitiveReference Packet, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public byte? NextHigh22 { get; init; }
}
public sealed record NativeBattleLogicalCommand(NativeBattlePrimitiveReference Packet, int WordOffset, byte Opcode, ImmutableArray<uint> Words, ushort Flags20, ImmutableArray<int> VertexPayload4, int? ModuleDraw)
{
    public int? OrderingBucket { get; init; }
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
    public string? TextureSourceOwner { get; init; }
}
public sealed record NativeBattleLogicalResolvedCommand(NativeBattleLogicalCommand Command, NativeBattlePrimitiveDrawMode DrawMode, byte Pass);

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

    public NativeBattleLogicalOrdering ResetAfterDraw45D080()
    {
        RequireOwner().ResetAfterDraw();
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

    public NativeBattleLogicalOrdering Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null)
    {
        using Builder builder = new(this);
        builder.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner);
        return builder.ToImmutable();
    }

    public (NativeBattleLogicalOrdering Ordering, uint? ProxyAddress) PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
    {
        using Builder builder = new(this);
        int index = builder.ProxyCount;
        builder.PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw);
        return (builder.ToImmutable(), index < NativeBattlePrimitiveOrdering.ProxyCapacity ? 0x1C48828u + (uint)(24 * index) : null);
    }

    private static void ValidateInsertion(int tableLength, int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ImmutableArray<int> vertexPayload4)
    {
        if ((uint)bucket >= tableLength || string.IsNullOrWhiteSpace(reference.Bank) || reference.Buffer > 1 || reference.ByteOffset < 0 || (reference.ByteOffset & 3) != 0 || words.IsDefaultOrEmpty || words.Length < 1 + (words[0] >> 24) || vertexPayload4.IsDefault || vertexPayload4.Length is not (0 or 4))
        {
            throw new InvalidDataException($"Native logical primitive insertion lacks its original bounded packet or bank identity: bucket {bucket}/{tableLength}, bank '{reference.Bank}', buffer {reference.Buffer}, byte offset {reference.ByteOffset}, words {(words.IsDefault ? -1 : words.Length)}, header {(words.IsDefaultOrEmpty ? 0 : words[0]):X8}, vertex payload {(vertexPayload4.IsDefault ? -1 : vertexPayload4.Length)}.");
        }
    }

    internal sealed class Delta(object identity, long expectedVersion, ImmutableArray<Action> edits)
    {
        internal object Identity { get; } = identity;
        internal long ExpectedVersion { get; } = expectedVersion;
        internal ImmutableArray<Action> Edits { get; } = edits;
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
        public void Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null)
        {
            RequireActive();
            storage.Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner);
        }
        public void PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
        {
            RequireActive();
            storage.PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw);
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
            ImmutableArray<Action> edits = storage.PendingEdits(current);
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
            ImmutableArray<Action> edits = storage.PendingEdits(boundary);
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
            foreach (Action edit in delta.Edits)
            {
                edit();
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
        internal List<NativeBattleLogicalProxy> Proxies = null!;
        internal List<NativeBattleLogicalPacket> Packets = null!;
        internal Dictionary<NativeBattlePrimitiveReference, int> PacketIndices = null!;
        internal Dictionary<(string Bank, byte Buffer), PacketRangeIndex> PacketRanges = null!;
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
        private List<NativeBattleLogicalProxy> proxies;
        private List<NativeBattleLogicalPacket> packets;
        private Dictionary<NativeBattlePrimitiveReference, int> packetIndices;
        private Dictionary<(string Bank, byte Buffer), PacketRangeIndex> packetRanges = [];
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
        private readonly List<(List<NativeBattleLogicalProxy> Rows, int Index, NativeBattleLogicalProxy Value)> proxyUndo = [];
        private readonly List<(List<NativeBattleLogicalPacket> Rows, int Index, NativeBattleLogicalPacket Value)> packetUndo = [];
        private readonly List<(PacketRangeIndex Ranges, (int Offset, int Index) Value)> rangeUndo = [];
        private readonly List<(Dictionary<NativeBattlePrimitiveReference, int> Indices, NativeBattlePrimitiveReference Key)> indexUndo = [];
        private readonly List<Action> edits = [];
        internal bool HasMarks => marks.Count != 0;
        private int ProxyCount => proxyCursor!.Value;
        private uint LastHeaderLink => lastHeaderLink;

        internal Owner(NativeBattleLogicalOrderingData source)
        {
            tableDefault = source.Table.IsDefault;
            proxyDefault = source.Proxies.IsDefault;
            packetDefault = source.Packets.IsDefault;
            table = tableDefault ? [] : source.Table.ToList();
            proxies = proxyDefault ? [] : source.Proxies.ToList();
            packets = packetDefault ? [] : source.Packets.ToList();
            otherTablePresent = source.OtherTable.HasValue;
            otherTableDefault = source.OtherTable is { } savedOther && savedOther.IsDefault;
            otherTable = source.OtherTable is { } other ? other.IsDefault ? [] : other.ToList() : null;
            proxyCursor = source.ProxyCursor;
            tableBuffer = source.TableBuffer;
            packetIndices = new(packets.Count);
            for (int index = 0; index < packets.Count; index++)
            {
                duplicateIdentity |= !packetIndices.TryAdd(NativeBattlePrimitiveProcess.NormalizeCommonReference(packets[index].Reference), index);
                RegisterPacketRange(packets[index], index);
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
        internal NativeBattleOrderingList<NativeBattleLogicalProxy> ProxyView(Action? validation) => proxyDefault ? default : new(proxies, validation, this, collectionGeneration);
        internal NativeBattleOrderingList<NativeBattleLogicalPacket> PacketView(Action? validation) => packetDefault ? default : new(packets, validation, this, collectionGeneration);
        internal NativeBattleOrderingList<NativeBattlePrimitiveLink>? OtherView(Action? validation) => !otherTablePresent ? null : otherTableDefault ? default(NativeBattleOrderingList<NativeBattlePrimitiveLink>) : new(otherTable!, validation, this, collectionGeneration);
        public void ValidateBorrow(long generation)
        {
            if (generation != collectionGeneration)
            {
                throw new InvalidOperationException("Native ordering collection borrow was invalidated by a container reset.");
            }
        }
        internal bool TryPacket(NativeBattlePrimitiveReference reference, [NotNullWhen(true)] out NativeBattleLogicalPacket? packet)
        {
            if (packetIndices.TryGetValue(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference), out int index))
            {
                packet = packets[index];
                return true;
            }
            packet = null;
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
                if (operationDepth == 1)
                {
                    version++;
                    if (HasMarks)
                    {
                        edits.Add(replay);
                    }
                }
            }
            finally
            {
                operationDepth--;
            }
        }
        internal void InvalidatePacketRange(NativeBattlePrimitiveReference reference, int length) => Run(() => InvalidatePacketRangeCore(reference, length), () => InvalidatePacketRange(reference, length));
        internal void Prepend(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates, string? textureSourceOwner)
        {
            Run(() => PrependCore(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner), () => Prepend(bucket, reference, words, flags20, vertexPayload4, moduleDraw, projectedCoordinates, textureSourceOwner));
        }
        internal void PrependAddressed(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw)
        {
            Run(() => PrependAddressedCore(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw), () => PrependAddressed(previousAddress, reference, words, flags20, vertexPayload4, moduleDraw));
        }
        private void InvalidatePacketRangeCore(NativeBattlePrimitiveReference reference, int length)
        {
            reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
            if (length == 0 || !packetRanges.TryGetValue((reference.Bank, reference.Buffer), out PacketRangeIndex? ranges))
            {
                return;
            }
            int minimum = Math.Max(0, reference.ByteOffset - maximumPacketBytes + 1);
            int maximum = checked(reference.ByteOffset + length - 1);
            for (PacketRangeIndex.Node? node = ranges.FirstAtOrAfter((minimum, int.MinValue)); node is not null && node.Key.Offset <= maximum; node = ranges.Next(node))
            {
                int index = node.Key.Index;
                NativeBattleLogicalPacket packet = packets[index];
                NativeBattlePrimitiveReference physical = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
                if ((!packet.RequiresPhysicalRead || packet.ProjectedCoordinates is not null || packet.TextureSourceOwner is not null) && physical.Bank == reference.Bank && physical.Buffer == reference.Buffer && physical.ByteOffset < reference.ByteOffset + length && reference.ByteOffset < physical.ByteOffset + packet.Words.Length * 4)
                {
                    if (HasMarks)
                    {
                        packetUndo.Add((packets, index, packet));
                    }
                    packets[index] = packet with { RequiresPhysicalRead = true, ProjectedCoordinates = null, TextureSourceOwner = null };
                }
            }
        }

        private void RegisterPacketRange(NativeBattleLogicalPacket packet, int index)
        {
            if (packet.RequiresPhysicalRead && packet.ProjectedCoordinates is null && packet.TextureSourceOwner is null)
            {
                return;
            }
            NativeBattlePrimitiveReference reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
            var key = (reference.Bank, reference.Buffer);
            if (!packetRanges.TryGetValue(key, out PacketRangeIndex? ranges))
            {
                ranges = new();
                packetRanges.Add(key, ranges);
            }
            if (ranges.Add((reference.ByteOffset, index)) && HasMarks)
            {
                rangeUndo.Add((ranges, (reference.ByteOffset, index)));
            }
            maximumPacketBytes = Math.Max(maximumPacketBytes, checked(packet.Words.Length * 4));
        }

        private void PrependCore(int bucket, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null)
        {
            ValidateInsertion(table.Count, bucket, reference, words, vertexPayload4);
            if (ProxyCount >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            int index = ProxyCount;
            AppendProxy(reference, words, flags20, vertexPayload4, moduleDraw, table[bucket], tableBuffer!.Value, LinkAddress(table[bucket], tableBuffer!.Value), false, projectedCoordinates, textureSourceOwner);
            if (HasMarks)
            {
                tableUndo.Add((table, bucket, table[bucket]));
            }
            table[bucket] = new(true, index);
        }

        private void PrependAddressedCore(uint previousAddress, NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw = null)
        {
            ValidateInsertion(1, 0, reference, words, vertexPayload4);
            if (ProxyCount >= NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                return;
            }
            AppendProxy(reference, words, flags20, vertexPayload4, moduleDraw, new(false, -1), 0, previousAddress, true);
        }

        private void AppendProxy(NativeBattlePrimitiveReference reference, ImmutableArray<uint> words, ushort flags20, ImmutableArray<int> vertexPayload4, int? moduleDraw, NativeBattlePrimitiveLink next, byte nextBuffer, uint previousAddress, bool physicalRead, ImmutableArray<BattleSceneScreenPoint>? projectedCoordinates = null, string? textureSourceOwner = null)
        {
            InvalidatePacketRange(reference, words.Length * 4);
            lastHeaderLink = previousAddress;
            NativeBattleLogicalPacket packet = new(reference, words, next) { NextTableBuffer = nextBuffer, RequiresPhysicalRead = physicalRead, ProjectedCoordinates = projectedCoordinates, TextureSourceOwner = textureSourceOwner };
            NativeBattlePrimitiveReference identity = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
            if (packetIndices.TryGetValue(identity, out int existing))
            {
                if (HasMarks)
                {
                    packetUndo.Add((packets, existing, packets[existing]));
                }
                packets[existing] = packet;
                RegisterPacketRange(packet, existing);
            }
            else
            {
                if (HasMarks)
                {
                    indexUndo.Add((packetIndices, identity));
                }
                packetIndices.Add(identity, packets.Count);
                RegisterPacketRange(packet, packets.Count);
                packets.Add(packet);
            }
            NativeBattleLogicalProxy proxy = new(reference, flags20, vertexPayload4, moduleDraw) { NextHigh22 = (byte)(LastHeaderLink >> 24) };
            if (ProxyCount < proxies.Count)
            {
                if (HasMarks)
                {
                    proxyUndo.Add((proxies, ProxyCount, proxies[ProxyCount]));
                }
                proxies[ProxyCount] = proxy;
            }
            else
            {
                proxies.Add(proxy);
            }
            proxyCursor++;
        }


        internal void ResetAfterDraw() => Run(ResetAfterDrawCore, ResetAfterDraw);
        private void ResetAfterDrawCore()
        {
            for (int index = 0; index < packets.Count; index++)
            {
                NativeBattleLogicalPacket packet = packets[index];
                if (!packet.RequiresPhysicalRead)
                {
                    if (HasMarks)
                    {
                        packetUndo.Add((packets, index, packet));
                    }
                    packets[index] = packet with { RequiresPhysicalRead = true };
                }
            }
            proxyCursor = 0;
        }
        internal void ClearRoots() => Run(ClearRootsCore, ClearRoots);
        private void ClearRootsCore()
        {
            bool otherEmpty = otherTablePresent && !otherTableDefault && otherTable is { Count: NativeBattlePrimitiveOrdering.BattleTableLength } && otherTable.Select((link, index) => !link.Proxy && link.Index == index - 1).All(empty => empty);
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
            if (otherEmpty)
            {
                collectionGeneration++;
                //A real native clear may discard these unreachable packet identities. Keep rollback containers private.
                if (HasMarks)
                {
                    packets = [];
                    packetIndices = [];
                    packetRanges = [];
                }
                else
                {
                    packets.Clear();
                    packetIndices.Clear();
                    packetRanges.Clear();
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
            mark.Proxies = proxies;
            mark.Packets = packets;
            mark.PacketIndices = packetIndices;
            mark.PacketRanges = packetRanges;
            mark.ProxyCount = proxies.Count;
            mark.PacketCount = packets.Count;
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
            }
        }
        internal void Publish(Checkpoint boundary, IReadOnlyList<Checkpoint> callbacks)
        {
            int first = marks.IndexOf(boundary);
            if (first < 0 || marks.Count != first + callbacks.Count + 1 || callbacks.Where((mark, index) => !ReferenceEquals(mark, marks[first + index + 1])).Any())
            {
                throw new InvalidOperationException("Native ordering publication cannot bypass an unfinished producer scope.");
            }
            if (first == 0)
            {
                ClearUndo();
            }
            for (int index = first; index < marks.Count; index++)
            {
                Remember(marks[index]);
            }
        }
        internal ImmutableArray<Action> PendingEdits(Checkpoint mark)
        {
            RequireTop(mark);
            return edits.Skip(mark.Edits).ToImmutableArray();
        }
        internal void RollbackMark(Checkpoint mark, bool remove = true)
        {
            RequireTop(mark);
            bool changed = tableUndo.Count != mark.Tables || proxyUndo.Count != mark.ProxiesUndo || packetUndo.Count != mark.PacketsUndo || indexUndo.Count != mark.Indices || rangeUndo.Count != mark.Ranges || edits.Count != mark.Edits || !ReferenceEquals(table, mark.Table) || !ReferenceEquals(packets, mark.Packets) || !ReferenceEquals(otherTable, mark.Other) || proxies.Count != mark.ProxyCount || packets.Count != mark.PacketCount || proxyCursor != mark.Cursor || tableBuffer != mark.Buffer || maximumPacketBytes != mark.Maximum || lastHeaderLink != mark.Header;
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
                entry.Rows[entry.Index] = entry.Value;
            }
            for (int index = tableUndo.Count - 1; index >= mark.Tables; index--)
            {
                var entry = tableUndo[index];
                entry.Rows[entry.Index] = entry.Value;
            }
            Trim(indexUndo, mark.Indices);
            Trim(rangeUndo, mark.Ranges);
            Trim(packetUndo, mark.PacketsUndo);
            Trim(proxyUndo, mark.ProxiesUndo);
            Trim(tableUndo, mark.Tables);
            Trim(edits, mark.Edits);
            table = mark.Table;
            otherTable = mark.Other;
            proxies = mark.Proxies;
            packets = mark.Packets;
            packetIndices = mark.PacketIndices;
            packetRanges = mark.PacketRanges;
            Trim(proxies, mark.ProxyCount);
            Trim(packets, mark.PacketCount);
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
            return new(tableDefault ? default : table.ToImmutableArray(), proxyDefault ? default : proxies.ToImmutableArray(), packetDefault ? default : packets.ToImmutableArray(), proxyCursor, tableBuffer, !otherTablePresent ? null : otherTableDefault ? default(ImmutableArray<NativeBattlePrimitiveLink>) : otherTable!.ToImmutableArray());
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
    private bool TryReadPacket(NativeBattlePrimitiveReference reference, [NotNullWhen(true)] out NativeBattleLogicalPacket? packet)
    {
        RequireReadAccess();
        if (owner is not null)
        {
            return owner.TryPacket(reference, out packet);
        }
        if (snapshotIndices!.TryGetValue(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference), out int index))
        {
            packet = snapshot!.Packets[index];
            return true;
        }
        packet = null;
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

    private static ImmutableArray<BattleSceneScreenPoint>? MatchingProjectedCoordinates(NativeBattleLogicalPacket packet, int word, int count)
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
        if (Table.IsDefault || Table.Length != NativeBattlePrimitiveOrdering.BattleTableLength || Proxies.IsDefault || Packets.IsDefault || wordLengths.IsDefault || wordLengths.Length != 256)
        {
            throw new InvalidDataException("Native logical OT requires its original4386 roots, proxy bank and command length table.");
        }
        ValidatePacketIdentityIndex();
        // Native OT banks and proxy links have bounded identities; addressed roots retain their full keys.
        const int rootVisits = 2 * NativeBattlePrimitiveOrdering.BattleTableLength;
        const int boundedVisits = rootVisits + NativeBattlePrimitiveOrdering.ProxyCapacity;
        Span<ulong> visited = stackalloc ulong[(boundedVisits + 63) / 64];
        visited.Clear();
        HashSet<(byte Bank, bool Proxy, int Index)>? otherVisited = null;
        ImmutableArray<NativeBattleLogicalCommand>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleLogicalCommand>();
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
            if (cursor.Index >= Proxies.Length)
            {
                throw new InvalidDataException("Native logical OT has a missing proxy row.");
            }
            NativeBattleLogicalProxy proxy = Proxies[cursor.Index];
            if (!TryReadPacket(proxy.Packet, out NativeBattleLogicalPacket? packet))
            {
                throw new NotSupportedException("Native logical OT reached an unobserved original packet owner.");
            }
            uint nextAddress;
            if (packet.RequiresPhysicalRead)
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
                uint address = LinkAddress(packet.Next, packet.NextTableBuffer ?? throw new NotSupportedException("Native packet link lacks its original OT bank identity."));
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
                    throw new InvalidDataException($"Native logical packet ends inside an original command: bank={packet.Reference.Bank}, buffer={packet.Reference.Buffer}, offset={packet.Reference.ByteOffset:X}, physical={packet.RequiresPhysicalRead}, bucket={bucket}, word={word}, opcode={opcode:X2}, count={count}, remaining={remaining}, words={string.Join(" ", packet.Words.Select(value => value.ToString("X8")))}.");
                }
                if (opcode != 0)
                {
                    commands.Add(new(packet.Reference, word, opcode, packet.Words.Slice(word, count), proxy.Flags20, proxy.VertexPayload4, proxy.ModuleDraw) { OrderingBucket = bucket, ProjectedCoordinates = MatchingProjectedCoordinates(packet, word, count), TextureSourceOwner = packet.TextureSourceOwner });
                }
                remaining -= count;
                word += count;
            }
            Follow(nextAddress);
        }
        return commands.ToImmutable();

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
    void ExecuteSoftware(NativeBattleLogicalCommand command);
    void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass);
    NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode);
    NativeBattleLogicalResolvedCommand RetainTexturePixels(NativeBattleLogicalResolvedCommand command) => command;
    void BeginPass(byte pass)
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
    public static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache = null, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        return ExecuteCore(ordering, tables, initial, drawFlags, cache, null, readPhysical);
    }

    public static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) Execute(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, INativeBattleLogicalExecutionContext context, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExecuteCore(ordering, tables, initial, 0, context.Cache, context, readPhysical);
    }

    private static (NativeBattlePrimitiveDrawMode Final, ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache? Cache) ExecuteCore(NativeBattleLogicalOrdering ordering, NativeBattlePrimitiveTables tables, NativeBattlePrimitiveDrawMode initial, uint drawFlags, NativeBattlePcTextureWindowCache? cache, INativeBattleLogicalExecutionContext? context, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical)
    {
        tables.Validate();
        if (ordering.ProxyCursor >= NativeBattlePrimitiveOrdering.ProxyCapacity)
        {
            context?.EndBatch();
            return (initial, [], context is null ? cache : context.Cache);
        }
        context?.BeginOrdering();
        ImmutableArray<NativeBattleLogicalCommand> commands = ordering.Traverse(tables.WordLengths, readPhysical);
        NativeBattlePrimitiveDrawMode mode = context?.SynchronizeDrawMode(initial) ?? initial;
        ImmutableArray<NativeBattleLogicalResolvedCommand>.Builder executed = ImmutableArray.CreateBuilder<NativeBattleLogicalResolvedCommand>();
        ImmutableArray<uint>[] passes = [tables.PreprocessingHandlers, tables.FirstHandlers, tables.SecondHandlers];
        for (byte pass = 0; pass < passes.Length; pass++)
        {
            if (pass == 1 && context?.HardwareEnabled == false)
            {
                break;
            }
            context?.BeginPass(pass);
            SetCache(BeginTexturePass(GetCache(), pass));
            foreach (NativeBattleLogicalCommand command in commands)
            {
                if (pass == 0 && context is not null && (context.LiveMode & 2) != 0 && context.SoftwareTable.Handler(command.Opcode) != 0)
                {
                    context.ExecuteSoftware(command);
                    mode = context.SynchronizeDrawMode(mode);
                }
                bool category = tables.FlaggedCommands[command.Opcode] != 0;
                uint liveMode = context?.LiveMode ?? drawFlags;
                bool hardware = pass != 0 || context?.HardwareEnabled != false;
                if (passes[pass][command.Opcode] != 0 && (!category || (liveMode & 1) != 0 && hardware))
                {
                    mode = mode.ExecuteWords(command.Opcode, command.Words);
                    mode = ClearTextureSelection(mode);
                    if (context is not null)
                    {
                        context.ExecuteNormal(command, mode, pass);
                        mode = ClearTextureSelection(context.SynchronizeDrawMode(mode));
                    }
                    NativeBattleLogicalResolvedCommand resolved = new(command, mode, pass);
                    if (GetCache() is { } currentCache)
                    {
                        var selected = SelectTextureCommand(resolved, currentCache);
                        resolved = selected.Command;
                        SetCache(selected.Cache);
                    }
                    executed.Add(context?.RetainTexturePixels(resolved) ?? resolved);
                }
                if (pass == 0 && category && (liveMode & 1) != 0 && context is not null)
                {
                    for (int delivery = 0; delivery < 2; delivery++)
                    {
                        if (context.LiveDebug != 0 && context.SoftwareTable.Handler(command.Opcode) != 0)
                        {
                            context.ExecuteSoftware(command);
                            mode = context.SynchronizeDrawMode(mode);
                        }
                    }
                }
            }
            SetCache(EndTexturePass(GetCache(), pass));
        }
        context?.EndBatch();
        return (mode, executed.ToImmutable(), GetCache());

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
    public static (ImmutableArray<NativeBattleLogicalResolvedCommand> Commands, NativeBattlePcTextureWindowCache Cache) SelectTextures(ImmutableArray<NativeBattleLogicalResolvedCommand> commands, NativeBattlePcTextureWindowCache cache)
    {
        if (commands.IsDefault || commands.Any(command => command.Pass > 2))
        {
            throw new InvalidDataException("Native texture selection requires the three actual executed OT passes.");
        }
        ImmutableArray<NativeBattleLogicalResolvedCommand>.Builder selected = ImmutableArray.CreateBuilder<NativeBattleLogicalResolvedCommand>();
        for (byte pass = 0; pass < 3; pass++)
        {
            cache = BeginTexturePass(cache, pass)!;
            foreach (NativeBattleLogicalResolvedCommand command in commands.Where(command => command.Pass == pass))
            {
                var item = SelectTextureCommand(command, cache);
                cache = item.Cache;
                selected.Add(item.Command);
            }
            cache = EndTexturePass(cache, pass)!;
        }
        return (selected.ToImmutable(), cache);
    }

    private static NativeBattlePcTextureWindowCache? BeginTexturePass(NativeBattlePcTextureWindowCache? cache, byte pass) => pass == 1 ? cache?.Prepare464BD0().Cache : cache;
    private static NativeBattlePcTextureWindowCache? EndTexturePass(NativeBattlePcTextureWindowCache? cache, byte pass) => pass == 1 ? cache?.Flush465930() : cache;

    private static (NativeBattleLogicalResolvedCommand Command, NativeBattlePcTextureWindowCache Cache) SelectTextureCommand(NativeBattleLogicalResolvedCommand command, NativeBattlePcTextureWindowCache cache)
    {
        NativeBattlePrimitiveDrawMode mode = ClearTextureSelection(command.DrawMode);
        bool sprite = command.Command.Opcode is 0x64 or 0x66;
        if ((command.Command.Opcode & 0xE4) == 0x24 || sprite)
        {
            ushort page = mode.TexturePage ?? throw new NotSupportedException("Native textured pass requires its executed texture page.");
            ushort clut = unchecked((ushort)(command.Command.Words[2] >> 16));
            if (command.Pass == 0)
            {
                cache = sprite ? cache.PreprocessSprite463120(command.Command.Opcode, page, clut) : cache.Preprocess462F90(page, clut);
            }
            else
            {
                uint window = mode.TextureWindowBits ?? throw new NotSupportedException("Native465CE0 requires its retained E2 window bits.");
                NativeBattlePcTextureWindowResolve resolved = sprite ? cache.ResolveSprite462DF0(command.Command.Opcode, page, clut, window) : cache.Resolve465CE0(page, clut, window);
                cache = resolved.Cache;
                mode = mode with { TextureSelection = resolved.Selection };
            }
        }
        return (ReferenceEquals(command.DrawMode, mode) ? command : command with { DrawMode = mode }, cache);
    }

    private static NativeBattlePrimitiveDrawMode ClearTextureSelection(NativeBattlePrimitiveDrawMode mode)
    {
        return mode.TextureSelection is null && mode.TexturePixelSnapshot is null ? mode : mode with { TextureSelection = null, TexturePixelSnapshot = null };
    }
}
