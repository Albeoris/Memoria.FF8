using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    public NativeBattleFileStaging CopyMemory(uint source, uint destination, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        NativeBattleFileStaging current = Rebase(Math.Min(BaseAddress, Math.Min(source, destination)));
        int first = checked((int)(destination - current.BaseAddress));
        int end = checked(first + length);
        int sourceFirst = checked((int)(source - current.BaseAddress));
        int sourceEnd = checked(sourceFirst + length);
        var copied = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        var kept = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach (NativeBattleEffectMemoryRegion range in current.KnownRanges)
        {
            int rangeEnd = checked(range.Offset + range.Data.Length);
            int a = Math.Max(sourceFirst, range.Offset);
            int b = Math.Min(sourceEnd, rangeEnd);
            if (a < b)
            {
                copied.Add(new(checked(first + a - sourceFirst), range.Data.Slice(a - range.Offset, b - a)));
            }
            if (rangeEnd <= first || range.Offset >= end)
            {
                kept.Add(range);
            }
            else
            {
                if (range.Offset < first)
                {
                    kept.Add(new(range.Offset, range.Data.Slice(0, first - range.Offset)));
                }
                if (rangeEnd > end)
                {
                    kept.Add(new(end, range.Data.Slice(end - range.Offset, rangeEnd - end)));
                }
            }
        }
        current = (current with { KnownRanges = kept.ToImmutable() }).InvalidateResourcePointers(destination, length);
        foreach (NativeBattleEffectMemoryRegion range in copied)
        {
            current = current.Write(checked(current.BaseAddress + (uint)range.Offset), range.Data.AsSpan());
        }
        return current;
    }
}
