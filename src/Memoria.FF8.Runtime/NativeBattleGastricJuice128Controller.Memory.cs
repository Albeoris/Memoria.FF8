using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGastricJuice128Controller
{
    public static NativeBattleGastricJuice128Frame DrawRandom(NativeBattleGastricJuice128Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleGastricJuice128Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Gastric Juice requires its captured action parameters.");
    private static int FirstContact(NativeBattleGastricJuice128Frame frame, NativeBattleGastricJuice128Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Gastric Juice requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Gastric Juice requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleGastricJuice128Frame frame)
    {
        if (frame.Effect.RequestedEffect != 128 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Gastric Juice requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Gastric Juice actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Gastric Juice actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleGastricJuice128Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleGastricJuice128Frame WithPool(NativeBattleGastricJuice128Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleGastricJuice128Frame Put(NativeBattleGastricJuice128Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, Task(frame, pool, slot).Next, task.Callback)));
    public static NativeBattleModulePoolTask Read(NativeBattleGastricJuice128Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Gastric Juice task pointer leaves its canonical pools.");
    }
    public static NativeBattleGastricJuice128Frame Write(NativeBattleGastricJuice128Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Gastric Juice task pointer leaves its canonical pools.");
    }
    private static (NativeBattleGastricJuice128Frame Frame, uint Address) AppendPool(NativeBattleGastricJuice128Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    public static (NativeBattleGastricJuice128Frame Frame, uint Address) Append(NativeBattleGastricJuice128Frame frame, int pool, uint callback) => AppendPool(frame, pool, callback);
    public static NativeBattleEffectArena Arena(NativeBattleGastricJuice128Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Gastric Juice requires its current working arena.");
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    public static NativeBattleGastricJuice128Frame WriteArena(NativeBattleGastricJuice128Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static uint Working(NativeBattleGastricJuice128Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    public static NativeBattleGastricJuice128Frame LoadMatrix(NativeBattleGastricJuice128Frame frame, NativeBattlePackedMatrix32 packed)
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
    public static NativeBattleFileStaging RequireStaging(NativeBattleGastricJuice128Frame frame) => frame.Staging ?? throw new NotSupportedException("Disolving Acid requires its actual addressed temporary memory.");
    public static (NativeBattleGastricJuice128Frame Frame, uint Address) Allocate(NativeBattleGastricJuice128Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    public static NativeBattleGastricJuice128Frame Release(NativeBattleGastricJuice128Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleGastricJuice128Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleGastricJuice128Frame PublishContext(NativeBattleGastricJuice128Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
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
}
