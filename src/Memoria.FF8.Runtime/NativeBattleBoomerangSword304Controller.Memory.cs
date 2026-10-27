using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBoomerangSword304Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleBoomerangSword304Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Boomerang Sword task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleBoomerangSword304Frame Write(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Boomerang Sword task write leaves its canonical physical pools.");
    }

    public static (NativeBattleBoomerangSword304Frame Frame, uint Address) Append(NativeBattleBoomerangSword304Frame frame, uint descriptor, uint callback)
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
            throw new InvalidDataException("Boomerang Sword append requires its actual addressed pool.");
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }
    public static NativeBattleEffectArena ReadBytes(NativeBattleBoomerangSword304Frame frame, uint address, int length)
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
            return NativeBattleEffectNodeContextMemory.Copy(frame.Memory.Arena ?? throw new NotSupportedException("Boomerang Sword requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return frame.State.ReadView(address, length);
    }

    public static NativeBattleBoomerangSword304Frame WriteBytes(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleEffectArena source, int length)
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
                throw new InvalidDataException("Boomerang Sword copy leaves its actual task payload.");
            }
            NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], field, source, length);
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), task);
        }
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
        {
            return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Boomerang Sword requires its actual COMMON arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length) } };
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

    public static NativeBattleBoomerangSword304Frame LoadMatrix(NativeBattleBoomerangSword304Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleBoomerangSword304Frame frame) => frame.Staging ?? throw new NotSupportedException("Boomerang Sword requires its actual addressed temporary memory.");
    public static (NativeBattleBoomerangSword304Frame Frame, uint Address) Allocate(NativeBattleBoomerangSword304Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleBoomerangSword304Frame Release(NativeBattleBoomerangSword304Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleBoomerangSword304Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleBoomerangSword304Frame PublishContext(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }

    public static NativeBattleEffectArena PointView(NativeBattleVector point, short? w = null)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return w is { } value ? result.Write(6, BitConverter.GetBytes(value)) : result;
    }
    public static NativeBattleBoomerangSword304Frame DrawRandom(NativeBattleBoomerangSword304Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleBoomerangSword304Frame WithPool(NativeBattleBoomerangSword304Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleBoomerangSword304Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Boomerang Sword requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleBoomerangSword304Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleBoomerangSword304Frame Word(NativeBattleBoomerangSword304Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleBoomerangSword304Frame Dword(NativeBattleBoomerangSword304Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleBoomerangSword304Frame Copy(NativeBattleBoomerangSword304Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleBoomerangSword304Frame frame)
    {
        if (frame.Effect.RequestedEffect != 304 || frame.Effect.Binding.Entry != 0x669790 || frame.Effect.Binding.Initializer != 0x6697A0)
        {
            throw new InvalidDataException("Boomerang Sword requires its original selected entry.");
        }
    }

    private static NativeBattleBoomerangSword304Frame DetachBanks(NativeBattleBoomerangSword304Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.State.RootBankStorage is not null || frame.Effect.Pools.IsEmpty || frame.Effect.Pools[0].Head is not null)
        {
            throw new InvalidDataException("Boomerang Sword bank transfer requires its retired root and exclusive physical owners.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Boomerang Sword requires its retained COMMON arena.");
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
            throw new NotSupportedException("Boomerang Sword requires an actual physical model row.");
        }
        return checked(0x1D972C0u + (uint)(actor * 156));
    }
}
