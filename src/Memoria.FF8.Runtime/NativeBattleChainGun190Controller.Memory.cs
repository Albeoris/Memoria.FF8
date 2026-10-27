using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleChainGun190Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Chain Gun task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleChainGun190Frame Write(NativeBattleChainGun190Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Chain Gun task write leaves its canonical physical pools.");
    }

    public static (NativeBattleChainGun190Frame Frame, uint Address) Append(NativeBattleChainGun190Frame frame, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, 1, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleChainGun190Frame frame, uint address, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && (ulong)address + (uint)length <= (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length))
            {
                return NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), checked((int)(address - pool.BankAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
        }
        return frame.State.ReadView(address, length);
    }

    public static NativeBattleChainGun190Frame WriteBytes(NativeBattleChainGun190Frame frame, uint address, NativeBattleEffectArena source, int length)
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
                throw new InvalidDataException("Chain Gun copy leaves its actual task payload.");
            }
            NativeBattleModulePoolTask task = CopyTask(pool.Slots[slot], field, source, length);
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), task);
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

    private static (NativeBattleChainGun190Frame Frame, uint Address) RequireAppend(NativeBattleChainGun190Frame frame, uint callback)
    {
        var appended = Append(frame, callback);
        return appended.Address == 0 ? throw new NotSupportedException("Chain Gun exhausted its actual child task bank.") : appended;
    }

    public static NativeBattleChainGun190Frame LoadMatrix(NativeBattleChainGun190Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleChainGun190Frame frame) => frame.Staging ?? throw new NotSupportedException("Chain Gun requires its actual addressed temporary memory.");
    public static (NativeBattleChainGun190Frame Frame, uint Address) Allocate(NativeBattleChainGun190Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleChainGun190Frame Release(NativeBattleChainGun190Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleChainGun190Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleChainGun190Frame PublishContext(NativeBattleChainGun190Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }

    public static NativeBattleEffectArena PointView(NativeBattleVector point, short? w = null)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return w is { } value ? result.Write(6, BitConverter.GetBytes(value)) : result;
    }
    public static NativeBattleChainGun190Frame DrawRandom(NativeBattleChainGun190Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleChainGun190Frame WithPool(NativeBattleChainGun190Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleChainGun190Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Chain Gun requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleChainGun190Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleChainGun190Frame Word(NativeBattleChainGun190Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleChainGun190Frame Dword(NativeBattleChainGun190Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleChainGun190Frame Copy(NativeBattleChainGun190Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleChainGun190Frame frame)
    {
        if (frame.Effect.RequestedEffect != 190 || frame.Effect.Binding.Entry != 0x5BB2C0 || frame.Effect.Binding.Initializer != 0x5BB2A0)
        {
            throw new InvalidDataException("Chain Gun requires its original selected entry.");
        }
    }

    private static NativeBattleEffectTaskPool ResetPool(NativeBattleChainGun190Frame frame, uint descriptor, uint bank, ushort stride, ushort count)
    {
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Chain Gun activation requires its retained bank owners and no active task pools.");
        }
        frame.State.Validate();
        _ = (bank == RootBank ? frame.State.RootBankStorage : frame.State.ChildBankStorage) ?? throw new NotSupportedException("Chain Gun activation requires its retained physical task bank; older snapshots require migration.");
        return NativeBattleEffectTaskPool.Initialize(descriptor, bank, stride, count);
    }

    private static NativeBattleChainGun190Frame DetachBanks(NativeBattleChainGun190Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.Effect.Pools.Length != 2 || frame.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)) || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Chain Gun bank transfer requires the actual closed queue and exclusive inactive task owners.");
        }
        NativeBattleEffectArena[] storage = frame.Effect.Pools.Select(NativeBattleEffectTaskMemory.CaptureBank).ToArray();
        return frame with { Effect = frame.Effect with { Pools = [] }, State = frame.State with { RootBankStorage = storage[0], ChildBankStorage = storage[1] } };
    }
}
