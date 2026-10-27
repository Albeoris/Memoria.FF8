using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Controller
{
    private const uint CommonAddress = 0x20DFAB8;

    public static short Word54(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    public static uint Dword54(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));

    public static NativeBattleModulePoolTask Read(NativeBattleEffect54Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect54 task pointer leaves its physical banks.");
    }

    public static NativeBattleEffect54Frame Write(NativeBattleEffect54Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Effect54 task write leaves its physical banks.");
    }

    public static (NativeBattleEffect54Frame Frame, uint Address) AppendHierarchy(NativeBattleEffect54Frame frame, int poolIndex, uint callback, int length, uint parent, NativeBattleEffect54Inputs inputs)
    {
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(poolIndex, callback, length, parent, (pointer, header, contact) => NativeBattleCureController.SelectActors(Parameters(frame), inputs.ReadPackets(frame), pointer, header, contact));
        if (appended.Address == 0)
        {
            throw new InvalidDataException("Effect54 exhausted a mandatory hierarchy allocation.");
        }
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect54Frame frame, uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (Contains(NativeBattleEffectNodeContextMemory.Address, 180, address, length))
        {
            return CopyBytes(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), checked((int)(address - NativeBattleEffectNodeContextMemory.Address)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        if (Contains(0x2793E58, 32, address, length))
        {
            return CopyBytes(PackedArena(frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Effect54 requires its current shared view.")), checked((int)(address - 0x2793E58)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
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
                source = frame.Memory.Arena ?? throw new NotSupportedException("Effect54 requires its current COMMON owner.");
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

    public static NativeBattleEffect54Frame WriteBytes(NativeBattleEffect54Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (Contains(NativeBattleEffectNodeContextMemory.Address, 180, address, length))
        {
            return frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, CopyBytes(source, 0, NativeBattleEffectNodeContextMemory.Capture(frame.Memory), checked((int)(address - NativeBattleEffectNodeContextMemory.Address)), length)) };
        }
        if (Contains(0x2793E58, 32, address, length))
        {
            NativeBattleEffectArena view = frame.Memory.SharedEffectView2793E58 is { } prior ? PackedArena(prior) : NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
            view = CopyBytes(source, 0, view, checked((int)(address - 0x2793E58)), length);
            return frame with { Memory = frame.Memory with { SharedEffectView2793E58 = Packed(view) } };
        }
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
                frame = frame with { Memory = frame.Memory with { Arena = CopyBytes(part, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect54 requires its current COMMON owner."), checked((int)(current - CommonAddress)), count) } };
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

    private static int RegionLength(NativeBattleEffect54Frame frame, uint address, int length, out int poolIndex, out bool state, out bool common)
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

    public static NativeBattleEffect54Frame ClearDwords701200(NativeBattleEffect54Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));
    public static NativeBattleEffect54Frame WriteCompletionByte(NativeBattleEffect54Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect54Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect54 requires its current temporary memory.");
    public static (NativeBattleEffect54Frame Frame, uint Address) Allocate(NativeBattleEffect54Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleEffect54Frame Release(NativeBattleEffect54Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect54Frame frame, uint address, int length) => ReadBytes(frame, address, length);
    public static NativeBattleEffect54Frame PublishContext(NativeBattleEffect54Frame frame, uint address, NativeBattleEffectArena context, int length) => WriteBytes(frame, address, context, length);
    public static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect54Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect54 requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect54 requires current arithmetic registers."));
    public static NativeBattleEffect54Frame Publish(NativeBattleEffect54Frame frame, NativeBattleGeometryRegisterStep step) => frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
    public static NativeBattleEffect54Frame LoadMatrix(NativeBattleEffect54Frame frame, NativeBattlePackedMatrix32 matrix, bool rotation = true, bool translation = true)
    {
        matrix.Validate();
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect54 requires current geometry registers.");
        geometry.Validate();
        for (int index = 0; index < 32; index++)
        {
            if ((rotation && index < 18 || translation && index >= 20) && !matrix.Known[index])
            {
                throw new NotSupportedException("Effect54 requires the selected native matrix components.");
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
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        foreach (NativeBattleEffectUnknownRegion range in task.UnknownPayloadRanges ?? [])
        {
            int first = Math.Max(offset, range.Offset);
            int last = Math.Min(offset + length, range.Offset + range.Length);
            if (first < last)
            {
                result = result.MarkUnknown(first - offset, last - first);
            }
        }
        return result;
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
                task = task.WriteResourcePointer(offset + field, pointer ?? throw new NotSupportedException("Effect54 task copies require an observed resource pointer."));
            }
        }
        return task;
    }
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    public static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset = 0)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    public static NativeBattleEffect54Frame ClearBytes(NativeBattleEffect54Frame frame, uint address, int length) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[length]), length);
    public static uint PhysicalModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(156 * actor)) : throw new InvalidDataException("Effect54 model index leaves its physical bank.");
    public static NativeBattleEffectParameters Parameters(NativeBattleEffect54Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect54 requires its current action parameters.");
    public static NativeBattleEffectArena Source(NativeBattleEffect54Frame frame, NativeBattleEffect54Resources resources, uint address, int length) => NativeBattleEffectArena.Clear().Write(0, resources.ReadResource(RequireStaging(frame), address, length));
    private static bool Contains(uint start, int extent, uint address, int length) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)extent;
    private static NativeBattleEffect54Frame WithPool(NativeBattleEffect54Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
}
