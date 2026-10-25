using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect312FollowingActionTests
{
    [Fact]
    public void Same312_following_action_preserves_warm_node_models_temporary_and_separate_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT312_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT312_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed Effect312 standalone and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect312PresentationTests.Released(source));
        Assert.Null(source.NativeCamera!.ControlCursor);
        Assert.Empty(source.NativeCamera.Order);
        NativeBattleEffect312State warm = source.NativeEffectMemory!.Effect312!;
        Assert.Equal(2, warm.FixedBanks.Count);
        Assert.All(warm.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.True(warm.StaticPacketBound);
        Assert.NotNull(warm.CapturePacket24);
        BattleSnapshot prepared = NativeBattleEffect312PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.NativeEffectMemory!.Effect312));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGraphics!.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.NativeCrtRandom), JsonSerializer.Serialize(prepared.NativeCrtRandom));
        BattleSnapshot completed = NativeBattleEffect312PresentationTests.RunPrepared(prepared, game, assets, output!);
        BattleSession cleanup = NativeBattleEffect312PresentationTests.Restore(completed, game);
        for (int callback = 1; callback <= 4; callback++)
        {
            cleanup.Tick();
            BattleSnapshot current = cleanup.Capture();
            Assert.True(NativeBattleEffect312PresentationTests.Released(current));
            Assert.Equal(JsonSerializer.Serialize(completed.NativeEffectMemory!.Effect312), JsonSerializer.Serialize(current.NativeEffectMemory!.Effect312));
            Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            NativeBattleEffect312PresentationTests.VerifyColdNext("separate-cleanup-" + callback, current, game, assets, output!);
        }
        BattleSnapshot drained = cleanup.Capture();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-drained", drained, null);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 312, SameFamily = true, SeparateCleanupCallbacks = 4, drained.Frame, Callback = drained.NativeGraphics!.Callback }));
    }
}
