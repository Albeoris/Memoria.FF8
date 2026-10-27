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
            if (write is null || write.Data.IsDefault || write.Mask.IsDefault || write.Data.Length != write.Mask.Length)
            {
                throw new InvalidDataException("Native common packet batch requires matching data and original byte masks.");
            }
            bool writesBytes = false;
            foreach (NativeBattleGfPacketByteState value in write.Mask.AsSpan())
            {
                if (value is not (NativeBattleGfPacketByteState.Untouched or NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown))
                {
                    throw new InvalidDataException("Native common packet batch requires matching data and original byte masks.");
                }
                writesBytes |= value != NativeBattleGfPacketByteState.Untouched;
            }
            RequireCommonRange(write.Offset, write.Data.Length, CommonPacketWriteCapacity);
            changed |= writesBytes;
        }
        if (!changed)
        {
            return this;
        }
        if (CommonTransaction is { } transaction)
        {
            transaction.Append(this, writes);
            return this;
        }
        ImmutableArray<NativeBattleCommonPacketStore>.Builder first = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        ImmutableArray<NativeBattleCommonPacketStore>.Builder second = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            foreach (CommonSlice slice in CommonSlices(write.Offset, write.Data.Length))
            {
                NativeBattleCommonPacketStore part;
                if (slice.Count == write.Data.Length)
                {
                    part = slice.Offset == write.Offset ? write : write with { Offset = slice.Offset };
                }
                else
                {
                    part = new(slice.Offset, ImmutableCollectionsMarshal.AsImmutableArray(write.Data.AsSpan().Slice(slice.SourceOffset, slice.Count).ToArray()), ImmutableCollectionsMarshal.AsImmutableArray(write.Mask.AsSpan().Slice(slice.SourceOffset, slice.Count).ToArray()));
                }
                (slice.Bank == bank ? first : second).Add(part);
            }
        }
        NativeBattlePrimitiveProcess result = ApplyCommonSliceWrites(bank, capacity, first.ToImmutable());
        return result.ApplyCommonSliceWrites(bank + 1, capacity, second.ToImmutable());
    }

    private NativeBattlePrimitiveProcess ApplyCommonSliceWrites(int bank, int capacity, ImmutableArray<NativeBattleCommonPacketStore> writes)
    {
        bool changed = false;
        foreach (NativeBattleCommonPacketStore write in writes.AsSpan())
        {
            foreach (NativeBattleGfPacketByteState value in write.Mask.AsSpan())
            {
                if (value != NativeBattleGfPacketByteState.Untouched)
                {
                    changed = true;
                    break;
                }
            }
            if (changed)
            {
                break;
            }
        }
        if (!changed)
        {
            return this;
        }
        NativeBattleEffectArena original = CommonArena(bank) ?? UnknownCommonArena(capacity);
        using NativeBattleEffectArenaWriter writer = new(original);
        foreach (NativeBattleCommonPacketStore write in writes)
        {
            for (int offset = 0; offset < write.Mask.Length;)
            {
                NativeBattleGfPacketByteState state = write.Mask[offset];
                int end = offset + 1;
                while (end < write.Mask.Length && write.Mask[end] == state)
                {
                    end++;
                }
                if (state == NativeBattleGfPacketByteState.Known)
                {
                    writer.Write(checked(write.Offset + offset), write.Data.AsSpan().Slice(offset, end - offset));
                }
                else if (state == NativeBattleGfPacketByteState.Unknown)
                {
                    writer.MarkUnknown(checked(write.Offset + offset), end - offset);
                }
                offset = end;
            }
        }
        return StoreCommonArena(bank, writer.Freeze());
    }
}