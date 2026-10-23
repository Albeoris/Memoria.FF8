using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun150Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleGatlingGun150Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Gatling Gun task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleGatlingGun150Frame Write(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Gatling Gun task write leaves its canonical physical pools.");
    }

    public static (NativeBattleGatlingGun150Frame Frame, uint Address) Append(NativeBattleGatlingGun150Frame frame, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, 1, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleGatlingGun150Frame frame, uint address, int length)
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

    public static NativeBattleGatlingGun150Frame WriteBytes(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleEffectArena source, int length)
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
                throw new InvalidDataException("Gatling Gun copy leaves its actual task payload.");
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

    private static (NativeBattleGatlingGun150Frame Frame, uint Address) RequireAppend(NativeBattleGatlingGun150Frame frame, uint callback)
    {
        var appended = Append(frame, callback);
        return appended.Address == 0 ? throw new NotSupportedException("Gatling Gun exhausted its actual child task bank.") : appended;
    }

    public static NativeBattleGatlingGun150Frame LoadMatrix(NativeBattleGatlingGun150Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleGatlingGun150Frame frame) => frame.Staging ?? throw new NotSupportedException("Gatling Gun requires its actual addressed temporary memory.");
    public static (NativeBattleGatlingGun150Frame Frame, uint Address) Allocate(NativeBattleGatlingGun150Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleGatlingGun150Frame Release(NativeBattleGatlingGun150Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleGatlingGun150Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleGatlingGun150Frame PublishContext(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
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
    public static NativeBattleGatlingGun150Frame DrawRandom(NativeBattleGatlingGun150Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleGatlingGun150Frame WithPool(NativeBattleGatlingGun150Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleGatlingGun150Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Gatling Gun requires its captured action parameters.");
    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleGatlingGun150Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleGatlingGun150Frame Word(NativeBattleGatlingGun150Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleGatlingGun150Frame Dword(NativeBattleGatlingGun150Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleGatlingGun150Frame Copy(NativeBattleGatlingGun150Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleGatlingGun150Frame frame)
    {
        if (frame.Effect.RequestedEffect != 150 || frame.Effect.Binding.Entry != 0x60E340 || frame.Effect.Binding.Initializer != 0x60E320)
        {
            throw new InvalidDataException("Gatling Gun requires its original selected entry.");
        }
    }

    private static NativeBattleEffectTaskPool ResetPool(NativeBattleGatlingGun150Frame frame, uint descriptor, uint bank, ushort stride, ushort count)
    {
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Gatling Gun activation requires its retained bank owners and no active task pools.");
        }
        frame.State.Validate();
        _ = (bank == RootBank ? frame.State.RootBankStorage : frame.State.ChildBankStorage) ?? throw new NotSupportedException("Gatling Gun activation requires its retained physical task bank; older snapshots require migration.");
        return NativeBattleEffectTaskPool.Initialize(descriptor, bank, stride, count);
    }

    private static NativeBattleGatlingGun150Frame DetachBanks(NativeBattleGatlingGun150Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.Effect.Pools.Length != 2 || frame.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)) || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Gatling Gun bank transfer requires the actual closed queue and exclusive inactive task owners.");
        }
        NativeBattleEffectArena[] storage = frame.Effect.Pools.Select(NativeBattleEffectTaskMemory.CaptureBank).ToArray();
        return frame with { Effect = frame.Effect with { Pools = [] }, State = frame.State with { RootBankStorage = storage[0], ChildBankStorage = storage[1] } };
    }
}
