using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDisolvingAcid132Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleDisolvingAcid132Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Disolving Acid task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleDisolvingAcid132Frame Write(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Disolving Acid task write leaves its canonical physical pools.");
    }

    public static (NativeBattleDisolvingAcid132Frame Frame, uint Address) Append(NativeBattleDisolvingAcid132Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena Arena(NativeBattleDisolvingAcid132Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Disolving Acid requires its actual working allocation.");
    public static NativeBattleDisolvingAcid132Frame WriteArena(NativeBattleDisolvingAcid132Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleDisolvingAcid132Frame WithPool(NativeBattleDisolvingAcid132Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleDisolvingAcid132Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Disolving Acid requires its actual captured parameters.");
    public static NativeBattleDisolvingAcid132Frame DrawRandom(NativeBattleDisolvingAcid132Frame frame, out ushort value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out value);
        return frame with { Random = random };
    }
    private static uint Working(NativeBattleDisolvingAcid132Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Disolving Acid model pointer leaves its seven-model bank.");
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Disolving Acid actor leaves its seven-model bank.");
    public static NativeBattleDisolvingAcid132Frame LoadMatrix(NativeBattleDisolvingAcid132Frame frame, NativeBattlePackedMatrix32 packed)
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
    private static NativeBattleFileStaging RequireStaging(NativeBattleDisolvingAcid132Frame frame) => frame.Staging ?? throw new NotSupportedException("Disolving Acid requires its actual addressed temporary memory.");
    private static (NativeBattleDisolvingAcid132Frame Frame, uint Address) Allocate(NativeBattleDisolvingAcid132Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }
    private static NativeBattleDisolvingAcid132Frame Release(NativeBattleDisolvingAcid132Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleDisolvingAcid132Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleDisolvingAcid132Frame PublishContext(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
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
    private static int FirstContact(NativeBattleDisolvingAcid132Frame frame, NativeBattleDisolvingAcid132Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new NotSupportedException("Disolving Acid requires its actual captured contact header.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Disolving Acid requires its actual contact pointer.");
    }
    private static void RequireBinding(NativeBattleDisolvingAcid132Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Disolving Acid requires its original selected entry.");
        }
    }

    private static NativeBattleModulePoolTask InitializeWorkspace(NativeBattleModulePoolTask task, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (offset - 12), size);
        return workspace.Initialize7016B0(source, size).CopyTo(task, offset, retainUnknown: false);
    }

}
