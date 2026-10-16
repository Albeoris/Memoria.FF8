using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectMemoryRegion([property: JsonRequired] int Offset, [property: JsonRequired] ImmutableArray<byte> Data);
public sealed record NativeBattleEffectUnknownRegion([property: JsonRequired] int Offset, [property: JsonRequired] int Length);

/// <summary>571870's exactly cleared1MB arena at20DFAB8; unwritten bytes are proved zero, not unknown.</summary>
public sealed record NativeBattleEffectArena([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> Regions)
{
    public const int Length = 0x100000;
    public ImmutableArray<NativeBattleEffectUnknownRegion> UnknownRegions { get; init; } = [];
    public static NativeBattleEffectArena Clear() => new([]);

    public void Validate()
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
    }

    public byte[] Read(int offset, int count)
    {
        Validate();
        RequireRange(offset, count);
        if (count > 0 && UnknownRegions.Any(region => region.Offset < offset + count && region.Offset + region.Length > offset))
        {
            throw new NotSupportedException("Native effect arena read requires unresolved physical pointer bytes; its logical packet-link owner does not provide a native address.");
        }
        return ReadStorage(offset, count);
    }

    private byte[] ReadStorage(int offset, int count)
    {
        byte[] result = new byte[count];
        foreach (NativeBattleEffectMemoryRegion region in Regions)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + count, region.Offset + region.Data.Length);
            if (end > first)
            {
                region.Data.AsSpan().Slice(first - region.Offset, end - first).CopyTo(result.AsSpan(first - offset));
            }
        }
        return result;
    }

    public NativeBattleEffectArena Write(int offset, ReadOnlySpan<byte> bytes)
    {
        Validate();
        RequireRange(offset, bytes.Length);
        if (bytes.Length == 0)
        {
            return this;
        }
        int writeEnd = offset + bytes.Length;
        NativeBattleEffectMemoryRegion[] overlap = Regions.Where(region => region.Offset <= writeEnd && region.Offset + region.Data.Length >= offset).ToArray();
        int first = overlap.Select(region => region.Offset).Append(offset).Min();
        int end = overlap.Select(region => region.Offset + region.Data.Length).Append(offset + bytes.Length).Max();
        byte[] data = ReadStorage(first, end - first);
        bytes.CopyTo(data.AsSpan(offset - first));
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
        return this with { Regions = Regions.Except(overlap).Append(new(first, data.ToImmutableArray())).OrderBy(region => region.Offset).ToImmutableArray(), UnknownRegions = unknown.ToImmutable() };
    }

    public NativeBattleEffectArena MarkUnknown(int offset, int count)
    {
        Validate();
        RequireRange(offset, count);
        if (count == 0)
        {
            return this;
        }
        int first = offset;
        int end = offset + count;
        NativeBattleEffectUnknownRegion[] overlap = UnknownRegions.Where(region => region.Offset <= end && region.Offset + region.Length >= first).ToArray();
        first = overlap.Select(region => region.Offset).Append(first).Min();
        end = overlap.Select(region => region.Offset + region.Length).Append(end).Max();
        return this with { UnknownRegions = UnknownRegions.Except(overlap).Append(new(first, end - first)).OrderBy(region => region.Offset).ToImmutableArray() };
    }

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native effect memory access exceeds20DFAB8..21DFAB8.");
        }
    }
}

