using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltEye158Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleMeltEye158Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Melt-Eye task address has no current physical owner.");
    }

    public static NativeBattleMeltEye158Frame Write(NativeBattleMeltEye158Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot(checked((int)((address - pool.BankAddress) / pool.Stride)), task));
            }
        }
        throw new NotSupportedException("Melt-Eye task write has no current physical owner.");
    }

    public static (NativeBattleMeltEye158Frame Frame, uint Address) Append(NativeBattleMeltEye158Frame frame, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, 1, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleMeltEye158Frame Random(NativeBattleMeltEye158Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    public static int Caster(NativeBattleMeltEye158Frame frame) => checked((int)frame.State.ReadDword(CasterGlobal));
    public static NativeBattleVector TaskVector(NativeBattleModulePoolTask task, int offset) => new(unchecked((short)task.ReadWord(offset)), unchecked((short)task.ReadWord(offset + 2)), unchecked((short)task.ReadWord(offset + 4)));
    public static NativeBattleModulePoolTask PutTaskVector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
    public static NativeBattleVector ReadVector(NativeBattleMeltEye158Frame frame, uint address) => new(unchecked((short)frame.State.ReadWord(address)), unchecked((short)frame.State.ReadWord(address + 2)), unchecked((short)frame.State.ReadWord(address + 4)));
    public static NativeBattleMeltEye158Frame WriteVector(NativeBattleMeltEye158Frame frame, uint address, NativeBattleVector point) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)point.X)).WriteWord(address + 2, unchecked((ushort)point.Y)).WriteWord(address + 4, unchecked((ushort)point.Z)) };

    public static NativeBattleActionMatrix WorldRotation(NativeBattleMeltEye158Frame frame, int actor, NativeBattleMeltEye158Inputs inputs)
    {
        NativeBattlePackedMatrix32 packed = inputs.ReadWorld(frame, actor);
        packed.Validate();
        if (packed.Known.AsSpan(0, 18).Contains(false))
        {
            throw new NotSupportedException("Melt-Eye requires its current caster rotation WORDs.");
        }
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(packed.Bytes.AsSpan(index * 2, 2))).ToImmutableArray();
        return new(rotation, 0, 0, 0, 4096);
    }

    public static NativeBattleMeltEye158Frame LoadGteMatrix(NativeBattleMeltEye158Frame frame, NativeBattlePackedMatrix32 packed)
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

    private static NativeBattleMeltEye158Frame WithPool(NativeBattleMeltEye158Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleMeltEye158Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Melt-Eye requires its captured native parameters.");
}
