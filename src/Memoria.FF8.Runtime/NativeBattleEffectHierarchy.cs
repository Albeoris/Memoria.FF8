using System.Buffers.Binary;
using System.Collections.Immutable;
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
        if (task.Payload.IsDefault || task.Payload.Length != pool.Stride - 12)
        {
            throw new InvalidDataException("Native hierarchy task write changes its physical stride.");
        }
        return this with { Pools = Pools.SetItem(poolIndex, pool with { Slots = pool.Slots.SetItem(slot, task) }) };
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
        (pool, int slot) = pool.Append(callback);
        uint address = Address(poolIndex, slot);
        NativeBattleEffectHierarchy hierarchy = this with { Pools = Pools.SetItem(poolIndex, pool) };
        NativeBattleModulePoolTask child = hierarchy.Read(address);
        byte[] bytes = child.Payload.ToArray();
        //IDIV-style division is exact here because the admitted allocation size is at least12.
        bytes.AsSpan(0, (requestedBytes - 12) / 4 * 4).Clear();
        child = child with { Payload = bytes.ToImmutableArray() };
        if (parentAddress == 0)
        {
            child = child.WriteDword(0x18, 0).WriteDword(0x14, 0).WriteDword(0x10, address);
        }
        else
        {
            NativeBattleModulePoolTask parent = hierarchy.Read(parentAddress);
            uint firstLevel = parent.ReadDword(0x14);
            uint parameterAddress = parent.ReadDword(0x0C);
            sbyte headerOffset = unchecked((sbyte)parent.ReadByte(0x2A));
            sbyte targetOffset = unchecked((sbyte)parent.ReadByte(0x2B));
            NativeBattleEffectTaskActors actors = (selectActors ?? throw new NotSupportedException("Native8DC540 requires its parameter/header/contact owner."))(parameterAddress, headerOffset, targetOffset);
            child = child.WriteDword(0x14, firstLevel == 0 ? address : firstLevel).WriteDword(0x10, parent.ReadDword(0x10)).WriteDword(0x0C, parameterAddress).WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20)).WriteDword(0x18, parentAddress);
            child = child.WriteByte(0x2A, unchecked((byte)headerOffset)).WriteByte(0x2B, unchecked((byte)targetOffset)).WriteByte(0x2E, parent.ReadByte(0x2E)).WriteByte(0x2F, parent.ReadByte(0x2F)).WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target);
            //Read after Append so a same-pool parent retains508360's new Next link.
            parent = parent.WriteByte(0x28, unchecked((byte)(parent.ReadByte(0x28) + 1)));
            hierarchy = hierarchy.Write(parentAddress, parent);
        }
        return new(hierarchy.Write(address, child), address);
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
            hierarchy = hierarchy with { Pools = hierarchy.Pools.SetItem(poolIndex, pool) };
            NativeBattleEffectHierarchyCallback result = callback(hierarchy, hierarchy.Address(poolIndex, slot));
            hierarchy = result.Hierarchy;
            return new(hierarchy.Pools[poolIndex], result.Result);
        });
        hierarchy = hierarchy with { Pools = hierarchy.Pools.SetItem(poolIndex, step.Pool) };
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
    public static byte ReadByte(this NativeBattleModulePoolTask task, int offset) => task.Payload[offset - 12];

    public static uint ReadDword(this NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12));

    public static NativeBattleModulePoolTask WriteByte(this NativeBattleModulePoolTask task, int offset, byte value)
    {
        byte[] bytes = task.Payload.ToArray();
        bytes[offset - 12] = value;
        return (task with { Payload = bytes.ToImmutableArray() }).WithoutResourcePointers(offset, 1);
    }

    public static NativeBattleModulePoolTask WriteDword(this NativeBattleModulePoolTask task, int offset, uint value)
    {
        byte[] bytes = task.Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset - 12), value);
        return (task with { Payload = bytes.ToImmutableArray() }).WithoutResourcePointers(offset, 4);
    }
}
