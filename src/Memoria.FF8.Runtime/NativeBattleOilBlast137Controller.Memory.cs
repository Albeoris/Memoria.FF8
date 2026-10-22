using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilBlast137Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilBlast137Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleOilBlast137Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Oil Blast requires the actual task pool owner.");
    }

    public static NativeBattleOilBlast137Frame Write(NativeBattleOilBlast137Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return WithPool(frame, index, pool with { Slots = pool.Slots.SetItem(checked((int)((address - pool.BankAddress) / pool.Stride)), task) });
            }
        }
        throw new NotSupportedException("Oil Blast task write has no current physical owner.");
    }

    public static (NativeBattleOilBlast137Frame Frame, uint Address) Append(NativeBattleOilBlast137Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleOilBlast137Frame Random(NativeBattleOilBlast137Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    public static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    public static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4));
    public static NativeBattleModulePoolTask PutVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));

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

    public static NativeBattleModulePoolTask CopyToTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    public static NativeBattleOilBlast137Frame LoadMatrix(NativeBattleOilBlast137Frame frame, NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleActionMatrix matrix = packed.ReadMatrix();
        NativeBattleGeometryRegisters geometry = (frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown()) with
        {
            Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray(),
            Translation = [matrix.X, matrix.Y, matrix.Z]
        };
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }

    private static (NativeBattleOilBlast137Frame Frame, uint Address) Allocate(NativeBattleOilBlast137Frame frame, int bytes)
    {
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Oil Blast requires its physical temporary memory."), bytes);
        return (frame with { Staging = allocated.Staging }, allocated.Address);
    }

    private static NativeBattleOilBlast137Frame Release(NativeBattleOilBlast137Frame frame, int bytes) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, bytes) };
    private static NativeBattleOilBlast137Frame WithPool(NativeBattleOilBlast137Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleOilBlast137Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Oil Blast requires its captured native parameters.");
    private static uint ModelAddress(int actor) => checked(0x1D972C0u + (uint)(actor * 156));

    private static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, int offset)
    {
        NativeBattleEffectArena view = TaskView(task, offset, 32);
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, 32);
        return new NativeBattlePackedMatrix32(bytes.ToImmutableArray(), known.ToImmutableArray()).ReadMatrix();
    }

    private static NativeBattleModulePoolTask PutMatrix(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan()).MarkUnknown(18, 2);
        return CopyToTask(task, offset, view, 32);
    }

    private static NativeBattleEffectArena Context(NativeBattlePackedMatrix32 matrix, int? depth, int mode, uint morph)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                result = result.MarkUnknown(index, 1);
            }
        }
        if (depth is int value)
        {
            result = result.Write(32, BitConverter.GetBytes(value));
        }
        return result.Write(36, BitConverter.GetBytes(mode)).Write(40, BitConverter.GetBytes(morph));
    }
}
