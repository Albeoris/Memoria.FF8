using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleReflectBeam135Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleReflectBeam135Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Reflect Beam task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleReflectBeam135Frame Write(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Reflect Beam task write leaves its canonical physical pools.");
    }

    public static (NativeBattleReflectBeam135Frame Frame, uint Address) Append(NativeBattleReflectBeam135Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena Arena(NativeBattleReflectBeam135Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Reflect Beam requires its actual working allocation.");
    public static NativeBattleReflectBeam135Frame WriteArena(NativeBattleReflectBeam135Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleReflectBeam135Frame WithPool(NativeBattleReflectBeam135Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleReflectBeam135Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Reflect Beam requires its actual captured parameters.");
    private static NativeBattleReflectBeam135Frame DrawRandom(NativeBattleReflectBeam135Frame frame, out ushort value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out value);
        return frame with { Random = random };
    }
    private static uint Working(NativeBattleReflectBeam135Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Reflect Beam model pointer leaves its seven-model bank.");
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Reflect Beam actor leaves its seven-model bank.");
    public static NativeBattleReflectBeam135Frame LoadMatrix(NativeBattleReflectBeam135Frame frame, NativeBattlePackedMatrix32 packed)
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
    private static NativeBattleFileStaging RequireStaging(NativeBattleReflectBeam135Frame frame) => frame.Staging ?? throw new NotSupportedException("Reflect Beam requires its actual addressed temporary memory.");
    private static (NativeBattleReflectBeam135Frame Frame, uint Address) Allocate(NativeBattleReflectBeam135Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    private static NativeBattleReflectBeam135Frame Release(NativeBattleReflectBeam135Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    private static NativeBattleEffectArena Context(NativeBattleReflectBeam135Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleReflectBeam135Frame PublishContext(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        return task.BorrowPayloadArena(offset, length);
    }

    private static NativeBattleEffectArena PointView(NativeBattleVector point, short? w = null)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return w is { } value ? result.Write(6, BitConverter.GetBytes(value)) : result;
    }
    private static NativeBattleEffectArena ActContext(NativeBattleReflectBeam135Frame frame, NativeBattleEffectArena anchor8, int depth)
    {
        NativeBattleEffectArena result = NativeBattleEffectNodeContextMemory.Copy(anchor8, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, 8);
        return result.Write(8, BitConverter.GetBytes(depth)).Write(12, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
    }
    private static NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Reflect Beam requires its actual model transform.");
    private static int FirstContact(NativeBattleReflectBeam135Frame frame, NativeBattleReflectBeam135Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new NotSupportedException("Reflect Beam requires its actual captured contact header.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Reflect Beam requires its actual contact pointer.");
    }
    private static void RequireBinding(NativeBattleReflectBeam135Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Reflect Beam requires its original selected entry.");
        }
    }

    private static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (offset - 12), size);
        return workspace.Initialize7016B0(source, size).CopyTo(task, offset, retainUnknown: false);
    }

}
