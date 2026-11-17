using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect233FollowingActionTests
{
    [Fact]
    public void Following_component_reuses_evolved_owners_and_releases_a_separate_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT233_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT233_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a returned Effect233 snapshot and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleFixtureInspection.ReadLogicalSnapshot(path!);
        Assert.True(NativeBattleEffect233PresentationTests.Released(source));
        Assert.True(NativeBattleEffect233PresentationTests.Restore(source, game).NativeEntryReady);
        NativeBattleEffect233SourceAdmissionTests.ValidateSource(source, assets, false);
        NativeBattleScriptedEffectMemory warm = source.ReadFixturePresentation().NativeEffectMemory!.ScriptedEffects!;
        Assert.Equal(233, source.ReadFixturePresentation().NativeEffectMemory!.Cinematic!.Effect);
        BattleSnapshot prepared = NativeBattleEffect233PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeEffectMemory!.ScriptedEffects));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeFileStaging), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.ReadFixturePresentation().NativeModelCpuResources), JsonSerializer.Serialize(prepared.ReadFixturePresentation().NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics!)), JsonSerializer.Serialize(ReadOriginalModelCells(prepared.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(source.NativeCrtRandom, prepared.NativeCrtRandom);
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(prepared.Random));
        NativeBattleRestorationPresentationTests.Publish(output!, "following-before-entry", prepared, null);
        BattleSnapshot completed = NativeBattleEffect233PresentationTests.RunPrepared(prepared, game, assets, output!);
        Assert.True(NativeBattleEffect233PresentationTests.Restore(completed, game).NativeEntryReady);
        Assert.True(completed.Frame > source.Frame);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 233, SameEffect = true, NoHistoryReset = true, SourceFrame = source.Frame, SourceCrtRandom = source.NativeCrtRandom, CompletedCrtRandom = completed.NativeCrtRandom, completed.Frame, Callback = completed.ReadFixturePresentation().NativeGraphics!.Callback }));
    }
}
