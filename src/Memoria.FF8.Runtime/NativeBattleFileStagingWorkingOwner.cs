using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleFileStagingData(uint BaseAddress, ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges, ImmutableArray<NativeBattleLoaderZeroRegion> LoaderZeroRanges, ImmutableDictionary<uint, NativeBattleResourcePointer?> ResourcePointers, ImmutableArray<NativeBattleEffectMemoryRegion> RetainedUnknownRanges);

/// <summary>Persistent sparse absolute-address pages; loader-proved zero does not materialize a page.</summary>
internal sealed class NativeBattleFileStagingWorkingOwner
{
    internal const int PageLength = 4096;
    private readonly record struct PointerUndo(uint Address, bool Present, NativeBattleResourcePointer? Pointer);
    private readonly record struct ZeroUndo(int Index, NativeBattleLoaderZeroRegion? Removed);
    private readonly Dictionary<uint, NativeBattleWorkingMemory> pages = [];
    private readonly Dictionary<uint, NativeBattleResourcePointer?> pointers = [];
    private readonly List<NativeBattleLoaderZeroRegion> zeros = [];
    private readonly List<uint> removedPointers = [];
    private readonly List<NativeBattleWorkingMemory> checkpointPages = [];
    private readonly List<PointerUndo> pointerUndo = [];
    private readonly List<ZeroUndo> zeroUndo = [];
    private readonly Stack<NativeBattleFileStagingOverlay> overlays = [];
    private bool checkpoint;
    private uint checkpointBase;
    private long version;

    public NativeBattleFileStagingWorkingOwner(NativeBattleFileStagingData source)
    {
        BaseAddress = source.BaseAddress;
        zeros.AddRange(source.LoaderZeroRanges);
        foreach (NativeBattleEffectMemoryRegion region in source.KnownRanges)
        {
            Write(checked(BaseAddress + (uint)region.Offset), region.Data.AsSpan());
        }
        Span<bool> unknown = stackalloc bool[PageLength];
        unknown.Clear();
        foreach (NativeBattleEffectMemoryRegion region in source.RetainedUnknownRanges)
        {
            for (int offset = 0; offset < region.Data.Length;)
            {
                int count = Math.Min(PageLength, region.Data.Length - offset);
                WriteWithKnownness(checked(BaseAddress + (uint)region.Offset + (uint)offset), region.Data.AsSpan().Slice(offset, count), unknown[..count]);
                offset += count;
            }
        }
        foreach (var entry in source.ResourcePointers)
        {
            pointers.Add(entry.Key, entry.Value);
        }
        version = 0;
    }

    public uint BaseAddress { get; private set; }
    public long Version => version;
    public int PageCount => pages.Count;
    public bool HasCheckpoint => checkpoint;
    public ulong KnownExtentEnd
    {
        get
        {
            ulong last = BaseAddress;
            foreach (var entry in pages)
            {
                if ((ulong)entry.Key + (uint)entry.Value.StoredLength <= last)
                {
                    continue;
                }
                for (int index = entry.Value.StoredLength - 1; index >= 0 && (ulong)entry.Key + (uint)index + 1 > last; index--)
                {
                    if (entry.Value.IsKnown(index, 1))
                    {
                        last = (ulong)entry.Key + (uint)index + 1;
                        break;
                    }
                }
            }
            return last;
        }
    }
    public Dictionary<uint, NativeBattleWorkingMemory>.Enumerator EnumeratePages() => pages.GetEnumerator();
    public Dictionary<uint, NativeBattleResourcePointer?>.Enumerator EnumeratePointers() => pointers.GetEnumerator();
    public ImmutableDictionary<uint, NativeBattleResourcePointer?> CapturePointers() => pointers.ToImmutableDictionary();
    public ImmutableArray<NativeBattleLoaderZeroRegion> CaptureLoaderZeros() => zeros.ToImmutableArray();

    public NativeBattleFileStagingOverlay LeaseOverlay()
    {
        NativeBattleFileStagingOverlay overlay = overlays.Count == 0 ? new(this) : overlays.Pop();
        overlay.Begin();
        return overlay;
    }

    public void ReturnOverlay(NativeBattleFileStagingOverlay overlay)
    {
        overlay.Reset();
        overlays.Push(overlay);
    }

    public void Rebase(uint address)
    {
        ValidateRebase(address);
        if (address == BaseAddress)
        {
            return;
        }
        BaseAddress = address;
        version++;
    }

    public void ValidateRebase(uint address)
    {
        if (address > BaseAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        foreach (var entry in pages)
        {
            if (entry.Value.StoredLength != 0 && (ulong)entry.Key + (uint)entry.Value.StoredLength - address > int.MaxValue)
            {
                throw new InvalidDataException("Native staging rebase exceeds its offset representation.");
            }
        }
    }

    public bool IsKnown(uint address, int length)
    {
        for (int offset = 0; offset < length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = (int)(current - pageAddress);
            int count = Math.Min(length - offset, PageLength - first);
            NativeBattleWorkingMemory? page = pages.GetValueOrDefault(pageAddress);
            if (page is not null && page.IsKnown(first, count) || LoaderZeroCovers(current, count))
            {
                offset += count;
                continue;
            }
            for (int index = 0; index < count; index++)
            {
                if ((page is null || !page.IsKnown(first + index, 1)) && !LoaderZero(checked(current + (uint)index)))
                {
                    return false;
                }
            }
            offset += count;
        }
        return true;
    }

    public void ReadWithKnownness(uint address, Span<byte> destination, Span<bool> destinationKnown)
    {
        for (int offset = 0; offset < destination.Length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = (int)(current - pageAddress);
            int count = Math.Min(destination.Length - offset, PageLength - first);
            Span<byte> data = destination.Slice(offset, count);
            Span<bool> known = destinationKnown.Slice(offset, count);
            if (pages.TryGetValue(pageAddress, out NativeBattleWorkingMemory? page))
            {
                page.ReadWithKnownness(first, data, known);
            }
            else
            {
                data.Clear();
                known.Clear();
                NativeBattleTelemetry.Record(1025, NativeBattleTrafficKind.ZeroFill, count * 2L);
            }
            for (int index = 0; index < count; index++)
            {
                if (!known[index] && LoaderZero(checked(current + (uint)index)))
                {
                    data[index] = 0;
                    known[index] = true;
                }
            }
            offset += count;
        }
    }

    public void Write(uint address, ReadOnlySpan<byte> data)
    {
        InvalidatePointers(address, data.Length);
        for (int offset = 0; offset < data.Length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = (int)(current - pageAddress);
            int count = Math.Min(data.Length - offset, PageLength - first);
            NativeBattleWorkingMemory page = WritablePage(pageAddress);
            long previous = page.Version;
            try
            {
                page.Write(first, data.Slice(offset, count));
            }
            finally
            {
                if (page.Version != previous)
                {
                    version++;
                }
            }
            offset += count;
        }
    }

    public void WriteWithKnownness(uint address, ReadOnlySpan<byte> data, ReadOnlySpan<bool> known)
    {
        InvalidatePointers(address, data.Length);
        for (int offset = 0; offset < data.Length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = (int)(current - pageAddress);
            int count = Math.Min(data.Length - offset, PageLength - first);
            NativeBattleWorkingMemory page = WritablePage(pageAddress);
            long previous = page.Version;
            try
            {
                page.WriteWithKnownness(first, data.Slice(offset, count), known.Slice(offset, count));
            }
            finally
            {
                if (page.Version != previous)
                {
                    version++;
                }
            }
            offset += count;
        }
    }

    public void MarkUnknown(uint address, int length)
    {
        ForgetLoaderZero(address, length);
        InvalidatePointers(address, length);
        ulong end = (ulong)address + (uint)length;
        foreach (var entry in pages)
        {
            ulong first = Math.Max(address, entry.Key);
            ulong last = Math.Min(end, (ulong)entry.Key + (uint)entry.Value.StoredLength);
            if (last <= first)
            {
                continue;
            }
            NativeBattleWorkingMemory page = WritablePage(entry.Key);
            long previous = page.Version;
            try
            {
                page.MarkUnknown((int)(first - entry.Key), (int)(last - first));
            }
            finally
            {
                if (page.Version != previous)
                {
                    version++;
                }
            }
        }
    }

    public bool TryGetPointer(uint address, out NativeBattleResourcePointer? pointer) => pointers.TryGetValue(address, out pointer);

    public bool IsExplicitKnown(uint address)
    {
        uint pageAddress = address / PageLength * PageLength;
        return pages.TryGetValue(pageAddress, out NativeBattleWorkingMemory? page) && page.IsKnown((int)(address - pageAddress), 1);
    }

    public void SetPointerMetadata(uint address, bool present, NativeBattleResourcePointer? pointer)
    {
        bool existed = pointers.TryGetValue(address, out NativeBattleResourcePointer? previous);
        if (checkpoint)
        {
            pointerUndo.Add(new(address, existed, previous));
        }
        if (present)
        {
            pointers[address] = pointer;
        }
        else
        {
            pointers.Remove(address);
        }
        version++;
    }

    public void InvalidatePointers(uint address, int length)
    {
        if (length == 0)
        {
            return;
        }
        removedPointers.Clear();
        foreach (uint pointer in pointers.Keys)
        {
            if (pointer < (ulong)address + (uint)length && (ulong)pointer + 4 > address)
            {
                removedPointers.Add(pointer);
            }
        }
        foreach (uint pointer in removedPointers)
        {
            SetPointerMetadata(pointer, false, null);
        }
    }

    public bool IntersectsLoaderZero(uint address, int length)
    {
        ulong end = (ulong)address + (uint)length;
        foreach (NativeBattleLoaderZeroRegion region in zeros)
        {
            if (region.Address >= end)
            {
                break;
            }
            if ((ulong)region.Address + (uint)region.Length > address)
            {
                return length != 0;
            }
        }
        return false;
    }

    public void ForgetLoaderZero(uint address, int length)
    {
        ulong end = (ulong)address + (uint)length;
        for (int index = 0; index < zeros.Count;)
        {
            NativeBattleLoaderZeroRegion region = zeros[index];
            ulong last = (ulong)region.Address + (uint)region.Length;
            if (region.Address >= end)
            {
                break;
            }
            if (last <= address || length == 0)
            {
                index++;
                continue;
            }
            if (checkpoint)
            {
                zeroUndo.Add(new(index, region));
            }
            zeros.RemoveAt(index);
            if (region.Address < address)
            {
                InsertZero(index++, new(region.Address, checked((int)(address - region.Address))));
            }
            if (last > end)
            {
                InsertZero(index++, new(checked((uint)end), checked((int)(last - end))));
            }
            version++;
        }
    }

    public void BeginCheckpoint()
    {
        if (checkpoint)
        {
            throw new InvalidOperationException("Native staging owner checkpoints cannot be nested.");
        }
        checkpoint = true;
        checkpointBase = BaseAddress;
        checkpointPages.Clear();
        pointerUndo.Clear();
        zeroUndo.Clear();
    }

    public void CommitCheckpoint()
    {
        RequireCheckpoint();
        foreach (NativeBattleWorkingMemory page in checkpointPages)
        {
            page.CommitCheckpoint();
        }
        checkpoint = false;
        checkpointPages.Clear();
        pointerUndo.Clear();
        zeroUndo.Clear();
    }

    public void RollbackCheckpoint()
    {
        RequireCheckpoint();
        Exception? failure = null;
        foreach (NativeBattleWorkingMemory page in checkpointPages)
        {
            try
            {
                page.RollbackCheckpoint();
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        for (int index = pointerUndo.Count - 1; index >= 0; index--)
        {
            PointerUndo entry = pointerUndo[index];
            if (entry.Present)
            {
                pointers[entry.Address] = entry.Pointer;
            }
            else
            {
                pointers.Remove(entry.Address);
            }
        }
        for (int index = zeroUndo.Count - 1; index >= 0; index--)
        {
            ZeroUndo entry = zeroUndo[index];
            if (entry.Removed is { } removed)
            {
                zeros.Insert(entry.Index, removed);
            }
            else
            {
                zeros.RemoveAt(entry.Index);
            }
        }
        BaseAddress = checkpointBase;
        checkpoint = false;
        checkpointPages.Clear();
        pointerUndo.Clear();
        zeroUndo.Clear();
        version++;
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public NativeBattleFileStagingData Capture()
    {
        if (checkpoint)
        {
            throw new InvalidOperationException("Native staging capture requires a committed owner boundary.");
        }
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder hidden = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        Span<byte> bytes = stackalloc byte[PageLength];
        Span<bool> known = stackalloc bool[PageLength];
        foreach (var entry in pages.OrderBy(entry => entry.Key))
        {
            int length = entry.Value.StoredLength;
            entry.Value.ReadWithKnownness(0, bytes[..length], known[..length]);
            for (int index = 0; index < length;)
            {
                bool assigned = known[index];
                if (!assigned && bytes[index] == 0)
                {
                    index++;
                    continue;
                }
                int first = index++;
                while (index < length && known[index] == assigned && (assigned || bytes[index] != 0))
                {
                    index++;
                }
                NativeBattleEffectMemoryRegion region = new(checked((int)((ulong)entry.Key + (uint)first - BaseAddress)), NativeBattleTelemetry.Immutable(1039, bytes[first..index]));
                (assigned ? ranges : hidden).Add(region);
            }
        }
        return new(BaseAddress, ranges.ToImmutable(), zeros.ToImmutableArray(), pointers.ToImmutableDictionary(), hidden.ToImmutable());
    }

    private NativeBattleWorkingMemory WritablePage(uint address)
    {
        if (!pages.TryGetValue(address, out NativeBattleWorkingMemory? page))
        {
            page = new(PageLength, initialKnownZero: false);
            pages.Add(address, page);
        }
        if (checkpoint && !page.HasCheckpoint)
        {
            page.BeginCheckpoint();
            checkpointPages.Add(page);
        }
        return page;
    }

    private bool LoaderZero(uint address)
    {
        foreach (NativeBattleLoaderZeroRegion region in zeros)
        {
            if (region.Address > address)
            {
                return false;
            }
            if ((ulong)region.Address + (uint)region.Length > address)
            {
                return true;
            }
        }
        return false;
    }

    private bool LoaderZeroCovers(uint address, int length)
    {
        ulong end = (ulong)address + (uint)length;
        foreach (NativeBattleLoaderZeroRegion region in zeros)
        {
            if (region.Address > address)
            {
                return false;
            }
            if ((ulong)region.Address + (uint)region.Length >= end)
            {
                return true;
            }
        }
        return false;
    }

    private void InsertZero(int index, NativeBattleLoaderZeroRegion region)
    {
        if (checkpoint)
        {
            zeroUndo.Add(new(index, null));
        }
        zeros.Insert(index, region);
    }

    private void RequireCheckpoint()
    {
        if (!checkpoint)
        {
            throw new InvalidOperationException("Native staging requires its active owner checkpoint.");
        }
    }
}
