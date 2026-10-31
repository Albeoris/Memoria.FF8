using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSelectedProgrammeVisualTests
{
    [Theory]
    [InlineData(279)]
    [InlineData(282)]
    public void Programme_births_resources_and_cold_task_epochs_change_only_presentation_identity(ushort effect)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 512, 1);
        (pool, _) = pool.Append(0x123456);
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(pool));
        NativeBattleSelectedProgrammeVisuals.Allocated([pool], 0x2018);
        NativeBattlePilotVisualIdentity a = NativeBattleSelectedProgrammeVisuals.Workspace(effect, pool.Slots[0], 0x2018, 0x10000000, 44);
        NativeBattlePilotVisualIdentity stable = NativeBattleSelectedProgrammeVisuals.Workspace(effect, pool.Slots[0], 0x2018, 0x10000000, 44);
        Assert.Equal(a, stable);
        NativeBattleSelectedProgrammeVisuals.Allocated([pool], 0x2090);
        var sibling = NativeBattleSelectedProgrammeVisuals.Workspace(effect, pool.Slots[0], 0x2090, 0x10000000, 44);
        Assert.Equal(a.Generation, sibling.Generation);
        Assert.NotEqual(a.ResourceGeneration, sibling.ResourceGeneration);
        var resource = NativeBattleSelectedProgrammeVisuals.Workspace(effect, pool.Slots[0], 0x2018, 0x20000000, 44);
        Assert.NotEqual(a.ResourceGeneration, resource.ResourceGeneration);
        Assert.NotEqual(NativeBattleSelectedProgrammeVisuals.Resource(a, 0x10000000, 0x1000004C), NativeBattleSelectedProgrammeVisuals.Resource(a, 0x10000000, 0x20E0EE4));
        NativeBattleSelectedProgrammeVisuals.Allocated([pool], 0x2018);
        var restarted = NativeBattleSelectedProgrammeVisuals.Workspace(effect, pool.Slots[0], 0x2018, 0x10000000, 44);
        Assert.Equal(a.Generation, restarted.Generation);
        Assert.NotEqual(a.ResourceGeneration, restarted.ResourceGeneration);
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(pool)));
        NativeBattleEffectTaskPool cold = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(pool));
        var restored = NativeBattleSelectedProgrammeVisuals.Workspace(effect, cold.Slots[0], 0x2018, 0x10000000, 44);
        Assert.NotEqual(a.Generation, restored.Generation);
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cold)));
    }

    [Fact]
    public void Linked279_segment_identity_tracks_both_actual_reused_rows_without_native_stores()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        const uint bank = 0x20DFAB8 + NativeBattleMegaSpark279Profile.RowsOffset;
        NativeBattlePilotVisualIdentity task = new(279, 0x25080B0, 0, 1, 0);
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(arena));
        NativeBattlePilotVisualLifetimes.Allocated(arena, bank, 0);
        NativeBattlePilotVisualLifetimes.Allocated(arena, bank, 1);
        var a = NativeBattleSelectedProgrammeVisuals.RowPair(arena, task, bank, bank + 40, 0);
        Assert.Equal(a, NativeBattleSelectedProgrammeVisuals.RowPair(arena, task, bank, bank + 40, 0));
        NativeBattlePilotVisualLifetimes.Allocated(arena, bank, 1);
        var b = NativeBattleSelectedProgrammeVisuals.RowPair(arena, task, bank, bank + 40, 0);
        Assert.Equal(a.Generation, b.Generation);
        Assert.NotEqual(a.ResourceGeneration, b.ResourceGeneration);
        NativeBattlePilotVisualLifetimes.Allocated(arena, bank, 0);
        Assert.NotEqual(b.Generation, NativeBattleSelectedProgrammeVisuals.RowPair(arena, task, bank, bank + 40, 0).Generation);
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(arena)));
    }
}
