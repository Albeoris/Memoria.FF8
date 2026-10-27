using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect183Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleEffect183Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Effect183 task pointer leaves its physical pools.");
    }

    public static NativeBattleEffect183Frame Write(NativeBattleEffect183Frame frame, uint address, NativeBattleModulePoolTask task)
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
        throw new InvalidDataException("Effect183 task write leaves its physical pools.");
    }

    public static NativeBattleEffect183Frame LoadMatrix(NativeBattleEffect183Frame frame, NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect183 requires its current geometry registers."), packed.ReadMatrix(), true, true, null);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }

    public static (NativeBattleEffect183Frame Frame, uint Address) Allocate(NativeBattleEffect183Frame frame, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), length);
        return (frame with { Staging = allocation.Staging }, allocation.Address);
    }

    public static NativeBattleEffect183Frame Release(NativeBattleEffect183Frame frame, int length) => frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), length) };
    public static NativeBattleEffectArena Context(NativeBattleEffect183Frame frame, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public static NativeBattleEffect183Frame PublishContext(NativeBattleEffect183Frame frame, uint address, NativeBattleEffectArena context, int length) => frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, context, length) };
    private static NativeBattleFileStaging RequireStaging(NativeBattleEffect183Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect183 requires its actual temporary memory.");
    private static NativeBattleEffect183Frame WithPool(NativeBattleEffect183Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleEffect183Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect183 requires its captured action parameters.");

    private static NativeBattleModulePoolTask CopyPoint(NativeBattleModulePoolTask task, NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 6);
        task = task.WriteBytes(16, bytes);
        for (int index = 0; index < 6; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(16 + index, 1);
            }
        }
        return task;
    }

    private static NativeBattleModulePoolTask CopyTaskPoint(NativeBattleModulePoolTask task, NativeBattleModulePoolTask source)
    {
        task = task.WriteBytes(16, source.ReadRawPayloadSpan().Slice(4, 8));
        if (source.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(16, range.Offset);
                int end = Math.Min(24, range.Offset + range.Length);
                if (first < end)
                {
                    task = task.MarkPayloadUnknown(first, end - first);
                }
            }
        }
        return task;
    }
}
