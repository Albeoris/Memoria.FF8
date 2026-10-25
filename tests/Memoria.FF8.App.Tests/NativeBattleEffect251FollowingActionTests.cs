using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect251FollowingActionTests
{
    [Fact]
    public void Same251_following_action_preserves_warm_node_models_temporary_and_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed Effect251 standalone and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect251PresentationTests.Released(source));
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        NativeBattleEffect251State warm = source.NativeEffectMemory!.Effect251!;
        Assert.True(warm.ScriptSourcesBound);
        Assert.Single(warm.FixedBanks);
        Assert.All(warm.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.Equal(5, warm.CommonQueueStorage.Count);
        Assert.Single(warm.ScriptStorage);
        Assert.All(warm.ScriptStorage.Values, script => Assert.NotNull(script));
        Assert.All(warm.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
        BattleSnapshot prepared = NativeBattleEffect251PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.NativeEffectMemory!.Effect251));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGraphics!.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.NativeCrtRandom), JsonSerializer.Serialize(prepared.NativeCrtRandom));
        BattleSnapshot completed = NativeBattleEffect251PresentationTests.RunPrepared(prepared, game, assets, output!);
        BattleSession cleanup = NativeBattleEffect251PresentationTests.Restore(completed, game);
        for (int callback = 1; callback <= 4; callback++)
        {
            cleanup.Tick();
            BattleSnapshot current = cleanup.Capture();
            Assert.True(NativeBattleEffect251PresentationTests.Released(current));
            Assert.Equal(JsonSerializer.Serialize(completed.NativeEffectMemory!.Effect251), JsonSerializer.Serialize(current.NativeEffectMemory!.Effect251));
            Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            NativeBattleEffect251PresentationTests.VerifyColdNext("separate-cleanup-" + callback, current, game, assets, output!);
        }
        BattleSnapshot drained = cleanup.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-drained", drained, null);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 251, SameFamily = true, SeparateCleanupCallbacks = 4, drained.Frame, Callback = drained.NativeGraphics!.Callback }));
    }
}
