using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    public NativeBattleFileStaging MarkUnknown(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        if (length == 0)
        {
            return this;
        }
        NativeBattleFileStaging current = address < BaseAddress ? Rebase(address) : this;
        if (current.Transaction is { } transaction)
        {
            transaction.MarkUnknown(address, length);
            return current.InvalidateResourcePointers(address, length);
        }
        int first = current.Offset(address);
        int end = checked(first + length);
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder known = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach (NativeBattleEffectMemoryRegion region in current.KnownRanges)
        {
            int regionEnd = checked(region.Offset + region.Data.Length);
            if (regionEnd <= first || region.Offset >= end)
            {
                known.Add(region);
                continue;
            }
            if (region.Offset < first)
            {
                known.Add(new(region.Offset, NativeBattleTelemetry.Slice(1028, region.Data, 0, first - region.Offset)));
            }
            if (regionEnd > end)
            {
                known.Add(new(end, NativeBattleTelemetry.Slice(1029, region.Data, end - region.Offset, regionEnd - end)));
            }
        }
        return (current with { KnownRanges = known.ToImmutable() }).ForgetLoaderZero(address, length).InvalidateResourcePointers(address, length);
    }
}
