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
    private readonly NativeBattleWorkingMemorySnapshot? capturedAuthority;
    private readonly int capturedOffset;
    private readonly int capturedBound;
    private static readonly ConditionalWeakTable<NativeBattleEffectArena, object> ValidatedSnapshots = new();
    private static readonly ConditionalWeakTable<NativeBattleEffectArena, NativeBattleArenaCoverage> SnapshotCoverage = new();
    private static readonly ConditionalWeakTable<NativeBattleWorkingMemory, Stack<NativeBattleArenaOverlay>> OverlayPools = new();

    public NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions) => initialRegions = regions;

    private NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions, int logicalLength)
    {
        RequireLogicalLength(logicalLength);
        initialRegions = regions;
        bound = logicalLength;
    }

    protected NativeBattleEffectArena(NativeBattleEffectArena original)
    {
        snapshot = original.snapshot;
        working = snapshot ? null : original.Working;
        workingOffset = original.workingOffset;
        bound = original.bound;
        initialRegions = snapshot ? original.initialRegions : [];
        initialUnknown = snapshot ? original.initialUnknown : [];
        initialPointers = snapshot ? original.initialPointers : null;
        initialTextureFiles = snapshot ? original.initialTextureFiles : null;
        capturedAuthority = original.capturedAuthority;
        capturedOffset = original.capturedOffset;
        capturedBound = original.capturedBound;
        StorageReference = original.StorageReference;
        CapturedBorrowed = original.CapturedBorrowed;
        Transaction = original.Transaction;
        borrowed = original.borrowed;
    }

    internal NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, int logicalLength = Length)
    {
        RequireLogicalLength(logicalLength);
        bound = logicalLength;
        initialRegions = regions;
        initialUnknown = unknown;
        initialPointers = pointers;
        snapshot = true;
    }

    internal NativeBattleEffectArena(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, NativeBattleWorkingMemorySnapshot authority, int offset, int length) : this(regions, unknown, pointers, length)
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

    }

    internal NativeBattleEffectArena(NativeBattleEffectArena source, NativeBattleEffectArenaTransaction transaction) : this(source.Working, source.workingOffset, source.bound)
    {
        Transaction = transaction;
        borrowed = source.borrowed;
    }

    [JsonIgnore] public bool IsSnapshot => snapshot;
    [JsonIgnore] public int LogicalLength => bound;
    [JsonIgnore] internal NativeBattleEffectArenaTransaction? Transaction { get; }
    [JsonIgnore] internal NativeBattleWorkingMemory Working => RetainedWorking();
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

    public static NativeBattleEffectArenaSnapshot CreateSnapshot(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers = null, int logicalLength = Length)
    {
        NativeBattleEffectArenaSnapshot result = new(regions, unknown, pointers, logicalLength);
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

    internal bool ExceedsExtent(int maximum) => maximum < 0 || maximum > Length || Math.Max(StoredLength, Classification.Extent) > maximum;

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
        else if (TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.ValidateExplicitCoverageWindow(routedOffset, capacity);
            return;
        }
        else
        {
            classified = Classification;
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
            Working.Write(workingOffset + offset, bytes);
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
            Working.MarkUnknown(workingOffset + offset, count);
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
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder regions = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach (NativeBattleEffectUnknownRegion receipt in captured.DataFootprint)
        {
            int first = Math.Max(workingOffset, receipt.Offset);
            int end = Math.Min(workingOffset + bound, receipt.Offset + receipt.Length);
            if (first >= end)
            {
                continue;
            }
            byte[] bytes = new byte[end - first];
            foreach (NativeBattleEffectMemoryRegion stored in captured.Regions)
            {
                int start = Math.Max(first, stored.Offset);
                int finish = Math.Min(end, stored.Offset + stored.Data.Length);
                if (start < finish)
                {
                    NativeBattleTelemetry.Copy(1002, stored.Data.AsSpan().Slice(start - stored.Offset, finish - start), bytes.AsSpan(start - first));
                }
            }
            regions.Add(new(first - workingOffset, ImmutableCollectionsMarshal.AsImmutableArray(bytes)));
        }
        NativeBattleArenaCoverage unknownRanges = new();
        foreach (NativeBattleEffectUnknownRegion unknown in captured.UnknownRegions)
        {
            int first = Math.Max(workingOffset, unknown.Offset);
            int end = Math.Min(workingOffset + bound, unknown.Offset + unknown.Length);
            if (first < end)
            {
                unknownRanges.Add(first - workingOffset, end - first);
            }
        }
        if (!captured.InitialKnownZero)
        {
            int first = Math.Max(workingOffset, captured.StoredLength);
            int end = Math.Min(workingOffset + bound, captured.LogicalLength);
            if (first < end)
            {
                unknownRanges.Add(first - workingOffset, end - first);
            }
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknowns = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>(unknownRanges.Ranges.Count);
        foreach ((int offset, int end) in unknownRanges.Ranges)
        {
            unknowns.Add(new(offset, end - offset));
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
        return new NativeBattleEffectArenaSnapshot(regions.ToImmutable(), unknowns.MoveToImmutable(), pointers.Count == 0 ? null : pointers.ToImmutable(), captured, workingOffset, bound) { CapturedBorrowed = borrowed, TextureFiles = NativeBattleTextureFileMetadata.Project(captured.TextureFiles, workingOffset, bound) };
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
        NativeBattleEffectArenaSnapshot result = CreateCaptured(context.GetOrAddOwner(Working.BackingIdentity, Working.Capture));
        context.RegisterCapturedArena(result);
        return result;
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
            NativeBattleEffectArena arena = new(restored, capturedOffset, capturedBound, CapturedBorrowed);
            return arena;
        }
        return context.GetOrAddOwner(this, () => new NativeBattleEffectArena(initialRegions, bound) { UnknownRegions = initialUnknown, ResourcePointers = initialPointers, TextureFiles = initialTextureFiles });
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

    internal void PrepareCoverage(int additional) => Working.ReserveStorageReceipts(additional);

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
        NativeBattleWorkingMemory result = NativeBattleWorkingMemory.Restore(new(bound, true, ImportExtent(), initialRegions, initialUnknown, initialPointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty, TextureFiles: initialTextureFiles));
        initialRegions = [];
        initialUnknown = [];
        initialPointers = null;
        initialTextureFiles = null;
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
            if (region is null || region.Data.IsDefaultOrEmpty || region.Offset < end || region.Offset > bound - region.Data.Length)
            {
                throw new InvalidDataException("Native effect memory regions overlap or exceed the original1MB arena.");
            }
            end = region.Offset + region.Data.Length;
        }
        end = -1;
        foreach (NativeBattleEffectUnknownRegion region in initialUnknown)
        {
            if (region is null || region.Length <= 0 || region.Offset <= end || region.Offset > bound - region.Length)
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

    private static void RequireLogicalLength(int logicalLength)
    {
        if (logicalLength < 0 || logicalLength > Length)
        {
            throw new InvalidDataException("Native arena logical length exceeds its physical capacity.");
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
    internal NativeBattleEffectArenaSnapshot(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, int logicalLength = Length) : base(regions, unknown, pointers, logicalLength)
    {
    }

    internal NativeBattleEffectArenaSnapshot(ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers, NativeBattleWorkingMemorySnapshot authority, int offset, int length) : base(regions, unknown, pointers, authority, offset, length)
    {
    }
}
