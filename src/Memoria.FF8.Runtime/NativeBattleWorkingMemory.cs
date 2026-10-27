using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Detached physical storage. Unmaterialized suffix bytes use InitialKnownZero.</summary>
internal sealed record NativeBattleWorkingMemorySnapshot(int LogicalLength, bool InitialKnownZero, int StoredLength, ImmutableArray<NativeBattleEffectMemoryRegion> Regions, ImmutableArray<NativeBattleEffectUnknownRegion> UnknownRegions, ImmutableDictionary<int, NativeBattleResourcePointer?> ResourcePointers);

/// <summary>One authoritative, single-threaded native memory owner retained across callbacks.</summary>
internal sealed class NativeBattleWorkingMemory : IDisposable
{
    private readonly record struct ByteUndo(int Offset, byte Data, bool Known);
    private readonly record struct PointerUndo(int Offset, bool Present, NativeBattleResourcePointer? Value);
    private readonly int logicalLength;
    private readonly bool initialKnownZero;
    private readonly int maxJournalEntries;
    private byte[] data = [];
    private bool[] known = [];
    private object backingIdentity = new();
    private readonly Dictionary<int, NativeBattleResourcePointer?> pointers = [];
    private readonly List<int> removedPointers = [];
    private readonly List<ByteUndo> byteUndo = [];
    private readonly List<PointerUndo> pointerUndo = [];
    private bool checkpoint;
    private bool revoked;
    private long version;
    private long bytesWritten;
    private long growthCopyBytes;
    private long captureBytes;
    private int storedLength;
    private int checkpointStoredLength;

    public NativeBattleWorkingMemory(int logicalLength, bool initialKnownZero = true, int initialCapacity = 0, int maxJournalEntries = 1_048_576)
    {
        if (logicalLength < 0 || initialCapacity < 0 || initialCapacity > logicalLength || maxJournalEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalLength));
        }
        this.logicalLength = logicalLength;
        this.initialKnownZero = initialKnownZero;
        this.maxJournalEntries = maxJournalEntries;
        EnsureCapacity(initialCapacity);
    }

    public int LogicalLength => Observe(logicalLength);
    public bool InitialKnownZero => Observe(initialKnownZero);
    public int Capacity => Observe(data.Length);
    public int StoredLength => Observe(storedLength);
    public object BackingIdentity => Observe(backingIdentity);
    public long Version => Observe(version);
    public long BytesWritten => Observe(bytesWritten);
    public long GrowthCopyBytes => Observe(growthCopyBytes);
    public long CaptureBytes => Observe(captureBytes);
    public int JournalEntryCount => Observe(byteUndo.Count + pointerUndo.Count);
    public int ResourcePointerCount => Observe(pointers.Count);
    public bool HasCheckpoint => Observe(checkpoint);

    public byte ReadByte(int offset)
    {
        RequireRange(offset, 1);
        RequireKnown(offset, 1);
        return offset < data.Length ? data[offset] : (byte)0;
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

    public short ReadInt16(int offset) => unchecked((short)ReadUInt16(offset));
    public int ReadInt32(int offset) => unchecked((int)ReadUInt32(offset));

    public void ReadInto(int offset, Span<byte> destination)
    {
        RequireRange(offset, destination.Length);
        RequireKnown(offset, destination.Length);
        ReadData(offset, destination);
    }

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> destinationKnown)
    {
        RequireRange(offset, destination.Length);
        if (destinationKnown.Length != destination.Length)
        {
            throw new ArgumentException("Native memory data and knownness destinations must have equal lengths.", nameof(destinationKnown));
        }
        ReadData(offset, destination);
        int count = Math.Min(destination.Length, Math.Max(0, known.Length - offset));
        NativeBattleTelemetry.Copy(1002, known.AsSpan(Math.Min(offset, known.Length), count), destinationKnown);
        destinationKnown[count..].Fill(initialKnownZero);
        NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.Fill, destinationKnown.Length - count);
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

    public void WriteInt16(int offset, short value) => WriteUInt16(offset, unchecked((ushort)value));
    public void WriteInt32(int offset, int value) => WriteUInt32(offset, unchecked((uint)value));

    public void Write(int offset, ReadOnlySpan<byte> source)
    {
        RequireRange(offset, source.Length);
        if (source.IsEmpty)
        {
            return;
        }
        PrepareMutation(offset, source.Length, source, false, 0);
        SaveBytes(offset, source.Length, source, false);
        InvalidatePointers();
        source.CopyTo(data.AsSpan(offset));
        Array.Fill(known, true, offset, source.Length);
        bytesWritten += source.Length;
        storedLength = Math.Max(storedLength, offset + source.Length);
        version++;
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, source.Length);
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.Fill, source.Length);
    }

    public void MarkUnknown(int offset, int length)
    {
        RequireRange(offset, length);
        if (length == 0)
        {
            return;
        }
        PrepareMutation(offset, length, default, true, 0);
        SaveBytes(offset, length, default, true);
        InvalidatePointers();
        Array.Fill(known, false, offset, length);
        storedLength = Math.Max(storedLength, offset + length);
        version++;
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.Fill, length);
    }

    /// <summary>Memmove of raw bytes and knownness; destination typed provenance is invalidated.</summary>
    public void Copy(int source, int destination, int length)
    {
        RequireRange(source, length);
        RequireRange(destination, length);
        if (length == 0)
        {
            return;
        }
        CollectPointers(destination, length);
        int changes = 0;
        if (checkpoint)
        {
            for (int index = 0; index < length; index++)
            {
                if (ByteAt(destination + index) != ByteAt(source + index) || IsKnown(destination + index) != IsKnown(source + index))
                {
                    changes++;
                }
            }
        }
        ReserveJournal(changes, removedPointers.Count);
        EnsureCapacity(destination + length);
        if (checkpoint)
        {
            for (int index = 0; index < length; index++)
            {
                int target = destination + index;
                if (data[target] != ByteAt(source + index) || known[target] != IsKnown(source + index))
                {
                    byteUndo.Add(new(target, data[target], known[target]));
                }
            }
            NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, changes * 2L);
        }
        InvalidatePointers();
        int count = Math.Min(length, Math.Max(0, data.Length - source));
        data.AsSpan(Math.Min(source, data.Length), count).CopyTo(data.AsSpan(destination, length));
        data.AsSpan(destination + count, length - count).Clear();
        known.AsSpan(Math.Min(source, known.Length), count).CopyTo(known.AsSpan(destination, length));
        known.AsSpan(destination + count, length - count).Fill(initialKnownZero);
        bytesWritten += length;
        storedLength = Math.Max(storedLength, destination + length);
        version++;
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, count * 2L);
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.ZeroFill, length - count);
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.Fill, length - count);
        NativeBattleTelemetry.Record(1041, NativeBattleTrafficKind.LogicalMemoryCopy, length);
    }

    public void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequireRange(offset, 4);
        if (pointer is not null && !pointer.HasValidOwner())
        {
            throw new InvalidDataException("Native working memory pointer has an invalid source owner.");
        }
        uint? numeric = pointer?.StagingAddress ?? pointer?.Cpu?.PhysicalAddress;
        Span<byte> bytes = stackalloc byte[4];
        if (numeric is uint address)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, address);
        }
        PrepareMutation(offset, 4, bytes, numeric is null, 1);
        SaveBytes(offset, 4, bytes, numeric is null);
        InvalidatePointers();
        if (numeric is not null)
        {
            bytes.CopyTo(data.AsSpan(offset));
            bytesWritten += 4;
        }
        Array.Fill(known, numeric is not null, offset, 4);
        if (checkpoint)
        {
            pointerUndo.Add(new(offset, false, null));
        }
        pointers[offset] = pointer;
        storedLength = Math.Max(storedLength, offset + 4);
        version++;
        if (numeric is not null)
        {
            NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, 4);
        }
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.Fill, 4);
    }

    public bool TryGetResourcePointer(int offset, out NativeBattleResourcePointer? pointer)
    {
        RequireRange(offset, 4);
        return pointers.TryGetValue(offset, out pointer);
    }

    public NativeBattleResourcePointer? ReadResourcePointer(int offset)
    {
        return TryGetResourcePointer(offset, out NativeBattleResourcePointer? pointer) ? pointer : new(null, ReadUInt32(offset));
    }

    public Dictionary<int, NativeBattleResourcePointer?>.Enumerator EnumerateResourcePointers()
    {
        RequireActive();
        return pointers.GetEnumerator();
    }

    public void ReserveCheckpointCapacity(int byteEntries, int pointerEntries)
    {
        RequireCheckpoint();
        ArgumentOutOfRangeException.ThrowIfNegative(byteEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(pointerEntries);
        ReserveJournal(byteEntries, pointerEntries);
    }

    public void BeginCheckpoint()
    {
        RequireActive();
        if (checkpoint)
        {
            throw new InvalidOperationException("Native working memory checkpoints cannot be nested.");
        }
        byteUndo.Clear();
        pointerUndo.Clear();
        checkpointStoredLength = storedLength;
        checkpoint = true;
    }

    public void CommitCheckpoint()
    {
        RequireCheckpoint();
        checkpoint = false;
        byteUndo.Clear();
        pointerUndo.Clear();
    }

    public void RollbackCheckpoint()
    {
        RequireCheckpoint();
        for (int index = byteUndo.Count - 1; index >= 0; index--)
        {
            ByteUndo entry = byteUndo[index];
            data[entry.Offset] = entry.Data;
            known[entry.Offset] = entry.Known;
        }
        long restoredBytes = byteUndo.Count * 2L;
        for (int index = pointerUndo.Count - 1; index >= 0; index--)
        {
            PointerUndo entry = pointerUndo[index];
            if (entry.Present)
            {
                pointers[entry.Offset] = entry.Value;
            }
            else
            {
                pointers.Remove(entry.Offset);
            }
        }
        storedLength = checkpointStoredLength;
        version++;
        CommitCheckpoint();
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, restoredBytes);
    }

    public NativeBattleWorkingMemorySnapshot Capture()
    {
        RequireActive();
        if (checkpoint)
        {
            throw new InvalidOperationException("Native working memory capture requires a committed boundary.");
        }
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder regions = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        if (storedLength != 0)
        {
            regions.Add(new(0, NativeBattleTelemetry.Immutable(1002, data.AsSpan(0, storedLength))));
            captureBytes += storedLength;
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        for (int index = 0; index < storedLength;)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < storedLength && !known[index])
            {
                index++;
            }
            unknown.Add(new(first, index - first));
        }
        return new(logicalLength, initialKnownZero, storedLength, regions.ToImmutable(), unknown.ToImmutable(), pointers.ToImmutableDictionary());
    }

    public static NativeBattleWorkingMemory Restore(NativeBattleWorkingMemorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.LogicalLength < 0 || snapshot.StoredLength < 0 || snapshot.StoredLength > snapshot.LogicalLength || snapshot.Regions.IsDefault || snapshot.UnknownRegions.IsDefault || snapshot.ResourcePointers is null)
        {
            throw new InvalidDataException("Native working snapshot lacks its bounded detached storage.");
        }
        int end = 0;
        foreach (NativeBattleEffectMemoryRegion region in snapshot.Regions)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Offset < end || region.Offset > snapshot.StoredLength - region.Data.Length)
            {
                throw new InvalidDataException("Native working snapshot data regions overlap or exceed storage.");
            }
            end = region.Offset + region.Data.Length;
        }
        end = 0;
        foreach (NativeBattleEffectUnknownRegion region in snapshot.UnknownRegions)
        {
            if (region is null || region.Offset < end || region.Length <= 0 || region.Offset > snapshot.StoredLength - region.Length)
            {
                throw new InvalidDataException("Native working snapshot unknown ranges overlap or exceed storage.");
            }
            end = region.Offset + region.Length;
        }
        NativeBattleWorkingMemory result = new(snapshot.LogicalLength, snapshot.InitialKnownZero, snapshot.StoredLength);
        result.storedLength = snapshot.StoredLength;
        Array.Fill(result.known, true, 0, snapshot.StoredLength);
        NativeBattleTelemetry.Record(1013, NativeBattleTrafficKind.Fill, snapshot.StoredLength);
        foreach (NativeBattleEffectMemoryRegion region in snapshot.Regions)
        {
            NativeBattleTelemetry.Copy(1002, region.Data.AsSpan(), result.data.AsSpan(region.Offset));
        }
        foreach (NativeBattleEffectUnknownRegion region in snapshot.UnknownRegions)
        {
            Array.Fill(result.known, false, region.Offset, region.Length);
            NativeBattleTelemetry.Record(1013, NativeBattleTrafficKind.Fill, region.Length);
        }
        foreach (var entry in snapshot.ResourcePointers)
        {
            result.RequireRange(entry.Key, 4);
            if (entry.Key > snapshot.StoredLength - 4)
            {
                throw new InvalidDataException("Native working snapshot pointer exceeds its stored physical extent.");
            }
            NativeBattleResourcePointer? pointer = entry.Value;
            if (pointer is not null && !pointer.HasValidOwner())
            {
                throw new InvalidDataException("Native working snapshot pointer has an invalid owner.");
            }
            uint? numeric = pointer?.StagingAddress ?? pointer?.Cpu?.PhysicalAddress;
            for (int lane = 0; lane < 4; lane++)
            {
                if (result.IsKnown(entry.Key + lane) != (numeric is not null))
                {
                    throw new InvalidDataException("Native working snapshot pointer contradicts physical knownness.");
                }
            }
            if (numeric is uint address && result.ReadUInt32(entry.Key) != address)
            {
                throw new InvalidDataException("Native working snapshot pointer contradicts physical bytes.");
            }
            result.pointers.Add(entry.Key, pointer);
        }
        return result;
    }

    public void Revoke()
    {
        RequireActive();
        revoked = true;
        data = [];
        known = [];
        pointers.Clear();
        byteUndo.Clear();
        pointerUndo.Clear();
        removedPointers.Clear();
    }

    public void Dispose()
    {
        if (!revoked)
        {
            Revoke();
        }
    }

    private void ReadData(int offset, Span<byte> destination)
    {
        int count = Math.Min(destination.Length, Math.Max(0, data.Length - offset));
        NativeBattleTelemetry.Copy(1002, data.AsSpan(Math.Min(offset, data.Length), count), destination);
        destination[count..].Clear();
        NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.ZeroFill, destination.Length - count);
    }

    private void RequireKnown(int offset, int length)
    {
        int count = Math.Min(length, Math.Max(0, known.Length - offset));
        if (known.AsSpan(Math.Min(offset, known.Length), count).Contains(false) || count < length && !initialKnownZero)
        {
            throw new NotSupportedException("Native working memory read intersects unknown physical bytes.");
        }
    }

    private bool IsKnown(int offset) => offset < known.Length ? known[offset] : initialKnownZero;
    private byte ByteAt(int offset) => offset < data.Length ? data[offset] : (byte)0;

    private void PrepareMutation(int offset, int length, ReadOnlySpan<byte> source, bool unknownOnly, int extraPointers)
    {
        CollectPointers(offset, length);
        int changes = 0;
        if (checkpoint)
        {
            for (int index = 0; index < length; index++)
            {
                if (unknownOnly ? IsKnown(offset + index) : !IsKnown(offset + index) || ByteAt(offset + index) != source[index])
                {
                    changes++;
                }
            }
        }
        ReserveJournal(changes, removedPointers.Count + extraPointers);
        EnsureCapacity(offset + length);
    }

    private void SaveBytes(int offset, int length, ReadOnlySpan<byte> source, bool unknownOnly)
    {
        if (!checkpoint)
        {
            return;
        }
        int previousCount = byteUndo.Count;
        for (int index = 0; index < length; index++)
        {
            int target = offset + index;
            if (unknownOnly ? known[target] : !known[target] || data[target] != source[index])
            {
                byteUndo.Add(new(target, data[target], known[target]));
            }
        }
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, (byteUndo.Count - previousCount) * 2L);
    }

    private void CollectPointers(int offset, int length)
    {
        removedPointers.Clear();
        foreach (int pointer in pointers.Keys)
        {
            if (pointer < (long)offset + length && pointer + 4L > offset)
            {
                removedPointers.Add(pointer);
            }
        }
    }

    private void InvalidatePointers()
    {
        foreach (int offset in removedPointers)
        {
            if (checkpoint)
            {
                pointerUndo.Add(new(offset, true, pointers[offset]));
            }
            pointers.Remove(offset);
        }
    }

    private void ReserveJournal(int bytes, int metadata)
    {
        if (!checkpoint)
        {
            return;
        }
        if ((long)byteUndo.Count + pointerUndo.Count + bytes + metadata > maxJournalEntries)
        {
            throw new InvalidOperationException("Native working memory operation exceeds its bounded checkpoint journal.");
        }
        byteUndo.EnsureCapacity(checked(byteUndo.Count + bytes));
        pointerUndo.EnsureCapacity(checked(pointerUndo.Count + metadata));
    }

    private void EnsureCapacity(int required)
    {
        if (required <= data.Length)
        {
            return;
        }
        int capacity = checked((int)Math.Min(logicalLength, Math.Max(required, Math.Max(32L, data.Length * 2L))));
        byte[] grown = new byte[capacity];
        bool[] grownKnown = new bool[capacity];
        NativeBattleTelemetry.Record(1013, NativeBattleTrafficKind.ZeroFill, capacity * 2L);
        NativeBattleTelemetry.Copy(1012, data.AsSpan(), grown.AsSpan());
        NativeBattleTelemetry.Copy(1012, known.AsSpan(), grownKnown.AsSpan());
        if (initialKnownZero)
        {
            Array.Fill(grownKnown, true, known.Length, capacity - known.Length);
            NativeBattleTelemetry.Record(1013, NativeBattleTrafficKind.Fill, capacity - known.Length);
        }
        growthCopyBytes += data.Length + known.Length;
        data = grown;
        known = grownKnown;
        backingIdentity = new();
    }

    private void RequireRange(int offset, int length)
    {
        RequireActive();
        if (offset < 0 || length < 0 || offset > logicalLength - length)
        {
            throw new InvalidDataException("Native working memory access leaves its logical bounds.");
        }
    }

    private void RequireCheckpoint()
    {
        RequireActive();
        if (!checkpoint)
        {
            throw new InvalidOperationException("Native working memory requires its active checkpoint.");
        }
    }

    private T Observe<T>(T value)
    {
        RequireActive();
        return value;
    }

    private void RequireActive() => ObjectDisposedException.ThrowIf(revoked, this);
}
