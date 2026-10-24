using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect345FollowingActionTests
{
    [Fact]
    public void Same345_following_action_preserves_warm_node_models_temporary_and_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT345_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT345_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed LV5 Death standalone and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect345PresentationTests.Released(source));
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        NativeBattleEffect345State warm = source.NativeEffectMemory!.Effect345!;
        Assert.True(warm.OwnedModelSourceBound);
        Assert.Equal(2, warm.NodeData!.Count);
        Assert.All(warm.BankStorage, bank => Assert.NotNull(bank));
        BattleSnapshot prepared = NativeBattleEffect345PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.NativeEffectMemory!.Effect345));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGraphics!.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        BattleSnapshot completed = NativeBattleEffect345PresentationTests.RunPrepared(prepared, game, assets, output!);
        BattleSession cleanup = NativeBattleEffect345PresentationTests.Restore(completed, game);
        for (int callback = 1; callback <= 4; callback++)
        {
            cleanup.Tick();
            BattleSnapshot current = cleanup.Capture();
            Assert.True(NativeBattleEffect345PresentationTests.Released(current));
            Assert.Equal(JsonSerializer.Serialize(completed.NativeEffectMemory!.Effect345), JsonSerializer.Serialize(current.NativeEffectMemory!.Effect345));
            Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            NativeBattleEffect345PresentationTests.VerifyColdNext("separate-cleanup-" + callback, current, game, assets, output!);
        }
        BattleSnapshot drained = cleanup.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-drained", drained, null);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 345, SameFamily = true, SeparateCleanupCallbacks = 4, drained.Frame, Callback = drained.NativeGraphics!.Callback }));
    }
}
