using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectTaskActors(byte Actor, byte Target);
public sealed record NativeBattleEffectHierarchyAppend(NativeBattleEffectHierarchy Hierarchy, uint Address);
public sealed record NativeBattleEffectHierarchyCallback(NativeBattleEffectHierarchy Hierarchy, int Result);
public sealed record NativeBattleEffectHierarchyStep(NativeBattleEffectHierarchy Hierarchy, int Survivors);

/// <summary>8DC540/8DC530's parent/root links over the original508300 task banks.</summary>
public sealed record NativeBattleEffectHierarchy([property: JsonRequired] ImmutableArray<NativeBattleEffectTaskPool> Pools)
{
    public void Validate()
    {
        if (Pools.IsDefault || Pools.Any(pool => pool is null || pool.Stride < 28) || Pools.Select(pool => pool.DescriptorAddress).Distinct().Count() != Pools.Length)
        {
            throw new InvalidDataException("Native8DC540 requires its original task descriptors and hierarchy-sized slots.");
        }
        foreach (NativeBattleEffectTaskPool pool in Pools)
        {
            pool.Validate();
        }
        for (int first = 0; first < Pools.Length; first++)
        {
            for (int second = first + 1; second < Pools.Length; second++)
            {
                NativeBattleEffectTaskPool left = Pools[first];
                NativeBattleEffectTaskPool right = Pools[second];
                if ((ulong)left.BankAddress < (ulong)right.BankAddress + (ulong)right.Stride * (uint)right.Slots.Length && (ulong)right.BankAddress < (ulong)left.BankAddress + (ulong)left.Stride * (uint)left.Slots.Length)
                {
                    throw new InvalidDataException("Native hierarchy task banks overlap physical storage.");
                }
            }
        }
    }

    public uint Address(int poolIndex, int slot)
    {
        NativeBattleEffectTaskPool pool = Pools[poolIndex];
        if ((uint)slot >= pool.Slots.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return checked(pool.BankAddress + (uint)(slot * pool.Stride));
    }

    public NativeBattleModulePoolTask Read(uint address)
    {
        (int pool, int slot) = Resolve(address);
        return Pools[pool].Slots[slot];
    }

    public NativeBattleEffectHierarchy Write(uint address, NativeBattleModulePoolTask task)
    {
        (int poolIndex, int slot) = Resolve(address);
        NativeBattleEffectTaskPool pool = Pools[poolIndex];
        if (task.PayloadLength != pool.Stride - 12)
        {
            throw new InvalidDataException("Native hierarchy task write changes its physical stride.");
        }
        pool.WriteSlot(slot, task);
        return this;
    }

    public NativeBattleEffectHierarchyAppend Append8DC540(int poolIndex, uint callback, int requestedBytes, uint parentAddress = 0, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors>? selectActors = null)
    {
        Validate();
        NativeBattleEffectTaskPool pool = Pools[poolIndex];
        if (requestedBytes < 12 || requestedBytes > pool.Stride || callback == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedBytes));
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            //508360 returns null;8DC540 returns without touching a parent or its child count.
            return new(this, 0);
        }
        int available = Enumerable.Range(0, pool.Slots.Length).First(index => (pool.Slots[index].Flags & 1) == 0);
        uint address = Address(poolIndex, available);
        NativeBattleModulePoolTask candidate = pool.Slots[available];
        int clearBytes = (requestedBytes - 12) / 4 * 4;
        candidate.RequirePayloadRange(12, clearBytes);
        candidate.RequirePayloadRange(0x10, 12);
        NativeBattleModulePoolTask? parent = null;
        uint firstLevel = 0;
        uint parameterAddress = 0;
        uint rootAddress = address;
        uint parentField1C = 0;
        uint parentField20 = 0;
        byte headerOffset = 0;
        byte targetOffset = 0;
        byte field2E = 0;
        byte field2F = 0;
        byte parentChildren = 0;
        NativeBattleEffectTaskActors? actors = null;
        if (parentAddress != 0)
        {
            candidate.RequirePayloadRange(0x0C, 0x24);
            parent = Read(parentAddress);
            byte ReadParentByte(int offset)
            {
                //An inactive parent alias can be the selected allocation itself; preserve the native clear prefix.
                return ReferenceEquals(parent, candidate) && offset < 12 + clearBytes ? (byte)0 : parent.ReadByte(offset);
            }
            uint ReadParentDword(int offset)
            {
                uint value = 0;
                for (int lane = 0; lane < 4; lane++)
                {
                    value |= (uint)ReadParentByte(offset + lane) << (lane * 8);
                }
                return value;
            }
            firstLevel = ReadParentDword(0x14);
            parameterAddress = ReadParentDword(0x0C);
            rootAddress = ReadParentDword(0x10);
            parentField1C = ReadParentDword(0x1C);
            parentField20 = ReadParentDword(0x20);
            headerOffset = ReadParentByte(0x2A);
            targetOffset = ReadParentByte(0x2B);
            field2E = ReadParentByte(0x2E);
            field2F = ReadParentByte(0x2F);
            parentChildren = ReadParentByte(0x28);
            //Selection observes the unpublished allocation prefix, before shared bank mutation.
            actors = (selectActors ?? throw new NotSupportedException("Native8DC540 requires its parameter/header/contact owner."))(parameterAddress, unchecked((sbyte)headerOffset), unchecked((sbyte)targetOffset));
        }
        //Reserve the whole local operation before any store when borrowing an outer journal.
        NativeBattleTaskBankOwner bank = pool.RequireOwner();
        if (bank.HasCheckpoint)
        {
            bank.Storage.ReserveCheckpointCapacity(checked(clearBytes + 96), checked(bank.Storage.ResourcePointerCount + 16));
        }
        if (parent is not null && !ReferenceEquals(parent.RequireOwner(), bank) && parent.RequireOwner().HasCheckpoint)
        {
            parent.RequireOwner().Storage.ReserveCheckpointCapacity(1, parent.RequireOwner().Storage.ResourcePointerCount);
        }
        (pool, int slot) = pool.Append(callback);
        NativeBattleEffectHierarchy hierarchy = this;
        NativeBattleModulePoolTask child = pool.Slots[slot];
        child.WriteBytes(12, new byte[clearBytes]);
        if (parentAddress == 0)
        {
            child.WriteDword(0x18, 0).WriteDword(0x14, 0).WriteDword(0x10, address);
        }
        else
        {
            child.WriteDword(0x14, firstLevel == 0 ? address : firstLevel).WriteDword(0x10, rootAddress).WriteDword(0x0C, parameterAddress).WriteDword(0x1C, parentField1C).WriteDword(0x20, parentField20).WriteDword(0x18, parentAddress);
            child.WriteByte(0x2A, headerOffset).WriteByte(0x2B, targetOffset).WriteByte(0x2E, field2E).WriteByte(0x2F, field2F).WriteByte(0x2C, actors!.Actor).WriteByte(0x2D, actors.Target);
            //Reread after Append so a same-pool parent retains508360's new Next link.
            parent = hierarchy.Read(parentAddress);
            parent.WriteByte(0x28, unchecked((byte)(parentChildren + 1)));
        }
        return new(hierarchy, address);
    }

    public NativeBattleEffectHierarchy Complete8DC530(uint address)
    {
        uint parentAddress = Read(address).ReadDword(0x18);
        if (parentAddress == 0)
        {
            return this;
        }
        NativeBattleModulePoolTask parent = Read(parentAddress);
        return Write(parentAddress, parent.WriteByte(0x28, unchecked((byte)(parent.ReadByte(0x28) - 1))));
    }

    public NativeBattleEffectHierarchyStep Advance(int poolIndex, Func<NativeBattleEffectHierarchy, uint, NativeBattleEffectHierarchyCallback> callback)
    {
        NativeBattleEffectHierarchy hierarchy = this;
        NativeBattleEffectTaskStep step = Pools[poolIndex].Advance((pool, slot) =>
        {
            if (!ReferenceEquals(hierarchy.Pools[poolIndex].RequireOwner(), pool.RequireOwner()))
            {
                throw new InvalidDataException("Native hierarchy callback replaced its pool authority.");
            }
            NativeBattleEffectHierarchyCallback result = callback(hierarchy, hierarchy.Address(poolIndex, slot));
            hierarchy = result.Hierarchy;
            return new(hierarchy.Pools[poolIndex], result.Result);
        });
        if (!ReferenceEquals(hierarchy.Pools[poolIndex].RequireOwner(), step.Pool.RequireOwner()))
        {
            throw new InvalidDataException("Native hierarchy pass replaced its pool authority.");
        }
        hierarchy.Validate();
        return new(hierarchy, step.Survivors);
    }

    private (int Pool, int Slot) Resolve(uint address)
    {
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = Pools[index];
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return (index, checked((int)(offset / pool.Stride)));
            }
        }
        throw new InvalidDataException($"Native task pointer{address:X} is not a physical hierarchy slot.");
    }
}

public static class NativeBattleEffectTaskPayload
{
    public static byte ReadByte(this NativeBattleModulePoolTask task, int offset) => task.ReadByte(offset);
    public static uint ReadDword(this NativeBattleModulePoolTask task, int offset) => task.ReadDword(offset);
    public static NativeBattleModulePoolTask WriteByte(this NativeBattleModulePoolTask task, int offset, byte value) => task.WriteByte(offset, value);
    public static NativeBattleModulePoolTask WriteDword(this NativeBattleModulePoolTask task, int offset, uint value) => task.WriteDword(offset, value);
}
