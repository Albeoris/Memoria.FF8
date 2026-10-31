namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActor33PresentationTests
{
    [Theory]
    [InlineData(316)]
    [InlineData(318)]
    [InlineData(319)]
    [InlineData(324)]
    public void Actual_task_reuse_workspace_birth_and_selected_resource_break_only_metadata(ushort effect)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 64, 1);
        (pool, int slot) = pool.Append(0x123456);
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[slot]);
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear().Write(17, [0xA5]);
        byte[] native = storage.Read(17, 1);
        NativeBattlePilotVisualLifetimes.Allocated(storage, 0x2020, 0);
        var first = NativeBattleActor33Presentation.Row(owner, storage, 0x2020, 0x3000, 44);
        Assert.Equal(first, NativeBattleActor33Presentation.Row(owner, storage, 0x2020, 0x3000, 44));
        Assert.NotEqual(first, NativeBattleActor33Presentation.Row(owner, storage, 0x2020, 0x3004, 44));
        NativeBattlePilotVisualLifetimes.Allocated(storage, 0x2020, 0);
        Assert.NotEqual(first, NativeBattleActor33Presentation.Row(owner, storage, 0x2020, 0x3000, 44));
        pool = pool.Advance((task, _) => new(task, 2)).Pool;
        (pool, slot) = pool.Append(0x123456);
        NativeBattlePilotVisualIdentity recycled = NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[slot]);
        Assert.NotEqual(owner.Generation, recycled.Generation);
        Assert.NotEqual(first, NativeBattleActor33Presentation.Row(recycled, storage, 0x2020, 0x3000, 44));
        Assert.NotEqual(NativeBattleActor33Presentation.Resource(first, 7, 8), NativeBattleActor33Presentation.Resource(first, 7, 16));
        Assert.Equal(native, storage.Read(17, 1));
        Assert.Null(NativeBattleActor33Presentation.Resource(new(18, 1, 2, 3, 4), 5));
    }
}
