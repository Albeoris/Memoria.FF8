using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect301Controller
{
    private const uint CommonAddress = 0x20DFAB8;

    public static NativeBattleModulePoolTask Read(NativeBattleEffect301Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect301 task pointer leaves its physical banks.");
    }

    public static NativeBattleEffect301Frame Write(NativeBattleEffect301Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                int slot = (int)(offset / pool.Stride);
                return WithPool(frame, index, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)));
            }
        }
        throw new InvalidDataException("Effect301 task write leaves its physical banks.");
    }

    public static (NativeBattleEffect301Frame Frame, uint Address) Append(NativeBattleEffect301Frame frame, uint descriptor, uint callback)
    {
        int index = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect301Frame frame, uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            int count = RegionLength(frame, current, length - copied, out int poolIndex, out bool state, out bool common);
            NativeBattleEffectArena source;
            int offset;
            if (poolIndex >= 0)
            {
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
                source = NativeBattleEffectTaskMemory.CaptureBank(pool);
                offset = checked((int)(current - pool.BankAddress));
            }
            else if (state)
            {
                uint? bank = InactiveBank(current, count);
                source = bank is uint bankAddress ? frame.State.ReadBank(bankAddress) : frame.State.ReadView(current, count);
                offset = bank is uint start ? checked((int)(current - start)) : 0;
            }
            else if (common)
            {
                source = frame.Memory.Arena ?? throw new NotSupportedException("Effect301 requires its current COMMON owner.");
                offset = checked((int)(current - CommonAddress));
            }
            else
            {
                source = NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), current, count);
                offset = 0;
            }
            result = CopyBytes(source, offset, result, copied, count);
            copied += count;
        }
        return result;
    }

    public static NativeBattleEffect301Frame WriteBytes(NativeBattleEffect301Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            int count = RegionLength(frame, current, length - copied, out int poolIndex, out bool state, out bool common);
            NativeBattleEffectArena part = CopyBytes(source, copied, NativeBattleEffectArena.Clear().MarkUnknown(0, count), 0, count);
            if (poolIndex >= 0)
            {
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
                frame = WithPool(frame, poolIndex, NativeBattleEffectTaskMemory.WriteBankSpan(pool, checked((int)(current - pool.BankAddress)), part, count));
            }
            else if (state)
            {
                uint? bank = InactiveBank(current, count);
                frame = bank is uint bankAddress ? frame with { State = frame.State with { FixedBanks = frame.State.FixedBanks.SetItem(bankAddress, CopyBytes(part, 0, frame.State.ReadBank(bankAddress), checked((int)(current - bankAddress)), count)) } } : frame with { State = frame.State.WriteView(current, part, count) };
            }
            else if (common)
            {
                frame = frame with { Memory = frame.Memory with { Arena = CopyBytes(part, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect301 requires its current COMMON owner."), checked((int)(current - CommonAddress)), count) } };
            }
            else
            {
                frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), current, part, count) };
            }
            copied += count;
        }
        return frame;
    }

    private static uint? InactiveBank(uint address, int length)
    {
        foreach (NativeBattleCureFamilyPool pool in Pools)
        {
            if (address >= pool.Bank && (ulong)address + (uint)length <= (ulong)pool.Bank + (uint)(pool.Stride * pool.Capacity))
            {
                return pool.Bank;
            }
        }
        return null;
    }

    private static int RegionLength(NativeBattleEffect301Frame frame, uint address, int length, out int poolIndex, out bool state, out bool common)
    {
        poolIndex = -1;
        state = false;
        common = false;
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            ulong end = (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length);
            if (address >= pool.BankAddress && address < end)
            {
                poolIndex = index;
                return Math.Min(length, checked((int)(end - address)));
            }
        }
        if (address >= ScriptPointerAddress && (ulong)address < (ulong)ScriptPointerAddress + 4)
        {
            state = true;
            return Math.Min(length, checked((int)(ScriptPointerAddress + 4 - address)));
        }
        foreach ((uint start, int count) in OwnedRanges.Concat(Pools.Select(pool => (pool.Bank, pool.Stride * pool.Capacity))))
        {
            ulong end = (ulong)start + (uint)count;
            if (address >= start && address < end)
            {
                state = true;
                return Math.Min(length, checked((int)(end - address)));
            }
        }
        if (address >= CommonAddress && (ulong)address < (ulong)CommonAddress + NativeBattleEffectArena.Length)
        {
            common = true;
            return Math.Min(length, checked((int)((ulong)CommonAddress + NativeBattleEffectArena.Length - address)));
        }
        return length;
    }
    public static NativeBattleEffectArena CopyBytes(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int count)
    {
        destination = NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, destination, destinationOffset, count);
        foreach ((int offset, NativeBattleResourcePointer? pointer) in source.EnumerateResourcePointers())
        {
            if (offset >= sourceOffset && offset + 4 <= sourceOffset + count)
            {
                destination = destination.WriteResourcePointer(destinationOffset + offset - sourceOffset, pointer);
            }
        }
        return destination;
    }

    public static NativeBattleVector Vector(NativeBattleEffectArena point, int offset = 0) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset + 4, 2)));
    public static NativeBattleEffectArena PointView(NativeBattleVector point) => PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, point);
    public static NativeBattleEffectArena PutPoint(NativeBattleEffectArena target, int offset, NativeBattleVector point) => target.Write(offset, BitConverter.GetBytes(unchecked((short)point.X))).Write(offset + 2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(offset + 4, BitConverter.GetBytes(unchecked((short)point.Z)));

    public static NativeBattleEffect301Frame ClearDwords701200(NativeBattleEffect301Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));
    public static NativeBattleEffect301Frame WriteCompletionByte(NativeBattleEffect301Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect301Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect301 requires its current temporary memory.");
    public static (NativeBattleEffect301Frame Frame, uint Address) Allocate(NativeBattleEffect301Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleEffect301Frame Release(NativeBattleEffect301Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect301Frame frame, uint address, int length) => ReadBytes(frame, address, length);
    public static NativeBattleEffect301Frame PublishContext(NativeBattleEffect301Frame frame, uint address, NativeBattleEffectArena context, int length) => WriteBytes(frame, address, context, length);
    public static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect301Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect301 requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect301 requires current arithmetic registers."));
    public static NativeBattleEffect301Frame Publish(NativeBattleEffect301Frame frame, NativeBattleGeometryRegisterStep step) => frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
    public static NativeBattleEffect301Frame LoadMatrix(NativeBattleEffect301Frame frame, NativeBattlePackedMatrix32 matrix, bool rotation = true, bool translation = true)
    {
        matrix.Validate();
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect301 requires current geometry registers.");
        geometry.Validate();
        for (int index = 0; index < 32; index++)
        {
            if ((rotation && index < 18 || translation && index >= 20) && !matrix.Known[index])
            {
                throw new NotSupportedException("Effect301 requires the selected native matrix components.");
            }
        }
        if (rotation)
        {
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray() };
        }
        if (translation)
        {
            geometry = geometry with { Translation = [BinaryPrimitives.ReadInt32LittleEndian(matrix.Bytes.AsSpan().Slice(20)), BinaryPrimitives.ReadInt32LittleEndian(matrix.Bytes.AsSpan().Slice(24)), BinaryPrimitives.ReadInt32LittleEndian(matrix.Bytes.AsSpan().Slice(28))] };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }
    public static NativeBattleEffectArena PackedArena(NativeBattlePackedMatrix32 matrix)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                result = result.MarkUnknown(index, 1);
            }
        }
        return result;
    }
    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }
    public static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        foreach ((int field, NativeBattleResourcePointer? pointer) in source.EnumerateResourcePointers())
        {
            if (field >= 0 && field + 4 <= length)
            {
                task = task.WriteResourcePointer(offset + field, pointer ?? throw new NotSupportedException("Effect301 task copies require an observed resource pointer."));
            }
        }
        return task;
    }
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffect301Frame WithPool(NativeBattleEffect301Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
}
