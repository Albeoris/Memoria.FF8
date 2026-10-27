using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A private producer pass publishes bounded changes into its retained physical common banks.</summary>
internal sealed class NativeBattleCommonPacketTransaction : IDisposable
{
    private readonly NativeBattleEffectArenaTransaction?[] banks = new NativeBattleEffectArenaTransaction?[4];
    private readonly List<NativeBattleEffectArenaTransaction> active = new(4);
    private NativeBattlePrimitiveProcess state;
    private NativeBattlePrimitiveProcess? checkpoint;
    private int checkpointBanks;
    private bool disposed;

    public NativeBattleCommonPacketTransaction(NativeBattlePrimitiveProcess source)
    {
        if (source.CommonTransaction is not null)
        {
            throw new InvalidOperationException("Common packet transactions cannot be nested.");
        }
        source.ValidateCommonPacketMemory();
        state = source;
        View = source with { CommonTransaction = this };
    }

    public NativeBattlePrimitiveProcess View { get; }
    internal NativeBattleCommonPacketMemory? CurrentMemory => state.CommonPacketMemory;

    public void Append(ImmutableArray<NativeBattleCommonPacketStore> stores) => Append(View, stores);

    public void Append(NativeBattlePrimitiveProcess process, ImmutableArray<NativeBattleCommonPacketStore> stores)
    {
        RequireActive();
        _ = process.CommonBank(out _);
        if (stores.IsDefault)
        {
            throw new InvalidDataException("Native common packet batch requires explicit ordered stores.");
        }
        foreach (NativeBattleCommonPacketStore store in stores)
        {
            if (store is null || store.Data.IsDefault || store.Mask.IsDefault || store.Data.Length != store.Mask.Length)
            {
                throw new InvalidDataException("Native common packet batch requires matching data and original byte masks.");
            }
            foreach (NativeBattleGfPacketByteState mask in store.Mask.AsSpan())
            {
                if (mask is not (NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown or NativeBattleGfPacketByteState.Untouched))
                {
                    throw new InvalidDataException("Native common packet batch requires matching data and original byte masks.");
                }
            }
            _ = process.CommonSlices(store.Offset, store.Data.Length);
        }
        foreach (NativeBattleCommonPacketStore store in stores)
        {
            foreach (NativeBattlePrimitiveProcess.CommonSlice slice in process.CommonSlices(store.Offset, store.Data.Length))
            {
                int offset = 0;
                while (offset < slice.Count)
                {
                    NativeBattleGfPacketByteState mask = store.Mask[slice.SourceOffset + offset];
                    int end = offset + 1;
                    while (end < slice.Count && store.Mask[slice.SourceOffset + end] == mask)
                    {
                        end++;
                    }
                    if (mask == NativeBattleGfPacketByteState.Known)
                    {
                        Bank(slice.Bank).Write(slice.Offset + offset, store.Data.AsSpan().Slice(slice.SourceOffset + offset, end - offset));
                    }
                    else if (mask == NativeBattleGfPacketByteState.Unknown)
                    {
                        Bank(slice.Bank).MarkUnknown(slice.Offset + offset, end - offset);
                    }
                    offset = end;
                }
            }
        }
    }

    public byte[] Read(int offset, int count) => Read(View, offset, count);

    public byte[] Read(NativeBattlePrimitiveProcess process, int offset, int count)
    {
        RequireActive();
        NativeBattlePrimitiveProcess.CommonSliceRange slices = process.CommonSlices(offset, count);
        byte[] result = new byte[count];
        foreach (NativeBattlePrimitiveProcess.CommonSlice slice in slices)
        {
            if (banks[slice.Bank] is { } transaction)
            {
                transaction.ReadInto(slice.Offset, result.AsSpan(slice.SourceOffset, slice.Count));
            }
            else
            {
                NativeBattleEffectArena arena = state.CommonArena(slice.Bank) ?? throw new NotSupportedException("Native common packet read requires the retained owner of every consumed physical slice.");
                arena.ReadInto(slice.Offset, result.AsSpan(slice.SourceOffset, slice.Count));
            }
        }
        return result;
    }

    public (byte[] Bytes, bool[] Known) ReadSnapshot(int offset, int count) => ReadSnapshot(View, offset, count);

    public (byte[] Bytes, bool[] Known) ReadSnapshot(NativeBattlePrimitiveProcess process, int offset, int count)
    {
        RequireActive();
        NativeBattlePrimitiveProcess.CommonSliceRange slices = process.CommonSlices(offset, count);
        byte[] bytes = new byte[count];
        bool[] known = new bool[count];
        foreach (NativeBattlePrimitiveProcess.CommonSlice slice in slices)
        {
            Span<byte> data = bytes.AsSpan(slice.SourceOffset, slice.Count);
            Span<bool> flags = known.AsSpan(slice.SourceOffset, slice.Count);
            if (banks[slice.Bank] is { } transaction)
            {
                transaction.ReadWithKnownness(slice.Offset, data, flags);
            }
            else if (state.CommonArena(slice.Bank) is { } arena)
            {
                arena.ReadWithKnownness(slice.Offset, data, flags);
            }
        }
        return (bytes, known);
    }

    public void MarkUnknown(int offset, int count) => MarkUnknown(View, offset, count);

    public void MarkUnknown(NativeBattlePrimitiveProcess process, int offset, int count)
    {
        RequireActive();
        foreach (NativeBattlePrimitiveProcess.CommonSlice slice in process.CommonSlices(offset, count))
        {
            Bank(slice.Bank).MarkUnknown(slice.Offset, slice.Count);
        }
    }

    private NativeBattleEffectArenaTransaction Bank(int bank)
    {
        if (banks[bank] is { } existing)
        {
            return existing;
        }
        NativeBattleEffectArena arena = state.CommonArena(bank) ?? NativeBattlePrimitiveProcess.UnknownCommonArena(bank < 2 ? 114688 : 147456);
        NativeBattleEffectArenaTransaction transaction = new(arena);
        if (checkpoint is not null)
        {
            transaction.BeginCheckpoint();
        }
        banks[bank] = transaction;
        active.Add(transaction);
        state = state.StoreCommonArena(bank, arena);
        return transaction;
    }

    public void BeginCheckpoint()
    {
        RequireActive();
        if (checkpoint is not null)
        {
            throw new InvalidOperationException("Common packet checkpoints cannot be nested.");
        }
        checkpointBanks = 0;
        for (int bank = 0; bank < banks.Length; bank++)
        {
            if (banks[bank] is { } transaction)
            {
                transaction.BeginCheckpoint();
                checkpointBanks |= 1 << bank;
            }
        }
        checkpoint = state;
    }

    public void CommitCheckpoint()
    {
        RequireCheckpoint();
        foreach (NativeBattleEffectArenaTransaction transaction in active)
        {
            transaction.CommitCheckpoint();
        }
        checkpoint = null;
    }

    public void RollbackCheckpoint()
    {
        RequireCheckpoint();
        for (int bank = 0; bank < banks.Length; bank++)
        {
            if (banks[bank] is not { } transaction)
            {
                continue;
            }
            transaction.RollbackCheckpoint();
            if ((checkpointBanks & (1 << bank)) == 0)
            {
                transaction.Dispose();
                active.Remove(transaction);
                banks[bank] = null;
            }
        }
        state = checkpoint!;
        checkpoint = null;
    }

    public NativeBattlePrimitiveProcess Freeze(NativeBattlePrimitiveProcess process)
    {
        RequireActive();
        if (checkpoint is not null)
        {
            throw new InvalidOperationException("Common packet publication requires its callback checkpoint to complete.");
        }
        NativeBattleEffectArenaTransaction.CommitGroup(active);
        return process with { CommonPacketMemory = state.CommonPacketMemory, CommonTransaction = null };
    }

    private void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);

    private void RequireCheckpoint()
    {
        RequireActive();
        if (checkpoint is null)
        {
            throw new InvalidOperationException("Common packet rollback requires its active checkpoint.");
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            foreach (NativeBattleEffectArenaTransaction transaction in active)
            {
                transaction.Dispose();
            }
        }
    }
}
