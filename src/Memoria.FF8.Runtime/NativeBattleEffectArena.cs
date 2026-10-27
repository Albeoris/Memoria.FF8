using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectMemoryRegion([property: JsonRequired] int Offset, [property: JsonRequired] ImmutableArray<byte> Data);
public sealed record NativeBattleEffectUnknownRegion([property: JsonRequired] int Offset, [property: JsonRequired] int Length);

/// <summary>One retained working authority, or an explicitly detached frozen arena snapshot.</summary>
[JsonConverter(typeof(NativeBattleEffectArenaJsonConverter))]
public partial record NativeBattleEffectArena : INativeBattleStorageNode
{
    public const int Length = 0x100000;
    private ImmutableArray<NativeBattleEffectMemoryRegion> initialRegions;
    private ImmutableArray<NativeBattleEffectUnknownRegion> initialUnknown = [];
    private ImmutableDictionary<int, NativeBattleResourcePointer?>? initialPointers;
    private NativeBattleWorkingMemory? working;
    private readonly int workingOffset;
    private readonly int bound = Length;
    private readonly bool snapshot;
    private readonly bool borrowed;
    private NativeBattleArenaCoverage? coverage;
    private readonly NativeBattleWorkingMemorySnapshot? capturedAuthority;
    private readonly int capturedOffset;
    private readonly int capturedBound;
    private static readonly ConditionalWeakTable<NativeBattleEffectArena, object> ValidatedSnapshots = new();
    private static readonly ConditionalWeakTable<NativeBattleEffectArena, NativeBattleArenaCoverage> SnapshotCoverage = new();
    private static readonly ConditionalWeakTable<NativeBattleWorkingMemory, Stack<NativeBattleArenaOverlay>> OverlayPools = new();

    public NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions) => initialRegions = regions;

    protected NativeBattleEffectArena(NativeBattleEffectArena original)
    {
        snapshot = original.snapshot;
        working = snapshot ? null : original.Working;
        workingOffset = original.workingOffset;
        bound = original.bound;
        initialRegions = snapshot ? original.initialRegions : [];
        initialUnknown = snapshot ? original.initialUnknown : [];
        initialPointers = snapshot ? original.initialPointers : null;
        capturedAuthority = original.capturedAuthority;
        capturedOffset = original.capturedOffset;
        capturedBound = original.capturedBound;
        Transaction = original.Transaction;
        coverage = original.coverage;
        dataCoverage = original.dataCoverage;
        unknownCoverage = original.unknownCoverage;
        metadataVersion = original.metadataVersion;
        borrowed = original.borrowed;
    }

    internal NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers)
    {
        initialRegions = regions;
        initialUnknown = unknown;
        initialPointers = pointers;
        snapshot = true;
    }

    internal NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, NativeBattleWorkingMemorySnapshot authority, int offset, int length) : this(regions, unknown, pointers)
    {
        capturedAuthority = authority;
        capturedOffset = offset;
        capturedBound = length;
    }

    private NativeBattleEffectArena(NativeBattleWorkingMemory owner, int offset, int length, bool borrowed = true)
    {
        working = owner;
        workingOffset = offset;
        bound = length;
        initialRegions = [];
        this.borrowed = borrowed;
        if (!owner.InitialKnownZero)
        {
            coverage = new();
            coverage.Add(0, length);
            dataCoverage = new();
            unknownCoverage = new();
            unknownCoverage.Add(0, length);
            metadataVersion = owner.Version;
        }
    }

    internal NativeBattleEffectArena(NativeBattleEffectArena source, NativeBattleEffectArenaTransaction transaction) : this(source.Working, source.workingOffset, source.bound)
    {
        Transaction = transaction;
        coverage = source.coverage;
        dataCoverage = source.dataCoverage;
        unknownCoverage = source.unknownCoverage;
        metadataVersion = source.metadataVersion;
        borrowed = source.borrowed;
    }

    [JsonIgnore] public bool IsSnapshot => snapshot;
    [JsonIgnore] internal NativeBattleEffectArenaTransaction? Transaction { get; }
    [JsonIgnore] internal NativeBattleWorkingMemory Working => working ??= Hydrate();
    [JsonIgnore] internal int WorkingOffset => workingOffset;
    [JsonIgnore] internal int Bound => bound;
    [JsonIgnore] internal int CommittedStoredLength => snapshot ? ImportExtent() : Math.Clamp(Working.StoredLength - workingOffset, 0, bound);
    [JsonIgnore] public int StoredLength
    {
        get
        {
            if (TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int offset))
            {
                return Math.Clamp(transaction!.StoredLength - offset, 0, bound);
            }
            return CommittedStoredLength;
        }
    }
    [JsonIgnore] public long Version => snapshot ? 0 : Working.Version;

    public ImmutableArray<NativeBattleEffectMemoryRegion> Regions
    {
        get
        {
            RequireSnapshotEnumeration();
            return initialRegions;
        }
        init
        {
            RequireImportMetadata();
            initialRegions = value;
        }
    }

    public ImmutableArray<NativeBattleEffectUnknownRegion> UnknownRegions
    {
        get
        {
            RequireSnapshotEnumeration();
            return initialUnknown;
        }
        init
        {
            RequireImportMetadata();
            initialUnknown = value;
        }
    }

    public static NativeBattleEffectArena Clear() => new([]);
    public static NativeBattleEffectArena CreateUnknown(int logicalLength) => new(new NativeBattleWorkingMemory(logicalLength, false), 0, logicalLength, false);

    internal static NativeBattleEffectArena BorrowWorking(NativeBattleWorkingMemory owner, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (offset < 0 || length < 0 || length > Length || offset > owner.LogicalLength - length)
        {
            throw new InvalidDataException("Native borrowed arena exceeds its physical working authority.");
        }
        return new(owner, offset, length);
    }

    public static NativeBattleEffectArenaSnapshot CreateSnapshot(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers = null)
    {
        NativeBattleEffectArenaSnapshot result = new(regions, unknown, pointers);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Transaction is { } transaction)
        {
            transaction.RequireActive();
        }
        else if (snapshot)
        {
            _ = ValidatedSnapshots.GetValue(this, static arena =>
            {
                arena.ValidateImport();
                return new object();
            });
        }
        else
        {
            _ = Working.StoredLength;
        }
    }

    internal bool ExceedsExtent(int maximum) => maximum < 0 || maximum > Length || Math.Max(StoredLength, coverage?.Extent ?? 0) > maximum;

    public void ValidateExtent(int maximum)
    {
        Validate();
        if (ExceedsExtent(maximum))
        {
            throw new InvalidDataException("Native arena storage exceeds its bounded owner extent.");
        }
    }

    public void ValidateExplicitCoverage(int capacity)
    {
        ValidateExtent(capacity);
        NativeBattleArenaCoverage classified;
        if (snapshot)
        {
            classified = SnapshotCoverage.GetValue(this, static arena => arena.ImportCoverage());
        }
        else if (Transaction is { } transaction)
        {
            transaction.ValidateExplicitCoverage(capacity);
            return;
        }
        else
        {
            classified = coverage!;
        }
        if (classified is null || classified.Prefix != capacity)
        {
            throw new InvalidDataException("Native packet storage must explicitly classify its entire bounded capacity.");
        }
    }

    public byte[] Read(int offset, int count)
    {
        RequireRange(offset, count);
        byte[] result = new byte[count];
        ReadInto(offset, result);
        return result;
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

    public short ReadInt16(int offset) => unchecked((short)ReadUInt16(offset));

    public uint ReadUInt32(int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public int ReadInt32(int offset) => unchecked((int)ReadUInt32(offset));

    public void ReadInto(int offset, Span<byte> destination)
    {
        RequireRange(offset, destination.Length);
        if (TryEffectiveRoute(offset, destination.Length, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.ReadInto(routedOffset, destination);
        }
        else if (!snapshot)
        {
            Working.ReadInto(workingOffset + offset, destination);
        }
        else
        {
            Validate();
            if (IntersectsUnknown(offset, destination.Length))
            {
                throw new NotSupportedException("Native effect arena read intersects unresolved physical bytes.");
            }
            ReadImportData(offset, destination);
        }
    }

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        RequireRange(offset, destination.Length);
        if (known.Length != destination.Length)
        {
            throw new ArgumentException("Native arena data and knownness destinations must have equal lengths.", nameof(known));
        }
        if (TryEffectiveRoute(offset, destination.Length, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.ReadWithKnownness(routedOffset, destination, known);
        }
        else if (!snapshot)
        {
            Working.ReadWithKnownness(workingOffset + offset, destination, known);
        }
        else
        {
            Validate();
            ReadImportData(offset, destination);
            known.Fill(true);
            NativeBattleTelemetry.Record(1050, NativeBattleTrafficKind.Fill, known.Length);
            foreach (NativeBattleEffectUnknownRegion region in initialUnknown)
            {
                int first = Math.Max(offset, region.Offset);
                int end = Math.Min(offset + destination.Length, region.Offset + region.Length);
                if (first < end)
                {
                    known.Slice(first - offset, end - first).Clear();
                    NativeBattleTelemetry.Record(1001, NativeBattleTrafficKind.ZeroFill, end - first);
                }
            }
        }
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(int offset, int count)
    {
        RequireRange(offset, count);
        byte[] bytes = new byte[count];
        bool[] known = new bool[count];
        ReadWithKnownness(offset, bytes, known);
        return (bytes, known);
    }

    public NativeBattleEffectArena Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireWritable();
        RequireRange(offset, bytes.Length);
        if (TryEffectiveRoute(offset, bytes.Length, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.Write(routedOffset, bytes);
        }
        else
        {
            PrepareCoverage(1);
            long previousVersion = Working.Version;
            try
            {
                Working.Write(workingOffset + offset, bytes);
            }
            finally
            {
                if (Working.Version != previousVersion)
                {
                    RecordCoverage(offset, bytes.Length);
                    RecordDataCoverage(offset, bytes.Length);
                    RecordKnowledge(offset, bytes.Length, true);
                }
            }
        }
        return this;
    }

    public NativeBattleEffectArena MarkUnknown(int offset, int count)
    {
        RequireWritable();
        RequireRange(offset, count);
        if (TryEffectiveRoute(offset, count, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.MarkUnknown(routedOffset, count);
        }
        else
        {
            PrepareCoverage(1);
            long previousVersion = Working.Version;
            try
            {
                Working.MarkUnknown(workingOffset + offset, count);
            }
            finally
            {
                if (Working.Version != previousVersion)
                {
                    RecordCoverage(offset, count);
                    RecordKnowledge(offset, count, false);
                }
            }
        }
        return this;
    }

    public void WriteByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        Write(offset, bytes);
    }

    public void WriteUInt16(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        Write(offset, bytes);
    }

    public void WriteUInt32(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Write(offset, bytes);
    }

    public NativeBattleEffectArenaSnapshot Capture()
    {
        if (snapshot)
        {
            return (NativeBattleEffectArenaSnapshot)this;
        }
        if (Transaction is not null || Working.HasCheckpoint)
        {
            throw new InvalidOperationException("Native arena capture requires a committed working owner.");
        }
        NativeBattleEffectArenaTransaction.RequireCommittedForCapture(Working);
        return CreateCaptured(Working.Capture());
    }

    private NativeBattleEffectArenaSnapshot CreateCaptured(NativeBattleWorkingMemorySnapshot captured)
    {
        _ = EnumerateDataRanges();
        ImmutableArray<NativeBattleEffectUnknownRegion> wholeUnknown = captured.UnknownRegions;
        if (!captured.InitialKnownZero && captured.StoredLength < captured.LogicalLength)
        {
            if (!wholeUnknown.IsEmpty && wholeUnknown[^1].Offset + wholeUnknown[^1].Length == captured.StoredLength)
            {
                NativeBattleEffectUnknownRegion last = wholeUnknown[^1];
                wholeUnknown = wholeUnknown.SetItem(wholeUnknown.Length - 1, new(last.Offset, captured.LogicalLength - last.Offset));
            }
            else
            {
                wholeUnknown = wholeUnknown.Add(new(captured.StoredLength, captured.LogicalLength - captured.StoredLength));
            }
        }
        ImmutableArray<NativeBattleEffectMemoryRegion> visibleRegions = captured.Regions;
        if (!borrowed && dataCoverage is not null)
        {
            ImmutableArray<NativeBattleEffectMemoryRegion>.Builder visible = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
            foreach ((int offset, int end) in dataCoverage.Ranges)
            {
                int first = offset;
                int last = Math.Min(end, captured.StoredLength);
                if (first >= last)
                {
                    continue;
                }
                if (captured.Regions.Length == 1 && captured.Regions[0].Offset == first && captured.Regions[0].Data.Length == last - first)
                {
                    visible.Add(captured.Regions[0]);
                }
                else
                {
                    byte[] data = new byte[last - first];
                    foreach (NativeBattleEffectMemoryRegion region in captured.Regions)
                    {
                        int start = Math.Max(first, region.Offset);
                        int finish = Math.Min(last, region.Offset + region.Data.Length);
                        if (start < finish)
                        {
                            NativeBattleTelemetry.Copy(1002, region.Data.AsSpan().Slice(start - region.Offset, finish - start), data.AsSpan(start - first));
                        }
                    }
                    visible.Add(new(first, ImmutableCollectionsMarshal.AsImmutableArray(data)));
                }
            }
            visibleRegions = visible.ToImmutable();
        }
        if (workingOffset == 0 && bound == captured.LogicalLength)
        {
            return new(visibleRegions, wholeUnknown, captured.ResourcePointers.Count == 0 ? null : captured.ResourcePointers, captured, workingOffset, bound);
        }
        int extent = captured.InitialKnownZero ? Math.Clamp(captured.StoredLength - workingOffset, 0, bound) : bound;
        NativeBattleEffectArenaSnapshot whole = new(captured.Regions, wholeUnknown, captured.ResourcePointers);
        var bytes = whole.ReadSnapshot(workingOffset, extent);
        ImmutableArray<NativeBattleEffectMemoryRegion> regions = extent == 0 ? [] : [new(0, ImmutableArray.CreateRange(bytes.Data))];
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        for (int index = 0; index < extent;)
        {
            if (bytes.Known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < extent && !bytes.Known[index])
            {
                index++;
            }
            unknown.Add(new(first, index - first));
        }
        ImmutableDictionary<int, NativeBattleResourcePointer?>.Builder pointers = ImmutableDictionary.CreateBuilder<int, NativeBattleResourcePointer?>();
        foreach ((int key, NativeBattleResourcePointer? pointer) in captured.ResourcePointers)
        {
            int offset = key - workingOffset;
            if (offset >= 0 && offset <= bound - 4)
            {
                pointers.Add(offset, pointer);
            }
        }
        return new(regions, unknown.ToImmutable(), pointers.Count == 0 ? null : pointers.ToImmutable(), captured, workingOffset, bound);
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        if (snapshot)
        {
            return this;
        }
        if (Transaction is not null || Working.HasCheckpoint)
        {
            throw new InvalidOperationException("Native arena capture requires a committed physical owner and cannot retain a transaction view.");
        }
        NativeBattleEffectArenaTransaction.RequireCommittedForCapture(Working);
        return CreateCaptured(context.GetOrAddOwner(Working.BackingIdentity, Working.Capture));
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        if (!snapshot)
        {
            throw new InvalidOperationException("Native arena restore requires a detached snapshot.");
        }
        if (capturedAuthority is { } authority)
        {
            NativeBattleWorkingMemory restored = context.GetOrAddOwner(authority, () => NativeBattleWorkingMemory.Restore(authority));
            NativeBattleEffectArena arena = new(restored, capturedOffset, capturedBound, capturedOffset != 0 || capturedBound != authority.LogicalLength);
            arena.coverage = ImportCoverage();
            arena.InitializeRangeMetadata();
            arena.metadataVersion = restored.Version;
            return arena;
        }
        return context.GetOrAddOwner(this, () => new NativeBattleEffectArena(initialRegions) { UnknownRegions = initialUnknown, ResourcePointers = initialPointers });
    }

    internal NativeBattleArenaOverlay RentOverlay()
    {
        RequireWritable();
        Validate();
        Stack<NativeBattleArenaOverlay> pool = OverlayPools.GetValue(Working, static _ => new());
        NativeBattleArenaOverlay overlay = pool.Count == 0 ? new() : pool.Pop();
        overlay.Begin(this);
        return overlay;
    }

    internal void ReturnOverlay(NativeBattleArenaOverlay overlay)
    {
        overlay.Reset();
        OverlayPools.GetValue(Working, static _ => new()).Push(overlay);
    }

    internal void PrepareCoverage(int additional)
    {
        _ = Working;
        if (coverage is not null && dataCoverage is not null && unknownCoverage is not null)
        {
            EnlistRangeMetadata();
        }
        EnsureUnknownMetadata();
        coverage ??= new();
        coverage.Reserve(additional);
        dataCoverage ??= new();
        dataCoverage.Reserve(additional);
        unknownCoverage ??= new();
        unknownCoverage.Reserve(checked(additional * 2));
        EnlistRangeMetadata();
    }

    internal void RecordCoverage(int offset, int length) => coverage!.Add(offset, length);
    internal NativeBattleArenaCoverage? Classification => coverage;

    internal void RequireRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bound - count)
        {
            throw new InvalidDataException("Native effect memory access exceeds20DFAB8..21DFAB8.");
        }
    }

    internal void RequireWritable()
    {
        if (snapshot)
        {
            throw new InvalidOperationException("Frozen native arena snapshots cannot be mutated; restore working storage explicitly.");
        }
        Transaction?.RequireActive();
    }

    private NativeBattleWorkingMemory Hydrate()
    {
        if (snapshot)
        {
            throw new InvalidOperationException("Frozen arena snapshots cannot acquire working authority implicitly.");
        }
        ValidateImport();
        coverage = ImportCoverage();
        InitializeRangeMetadata();
        NativeBattleWorkingMemory result = NativeBattleWorkingMemory.Restore(new(bound, true, ImportExtent(), initialRegions, initialUnknown, initialPointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty));
        initialRegions = [];
        initialUnknown = [];
        initialPointers = null;
        metadataVersion = result.Version;
        return result;
    }

    private NativeBattleArenaCoverage ImportCoverage()
    {
        NativeBattleArenaCoverage result = new();
        foreach (NativeBattleEffectMemoryRegion region in initialRegions)
        {
            result.Add(region.Offset, region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in initialUnknown)
        {
            result.Add(region.Offset, region.Length);
        }
        return result;
    }

    private void ValidateImport()
    {
        if (initialRegions.IsDefault || initialUnknown.IsDefault)
        {
            throw new InvalidDataException("Native effect arena requires its explicit cleared memory owner.");
        }
        int end = 0;
        foreach (NativeBattleEffectMemoryRegion region in initialRegions)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Offset < end || region.Offset > Length - region.Data.Length)
            {
                throw new InvalidDataException("Native effect memory regions overlap or exceed the original1MB arena.");
            }
            end = region.Offset + region.Data.Length;
        }
        end = -1;
        foreach (NativeBattleEffectUnknownRegion region in initialUnknown)
        {
            if (region is null || region.Length <= 0 || region.Offset <= end || region.Offset > Length - region.Length)
            {
                throw new InvalidDataException("Native effect unknown-byte spans must be bounded, disjoint and merged.");
            }
            end = region.Offset + region.Length;
        }
        ValidateResourcePointerStorage();
        ValidateTextureFiles();
    }

    private int ImportExtent()
    {
        int extent = initialRegions.IsDefaultOrEmpty ? 0 : initialRegions[^1].Offset + initialRegions[^1].Data.Length;
        if (!initialUnknown.IsDefaultOrEmpty)
        {
            extent = Math.Max(extent, initialUnknown[^1].Offset + initialUnknown[^1].Length);
        }
        return extent;
    }

    private void ReadImportData(int offset, Span<byte> destination)
    {
        destination.Clear();
        NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.ZeroFill, destination.Length);
        foreach (NativeBattleEffectMemoryRegion region in initialRegions)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + destination.Length, region.Offset + region.Data.Length);
            if (first < end)
            {
                NativeBattleTelemetry.Copy(1002, region.Data.AsSpan().Slice(first - region.Offset, end - first), destination.Slice(first - offset));
            }
        }
    }

    private bool IntersectsUnknown(int offset, int count) => count > 0 && initialUnknown.Any(region => region.Offset < offset + count && region.Offset + region.Length > offset);

    private void RequireSnapshotEnumeration()
    {
        if (!snapshot)
        {
            throw new InvalidOperationException("Working arena region enumeration requires an explicit snapshot or bounded memory API.");
        }
    }

    private void RequireImportMetadata()
    {
        if (working is not null || snapshot || Transaction is not null)
        {
            throw new InvalidOperationException("Arena import metadata cannot replace a working or frozen authority.");
        }
    }
}

[JsonConverter(typeof(NativeBattleEffectArenaSnapshotJsonConverter))]
public sealed record NativeBattleEffectArenaSnapshot : NativeBattleEffectArena
{
    internal NativeBattleEffectArenaSnapshot(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers) : base(regions, unknown, pointers)
    {
    }

    internal NativeBattleEffectArenaSnapshot(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, NativeBattleWorkingMemorySnapshot authority, int offset, int length) : base(regions, unknown, pointers, authority, offset, length)
    {
    }
}
