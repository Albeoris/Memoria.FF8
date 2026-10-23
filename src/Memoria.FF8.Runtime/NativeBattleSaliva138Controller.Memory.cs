using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleSaliva138Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Saliva task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleSaliva138Frame Write(NativeBattleSaliva138Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Saliva task write leaves its canonical physical pools.");
    }

    public static (NativeBattleSaliva138Frame Frame, uint Address) Append(NativeBattleSaliva138Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena Arena(NativeBattleSaliva138Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Saliva requires its actual working allocation.");
    public static NativeBattleSaliva138Frame WriteArena(NativeBattleSaliva138Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleSaliva138Frame WithPool(NativeBattleSaliva138Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleSaliva138Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Saliva requires its actual captured parameters.");
    private static NativeBattleSaliva138Frame DrawRandom(NativeBattleSaliva138Frame frame, out ushort value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out value);
        return frame with { Random = random };
    }
    private static uint Working(NativeBattleSaliva138Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Saliva model pointer leaves its seven-model bank.");
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Saliva actor leaves its seven-model bank.");
    public static NativeBattleSaliva138Frame LoadMatrix(NativeBattleSaliva138Frame frame, NativeBattlePackedMatrix32 packed)
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
    private static NativeBattleFileStaging RequireStaging(NativeBattleSaliva138Frame frame) => frame.Staging ?? throw new NotSupportedException("Saliva requires its actual addressed temporary memory.");
    private static (NativeBattleSaliva138Frame Frame, uint Address) Allocate(NativeBattleSaliva138Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    private static NativeBattleSaliva138Frame Release(NativeBattleSaliva138Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    private static NativeBattleEffectArena Context(NativeBattleSaliva138Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleSaliva138Frame PublishContext(NativeBattleSaliva138Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
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

    private static NativeBattleEffectArena PointView(NativeBattleVector point, short? w = null)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return w is { } value ? result.Write(6, BitConverter.GetBytes(value)) : result;
    }
    private static NativeBattleEffectArena ActContext(NativeBattleSaliva138Frame frame, NativeBattlePackedMatrix32 matrix, int depth)
    {
        return PackedArena(matrix).Write(32, BitConverter.GetBytes(depth)).Write(36, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
    }
    private static int FirstContact(NativeBattleSaliva138Frame frame, NativeBattleSaliva138Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new NotSupportedException("Saliva requires its actual captured contact header.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Saliva requires its actual contact pointer.");
    }
    private static void RequireBinding(NativeBattleSaliva138Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Saliva requires its original selected entry.");
        }
    }

    private static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(offset - 12, size).ToArray().ToImmutableArray());
        return task.WriteBytes(offset, workspace.Initialize7016B0(source, size).Data.AsSpan());
    }

}
