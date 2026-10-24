using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectTaskTransfer(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

public static partial class NativeBattleEffectTaskMemory
{
    private const uint ArenaAddress = 0x20DFAB8;

    public static NativeBattleEffectArena CaptureBank(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        if ((long)pool.Stride * pool.Slots.Length > NativeBattleEffectArena.Length)
        {
            throw new NotSupportedException("Native task bank capture leaves its bounded physical storage carrier.");
        }
        return PublishPool(NativeBattleEffectArena.Clear(), pool, 0);
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
        return pools.SetItem(pool, selected with { Slots = selected.Slots.SetItem(slot, selected.Slots[slot].WriteResourcePointer(offset, pointer)) });
    }

    public static NativeBattleEffectMemory RevokeArenaOwnership(NativeBattleEffectMemory memory, ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native task bank requires its retained arena owner.");
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            if (ArenaOffset(pool) is int offset)
            {
                arena = arena.MarkUnknown(offset, checked(pool.Stride * pool.Slots.Length));
            }
        }
        return memory with { Arena = arena };
    }

    public static NativeBattleEffectTaskTransfer TransferInactiveArenaBanks(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        if (effect.ReturnedDescriptor != 0 || effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)))
        {
            throw new InvalidDataException("Native task transfer requires the actual closed returned queue and inactive banks.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native task transfer requires its retained arena owner.");
        ImmutableArray<NativeBattleEffectTaskPool>.Builder retained = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>();
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            pool.Validate();
            if (ArenaOffset(pool) is not int offset)
            {
                retained.Add(pool);
                continue;
            }
            arena = PublishPool(arena, pool, offset);
        }
        return new(memory with { Arena = arena }, effect with { Pools = retained.ToImmutable() });
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
        int offset = ArenaOffset(pool) ?? throw new NotSupportedException("Native descriptor reassignment requires its actual arena-backed bank.");
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native descriptor reassignment requires its retained arena owner.");
        arena = PublishPool(arena, pool, offset);
        return new(memory with { Arena = arena }, effect with { Pools = effect.Pools.RemoveAt(index) });
    }

    private static NativeBattleEffectArena PublishPool(NativeBattleEffectArena arena, NativeBattleEffectTaskPool pool, int offset)
    {
        for (int slot = 0; slot < pool.Slots.Length; slot++)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            byte[] data = new byte[pool.Stride];
            BinaryPrimitives.WriteUInt16LittleEndian(data, task.Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), task.State);
            uint next = task.Next is int linked ? checked(pool.BankAddress + (uint)(linked * pool.Stride)) : 0;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), next);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), task.Callback);
            task.Payload.AsSpan().CopyTo(data.AsSpan(12));
            int target = checked(offset + slot * pool.Stride);
            arena = arena.Write(target, data);
            if (task.UnknownPayloadRanges is { } unknown)
            {
                foreach (NativeBattleEffectUnknownRegion range in unknown)
                {
                    arena = arena.MarkUnknown(checked(target + range.Offset), range.Length);
                }
            }
            if (task.ResourcePointers is { } pointers)
            {
                foreach ((int field, NativeBattleResourcePointer pointer) in pointers)
                {
                    arena = arena.WriteResourcePointer(checked(target + field), pointer);
                }
            }
        }
        return arena;
    }

    private static int? ArenaOffset(NativeBattleEffectTaskPool pool)
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
