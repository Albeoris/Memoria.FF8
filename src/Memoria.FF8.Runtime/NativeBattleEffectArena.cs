using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectMemoryRegion([property: JsonRequired] int Offset, [property: JsonRequired] ImmutableArray<byte> Data);
public sealed record NativeBattleEffectUnknownRegion([property: JsonRequired] int Offset, [property: JsonRequired] int Length);

/// <summary>571870's exactly cleared1MB arena at20DFAB8; unwritten bytes are proved zero, not unknown.</summary>
public sealed partial record NativeBattleEffectArena([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> Regions)
{
    private static readonly ConditionalWeakTable<NativeBattleEffectArena, object> Validated = new();
    public const int Length = 0x100000;
    [JsonIgnore]
    internal NativeBattleEffectArenaTransaction? Transaction { get; init; }
    public ImmutableArray<NativeBattleEffectUnknownRegion> UnknownRegions { get; init; } = [];
    public static NativeBattleEffectArena Clear() => new([]);

    public void Validate()
    {
        Transaction?.RequireActive();
        _ = Validated.GetValue(this, static arena =>
        {
            arena.ValidateStorage();
            return new object();
        });
    }

    private void ValidateStorage()
    {
        if (Regions.IsDefault || UnknownRegions.IsDefault)
        {
            throw new InvalidDataException("Native effect arena requires its explicit cleared memory owner.");
        }
        int end = 0;
        foreach (NativeBattleEffectMemoryRegion region in Regions)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Offset < end || region.Offset < 0 || region.Offset > Length - region.Data.Length)
            {
                throw new InvalidDataException("Native effect memory regions overlap or exceed the original1MB arena.");
            }
            end = region.Offset + region.Data.Length;
        }
        end = -1;
        foreach (NativeBattleEffectUnknownRegion region in UnknownRegions)
        {
            if (region is null || region.Length <= 0 || region.Offset <= end || region.Offset < 0 || region.Offset > Length - region.Length)
            {
                throw new InvalidDataException("Native effect unknown-byte spans must be bounded, disjoint and merged.");
            }
            end = region.Offset + region.Length;
        }
        ValidateResourcePointerStorage();
        ValidateTextureFiles();
    }

    public byte[] Read(int offset, int count)
    {
        if (Transaction is { } transaction)
        {
            return transaction.Read(offset, count);
        }
        Validate();
        RequireRange(offset, count);
        if (count > 0 && IntersectsUnknown(offset, count))
        {
            throw new NotSupportedException($"Native effect arena read intersects unresolved physical bytes at arena offset 0x{offset:X} for {count} bytes; unknown padding or pointer data cannot be substituted.");
        }
        return ReadStorage(offset, count);
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(int offset, int count)
    {
        if (Transaction is { } transaction)
        {
            return transaction.ReadSnapshot(offset, count);
        }
        Validate();
        RequireRange(offset, count);
        bool[] known = new bool[count];
        NativeBattleTelemetry.Fill(1050, known, true, 0, count);
        foreach (NativeBattleEffectUnknownRegion region in UnknownRegions)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + count, region.Offset + region.Length);
            if (end > first)
            {
                NativeBattleTelemetry.Clear(1001, known, first - offset, end - first);
            }
        }
        return (ReadStorage(offset, count), known);
    }

    private byte[] ReadStorage(int offset, int count)
    {
        byte[] result = new byte[count];
        int firstRegion = FirstRegionEndingAt(offset, false);
        for (int index = firstRegion; index < Regions.Length && Regions[index].Offset < offset + count; index++)
        {
            NativeBattleEffectMemoryRegion region = Regions[index];
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + count, region.Offset + region.Data.Length);
            if (end > first)
            {
                NativeBattleTelemetry.Copy(1002, region.Data.AsSpan().Slice(first - region.Offset, end - first), result.AsSpan(first - offset));
            }
        }
        return result;
    }

    public NativeBattleEffectArena Write(int offset, ReadOnlySpan<byte> bytes)
    {
        if (Transaction is null && RemoveResourcePointers(offset, bytes.Length) is { } changed)
        {
            return changed.Write(offset, bytes);
        }
        if (Transaction is { } transaction)
        {
            transaction.Write(offset, bytes);
            return this;
        }
        Validate();
        RequireRange(offset, bytes.Length);
        if (bytes.Length == 0)
        {
            return this;
        }
        int writeEnd = offset + bytes.Length;
        int firstRegion = FirstRegionEndingAt(offset, true);
        int afterRegion = firstRegion;
        while (afterRegion < Regions.Length && Regions[afterRegion].Offset <= writeEnd)
        {
            afterRegion++;
        }
        bool changesUnknown = IntersectsUnknown(offset, bytes.Length);
        if (afterRegion == firstRegion + 1)
        {
            NativeBattleEffectMemoryRegion existing = Regions[firstRegion];
            if (!changesUnknown && existing.Offset <= offset && existing.Offset + existing.Data.Length >= writeEnd && existing.Data.AsSpan().Slice(offset - existing.Offset, bytes.Length).SequenceEqual(bytes))
            {
                return this;
            }
        }
        int first = firstRegion == afterRegion ? offset : Math.Min(offset, Regions[firstRegion].Offset);
        int end = firstRegion == afterRegion ? writeEnd : Math.Max(writeEnd, Regions[afterRegion - 1].Offset + Regions[afterRegion - 1].Data.Length);
        byte[] data = new byte[end - first];
        for (int index = firstRegion; index < afterRegion; index++)
        {
            NativeBattleEffectMemoryRegion region = Regions[index];
            NativeBattleTelemetry.Copy(1003, region.Data.AsSpan(), data.AsSpan(region.Offset - first));
        }
        NativeBattleTelemetry.Copy(1004, bytes, data.AsSpan(offset - first));
        ImmutableArray<NativeBattleEffectUnknownRegion> updatedUnknown = UnknownRegions;
        if (changesUnknown)
        {
            ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
            foreach (NativeBattleEffectUnknownRegion region in UnknownRegions)
            {
                int regionEnd = region.Offset + region.Length;
                if (region.Offset >= writeEnd || regionEnd <= offset)
                {
                    unknown.Add(region);
                }
                else
                {
                    if (region.Offset < offset)
                    {
                        unknown.Add(new(region.Offset, offset - region.Offset));
                    }
                    if (regionEnd > writeEnd)
                    {
                        unknown.Add(new(writeEnd, regionEnd - writeEnd));
                    }
                }
            }
            updatedUnknown = unknown.ToImmutable();
        }
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder regions = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>(Regions.Length - (afterRegion - firstRegion) + 1);
        regions.AddRange(Regions.AsSpan().Slice(0, firstRegion));
        regions.Add(new(first, ImmutableCollectionsMarshal.AsImmutableArray(data)));
        regions.AddRange(Regions.AsSpan().Slice(afterRegion));
        return this with { Regions = regions.MoveToImmutable(), UnknownRegions = updatedUnknown };
    }

    public NativeBattleEffectArena MarkUnknown(int offset, int count)
    {
        if (Transaction is null && RemoveResourcePointers(offset, count) is { } changed)
        {
            return changed.MarkUnknown(offset, count);
        }
        if (Transaction is { } transaction)
        {
            transaction.MarkUnknown(offset, count);
            return this;
        }
        Validate();
        RequireRange(offset, count);
        if (count == 0)
        {
            return this;
        }
        int first = offset;
        int end = offset + count;
        int firstRegion = 0;
        while (firstRegion < UnknownRegions.Length && UnknownRegions[firstRegion].Offset + UnknownRegions[firstRegion].Length < offset)
        {
            firstRegion++;
        }
        int afterRegion = firstRegion;
        while (afterRegion < UnknownRegions.Length && UnknownRegions[afterRegion].Offset <= end)
        {
            afterRegion++;
        }
        if (firstRegion < afterRegion)
        {
            NativeBattleEffectUnknownRegion existing = UnknownRegions[firstRegion];
            if (afterRegion == firstRegion + 1 && existing.Offset <= offset && existing.Offset + existing.Length >= end)
            {
                return this;
            }
            first = Math.Min(first, existing.Offset);
            end = Math.Max(end, UnknownRegions[afterRegion - 1].Offset + UnknownRegions[afterRegion - 1].Length);
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>(UnknownRegions.Length - (afterRegion - firstRegion) + 1);
        unknown.AddRange(UnknownRegions.AsSpan().Slice(0, firstRegion));
        unknown.Add(new(first, end - first));
        unknown.AddRange(UnknownRegions.AsSpan().Slice(afterRegion));
        return this with { UnknownRegions = unknown.MoveToImmutable() };
    }

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native effect memory access exceeds20DFAB8..21DFAB8.");
        }
    }

    private bool IntersectsUnknown(int offset, int count)
    {
        int first = 0;
        int after = UnknownRegions.Length;
        while (first < after)
        {
            int middle = first + (after - first) / 2;
            NativeBattleEffectUnknownRegion region = UnknownRegions[middle];
            if (region.Offset + region.Length <= offset)
            {
                first = middle + 1;
            }
            else
            {
                after = middle;
            }
        }
        return first < UnknownRegions.Length && UnknownRegions[first].Offset < offset + count;
    }

    private int FirstRegionEndingAt(int offset, bool includeAdjacent)
    {
        int first = 0;
        int after = Regions.Length;
        while (first < after)
        {
            int middle = first + (after - first) / 2;
            NativeBattleEffectMemoryRegion region = Regions[middle];
            int end = region.Offset + region.Data.Length;
            if (end < offset || !includeAdjacent && end == offset)
            {
                first = middle + 1;
            }
            else
            {
                after = middle;
            }
        }
        return first;
    }
}

