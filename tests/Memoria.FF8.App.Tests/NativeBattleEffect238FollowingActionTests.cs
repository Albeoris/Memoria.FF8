using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect238FollowingActionTests
{
    [Fact]
    public void Following_Breath_reuses_evolved_owners_and_releases_a_separate_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT238_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT238_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a returned Effect238 snapshot and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect238PresentationTests.Released(source));
        BattleSession cooldown = NativeBattleEffect238PresentationTests.Restore(source, game);
        for (int callback = 0; callback < 4 && !cooldown.NativeEntryReady; callback++)
        {
            cooldown.Tick();
            Assert.True(NativeBattleEffect238PresentationTests.Released(cooldown.Capture()));
        }
        Assert.True(cooldown.NativeEntryReady);
        source = cooldown.Capture();
        NativeBattleEffect238SourceAdmissionTests.ValidateSource(source, game, assets, false);
        NativeBattleScriptedEffectMemory warm = source.NativeEffectMemory!.ScriptedEffects!;
        Assert.Equal(238, source.NativeEffectMemory.Cinematic!.Effect);
        BattleSnapshot prepared = NativeBattleEffect238PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.NativeEffectMemory!.ScriptedEffects));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.NativeModelCpuResources), JsonSerializer.Serialize(prepared.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGraphics!.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.NativeCrtRandom, prepared.NativeCrtRandom);
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(prepared.Random));
        NativeBattleRestorationPresentationTests.Publish(output!, "following-before-entry", prepared, null);
        BattleSnapshot completed = NativeBattleEffect238PresentationTests.RunPrepared(prepared, game, assets, output!);
        Assert.True(NativeBattleEffect238PresentationTests.Restore(completed, game).NativeEntryReady);
        Assert.True(completed.Frame > source.Frame);
        Assert.NotEqual(source.NativeCrtRandom, completed.NativeCrtRandom);
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 238, SameEffect = true, NoHistoryReset = true, completed.Frame, Callback = completed.NativeGraphics!.Callback }));
    }
}
