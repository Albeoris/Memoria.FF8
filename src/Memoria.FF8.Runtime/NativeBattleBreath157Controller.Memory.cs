using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath157Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath157Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleBreath157Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Breath task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleBreath157Frame Write(NativeBattleBreath157Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Breath task write leaves its canonical physical pools.");
    }

    public static (NativeBattleBreath157Frame Frame, uint Address) Append(NativeBattleBreath157Frame frame, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, 1, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleBreath157Frame LoadMatrix(NativeBattleBreath157Frame frame, NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), packed.ReadMatrix(), true, true, null);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleBreath157Frame frame) => frame.Staging ?? throw new NotSupportedException("Breath requires its actual addressed temporary memory.");
    public static (NativeBattleBreath157Frame Frame, uint Address) Allocate(NativeBattleBreath157Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleBreath157Frame Release(NativeBattleBreath157Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleBreath157Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleBreath157Frame PublishContext(NativeBattleBreath157Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
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
    public static NativeBattleBreath157Frame DrawRandom(NativeBattleBreath157Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleBreath157Frame WithPool(NativeBattleBreath157Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleBreath157Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Breath requires its captured action parameters.");
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleBreath157Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleBreath157Frame Word(NativeBattleBreath157Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleBreath157Frame Dword(NativeBattleBreath157Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleBreath157Frame Copy(NativeBattleBreath157Frame frame, uint source, uint destination, int length) => frame with { State = frame.State.WriteView(destination, frame.State.ReadView(source, length), length) };
    private static void RequireBinding(NativeBattleBreath157Frame frame)
    {
        if (frame.Effect.RequestedEffect != 157 || frame.Effect.Binding.Entry != 0x603E90 || frame.Effect.Binding.Initializer != 0x603E70)
        {
            throw new InvalidDataException("Breath requires its original selected entry.");
        }
    }

    private static NativeBattleEffectTaskPool ResetPool(NativeBattleBreath157Frame frame, uint descriptor, uint bank, ushort stride, ushort count)
    {
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Breath activation requires its retained bank owners and no active task pools.");
        }
        frame.State.Validate();
        _ = (bank == RootBank ? frame.State.RootBankStorage : frame.State.ChildBankStorage) ?? throw new NotSupportedException("Breath activation requires its retained physical task bank; older snapshots require migration.");
        return NativeBattleEffectTaskPool.Initialize(descriptor, bank, stride, count);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int poolIndex = -1;
        for (int index = 0; index < effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = effect.Pools[index];
            if (pool.BankAddress == 0x23EB5F8 && pool.Stride == 16 && pool.Slots.Length == 2)
            {
                poolIndex = index;
                break;
            }
        }
        if (poolIndex < 0 || address < 0x23EB5F8 || address >= 0x23EB618 || (address - 0x23EB5F8) % 16 != 14)
        {
            throw new InvalidDataException("Breath completion byte leaves its actual root task field.");
        }
        NativeBattleEffectTaskPool selected = effect.Pools[poolIndex];
        int slot = (int)((address - selected.BankAddress) / selected.Stride);
        NativeBattleModulePoolTask task = selected.Slots[slot].WriteBytes(14, [value]);
        return effect with { Pools = effect.Pools.SetItem(poolIndex, selected.WriteSlot(slot, task)) };
    }

    private static NativeBattleBreath157Frame DetachBanks(NativeBattleBreath157Frame frame)
    {
        if (frame.Effect.ReturnedDescriptor != 0 || frame.Effect.Pools.Length != 2 || frame.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)) || frame.State.RootBankStorage is not null || frame.State.ChildBankStorage is not null)
        {
            throw new InvalidDataException("Breath bank transfer requires the actual closed queue and exclusive inactive task owners.");
        }
        NativeBattleEffectArena[] storage = frame.Effect.Pools.Select(NativeBattleEffectTaskMemory.CaptureBank).ToArray();
        return frame with { Effect = frame.Effect with { Pools = [] }, State = frame.State with { RootBankStorage = storage[0], ChildBankStorage = storage[1] } };
    }
}
