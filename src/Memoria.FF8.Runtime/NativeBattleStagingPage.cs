using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>One immutable source-backed page, or one writable page after copy-on-write.</summary>
internal sealed class NativeBattleStagingPage : INativeBattleReadWindowSource
{
    private readonly record struct Region(int Offset, ImmutableArray<byte> Source, int SourceOffset, int Length, ImmutableArray<bool> Known);
    private readonly ImmutableArray<Region> regions;
    private NativeBattleReadWindowStore? readWindows;
    internal NativeBattleWorkingMemory? Working { get; }
    internal int StoredLength => Working?.StoredLength ?? (regions.IsEmpty ? 0 : regions[^1].Offset + regions[^1].Length);

    internal NativeBattleStagingPage(NativeBattleWorkingMemory working)
    {
        Working = working;
        regions = [];
    }

    private NativeBattleStagingPage(ImmutableArray<Region> regions)
    {
        this.regions = regions;
    }

    internal static NativeBattleStagingPage Bind(NativeBattleStagingPage? previous, int first, ImmutableArray<byte> source, int sourceOffset, int length) => Bind(previous, first, source, sourceOffset, length, default);

    private static NativeBattleStagingPage Bind(NativeBattleStagingPage? previous, int first, ImmutableArray<byte> source, int sourceOffset, int length, ImmutableArray<bool> known)
    {
        ImmutableArray<Region>.Builder regions = ImmutableArray.CreateBuilder<Region>();
        Preserve(0, Math.Min(first, previous?.StoredLength ?? 0));
        Append(new(first, source, sourceOffset, length, known));
        int end = first + length;
        Preserve(end, Math.Max(0, (previous?.StoredLength ?? 0) - end));
        return new(regions.ToImmutable());

        void Append(Region region)
        {
            if (regions.Count > 0 && regions[^1] is var previousRegion && previousRegion.Offset + previousRegion.Length == region.Offset && previousRegion.Source.Equals(region.Source) && previousRegion.SourceOffset + previousRegion.Length == region.SourceOffset && previousRegion.Known.IsDefaultOrEmpty && region.Known.IsDefaultOrEmpty)
            {
                regions[^1] = previousRegion with { Length = previousRegion.Length + region.Length };
            }
            else
            {
                regions.Add(region);
            }
        }

        void Preserve(int offset, int count)
        {
            if (count == 0)
            {
                return;
            }
            if (previous!.Working is null)
            {
                foreach (Region region in previous.regions)
                {
                    int first = Math.Max(offset, region.Offset);
                    int end = Math.Min(offset + count, region.Offset + region.Length);
                    if (end > first)
                    {
                        Append(new(first, region.Source, region.SourceOffset + first - region.Offset, end - first, region.Known.IsDefaultOrEmpty ? default : region.Known.Slice(first - region.Offset, end - first)));
                    }
                }
                return;
            }
            byte[] bytes = new byte[count];
            bool[] known = new bool[count];
            previous.ReadWithKnownness(offset, bytes, known);
            Append(new(offset, ImmutableCollectionsMarshal.AsImmutableArray(bytes), 0, count, known.All(value => value) ? default : ImmutableCollectionsMarshal.AsImmutableArray(known)));
        }
    }

    internal static NativeBattleStagingPage FromOwnedSnapshot(byte[] data, bool[] known)
    {
        if (data.Length != known.Length || data.Length > NativeBattleFileStagingWorkingOwner.PageLength)
        {
            throw new InvalidDataException("Native source-backed page snapshot requires matching bounded data and knownness.");
        }
        return new([new(0, ImmutableCollectionsMarshal.AsImmutableArray(data), 0, data.Length, known.All(value => value) ? default : ImmutableCollectionsMarshal.AsImmutableArray(known))]);
    }

    internal NativeBattleStagingPage Freeze()
    {
        if (Working is null)
        {
            return this;
        }
        byte[] data = new byte[StoredLength];
        bool[] known = new bool[data.Length];
        Working.ReadWithKnownness(0, data, known);
        return FromOwnedSnapshot(data, known);
    }

    internal static NativeBattleStagingPage Copy(NativeBattleStagingPage? previous, int first, NativeBattleStagingPage source, int sourceOffset, int length)
    {
        NativeBattleStagingPage frozen = source.Freeze();
        NativeBattleStagingPage? result = previous;
        int observed = 0;
        foreach (Region region in frozen.regions)
        {
            int start = Math.Max(sourceOffset, region.Offset);
            int end = Math.Min(sourceOffset + length, region.Offset + region.Length);
            if (end <= start)
            {
                continue;
            }
            int destination = start - sourceOffset;
            Gap(destination);
            result = Bind(result, first + destination, region.Source, region.SourceOffset + start - region.Offset, end - start, region.Known.IsDefaultOrEmpty ? default : region.Known.Slice(start - region.Offset, end - start));
            observed = end - sourceOffset;
        }
        Gap(length);
        return result ?? throw new InvalidDataException("Native page copy requires an explicit destination or nonempty source.");

        void Gap(int end)
        {
            if (end > observed)
            {
                int count = end - observed;
                result = Bind(result, first + observed, new byte[count].ToImmutableArray(), 0, count, new bool[count].ToImmutableArray());
                observed = end;
            }
        }
    }

    internal void Publish(NativeBattleFileStaging destination, uint address, int offset, int length)
    {
        if (Working is not null)
        {
            throw new InvalidOperationException("Native source publication requires its frozen page owner.");
        }
        int observed = offset;
        foreach (Region region in regions)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + length, region.Offset + region.Length);
            if (end <= first)
            {
                continue;
            }
            if (first != observed)
            {
                throw new InvalidDataException("Native published page range requires explicit retained data and knownness.");
            }
            for (int cursor = first; cursor < end;)
            {
                int local = cursor - region.Offset;
                bool known = region.Known.IsDefaultOrEmpty || region.Known[local];
                int next = cursor + 1;
                while (next < end && (region.Known.IsDefaultOrEmpty || region.Known[next - region.Offset]) == known)
                {
                    next++;
                }
                uint target = checked(address + (uint)(cursor - offset));
                int source = region.SourceOffset + local;
                if (known)
                {
                    destination.BindResource(target, region.Source, source, next - cursor);
                }
                else
                {
                    destination.WriteWithKnownness(target, region.Source.AsSpan(source, next - cursor), region.Known.AsSpan(local, next - cursor));
                }
                cursor = next;
            }
            observed = end;
        }
        if (observed != offset + length)
        {
            throw new InvalidDataException("Native published page range exceeds its retained source sections.");
        }
    }

    internal NativeBattleWorkingMemory Materialize()
    {
        if (Working is not null)
        {
            return Working;
        }
        NativeBattleWorkingMemory page = new(NativeBattleFileStagingWorkingOwner.PageLength, initialKnownZero: false);
        Span<byte> bytes = stackalloc byte[NativeBattleFileStagingWorkingOwner.PageLength];
        Span<bool> known = stackalloc bool[NativeBattleFileStagingWorkingOwner.PageLength];
        ReadWithKnownness(0, bytes[..StoredLength], known[..StoredLength]);
        page.WriteWithKnownness(0, bytes[..StoredLength], known[..StoredLength]);
        return page;
    }

    internal bool IsKnown(int offset, int length)
    {
        if (Working is not null)
        {
            return Working.IsKnown(offset, length);
        }
        Span<byte> bytes = stackalloc byte[64];
        Span<bool> known = stackalloc bool[64];
        for (int copied = 0; copied < length;)
        {
            int count = Math.Min(length - copied, known.Length);
            ReadWithKnownness(offset + copied, bytes[..count], known[..count]);
            if (known[..count].Contains(false))
            {
                return false;
            }
            copied += count;
        }
        return true;
    }

    internal void ReadWithKnownness(int offset, Span<byte> bytes, Span<bool> known)
    {
        if (Working is not null)
        {
            Working.ReadWithKnownness(offset, bytes, known);
            return;
        }
        bytes.Clear();
        known.Clear();
        foreach (Region region in regions)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + bytes.Length, region.Offset + region.Length);
            if (first >= end)
            {
                continue;
            }
            region.Source.AsSpan(region.SourceOffset + first - region.Offset, end - first).CopyTo(bytes[(first - offset)..]);
            if (region.Known.IsDefaultOrEmpty)
            {
                known.Slice(first - offset, end - first).Fill(true);
            }
            else
            {
                region.Known.AsSpan(first - region.Offset, end - first).CopyTo(known[(first - offset)..]);
            }
        }
    }

    internal bool TrySource(int offset, int length, out ImmutableArray<byte> source, out int sourceOffset)
    {
        foreach (Region region in regions)
        {
            if (region.Known.IsDefaultOrEmpty && offset >= region.Offset && offset <= region.Offset + region.Length - length)
            {
                source = region.Source;
                sourceOffset = region.SourceOffset + offset - region.Offset;
                return true;
            }
        }
        source = default;
        sourceOffset = 0;
        return false;
    }

    internal NativeBattleReadWindow OpenReadWindow(int offset, int length, NativeBattleReadWindowBudget budget, int maximum)
    {
        return Working is not null ? Working.OpenReadWindow(offset, length, budget, maximum) : (readWindows ??= new(maximum)).Open(this, offset, length, maximum, budget);
    }

    internal void PrepareReadWindowWrite(int offset, int length) => Working?.PrepareReadWindowWrite(offset, length);
    long INativeBattleReadWindowSource.ReadWindowGeneration => 0;
    void INativeBattleReadWindowSource.ValidateReadWindow(long generation)
    {
    }
    void INativeBattleReadWindowSource.SeedReadWindow(NativeBattleReadWindowLease lease)
    {
    }
    void INativeBattleReadWindowSource.ReadWindowByte(int offset, out byte value, out bool known)
    {
        Span<byte> bytes = stackalloc byte[1];
        Span<bool> flags = stackalloc bool[1];
        ReadWithKnownness(offset, bytes, flags);
        value = bytes[0];
        known = flags[0];
    }
    void INativeBattleReadWindowSource.ReadWindowWithKnownness(int offset, Span<byte> bytes, Span<bool> known) => ReadWithKnownness(offset, bytes, known);
}
