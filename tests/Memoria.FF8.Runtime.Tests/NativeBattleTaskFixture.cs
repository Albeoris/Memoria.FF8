using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleTaskFixture
{
    internal static NativeBattleModulePoolTask ObserveTask(NativeBattleModulePoolTask task)
    {
        (byte[] bytes, bool[] known) = task.ReadSnapshot(12, task.PayloadLength);
        NativeBattleModulePoolTask observed = new(task.Flags, task.State, task.Next, task.Callback, bytes.ToImmutableArray());
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                observed.MarkPayloadUnknown(index + 12, 1);
            }
        }
        foreach ((int offset, NativeBattleResourcePointer pointer) in task.EnumerateResourcePointers())
        {
            observed.WriteResourcePointer(offset, pointer);
        }
        return NativeBattleStorageSnapshots.Capture(observed);
    }

    internal sealed record PoolData(ImmutableArray<NativeBattleModulePoolTask> Slots, int? Head, int? Tail);

    internal static NativeBattleModulePoolTask EditTask(NativeBattleModulePoolTask task, Func<NativeBattleTaskSnapshotData, NativeBattleTaskSnapshotData> edit)
    {
        task.Replace(edit(task.ExportSnapshotData()));
        return task;
    }

    internal static NativeBattleEffectTaskPool EditPool(NativeBattleEffectTaskPool pool, Func<PoolData, PoolData> edit)
    {
        PoolData data = edit(new(pool.Slots, pool.Head, pool.Tail));
        for (int index = 0; index < data.Slots.Length; index++)
        {
            pool.WriteSlot(index, data.Slots[index]);
        }
        return pool.UpdateLinks(data.Head, data.Tail);
    }
}
