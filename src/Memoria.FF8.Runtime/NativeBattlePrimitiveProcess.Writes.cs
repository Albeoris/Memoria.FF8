using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommonPacketStore(int Offset, ImmutableArray<byte> Data, ImmutableArray<NativeBattleGfPacketByteState> Mask);

public sealed partial record NativeBattlePrimitiveProcess
{
    /// <summary>Apply one ordered producer pass with one transient physical bank, retaining every byte's knowledge.</summary>
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
            RequireCommonRange(write.Offset, write.Data.Length, capacity);
            changed |= write.Mask.Any(value => value != NativeBattleGfPacketByteState.Untouched);
        }
        if (!changed)
        {
            return this;
        }
        NativeBattleEffectArena original = CommonArena(bank) ?? UnknownCommonArena(capacity);
        byte[] bytes = new byte[capacity];
        bool[] unknown = new bool[capacity];
        int firstData = capacity;
        int endData = 0;
        foreach (NativeBattleEffectMemoryRegion region in original.Regions)
        {
            region.Data.AsSpan().CopyTo(bytes.AsSpan(region.Offset));
            firstData = Math.Min(firstData, region.Offset);
            endData = Math.Max(endData, region.Offset + region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in original.UnknownRegions)
        {
            Array.Fill(unknown, true, region.Offset, region.Length);
        }
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            for (int index = 0; index < write.Mask.Length; index++)
            {
                int address = write.Offset + index;
                if (write.Mask[index] == NativeBattleGfPacketByteState.Known)
                {
                    bytes[address] = write.Data[index];
                    unknown[address] = false;
                    firstData = Math.Min(firstData, address);
                    endData = Math.Max(endData, address + 1);
                }
                else if (write.Mask[index] == NativeBattleGfPacketByteState.Unknown)
                {
                    unknown[address] = true;
                }
            }
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknownRegions = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        for (int index = 0; index < capacity;)
        {
            if (!unknown[index])
            {
                index++;
                continue;
            }
            int end = index + 1;
            while (end < capacity && unknown[end])
            {
                end++;
            }
            unknownRegions.Add(new(index, end - index));
            index = end;
        }
        ImmutableArray<NativeBattleEffectMemoryRegion> regions = endData > firstData ? [new(firstData, bytes.AsSpan(firstData, endData - firstData).ToArray().ToImmutableArray())] : [];
        NativeBattleEffectArena result = new(regions) { UnknownRegions = unknownRegions.ToImmutable() };
        return StoreCommonArena(bank, result);
    }
}
