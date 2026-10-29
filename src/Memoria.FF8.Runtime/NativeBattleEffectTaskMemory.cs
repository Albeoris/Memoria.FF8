using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectTaskTransfer(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

public static partial class NativeBattleEffectTaskMemory
{
    private const uint ArenaAddress = 0x20DFAB8;

    /// <summary>Borrows the live physical bank; the view does not create a second storage authority.</summary>
    public static NativeBattleEffectArena CaptureBank(NativeBattleEffectTaskPool pool)
    {
        NativeBattleTaskBankOwner owner = pool.RequireOwner();
        if (!owner.IsPool)
        {
            throw new InvalidOperationException("An unbound logical task cannot expose a physical bank view.");
        }
        return NativeBattleEffectArena.BorrowWorking(owner.Storage, owner.StorageOffset, owner.BankLength);
    }

    public static bool TryReadPointer(ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, out NativeBattleResourcePointer? pointer)
    {
        if (Find(pools, address, 4, out int pool, out int slot, out int offset))
        {
            pointer = pools[pool].Slots[slot].ReadResourcePointer(offset);
            return true;
        }
        pointer = null;
        return false;
    }

    public static bool TryRead(ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, int length, out byte[] bytes)
    {
        if (Find(pools, address, length, out int pool, out int slot, out int offset))
        {
            bytes = pools[pool].Slots[slot].ReadBytes(offset, length);
            return true;
        }
        bytes = [];
        return false;
    }

    public static ImmutableArray<NativeBattleEffectTaskPool> WritePointer(ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, NativeBattleResourcePointer? pointer)
    {
        if (!Find(pools, address, 4, out int pool, out int slot, out int offset))
        {
            throw new NotSupportedException("Native resource pointer has no physical task payload owner.");
        }
        NativeBattleEffectTaskPool selected = pools[pool];
        selected.Slots[slot].WriteResourcePointer(offset, pointer);
        return pools;
    }

    public static NativeBattleEffectMemory BindArenaBankOwnership(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        return BindArenaBankOwnership(memory, pools, restoring: false);
    }

    internal static NativeBattleEffectMemory BindArenaBankOwnership(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools, bool restoring)
    {
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            // Missing receipts retain the legacy restore publication policy. Explicit independent banks keep their owner.
            if (restoring && pool.RestoredArenaBinding == false)
            {
                continue;
            }
            if (ArenaOffset(pool) is not null)
            {
                NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native task bank requires its retained arena owner.");
                pool.BindArena(arena);
            }
        }
        return memory;
    }

    public static NativeBattleEffectTaskTransfer TransferInactiveArenaBanks(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        if (effect.ReturnedDescriptor != 0 || effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)))
        {
            throw new InvalidDataException("Native task transfer requires the actual closed returned queue and inactive banks.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native task transfer requires its retained arena owner.");
        ImmutableArray<NativeBattleEffectTaskPool>.Builder retained = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>();
        List<NativeBattleEffectTaskPool> transferred = [];
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            pool.Validate();
            if (ArenaOffset(pool) is null)
            {
                retained.Add(pool);
                continue;
            }
            if (!pool.IsBoundToStorage(arena, ArenaAddress))
            {
                throw new InvalidOperationException("Native arena task transfer requires its previously bound canonical physical owner.");
            }
            pool.RequireOwner().ValidateRetirementBoundary();
            transferred.Add(pool);
        }
        foreach (NativeBattleEffectTaskPool pool in transferred)
        {
            pool.Revoke();
        }
        return new(memory, effect with { Pools = retained.ToImmutable() });
    }

    public static NativeBattleEffectTaskTransfer DetachArenaBank(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, uint descriptor)
    {
        int index = -1;
        for (int current = 0; current < effect.Pools.Length; current++)
        {
            if (effect.Pools[current].DescriptorAddress == descriptor)
            {
                if (index >= 0)
                {
                    throw new InvalidDataException("Native descriptor reassignment has multiple physical pool owners.");
                }
                index = current;
            }
        }
        if (index < 0)
        {
            throw new InvalidDataException("Native descriptor reassignment requires its existing physical pool owner.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        pool.Validate();
        _ = ArenaOffset(pool) ?? throw new NotSupportedException("Native descriptor reassignment requires its actual arena-backed bank.");
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native descriptor reassignment requires its retained arena owner.");
        if (!pool.IsBoundToStorage(arena, ArenaAddress))
        {
            throw new InvalidOperationException("Native descriptor reassignment requires its bound canonical physical owner.");
        }
        pool.Revoke();
        return new(memory, effect with { Pools = effect.Pools.RemoveAt(index) });
    }

    /// <summary>Explicit detached bank export for lifecycle handoff; ordinary readers borrow CaptureBank.</summary>
    public static NativeBattleEffectArena ExportBank(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        using NativeBattleEffectArenaWriter writer = new(NativeBattleEffectArena.Clear());
        PublishPool(writer, pool, 0);
        return writer.Freeze();
    }

    private static void PublishPool(NativeBattleEffectArenaWriter writer, NativeBattleEffectTaskPool pool, int offset)
    {
        for (int slot = 0; slot < pool.Slots.Length; slot++)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            byte[] data = new byte[pool.Stride];
            bool[] known = new bool[pool.Stride];
            Array.Fill(known, true, 0, 12);
            BinaryPrimitives.WriteUInt16LittleEndian(data, task.Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), task.State);
            uint next = task.Next is int linked ? checked(pool.BankAddress + (uint)(linked * pool.Stride)) : 0;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), next);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), task.Callback);
            task.ReadWithKnownness(12, data.AsSpan(12), known.AsSpan(12));
            int target = checked(offset + slot * pool.Stride);
            writer.Write(target, data);
            for (int index = 12; index < known.Length;)
            {
                if (known[index])
                {
                    index++;
                    continue;
                }
                int first = index++;
                while (index < known.Length && !known[index])
                {
                    index++;
                }
                writer.MarkUnknown(target + first, index - first);
            }
            foreach ((int field, NativeBattleResourcePointer pointer) in task.EnumerateResourcePointers())
            {
                writer.SetResourcePointer(checked(target + field), pointer);
            }
        }
    }

    internal static int? ArenaOffset(NativeBattleEffectTaskPool pool)
    {
        long offset = (long)pool.BankAddress - ArenaAddress;
        long length = (long)pool.Stride * pool.Slots.Length;
        if (offset >= 0 && offset <= NativeBattleEffectArena.Length - length)
        {
            return (int)offset;
        }
        if (offset < NativeBattleEffectArena.Length && offset + length > 0)
        {
            throw new NotSupportedException("Native task bank partially overlaps its retained arena extent.");
        }
        return null;
    }

    private static bool Find(ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, int length, out int pool, out int slot, out int offset)
    {
        if (pools.IsDefault || length < 0)
        {
            throw new InvalidDataException("Native task memory requires its actual pools and byte extent.");
        }
        for (int index = 0; index < pools.Length; index++)
        {
            NativeBattleEffectTaskPool current = pools[index];
            long relative = (long)address - current.BankAddress;
            if (relative < 0 || relative >= (long)current.Stride * current.Slots.Length)
            {
                continue;
            }
            offset = (int)(relative % current.Stride);
            if (offset < 12 || length > current.Stride - offset)
            {
                throw new NotSupportedException("Native task resource read leaves its canonical payload.");
            }
            pool = index;
            slot = checked((int)(relative / current.Stride));
            return true;
        }
        pool = slot = offset = -1;
        return false;
    }
}
