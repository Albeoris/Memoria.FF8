using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleResonance49Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleResonance49Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Resonance task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleResonance49Frame Write(NativeBattleResonance49Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                int slot = (int)(offset / pool.Stride);
                return WithPool(frame, index, pool with { Slots = pool.Slots.SetItem(slot, task with { Next = pool.Slots[slot].Next }) });
            }
        }
        throw new InvalidDataException("Resonance task write leaves its canonical physical pools.");
    }

    public static (NativeBattleResonance49Frame Frame, uint Address) Append(NativeBattleResonance49Frame frame, uint descriptor, uint callback, int requestedBytes, uint parentAddress, NativeBattleActionPacketsState packets)
    {
        int pool = -1;
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            if (frame.Effect.Pools[index].DescriptorAddress == descriptor)
            {
                pool = index;
                break;
            }
        }
        if (pool < 0)
        {
            throw new InvalidDataException("Resonance append requires its actual physical descriptor.");
        }
        NativeBattleEffectHierarchyAppend result = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, requestedBytes, parentAddress, (pointer, group, target) => NativeBattleCureController.SelectActors(Parameters(frame), packets, pointer, group, target));
        if (result.Address == 0)
        {
            throw new NotSupportedException("Resonance reached an unguarded exhausted native hierarchy allocation.");
        }
        return (frame with { Effect = frame.Effect with { Pools = result.Hierarchy.Pools } }, result.Address);
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleResonance49Frame frame, uint address, int length)
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
            return NativeBattleEffectNodeContextMemory.Copy(frame.Memory.Arena ?? throw new NotSupportedException("Resonance requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return frame.State.ReadView(address, length);
    }

    public static NativeBattleResonance49Frame WriteBytes(NativeBattleResonance49Frame frame, uint address, NativeBattleEffectArena source, int length)
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
                throw new InvalidDataException("Resonance copy leaves its actual task payload.");
            }
            NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], field, source, length);
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), task);
        }
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
        {
            return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Resonance requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length) } };
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

    public static NativeBattleResonance49Frame LoadMatrix(NativeBattleResonance49Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleResonance49Frame frame) => frame.Staging ?? throw new NotSupportedException("Resonance requires its actual addressed temporary memory.");
    public static (NativeBattleResonance49Frame Frame, uint Address) Allocate(NativeBattleResonance49Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleResonance49Frame Release(NativeBattleResonance49Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleResonance49Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleResonance49Frame PublishContext(NativeBattleResonance49Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.Payload.AsSpan().Slice(offset - 12, length));
        if (task.UnknownPayloadRanges is { } ranges)
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
    public static NativeBattleResonance49Frame DrawRandom(NativeBattleResonance49Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleResonance49Frame WithPool(NativeBattleResonance49Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleResonance49Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Resonance requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleResonance49Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleResonance49Frame Word(NativeBattleResonance49Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleResonance49Frame Dword(NativeBattleResonance49Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleResonance49Frame Copy(NativeBattleResonance49Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleResonance49Frame frame)
    {
        if (frame.Effect.RequestedEffect != 49 || frame.Effect.Binding.Entry != 0x821540 || frame.Effect.Binding.Initializer != 0x821520)
        {
            throw new InvalidDataException("Resonance requires its original selected entry.");
        }
    }

    private static NativeBattleResonance49Frame DetachBanks(NativeBattleResonance49Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.Effect.Pools.Length != 6 || frame.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)) || frame.State.InactiveBanks.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Resonance bank transfer requires closed queues and exclusive inactive owners.");
        }
        ImmutableArray<NativeBattleEffectArena?> storage = frame.Effect.Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectTaskMemory.CaptureBank(pool)).ToImmutableArray();
        return frame with { Effect = frame.Effect with { Pools = [] }, State = frame.State with { InactiveBanks = storage } };
    }

    public static NativeBattleResonance49Frame WriteCompletionByte(NativeBattleResonance49Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[] { value }), 1);
}
