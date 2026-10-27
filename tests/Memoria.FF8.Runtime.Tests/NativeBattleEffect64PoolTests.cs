using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64PoolTests
{
    [Fact]
    public void Linked_task_with_cleared_allocation_flags_runs_until_callback_returns_retirement()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2545058, 0x20DFDD0, 0x488, 1);
        (pool, int slot) = pool.Append(0x6E53F0);
        pool = pool.WriteSlot(slot, pool.Slots[slot].UpdateHeader(0, 0, null, pool.Slots[slot].Callback));
        pool.Validate();
        pool = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTaskPool>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(pool)))!);
        int calls = 0;
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            calls++;
            NativeBattleModulePoolTask task = current.Slots[index];
            Assert.Equal((ushort)0, task.Flags);
            Assert.Equal(0x6E53F0u, task.Callback);
            return new(current.WriteSlot(index, task.WriteWord(12, 1)), 0);
        });
        Assert.Equal(1, calls);
        Assert.Equal(1, step.Survivors);
        Assert.Equal((ushort)1, step.Pool.Slots[slot].ReadWord(12));
        Assert.Equal(slot, step.Pool.Head);
        NativeBattleEffectTaskStep retired = step.Pool.Advance((current, _) => new(current, 2));
        Assert.Equal(0, retired.Survivors);
        Assert.Null(retired.Pool.Head);
        Assert.Null(retired.Pool.Tail);
    }

    [Fact]
    public void Cleared_allocation_flag_does_not_allow_a_linked_null_callback()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2545058, 0x20DFDD0, 0x488, 1);
        pool = pool.UpdateLinks(0, 0);
        Assert.Throws<InvalidDataException>(() => pool.Validate());
    }

    [Fact]
    public void Cleared_allocation_flag_does_not_allow_a_link_cycle()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2545058, 0x20DFDD0, 0x488, 1);
        pool.Slots[0].UpdateHeader(pool.Slots[0].Flags, pool.Slots[0].State, 0, 0x6E53F0);
        pool.UpdateLinks(0, 0);
        Assert.Throws<InvalidDataException>(() => pool.Validate());
    }
}
