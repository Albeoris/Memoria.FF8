using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommonPacketStore(int Offset, ImmutableArray<byte> Data, ImmutableArray<NativeBattleGfPacketByteState> Mask);

public sealed partial record NativeBattlePrimitiveProcess
{
    /// <summary>Apply one ordered producer pass over its affected spans, retaining every byte's knowledge.</summary>
    public NativeBattlePrimitiveProcess ApplyCommonWrites(ImmutableArray<NativeBattleCommonPacketStore> writes)
    {
        int bank = CommonBank(out int capacity);
        ValidateCommonPacketMemory();
        if (writes.IsDefault)
        {
            throw new InvalidDataException("Native common packet batch requires explicit ordered stores.");
        }
        bool changed = false;
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            if (write is null || write.Data.IsDefault || write.Mask.IsDefault || write.Data.Length != write.Mask.Length || write.Mask.Any(value => value is not (NativeBattleGfPacketByteState.Untouched or NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown)))
            {
                throw new InvalidDataException("Native common packet batch requires matching data and original byte masks.");
            }
            RequireCommonRange(write.Offset, write.Data.Length, CommonPacketWriteCapacity);
            changed |= write.Mask.Any(value => value != NativeBattleGfPacketByteState.Untouched);
        }
        if (!changed)
        {
            return this;
        }
        if (CommonTransaction is { } transaction)
        {
            transaction.Append(writes);
            return this;
        }
        ImmutableArray<NativeBattleCommonPacketStore>.Builder first = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        ImmutableArray<NativeBattleCommonPacketStore>.Builder second = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            foreach (CommonSlice slice in CommonSlices(write.Offset, write.Data.Length))
            {
                NativeBattleCommonPacketStore part = slice.Count == write.Data.Length ? write with { Offset = slice.Offset } : new(slice.Offset, write.Data.AsSpan().Slice(slice.SourceOffset, slice.Count).ToArray().ToImmutableArray(), write.Mask.AsSpan().Slice(slice.SourceOffset, slice.Count).ToArray().ToImmutableArray());
                (slice.Bank == bank ? first : second).Add(part);
            }
        }
        NativeBattlePrimitiveProcess result = ApplyCommonSliceWrites(bank, capacity, first.ToImmutable());
        return result.ApplyCommonSliceWrites(bank + 1, capacity, second.ToImmutable());
    }

    private NativeBattlePrimitiveProcess ApplyCommonSliceWrites(int bank, int capacity, ImmutableArray<NativeBattleCommonPacketStore> writes)
    {
        if (!writes.Any(write => write.Mask.Any(value => value != NativeBattleGfPacketByteState.Untouched)))
        {
            return this;
        }
        NativeBattleEffectArena original = CommonArena(bank) ?? UnknownCommonArena(capacity);
        int firstData = capacity;
        int endData = 0;
        int firstWrite = capacity;
        int endWrite = 0;
        foreach (NativeBattleEffectMemoryRegion region in original.Regions)
        {
            firstData = Math.Min(firstData, region.Offset);
            endData = Math.Max(endData, region.Offset + region.Data.Length);
        }
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            for (int index = 0; index < write.Mask.Length; index++)
            {
                int address = write.Offset + index;
                if (write.Mask[index] != NativeBattleGfPacketByteState.Untouched)
                {
                    firstWrite = Math.Min(firstWrite, address);
                    endWrite = Math.Max(endWrite, address + 1);
                }
                if (write.Mask[index] == NativeBattleGfPacketByteState.Known)
                {
                    firstData = Math.Min(firstData, address);
                    endData = Math.Max(endData, address + 1);
                }
            }
        }
        byte[] bytes = endData > firstData ? new byte[endData - firstData] : [];
        bool[] unknown = new bool[endWrite - firstWrite];
        foreach (NativeBattleEffectMemoryRegion region in original.Regions)
        {
            region.Data.AsSpan().CopyTo(bytes.AsSpan(region.Offset - firstData));
        }
        foreach (NativeBattleEffectUnknownRegion region in original.UnknownRegions)
        {
            int first = Math.Max(firstWrite, region.Offset);
            int end = Math.Min(endWrite, region.Offset + region.Length);
            if (end > first)
            {
                Array.Fill(unknown, true, first - firstWrite, end - first);
            }
        }
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            for (int index = 0; index < write.Mask.Length; index++)
            {
                int address = write.Offset + index;
                if (write.Mask[index] == NativeBattleGfPacketByteState.Known)
                {
                    bytes[address - firstData] = write.Data[index];
                    unknown[address - firstWrite] = false;
                }
                else if (write.Mask[index] == NativeBattleGfPacketByteState.Unknown)
                {
                    unknown[address - firstWrite] = true;
                }
            }
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknownRegions = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        foreach (NativeBattleEffectUnknownRegion region in original.UnknownRegions)
        {
            int end = Math.Min(firstWrite, region.Offset + region.Length);
            if (end > region.Offset)
            {
                AppendUnknownRegion(unknownRegions, region.Offset, end);
            }
        }
        for (int index = 0; index < unknown.Length;)
        {
            if (!unknown[index])
            {
                index++;
                continue;
            }
            int end = index + 1;
            while (end < unknown.Length && unknown[end])
            {
                end++;
            }
            AppendUnknownRegion(unknownRegions, firstWrite + index, firstWrite + end);
            index = end;
        }
        foreach (NativeBattleEffectUnknownRegion region in original.UnknownRegions)
        {
            int first = Math.Max(endWrite, region.Offset);
            int end = region.Offset + region.Length;
            if (end > first)
            {
                AppendUnknownRegion(unknownRegions, first, end);
            }
        }
        ImmutableArray<NativeBattleEffectMemoryRegion> regions = endData > firstData ? [new(firstData, ImmutableCollectionsMarshal.AsImmutableArray(bytes))] : [];
        NativeBattleEffectArena result = new(regions) { UnknownRegions = unknownRegions.ToImmutable() };
        return StoreCommonArena(bank, result);
    }

    private static void AppendUnknownRegion(ImmutableArray<NativeBattleEffectUnknownRegion>.Builder regions, int first, int end)
    {
        if (regions.Count > 0 && regions[^1].Offset + regions[^1].Length == first)
        {
            NativeBattleEffectUnknownRegion previous = regions[^1];
            regions[^1] = previous with { Length = end - previous.Offset };
        }
        else
        {
            regions.Add(new(first, end - first));
        }
    }
}
