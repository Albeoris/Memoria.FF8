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

    internal static NativeBattleStagingPage Bind(NativeBattleStagingPage? previous, int first, ImmutableArray<byte> source, int sourceOffset, int length)
    {
        ImmutableArray<Region>.Builder regions = ImmutableArray.CreateBuilder<Region>();
        Preserve(0, Math.Min(first, previous?.StoredLength ?? 0));
        Append(new(first, source, sourceOffset, length, default));
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
