using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public const uint CommonAddress = 0x20DFAB8;

    public static NativeBattleModulePoolTask Read(NativeBattleEffect326Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect326 task pointer leaves its physical banks.");
    }

    public static NativeBattleEffect326Frame Write(NativeBattleEffect326Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            long offset = (long)address - pool.BankAddress;
            if (offset >= 0 && offset < (long)pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot((int)(offset / pool.Stride), task.UpdateHeader(task.Flags, task.State, pool.Slots[(int)(offset / pool.Stride)].Next, task.Callback)));
            }
        }
        throw new InvalidDataException("Effect326 task write leaves its physical banks.");
    }

    public static (NativeBattleEffect326Frame Frame, uint Address) Append(NativeBattleEffect326Frame frame, uint descriptor, uint callback)
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
        throw new InvalidDataException("Effect326 append requires its actual descriptor.");
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect326Frame frame, uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
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
                source = frame.Memory.Arena ?? throw new NotSupportedException("Effect326 requires its current COMMON owner.");
                offset = checked((int)(current - CommonAddress));
            }
            else if (stateKind == 2)
            {
                NativeBattleCureFamilyPool inactive = InactiveBank(current);
                source = frame.State.ReadBank(inactive.Bank);
                offset = checked((int)(current - inactive.Bank));
            }
            else if (stateKind == 3)
            {
                source = frame.State.ReadView(current, count);
                offset = 0;
            }
            else if (stateKind == 5)
            {
                NativeBattleCameraPoint eye = frame.Memory.GuardianCameraProcess?.PreviousPose?.Eye ?? throw new NotSupportedException("Effect326 requires its actual previous world eye publication.");
                source = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)eye.X))).Write(2, BitConverter.GetBytes(unchecked((short)eye.Y))).Write(4, BitConverter.GetBytes(unchecked((short)eye.Z)));
                offset = checked((int)(current - 0x24FD358));
            }
            else if (stateKind == 4)
            {
                NativeBattleGuardianWorkBuffer work = frame.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect326 requires its separately retained work buffer.");
                source = NativeBattleEffectArena.Clear().Write(0, work.Read(checked((int)(current - 0x209FAB8)), count));
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

    public static NativeBattleEffect326Frame WriteBytes(NativeBattleEffect326Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
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
                frame = frame with { Memory = frame.Memory with { Arena = CopyBytes(selected, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect326 requires its current COMMON owner."), checked((int)(current - CommonAddress)), count) } };
            }
            else if (stateKind == 2)
            {
                NativeBattleCureFamilyPool inactive = InactiveBank(current);
                frame = frame with { State = frame.State with { FixedBanks = frame.State.FixedBanks.SetItem(inactive.Bank, CopyBytes(selected, 0, frame.State.ReadBank(inactive.Bank), checked((int)(current - inactive.Bank)), count)) } };
            }
            else if (stateKind == 3)
            {
                frame = frame with { State = frame.State.WriteView(current, selected, count) };
            }
            else if (stateKind == 4)
            {
                NativeBattleGuardianWorkBuffer work = frame.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect326 requires its separately retained work buffer.");
                frame = frame with { Memory = frame.Memory with { GuardianWorkBuffer = work.Write(checked((int)(current - 0x209FAB8)), selected.Read(0, count)) } };
            }
            else if (stateKind == 5)
            {
                throw new NotSupportedException("Effect326 does not write its canonical previous camera eye.");
            }
            else
            {
                frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), current, selected, count) };
            }
            copied += count;
        }
        return frame;
    }

    private static int RegionLength(NativeBattleEffect326Frame frame, uint address, int maximum, out int poolIndex, out int stateKind)
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
        if (address >= 0x209FAB8 && address < CommonAddress)
        {
            stateKind = 4;
            return Math.Min(maximum, checked((int)(CommonAddress - address)));
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
        foreach (NativeBattleCureFamilyPool pool in Pools)
        {
            int length = checked(pool.Stride * pool.Capacity);
            if (address >= pool.Bank && (ulong)address < (ulong)pool.Bank + (uint)length)
            {
                stateKind = 2;
                return Math.Min(maximum, checked((int)((ulong)pool.Bank + (uint)length - address)));
            }
        }
        foreach ((uint start, int count) in ModelBindings.SelectMany(binding => new (uint, int)[] { (binding.Model, 0x90), (binding.Control, 0x34) }))
        {
            if (address >= start && (ulong)address < (ulong)start + (uint)count)
            {
                stateKind = 3;
                return Math.Min(maximum, checked((int)((ulong)start + (uint)count - address)));
            }
        }
        foreach ((uint start, int count) in OwnedRanges)
        {
            if (address >= start && (ulong)address < (ulong)start + (uint)count)
            {
                stateKind = 3;
                return Math.Min(maximum, checked((int)((ulong)start + (uint)count - address)));
            }
        }
        if (address >= 0x24FD358u && address < 0x24FD358u + 6)
        {
            stateKind = 5;
            return Math.Min(maximum, checked((int)(0x24FD358u + 6 - address)));
        }
        return maximum;
    }

    private static NativeBattleCureFamilyPool InactiveBank(uint address) => Pools.Single(pool => address >= pool.Bank && (ulong)address < (ulong)pool.Bank + (uint)(pool.Stride * pool.Capacity));

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

    public static NativeBattleEffect326Frame ClearDwords6DA380(NativeBattleEffect326Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));
    public static NativeBattleEffect326Frame WriteCompletionByte(NativeBattleEffect326Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);

    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect326Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect326 requires its current temporary memory.");
    public static (NativeBattleEffect326Frame Frame, uint Address) Allocate(NativeBattleEffect326Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleEffect326Frame Release(NativeBattleEffect326Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect326Frame frame, uint address, int length) => ReadBytes(frame, address, length);
    public static NativeBattleEffect326Frame PublishContext(NativeBattleEffect326Frame frame, uint address, NativeBattleEffectArena context, int length) => WriteBytes(frame, address, context, length);
    public static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect326Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect326 requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect326 requires current arithmetic registers."));
    public static NativeBattleEffect326Frame Publish(NativeBattleEffect326Frame frame, NativeBattleGeometryRegisterStep step) => frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
    public static NativeBattleEffect326Frame LoadMatrix(NativeBattleEffect326Frame frame, NativeBattlePackedMatrix32 matrix, bool rotation = true, bool translation = true)
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
                task = task.WriteResourcePointer(offset + field, pointer ?? throw new NotSupportedException("Effect326 task copies require an observed resource pointer."));
            }
        }
        return task;
    }
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleEffect326Frame WithPool(NativeBattleEffect326Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
}
