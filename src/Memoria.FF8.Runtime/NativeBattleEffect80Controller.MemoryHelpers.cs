using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleEffect80Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);

    public static NativeBattleEffect80Frame Write(NativeBattleEffect80Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task.UpdateHeader(task.Flags, task.State, hierarchy.Read(address).Next, task.Callback)).Pools } };
    }

    public static (NativeBattleEffect80Frame Frame, uint Address) AppendHierarchy(NativeBattleEffect80Frame frame, int index, uint callback, int length, uint parent, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleEffectHierarchyAppend result = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(index, callback, length, parent, (pointer, header, contact) => NativeBattleCureController.SelectActors(Parameters(frame), inputs.ReadPackets(frame), pointer, header, contact));
        if (result.Address == 0)
        {
            throw new InvalidDataException("Effect80 exhausted a mandatory hierarchy allocation.");
        }
        return (frame with { Effect = frame.Effect with { Pools = result.Hierarchy.Pools } }, result.Address);
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect80Frame frame, uint address, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (Contains(pool.BankAddress, pool.Stride * pool.Slots.Length, address, length))
            {
                return Slice(NativeBattleEffectTaskMemory.CaptureBank(pool), checked((int)(address - pool.BankAddress)), length);
            }
        }
        if (Contains(NativeBattleEffectNodeContextMemory.Address, NativeBattleEffectNodeContextMemory.Length, address, length))
        {
            return Slice(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), checked((int)(address - NativeBattleEffectNodeContextMemory.Address)), length);
        }
        if (Contains(ViewAddress, 32, address, length))
        {
            return Slice(PackedArena(frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Effect80 requires its actual shared view copy.")), checked((int)(address - ViewAddress)), length);
        }
        if (Contains(0x1D99AB0, 32, address, length))
        {
            return Slice(PackedArena(frame.Memory.CaptureEffectLight1D99AB0()), checked((int)(address - 0x1D99AB0)), length);
        }
        if (Contains(0x20DFAB8, NativeBattleEffectArena.Length, address, length))
        {
            return Slice(frame.Memory.Arena ?? throw new NotSupportedException("Effect80 requires its current physical arena."), checked((int)(address - 0x20DFAB8)), length);
        }
        if (IsStateRange(address, length))
        {
            return frame.State.ReadView(address, length);
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            if (Contains(Pools[index].Bank, Pools[index].Stride * Pools[index].Capacity, address, length))
            {
                return Slice(frame.State.ReadBank(index), checked((int)(address - Pools[index].Bank)), length);
            }
        }
        return Slice(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, length);
    }

    public static NativeBattleEffect80Frame WriteBytes(NativeBattleEffect80Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (!Contains(pool.BankAddress, pool.Stride * pool.Slots.Length, address, length))
            {
                continue;
            }
            int offset = checked((int)(address - pool.BankAddress));
            int slot = offset / pool.Stride;
            int field = offset % pool.Stride;
            if (field < 12 || field + length > pool.Stride)
            {
                throw new InvalidDataException("Effect80 writes must remain inside their actual task payload.");
            }
            return Write(frame, pool.BankAddress + (uint)(slot * pool.Stride), CopyTask(pool.Slots[slot], field, source, length));
        }
        if (Contains(NativeBattleEffectNodeContextMemory.Address, NativeBattleEffectNodeContextMemory.Length, address, length))
        {
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(source, 0, NativeBattleEffectNodeContextMemory.Capture(frame.Memory), checked((int)(address - NativeBattleEffectNodeContextMemory.Address)), length);
            return frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, context) };
        }
        if (Contains(ViewAddress, 32, address, length) || Contains(0x1D99AB0, 32, address, length))
        {
            bool view = address >= ViewAddress;
            uint start = view ? ViewAddress : 0x1D99AB0;
            NativeBattlePackedMatrix32 prior = view ? frame.Memory.SharedEffectView2793E58 ?? new NativeBattlePackedMatrix32(new byte[32].ToImmutableArray(), new bool[32].ToImmutableArray()) : frame.Memory.CaptureEffectLight1D99AB0();
            NativeBattleEffectArena changed = NativeBattleEffectNodeContextMemory.Copy(source, 0, PackedArena(prior), checked((int)(address - start)), length);
            (byte[] bytes, bool[] known) = changed.ReadSnapshot(0, 32);
            NativeBattlePackedMatrix32 packed = new(bytes.ToImmutableArray(), known.ToImmutableArray());
            return frame with { Memory = view ? frame.Memory with { SharedEffectView2793E58 = packed } : frame.Memory.PublishEffectLight1D99AB0(packed) };
        }
        if (Contains(0x20DFAB8, NativeBattleEffectArena.Length, address, length))
        {
            return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect80 requires its current physical arena."), checked((int)(address - 0x20DFAB8)), length) } };
        }
        if (IsStateRange(address, length))
        {
            return frame with { State = frame.State.WriteView(address, source, length) };
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            if (Contains(shape.Bank, shape.Stride * shape.Capacity, address, length))
            {
                NativeBattleEffectArena bank = NativeBattleEffectNodeContextMemory.Copy(source, 0, frame.State.ReadBank(index), checked((int)(address - shape.Bank)), length);
                return frame with { State = frame.State.AttachBank(index, bank) };
            }
        }
        return frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, source, length) };
    }

    public static NativeBattleEffect80Frame WriteCompletionByte(NativeBattleEffect80Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    public static NativeBattleEffect80Frame ClearBytes(NativeBattleEffect80Frame frame, uint address, int length) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[length]), length);
    public static NativeBattleEffectArena Context(NativeBattleEffect80Frame frame, uint address, int length) => ReadBytes(frame, address, length);
    public static NativeBattleEffect80Frame PublishContext(NativeBattleEffect80Frame frame, uint address, NativeBattleEffectArena context, int length) => WriteBytes(frame, address, context, length);
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect80Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect80 requires its actual addressed temporary memory.");

    public static (NativeBattleEffect80Frame Frame, uint Address) Allocate(NativeBattleEffect80Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }

    public static NativeBattleEffect80Frame Release(NativeBattleEffect80Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect80Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect80 requires its current GTE controls."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect80 requires its current GTE arithmetic registers."));
    public static NativeBattleEffect80Frame Publish(NativeBattleEffect80Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

    public static NativeBattleEffect80Frame LoadMatrix(NativeBattleEffect80Frame frame, NativeBattlePackedMatrix32 packed, bool rotation = true, bool translation = true)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, packed.ReadMatrix(), rotation, translation, null);
        if (rotation)
        {
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }

    public static NativeBattleEffectArena PackedArena(NativeBattlePackedMatrix32 packed)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!packed.Known[index])
            {
                arena = arena.MarkUnknown(index, 1);
            }
        }
        return arena;
    }

    public static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
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

    public static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        foreach (NativeBattleEffectUnknownRegion range in task.ReadUnknownPayloadRanges() ?? [])
        {
            int first = Math.Max(offset, range.Offset);
            int last = Math.Min(offset + length, range.Offset + range.Length);
            if (first < last)
            {
                source = source.MarkUnknown(first - offset, last - first);
            }
        }
        return source;
    }

    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    public static NativeBattleVector Vector(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    public static uint PhysicalModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(156 * actor)) : throw new InvalidDataException("Effect80 requires an actual physical model row.");
    private static NativeBattleEffectParameters Parameters(NativeBattleEffect80Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect80 requires its actual captured parameters.");
    private static bool IsStateRange(uint address, int length) => OwnedRanges.Any(range => Contains(range.Address, range.Length, address, length)) || NodeSourceLengths.Any(range => Contains(range.Key, range.Value, address, length)) || InitializedSourceLengths.Any(range => Contains(range.Key, range.Value, address, length));
    private static bool Contains(uint start, int extent, uint address, int length) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)extent;
    private static NativeBattleEffectArena Slice(NativeBattleEffectArena source, int offset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
}
