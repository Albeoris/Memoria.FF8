using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSonic139Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleSonic139Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Sonic Wave task pointer leaves its actual pools.");
    }

    public static NativeBattleSonic139Frame Write(NativeBattleSonic139Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                int slot = (int)(offset / pool.Stride);
                return WithPool(frame, index, pool with { Slots = pool.Slots.SetItem(slot, task with { Next = pool.Slots[slot].Next }) });
            }
        }
        throw new InvalidDataException("Sonic Wave task write leaves its actual pools.");
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleSonic139Frame frame, uint address, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && (ulong)address + (uint)length <= (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length))
            {
                return Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), checked((int)(address - pool.BankAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
        }
        if (address is >= 0x2517B50 and < 0x2520C48 || address is >= 0x11D7900 and < 0x11D8BF0)
        {
            return frame.State.ReadView(address, length);
        }
        return Copy(frame.Memory.Arena ?? throw new NotSupportedException("Sonic Wave requires its current arena."), checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public static NativeBattleSonic139Frame WriteBytes(NativeBattleSonic139Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        for (int poolIndex = 0; poolIndex < frame.Effect.Pools.Length; poolIndex++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
            if (address < pool.BankAddress || (ulong)address + (uint)length > (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length))
            {
                continue;
            }
            int offset = checked((int)(address - pool.BankAddress));
            int slot = offset / pool.Stride;
            int field = offset % pool.Stride;
            if (field < 12 || field + length > pool.Stride)
            {
                throw new InvalidDataException("Sonic Wave copy leaves its actual task payload.");
            }
            (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteBytes(field, bytes);
            for (int index = 0; index < known.Length; index++)
            {
                if (!known[index])
                {
                    task = task.MarkPayloadUnknown(field + index, 1);
                }
            }
            return Write(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), task);
        }
        if (address is >= 0x2517B50 and < 0x2520C48 || address is >= 0x11D7900 and < 0x11D8BF0)
        {
            return frame with { State = frame.State.WriteView(address, source, length) };
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Sonic Wave requires its current arena.");
        return frame with { Memory = frame.Memory with { Arena = Copy(source, 0, arena, checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length) } };
    }

    public static NativeBattleSonic139Frame LoadMatrix(NativeBattleSonic139Frame frame, NativeBattlePackedMatrix32 packed)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Sonic Wave requires its actual geometry registers."), packed.ReadMatrix(), true, true, null);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }

    public static (NativeBattleSonic139Frame Frame, uint Address) Allocate(NativeBattleSonic139Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }

    public static NativeBattleSonic139Frame Release(NativeBattleSonic139Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleSonic139Frame frame, uint address, int length) => Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleSonic139Frame PublishContext(NativeBattleSonic139Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    private static NativeBattleFileStaging RequireStaging(NativeBattleSonic139Frame frame) => frame.Staging ?? throw new NotSupportedException("Sonic Wave requires its current temporary memory.");
    private static NativeBattleSonic139Frame WithPool(NativeBattleSonic139Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int offset, NativeBattleEffectArena destination, int target, int length) => NativeBattleEffectNodeContextMemory.Copy(source, offset, destination, target, length);
    private static NativeBattleEffectParameters Parameters(NativeBattleSonic139Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Sonic Wave requires its captured parameters.");
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Point(NativeBattleEffectArena source, int offset = 0) => new(BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset + 4, 2)));
    private static uint ModelAddress(int actor) => checked(0x1D972C0u + (uint)actor * 156);

    private static (NativeBattleSonic139Frame Frame, uint Address) Append(NativeBattleSonic139Frame frame, int index, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }
}
