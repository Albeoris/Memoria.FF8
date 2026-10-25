using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect249FollowingActionTests
{
    [Fact]
    public void Following_component_reuses_evolved_owners_and_releases_a_separate_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT249_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT249_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a returned Effect249 snapshot and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect249PresentationTests.Released(source));
        BattleSession cooldown = NativeBattleEffect249PresentationTests.Restore(source, game);
        for (int callback = 0; callback < 4 && !cooldown.NativeEntryReady; callback++)
        {
            cooldown.Tick();
            Assert.True(NativeBattleEffect249PresentationTests.Released(cooldown.Capture()));
        }
        Assert.True(cooldown.NativeEntryReady);
        source = cooldown.Capture();
        NativeBattleEffect249SourceAdmissionTests.ValidateSource(source, game, assets, false);
        NativeBattleScriptedEffectMemory warm = source.NativeEffectMemory!.ScriptedEffects!;
        Assert.Equal(249, source.NativeEffectMemory.Cinematic!.Effect);
        BattleSnapshot prepared = NativeBattleEffect249PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.NativeEffectMemory!.ScriptedEffects));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.NativeModelCpuResources), JsonSerializer.Serialize(prepared.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGraphics!.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.NativeCrtRandom, prepared.NativeCrtRandom);
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(prepared.Random));
        NativeBattleRestorationPresentationTests.Publish(output!, "following-before-entry", prepared, null);
        BattleSnapshot completed = NativeBattleEffect249PresentationTests.RunPrepared(prepared, game, assets, output!);
        Assert.True(NativeBattleEffect249PresentationTests.Restore(completed, game).NativeEntryReady);
        Assert.True(completed.Frame > source.Frame);
        Assert.NotEqual(source.NativeCrtRandom, completed.NativeCrtRandom);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 249, SameEffect = true, NoHistoryReset = true, SourceFrame = source.Frame, SourceCrtRandom = source.NativeCrtRandom, CompletedCrtRandom = completed.NativeCrtRandom, completed.Frame, Callback = completed.NativeGraphics!.Callback }));
    }
}
