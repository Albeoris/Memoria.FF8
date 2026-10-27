using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    private const uint CommonAddress = 0x20DFAB8;

    public static NativeBattleModulePoolTask Read(NativeBattleEffect65Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect65 task pointer leaves its physical banks.");
    }

    public static NativeBattleEffect65Frame Write(NativeBattleEffect65Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot((int)(offset / pool.Stride), task));
            }
        }
        throw new InvalidDataException("Effect65 task write leaves its physical banks.");
    }

    public static (NativeBattleEffect65Frame Frame, uint Address) Append(NativeBattleEffect65Frame frame, uint descriptor, uint callback)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != descriptor)
            {
                continue;
            }
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                return (frame, 0);
            }
            (pool, int slot) = pool.Append(callback);
            return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
        }
        throw new InvalidDataException("Effect65 append requires its actual descriptor.");
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect65Frame frame, uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (address >= ChildDescriptor && (ulong)address + (uint)length <= (ulong)ChildDescriptor + ChildQueueLength && frame.State.ChildQueueStorage is null)
        {
            NativeBattleEffectTaskPool child = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
            NativeBattleEffectArena queue = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(child.Head is int head ? checked(child.BankAddress + (uint)(head * child.Stride)) : 0u)).Write(4, BitConverter.GetBytes(child.Tail is int tail ? checked(child.BankAddress + (uint)(tail * child.Stride)) : 0u));
            return CopyBytes(queue, checked((int)(address - ChildDescriptor)), NativeBattleEffectArena.Clear(), 0, length);
        }
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            int count = RegionLength(frame, current, length - copied, out int poolIndex, out int stateKind);
            NativeBattleEffectArena source;
            int offset;
            if (poolIndex >= 0)
            {
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
                source = NativeBattleEffectTaskMemory.CaptureBank(pool);
                offset = checked((int)(current - pool.BankAddress));
            }
            else if (stateKind == 1)
            {
                source = frame.Memory.Arena ?? throw new NotSupportedException("Effect65 requires its current COMMON owner.");
                offset = checked((int)(current - CommonAddress));
            }
            else if (stateKind == 2)
            {
                source = frame.State.RootBankStorage ?? throw new NotSupportedException("Effect65 requires its inactive root bank.");
                offset = checked((int)(current - RootBank));
            }
            else if (stateKind == 3)
            {
                source = frame.State.ReadView(current, count);
                offset = 0;
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

    public static NativeBattleEffect65Frame WriteBytes(NativeBattleEffect65Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (address >= ChildDescriptor && (ulong)address + (uint)length <= (ulong)ChildDescriptor + ChildQueueLength && frame.State.ChildQueueStorage is null)
        {
            int index = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == ChildDescriptor);
            NativeBattleEffectTaskPool child = frame.Effect.Pools[index];
            NativeBattleEffectArena queue = CopyBytes(source, 0, ReadBytes(frame, ChildDescriptor, ChildQueueLength), checked((int)(address - ChildDescriptor)), length);
            int? Pointer(int offset)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(queue.Read(offset, 4));
                if (pointer == 0)
                {
                    return null;
                }
                long relative = (long)pointer - child.BankAddress;
                if (relative < 0 || relative >= (long)child.Stride * child.Slots.Length || relative % child.Stride != 0)
                {
                    throw new InvalidDataException("Effect65 child descriptor pointer leaves its actual bank.");
                }
                return checked((int)(relative / child.Stride));
            }
            return WithPool(frame, index, child.UpdateLinks(Pointer(0), Pointer(4)));
        }
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            int count = RegionLength(frame, current, length - copied, out int poolIndex, out int stateKind);
            NativeBattleEffectArena selected = CopyBytes(source, copied, NativeBattleEffectArena.Clear().MarkUnknown(0, count), 0, count);
            if (poolIndex >= 0)
            {
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
                frame = WithPool(frame, poolIndex, NativeBattleEffectTaskMemory.WriteBankSpan(pool, checked((int)(current - pool.BankAddress)), selected, count));
            }
            else if (stateKind == 1)
            {
                frame = frame with { Memory = frame.Memory with { Arena = CopyBytes(selected, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect65 requires its current COMMON owner."), checked((int)(current - CommonAddress)), count) } };
            }
            else if (stateKind == 2)
            {
                frame = frame with { State = frame.State with { RootBankStorage = CopyBytes(selected, 0, frame.State.RootBankStorage ?? throw new NotSupportedException("Effect65 requires its inactive root bank."), checked((int)(current - RootBank)), count) } };
            }
            else if (stateKind == 3)
            {
                frame = frame with { State = frame.State.WriteView(current, selected, count) };
            }
            else
            {
                frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), current, selected, count) };
            }
            copied += count;
        }
        return frame;
    }

    private static int RegionLength(NativeBattleEffect65Frame frame, uint address, int maximum, out int poolIndex, out int stateKind)
    {
        poolIndex = -1;
        stateKind = 0;
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length)
            {
                poolIndex = index;
                return Math.Min(maximum, checked(pool.Stride * pool.Slots.Length - (int)offset));
            }
        }
        if (address >= CommonAddress && (ulong)address < (ulong)CommonAddress + NativeBattleEffectArena.Length)
        {
            stateKind = 1;
            int count = Math.Min(maximum, checked((int)((ulong)CommonAddress + NativeBattleEffectArena.Length - address)));
            foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
            {
                if (pool.BankAddress > address)
                {
                    count = (int)Math.Min((uint)count, pool.BankAddress - address);
                }
            }
            return count;
        }
        if (address >= RootBank && address < RootBank + RootBankLength)
        {
            stateKind = 2;
            return Math.Min(maximum, checked((int)(RootBank + RootBankLength - address)));
        }
        foreach ((uint start, int count) in OwnedRanges.Concat(LiteralRanges).Append((ChildDescriptor, ChildQueueLength)))
        {
            if (address >= start && (ulong)address < (ulong)start + (uint)count)
            {
                stateKind = 3;
                return Math.Min(maximum, checked((int)((ulong)start + (uint)count - address)));
            }
        }
        return maximum;
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

    public static NativeBattleEffectArena ReadSource(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, uint address, int length)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        int copied = 0;
        while (copied < length)
        {
            uint current = checked(address + (uint)copied);
            int remaining = length - copied;
            int count = 0;
            foreach ((uint start, int extent) in LiteralRanges.Concat(OwnedRanges))
            {
                if (current >= start && (ulong)current < (ulong)start + (uint)extent)
                {
                    count = Math.Min(remaining, checked((int)((ulong)start + (uint)extent - current)));
                    result = CopyBytes(ReadBytes(frame, current, count), 0, result, copied, count);
                    break;
                }
            }
            if (count == 0 && current >= CommonAddress && (ulong)current < (ulong)CommonAddress + NativeBattleEffectArena.Length)
            {
                count = Math.Min(remaining, checked((int)((ulong)CommonAddress + NativeBattleEffectArena.Length - current)));
                result = CopyBytes(ReadBytes(frame, current, count), 0, result, copied, count);
            }
            if (count == 0)
            {
                foreach ((uint start, ImmutableArray<byte> data) in resources.StaticFronts.Concat(resources.Meshes.Select(pair => new KeyValuePair<uint, ImmutableArray<byte>>(pair.Key, pair.Value.Bytes))))
                {
                    if (current < start || (ulong)current >= (ulong)start + (uint)data.Length)
                    {
                        continue;
                    }
                    count = Math.Min(remaining, checked((int)((ulong)start + (uint)data.Length - current)));
                    foreach ((uint mutable, int _) in LiteralRanges)
                    {
                        if (mutable > current)
                        {
                            count = (int)Math.Min((uint)count, mutable - current);
                        }
                    }
                    result = result.Write(copied, data.AsSpan().Slice(checked((int)(current - start)), count));
                    break;
                }
            }
            if (count == 0)
            {
                throw new NotSupportedException("Effect65 source read has no current physical or selected readonly owner.");
            }
            copied += count;
        }
        return result;
    }

    public static NativeBattleVector Vector(NativeBattleEffectArena point, int offset = 0) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(offset + 4, 2)));
    public static NativeBattleEffectArena PointView(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    public static NativeBattleEffectArena PutPoint(NativeBattleEffectArena target, int offset, NativeBattleVector point) => target.Write(offset, BitConverter.GetBytes(point.X)).Write(offset + 2, BitConverter.GetBytes(point.Y)).Write(offset + 4, BitConverter.GetBytes(point.Z));

    public static NativeBattleEffect65Frame ClearDwords701200(NativeBattleEffect65Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));
    public static NativeBattleEffect65Frame WriteCompletionByte(NativeBattleEffect65Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect65Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect65 requires its current temporary memory.");
    public static (NativeBattleEffect65Frame Frame, uint Address) Allocate(NativeBattleEffect65Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleEffect65Frame Release(NativeBattleEffect65Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect65Frame frame, uint address, int length) => ReadBytes(frame, address, length);
    public static NativeBattleEffect65Frame PublishContext(NativeBattleEffect65Frame frame, uint address, NativeBattleEffectArena context, int length) => WriteBytes(frame, address, context, length);
    public static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect65Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect65 requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect65 requires current arithmetic registers."));
    public static NativeBattleEffect65Frame Publish(NativeBattleEffect65Frame frame, NativeBattleGeometryRegisterStep step) => frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
    public static NativeBattleEffect65Frame LoadMatrix(NativeBattleEffect65Frame frame, NativeBattlePackedMatrix32 matrix, bool rotation = true, bool translation = true)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix.ReadMatrix(), rotation, translation, null);
        if (rotation)
        {
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray() };
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
        foreach (NativeBattleEffectUnknownRegion range in task.ReadUnknownPayloadRanges() ?? [])
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
                task = task.WriteResourcePointer(offset + field, pointer ?? throw new NotSupportedException("Effect65 task copies require an observed resource pointer."));
            }
        }
        return task;
    }
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffect65Frame WithPool(NativeBattleEffect65Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
}
