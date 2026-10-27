using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleEffect298Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect298 task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleEffect298Frame Write(NativeBattleEffect298Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                int slot = (int)(offset / pool.Stride);
                return WithPool(frame, index, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)));
            }
        }
        throw new InvalidDataException("Effect298 task write leaves its canonical physical pools.");
    }

    public static (NativeBattleEffect298Frame Frame, uint Address) Append(NativeBattleEffect298Frame frame, uint descriptor, uint callback)
    {
        int index = -1;
        for (int current = 0; current < frame.Effect.Pools.Length; current++)
        {
            if (frame.Effect.Pools[current].DescriptorAddress == descriptor)
            {
                index = current;
                break;
            }
        }
        if (index < 0)
        {
            throw new InvalidDataException("Effect298 append requires its actual addressed pool.");
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }
    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect298Frame frame, uint address, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && (ulong)address + (uint)length <= (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length))
            {
                return NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), checked((int)(address - pool.BankAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
        }
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
        {
            return NativeBattleEffectNodeContextMemory.Copy(frame.Memory.Arena ?? throw new NotSupportedException("Effect298 requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return frame.State.ReadView(address, length);
    }

    public static NativeBattleEffect298Frame WriteBytes(NativeBattleEffect298Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address < pool.BankAddress || (ulong)address + (uint)length > (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length))
            {
                continue;
            }
            int offset = checked((int)(address - pool.BankAddress));
            int slot = offset / pool.Stride;
            int field = offset % pool.Stride;
            if (field < 12 || field + length > pool.Stride)
            {
                throw new InvalidDataException("Effect298 copy leaves its actual task payload.");
            }
            NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], field, source, length);
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), task);
        }
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
        {
            return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect298 requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length) } };
        }
        return frame with { State = frame.State.WriteView(address, source, length) };
    }

    public static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    public static NativeBattleEffect298Frame LoadMatrix(NativeBattleEffect298Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
    {
        packed.Validate();
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), packed.ReadMatrix(), rotation, translation, null);
        if (rotation)
        {
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }
    public static NativeBattleEffectArena PackedArena(NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!packed.Known[index])
            {
                result = result.MarkUnknown(index, 1);
            }
        }
        return result;
    }
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect298Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect298 requires its actual addressed temporary memory.");
    public static (NativeBattleEffect298Frame Frame, uint Address) Allocate(NativeBattleEffect298Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleEffect298Frame Release(NativeBattleEffect298Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect298Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleEffect298Frame PublishContext(NativeBattleEffect298Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        if (task.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(offset, range.Offset);
                int end = Math.Min(offset + length, range.Offset + range.Length);
                if (first < end)
                {
                    view = view.MarkUnknown(first - offset, end - first);
                }
            }
        }
        return view;
    }

    public static NativeBattleEffectArena PointView(NativeBattleVector point, short? w = null)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return w is { } value ? result.Write(6, BitConverter.GetBytes(value)) : result;
    }
    private static NativeBattleEffect298Frame WithPool(NativeBattleEffect298Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleEffect298Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect298 requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleEffect298Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleEffect298Frame Word(NativeBattleEffect298Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleEffect298Frame Dword(NativeBattleEffect298Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleEffect298Frame Copy(NativeBattleEffect298Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleEffect298Frame frame)
    {
        if (frame.Effect.RequestedEffect != 298 || frame.Effect.Binding.Entry != 0x670970 || frame.Effect.Binding.Initializer != 0x670980)
        {
            throw new InvalidDataException("Effect298 requires its original selected entry.");
        }
    }

    private static NativeBattleEffect298Frame DetachBanks(NativeBattleEffect298Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.State.RootBankStorage is not null || frame.Effect.Pools.IsEmpty || frame.Effect.Pools[0].Head is not null)
        {
            throw new InvalidDataException("Effect298 bank transfer requires its retired root and exclusive physical owners.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect298 requires its retained COMMON arena.");
        for (int index = 1; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            arena = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), 0, arena, checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress)), pool.Stride * pool.Slots.Length);
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = [] }, State = frame.State with { RootBankStorage = NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[0]) } };
    }

    private static uint ModelAddress(int actor)
    {
        if ((uint)actor >= 7)
        {
            throw new NotSupportedException("Effect298 requires an actual physical model row.");
        }
        return checked(0x1D972C0u + (uint)(actor * 156));
    }
}
