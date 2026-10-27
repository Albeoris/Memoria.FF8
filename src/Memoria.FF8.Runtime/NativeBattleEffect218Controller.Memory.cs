using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleEffect218Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect218 task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleEffect218Frame Write(NativeBattleEffect218Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Effect218 task write leaves its canonical physical pools.");
    }

    public static (NativeBattleEffect218Frame Frame, uint Address) Append(NativeBattleEffect218Frame frame, uint descriptor, uint callback)
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
            throw new InvalidDataException("Effect218 append requires its actual addressed pool.");
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }
    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect218Frame frame, uint address, int length)
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
            return NativeBattleEffectNodeContextMemory.Copy(frame.Memory.Arena ?? throw new NotSupportedException("Effect218 requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return frame.State.ReadView(address, length);
    }

    public static NativeBattleEffect218Frame WriteBytes(NativeBattleEffect218Frame frame, uint address, NativeBattleEffectArena source, int length)
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
                throw new InvalidDataException("Effect218 copy leaves its actual task payload.");
            }
            NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], field, source, length);
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), task);
        }
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
        {
            return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect218 requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length) } };
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

    public static NativeBattleEffect218Frame LoadMatrix(NativeBattleEffect218Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect218Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect218 requires its actual addressed temporary memory.");
    public static (NativeBattleEffect218Frame Frame, uint Address) Allocate(NativeBattleEffect218Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleEffect218Frame Release(NativeBattleEffect218Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect218Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleEffect218Frame PublishContext(NativeBattleEffect218Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
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
    private static NativeBattleEffect218Frame WithPool(NativeBattleEffect218Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleEffect218Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect218 requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleEffect218Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleEffect218Frame Word(NativeBattleEffect218Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleEffect218Frame Dword(NativeBattleEffect218Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleEffect218Frame Copy(NativeBattleEffect218Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleEffect218Frame frame)
    {
        if (frame.Effect.RequestedEffect != 218 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect218 requires its original selected entry.");
        }
    }

    private static NativeBattleEffect218Frame DetachBanks(NativeBattleEffect218Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.Effect.Pools.Any(pool => pool.Head is not null))
        {
            throw new InvalidDataException("Effect218 bank transfer requires its fully retired physical task pools.");
        }
        NativeBattleEffect218State state = frame.State;
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            state = state.WriteView(pool.BankAddress, NativeBattleEffectTaskMemory.CaptureBank(pool), pool.Stride * pool.Slots.Length);
        }
        return frame with { State = state, Effect = frame.Effect with { Pools = [] } };
    }

    private static uint ModelAddress(int actor)
    {
        if ((uint)actor >= 7)
        {
            throw new NotSupportedException("Effect218 requires an actual physical model row.");
        }
        return checked(0x1D972C0u + (uint)(actor * 156));
    }
}
