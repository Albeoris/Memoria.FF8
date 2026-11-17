using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect316FollowingActionTests
{
    [Fact]
    public void Same316_following_action_preserves_warm_RNG_geometry_and_models_before_separate_four_callback_cleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT316_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a completed effect316 standalone and a new following-action evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect316PresentationTests.Released(source));
        Assert.Null(source.ReadFixturePresentation().NativeCamera!.ControlCursor);
        Assert.Empty(source.ReadFixturePresentation().NativeCamera!.Order);
        NativeBattleEffect316State warm = source.ReadFixturePresentation().NativeEffectMemory!.Effect316!;
        uint random = warm.ReadDword(RandomGlobal);
        Assert.All(warm.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        BattleSnapshot prepared = NativeBattleEffect316PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(random, prepared.ReadFixturePresentation().NativeEffectMemory!.Effect316!.ReadDword(RandomGlobal));
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeEffectMemory!.Effect316));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics!)), JsonSerializer.Serialize(ReadOriginalModelCells(prepared.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeFileStaging), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeFileStaging));
        File.WriteAllText(Path.Combine(output!, "following-warm-source.json"), JsonSerializer.Serialize(new { Status = "prepared", Effect, WarmRandom = random, WarmColor = warm.ReadDword(ColorGlobal), ModelsAndSourceMarkersPreserved = true, RootBankExclusive = true }));
        BattleSnapshot completed = NativeBattleEffect316PresentationTests.RunPrepared(prepared, game, assets, output!);
        Assert.Equal(ReadOriginalModelCell(source.ReadFixturePresentation().NativeGraphics!, 3).Color44, ReadOriginalModelCell(completed.ReadFixturePresentation().NativeGraphics!, 3).Color44);
        uint completedRandom = completed.ReadFixturePresentation().NativeEffectMemory!.Effect316!.ReadDword(RandomGlobal);
        BattleSession cleanup = NativeBattleEffect316PresentationTests.Restore(completed, game);
        for (int callback = 1; callback <= 4; callback++)
        {
            cleanup.Tick();
            BattleSnapshot current = cleanup.CaptureForContinuation();
            Assert.True(NativeBattleEffect316PresentationTests.Released(current));
            Assert.Equal(completedRandom, current.ReadFixturePresentation().NativeEffectMemory!.Effect316!.ReadDword(RandomGlobal));
            Assert.All(current.ReadFixturePresentation().NativeEffectMemory!.Effect316!.FixedBanks.Values, bank => Assert.NotNull(bank));
            Assert.Equal(source.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), current.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            NativeBattleEffect316PresentationTests.VerifyColdNext("separate-cleanup-" + callback, current, game, assets, output!);
        }
        BattleSnapshot drained = cleanup.CaptureForContinuation();
        NativeBattleRestorationPresentationTests.Publish(output!, "following-drained", drained, null);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect, SameFamily = true, WarmRandom = random, FinalRandom = completedRandom, SeparateCleanupCallbacks = 4, drained.Frame, Callback = drained.ReadFixturePresentation().NativeGraphics!.Callback }));
    }
}
