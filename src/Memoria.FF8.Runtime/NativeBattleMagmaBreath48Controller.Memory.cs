using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMagmaBreath48Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleMagmaBreath48Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Magma Breath task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleMagmaBreath48Frame Write(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Magma Breath task write leaves its canonical physical pools.");
    }

    public static (NativeBattleMagmaBreath48Frame Frame, uint Address) Append(NativeBattleMagmaBreath48Frame frame, uint descriptor, uint callback, int requestedBytes, uint parentAddress, NativeBattleActionPacketsState packets)
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
            throw new InvalidDataException("Magma Breath append requires its actual physical descriptor.");
        }
        NativeBattleEffectHierarchyAppend result = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(pool, callback, requestedBytes, parentAddress, (pointer, group, target) => NativeBattleCureController.SelectActors(Parameters(frame), packets, pointer, group, target));
        if (result.Address == 0)
        {
            throw new NotSupportedException("Magma Breath reached an unguarded exhausted native hierarchy allocation.");
        }
        return (frame with { Effect = frame.Effect with { Pools = result.Hierarchy.Pools } }, result.Address);
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleMagmaBreath48Frame frame, uint address, int length)
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
            return NativeBattleEffectNodeContextMemory.Copy(frame.Memory.Arena ?? throw new NotSupportedException("Magma Breath requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return frame.State.ReadView(address, length);
    }

    public static NativeBattleMagmaBreath48Frame WriteBytes(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleEffectArena source, int length)
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
                throw new InvalidDataException("Magma Breath copy leaves its actual task payload.");
            }
            NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], field, source, length);
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), task);
        }
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
        {
            return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Magma Breath requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length) } };
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

    public static NativeBattleMagmaBreath48Frame LoadMatrix(NativeBattleMagmaBreath48Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleMagmaBreath48Frame frame) => frame.Staging ?? throw new NotSupportedException("Magma Breath requires its actual addressed temporary memory.");
    public static (NativeBattleMagmaBreath48Frame Frame, uint Address) Allocate(NativeBattleMagmaBreath48Frame frame, int length)
    {
        RequireScratchLength(length);
        uint cursor = frame.State.ReadDword(0x26495D8);
        uint origin = NativeBattleEffectNodeContextMemory.Address;
        uint end = origin + NativeBattleEffectNodeContextMemory.Length;
        if (cursor < origin + (uint)length || cursor > end)
        {
            throw new InvalidDataException("Magma Breath allocation leaves its actual downward scratch owner.");
        }
        uint address = cursor - (uint)length;
        return (Dword(frame, 0x26495D8, address), address);
    }

    public static NativeBattleMagmaBreath48Frame Release(NativeBattleMagmaBreath48Frame frame, int length)
    {
        RequireScratchLength(length);
        uint cursor = frame.State.ReadDword(0x26495D8);
        uint origin = NativeBattleEffectNodeContextMemory.Address;
        uint end = origin + NativeBattleEffectNodeContextMemory.Length;
        if (cursor < origin || cursor > end - (uint)length)
        {
            throw new InvalidDataException("Magma Breath release exceeds its actual downward scratch top.");
        }
        return Dword(frame, 0x26495D8, cursor + (uint)length);
    }

    public static NativeBattleEffectArena Context(NativeBattleMagmaBreath48Frame frame, uint address, int length)
    {
        int offset = ScratchOffset(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public static NativeBattleMagmaBreath48Frame PublishContext(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleEffectArena context, int length)
    {
        int offset = ScratchOffset(address, length);
        NativeBattleEffectArena shared = NativeBattleEffectNodeContextMemory.Copy(context, 0, NativeBattleEffectNodeContextMemory.Capture(frame.Memory), offset, length);
        return frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, shared) };
    }

    private static int ScratchOffset(uint address, int length)
    {
        RequireScratchLength(length);
        uint origin = NativeBattleEffectNodeContextMemory.Address;
        if (address < origin || (ulong)address + (uint)length > (ulong)origin + NativeBattleEffectNodeContextMemory.Length)
        {
            throw new InvalidDataException("Magma Breath context leaves its canonical shared scratch bytes.");
        }
        return checked((int)(address - origin));
    }

    private static void RequireScratchLength(int length)
    {
        if (length < 0 || length > NativeBattleEffectNodeContextMemory.Length || (length & 3) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }

    public static NativeBattleEffectArena PointView(NativeBattleVector point, short? w = null)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return w is { } value ? result.Write(6, BitConverter.GetBytes(value)) : result;
    }
    public static NativeBattleMagmaBreath48Frame DrawRandom(NativeBattleMagmaBreath48Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleMagmaBreath48Frame WithPool(NativeBattleMagmaBreath48Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleMagmaBreath48Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Magma Breath requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleMagmaBreath48Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleMagmaBreath48Frame Word(NativeBattleMagmaBreath48Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleMagmaBreath48Frame Dword(NativeBattleMagmaBreath48Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleMagmaBreath48Frame Copy(NativeBattleMagmaBreath48Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleMagmaBreath48Frame frame)
    {
        if (frame.Effect.RequestedEffect != 48 || frame.Effect.Binding.Entry != 0x824CF0 || frame.Effect.Binding.Initializer != 0x824CD0)
        {
            throw new InvalidDataException("Magma Breath requires its original selected entry.");
        }
    }

    private static NativeBattleMagmaBreath48Frame DetachBanks(NativeBattleMagmaBreath48Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.Effect.Pools.Length != 6 || frame.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)) || frame.State.InactiveBanks.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Magma Breath bank transfer requires closed queues and exclusive inactive owners.");
        }
        ImmutableArray<NativeBattleEffectArena?> storage = frame.Effect.Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectTaskMemory.CaptureBank(pool)).ToImmutableArray();
        return frame with { Effect = frame.Effect with { Pools = [] }, State = frame.State with { InactiveBanks = storage } };
    }

}
