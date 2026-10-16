using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModelCpuReference(byte Bank, int Offset);

/// <summary>One mutable64KB party bank. Unwritten heap bytes are unknown, unlike the cleared effect arena.</summary>
public sealed record NativeBattleModelCpuBank(ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    public const int Length = 0x10000;
    public static NativeBattleModelCpuBank Unknown() => new([]);

    public byte[] Read(int offset, int length)
    {
        Require(offset, length);
        byte[] bytes = new byte[length];
        int observed = offset;
        foreach (NativeBattleEffectMemoryRegion region in KnownRanges)
        {
            int first = Math.Max(offset, region.Offset);
            int end = Math.Min(offset + length, region.Offset + region.Data.Length);
            if (end <= first)
            {
                continue;
            }
            if (first != observed)
            {
                throw new NotSupportedException("Native CPU model-bank read reaches unobserved retained heap bytes.");
            }
            region.Data.AsSpan().Slice(first - region.Offset, end - first).CopyTo(bytes.AsSpan(first - offset));
            observed = end;
        }
        if (observed != offset + length)
        {
            throw new NotSupportedException("Native CPU model-bank read reaches unobserved retained heap bytes.");
        }
        return bytes;
    }

    public NativeBattleModelCpuBank Write(int offset, ReadOnlySpan<byte> bytes)
    {
        Require(offset, bytes.Length);
        if (bytes.IsEmpty)
        {
            return this;
        }
        int writeEnd = offset + bytes.Length;
        NativeBattleEffectMemoryRegion[] overlaps = KnownRanges.Where(region => region.Offset <= writeEnd && region.Offset + region.Data.Length >= offset).ToArray();
        int first = overlaps.Select(region => region.Offset).Append(offset).Min();
        int end = overlaps.Select(region => region.Offset + region.Data.Length).Append(writeEnd).Max();
        byte[] data = new byte[end - first];
        foreach (NativeBattleEffectMemoryRegion region in overlaps)
        {
            region.Data.AsSpan().CopyTo(data.AsSpan(region.Offset - first));
        }
        bytes.CopyTo(data.AsSpan(offset - first));
        return new(KnownRanges.Except(overlaps).Append(new(first, data.ToImmutableArray())).OrderBy(region => region.Offset).ToImmutableArray());
    }

    private void Require(int offset, int length)
    {
        if (KnownRanges.IsDefault || offset < 0 || length < 0 || offset > Length - length)
        {
            throw new InvalidDataException("Native CPU model-bank access exceeds its64KB allocation.");
        }
        int previousEnd = 0;
        foreach (NativeBattleEffectMemoryRegion region in KnownRanges)
        {
            if (region.Data.IsDefaultOrEmpty || region.Offset < previousEnd || region.Offset > Length - region.Data.Length)
            {
                throw new InvalidDataException("Native CPU model-bank known ranges overlap or leave the allocation.");
            }
            previousEnd = region.Offset + region.Data.Length;
        }
    }
}

public sealed record NativeBattleModelCpuBanks(ImmutableArray<NativeBattleModelCpuBank> Banks)
{
    public static NativeBattleModelCpuBanks Unknown() => new([NativeBattleModelCpuBank.Unknown(), NativeBattleModelCpuBank.Unknown(), NativeBattleModelCpuBank.Unknown()]);

    public NativeBattleModelCpuBanks Copy64K50B2A0(byte source, byte destination)
    {
        if (Banks.IsDefault || Banks.Length != 3 || source >= 3 || destination >= 3)
        {
            throw new InvalidDataException("Native50B2A0 CPU copy requires its three physical party banks.");
        }
        //memcpy transports unknown retained bytes as unknown too, erasing any prior destination knowledge there.
        _ = Banks[source].Read(0, 0);
        return this with { Banks = Banks.SetItem(destination, new(Banks[source].KnownRanges)) };
    }
}
