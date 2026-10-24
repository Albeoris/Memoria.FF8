using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>Private ordered scratch stores read through sparse pages before one immutable publication.</summary>
internal sealed class NativeBattleFileStagingTransaction : IDisposable
{
    private const int PageLength = 4096;
    private sealed record Page(byte[] Data, bool[] Known)
    {
        public bool Dirty { get; set; }
    }
    private readonly NativeBattleFileStaging source;
    private readonly Dictionary<uint, Page> pages = [];
    private bool disposed;

    public NativeBattleFileStagingTransaction(NativeBattleFileStaging source)
    {
        if (source.Transaction is not null)
        {
            throw new InvalidOperationException("Native staging transactions cannot be nested.");
        }
        source.Validate();
        this.source = source;
        View = source with { Transaction = this };
    }

    public NativeBattleFileStaging View { get; }

    public byte[] Read(uint address, int length)
    {
        RequireRange(address, length);
        for (int offset = 0; offset < length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = checked((int)(current - pageAddress));
            int count = Math.Min(length - offset, PageLength - first);
            if (CachedPage(pageAddress).Known.AsSpan(first, count).Contains(false))
            {
                throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
            }
            offset += count;
        }
        byte[] data = new byte[length];
        for (int offset = 0; offset < length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = checked((int)(current - pageAddress));
            int count = Math.Min(length - offset, PageLength - first);
            pages[pageAddress].Data.AsSpan(first, count).CopyTo(data.AsSpan(offset));
            offset += count;
        }
        return data;
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(uint address, int length)
    {
        RequireRange(address, length);
        byte[] data = new byte[length];
        bool[] known = new bool[length];
        for (int offset = 0; offset < length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = checked((int)(current - pageAddress));
            int count = Math.Min(length - offset, PageLength - first);
            Page page = CachedPage(pageAddress);
            page.Data.AsSpan(first, count).CopyTo(data.AsSpan(offset));
            page.Known.AsSpan(first, count).CopyTo(known.AsSpan(offset));
            offset += count;
        }
        return (data, known);
    }

    public void Write(uint address, ReadOnlySpan<byte> bytes)
    {
        RequireRange(address, bytes.Length);
        for (int offset = 0; offset < bytes.Length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = checked((int)(current - pageAddress));
            int count = Math.Min(bytes.Length - offset, PageLength - first);
            Page page = CachedPage(pageAddress);
            page.Dirty = true;
            bytes.Slice(offset, count).CopyTo(page.Data.AsSpan(first));
            Array.Fill(page.Known, true, first, count);
            offset += count;
        }
    }

    public void MarkUnknown(uint address, int length)
    {
        RequireRange(address, length);
        for (int offset = 0; offset < length;)
        {
            uint current = checked(address + (uint)offset);
            uint pageAddress = current / PageLength * PageLength;
            int first = checked((int)(current - pageAddress));
            int count = Math.Min(length - offset, PageLength - first);
            Page page = CachedPage(pageAddress);
            page.Dirty = true;
            Array.Clear(page.Data, first, count);
            Array.Fill(page.Known, false, first, count);
            offset += count;
        }
    }

    public NativeBattleFileStaging Freeze(NativeBattleFileStaging current)
    {
        RequireActive();
        if (current.Transaction != this)
        {
            throw new InvalidOperationException("Native staging publication requires its current transaction view.");
        }
        NativeBattleFileStaging rebased = source.Rebase(current.BaseAddress);
        List<NativeBattleEffectMemoryRegion> ranges = [];
        uint[] changed = pages.Where(pair => pair.Value.Dirty).Select(pair => pair.Key).Order().ToArray();
        foreach (NativeBattleEffectMemoryRegion region in rebased.KnownRanges)
        {
            int cursor = region.Offset;
            int end = checked(region.Offset + region.Data.Length);
            foreach (uint address in changed)
            {
                long first = (long)address - current.BaseAddress;
                long last = first + PageLength;
                if (last <= cursor || first >= end)
                {
                    continue;
                }
                if (first > cursor)
                {
                    Keep(cursor, checked((int)first));
                }
                cursor = checked((int)Math.Min(end, last));
            }
            if (cursor < end)
            {
                Keep(cursor, end);
            }

            void Keep(int first, int last)
            {
                ranges.Add(first == region.Offset && last == end ? region : new(first, region.Data.Slice(first - region.Offset, last - first)));
            }
        }
        foreach (uint address in changed)
        {
            Page page = pages[address];
            for (int index = 0; index < PageLength;)
            {
                if (!page.Known[index])
                {
                    index++;
                    continue;
                }
                int first = index++;
                while (index < PageLength && page.Known[index])
                {
                    index++;
                }
                ranges.Add(new(checked((int)((long)address + first - current.BaseAddress)), ImmutableCollectionsMarshal.AsImmutableArray(page.Data.AsSpan(first, index - first).ToArray())));
            }
        }
        NativeBattleFileStaging result = current with { KnownRanges = ranges.OrderBy(range => range.Offset).ToImmutableArray(), Transaction = null };
        result.Validate();
        return result;
    }

    private Page CachedPage(uint address)
    {
        if (!pages.TryGetValue(address, out Page? page))
        {
            (byte[] data, bool[] known) = source.ReadSnapshotStorage(address, PageLength);
            page = new(data, known);
            pages.Add(address, page);
        }
        return page;
    }

    private void RequireRange(uint address, int length)
    {
        RequireActive();
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new InvalidDataException("Native staging access leaves its physical address extent.");
        }
    }

    internal void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);

    public void Dispose()
    {
        disposed = true;
        pages.Clear();
    }
}
