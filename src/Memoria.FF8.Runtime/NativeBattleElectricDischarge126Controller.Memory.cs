using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectricDischarge126Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleElectricDischarge126Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Electric Discharge task pointer leaves its canonical physical pools.");
    }

    public static NativeBattleElectricDischarge126Frame Write(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Electric Discharge task write leaves its canonical physical pools.");
    }

    public static (NativeBattleElectricDischarge126Frame Frame, uint Address) Append(NativeBattleElectricDischarge126Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena Arena(NativeBattleElectricDischarge126Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Electric Discharge requires its actual working allocation.");
    public static NativeBattleElectricDischarge126Frame WriteArena(NativeBattleElectricDischarge126Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Arena = Arena(frame).Write(Offset(address, bytes.Length), bytes) } };
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleElectricDischarge126Frame WithPool(NativeBattleElectricDischarge126Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleElectricDischarge126Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Electric Discharge requires its captured native parameters.");
    private static NativeBattleElectricDischarge126Frame DrawRandom(NativeBattleElectricDischarge126Frame frame, out ushort value) => frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out value) };
    private static uint Working(NativeBattleElectricDischarge126Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    private static short PointSigned(NativeBattleElectricDischarge126Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address, 2), 2));
    private static uint PointDword(NativeBattleElectricDischarge126Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static NativeBattleElectricDischarge126Frame PointWord(NativeBattleElectricDischarge126Frame frame, uint address, short value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleElectricDischarge126Frame PointVector(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleVector point) => PointWord(PointWord(PointWord(frame, address, point.X), address + 2, point.Y), address + 4, point.Z);
    private static NativeBattleVector PointVector(NativeBattleElectricDischarge126Frame frame, uint address) => new(PointSigned(frame, address), PointSigned(frame, address + 2), PointSigned(frame, address + 4));

    private static NativeBattleElectricDischarge126Frame WeightedBlend(NativeBattleElectricDischarge126Frame frame, NativeBattleVector first, NativeBattleVector second, int firstFactor, int secondFactor, NativeBattleElectricDischarge126Inputs inputs, out NativeBattleVector point)
    {
        NativeBattleWeightedPointsStep step = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, first, second, unchecked((short)firstFactor), unchecked((short)secondFactor), inputs.Colors);
        point = step.Position;
        return frame with { Memory = step.Memory };
    }

    private static void RequireWorldPoint(NativeBattleElectricDischarge126Frame frame, uint address, int visited)
    {
        uint offset = unchecked(address - (Working(frame) + WorldPointsOffset));
        if (visited >= PointCount || offset >= PointCount * PointStride || offset % PointStride != 0)
        {
            throw new NotSupportedException("Electric Discharge world chain leaves its actual finite point bank.");
        }
    }

    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        return offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Electric Discharge model pointer leaves its seven-model bank.");
    }

    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Electric Discharge actor leaves its seven-model bank.");

    private static NativeBattleEffectTaskPool ResetPool(NativeBattleElectricDischarge126Frame frame, uint descriptor, uint address, ushort stride, ushort count)
    {
        NativeBattleEffectTaskPool? retained = frame.Effect.Pools.SingleOrDefault(pool => pool.BankAddress == address && pool.Stride == stride && pool.Slots.Length == count);
        if (retained is not null)
        {
            return retained with { DescriptorAddress = descriptor, Slots = retained.Slots.Select(task => task with { Flags = 0 }).ToImmutableArray(), Head = null, Tail = null };
        }
        NativeBattleEffectTaskPool result = NativeBattleEffectTaskPool.Initialize(descriptor, address, stride, count);
        for (int slot = 0; slot < count; slot++)
        {
            int offset = Offset(checked(address + (uint)(slot * stride) + 12), stride - 12);
            (byte[] bytes, bool[] known) = Arena(frame).ReadSnapshot(offset, stride - 12);
            NativeBattleModulePoolTask task = result.Slots[slot].WriteBytes(12, bytes);
            for (int index = 0; index < known.Length; index++)
            {
                if (!known[index])
                {
                    task = task.MarkPayloadUnknown(index + 12, 1);
                }
            }
            NativeBattleEffectArena arena = Arena(frame);
            var pointers = arena.Transaction is { } transaction ? transaction.ResourcePointers : arena.ResourcePointers;
            if (pointers is not null)
            {
                foreach (int field in pointers.Keys.Where(field => field >= offset && field + 4L <= offset + stride - 12L))
                {
                    if (arena.ReadResourcePointer(field) is { } pointer)
                    {
                        task = task.WriteResourcePointer(checked(field - offset + 12), pointer);
                    }
                }
            }
            result = result with { Slots = result.Slots.SetItem(slot, task) };
        }
        return result;
    }
}
