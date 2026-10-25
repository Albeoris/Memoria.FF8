using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect31FollowingActionTests
{
    [Fact]
    public void Following_Heartbreak_reuses_evolved_owners_and_releases_a_separate_action()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT31_FOLLOWING_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT31_FOLLOWING_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a returned Effect31 snapshot and a distinct following output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(NativeBattleEffect31PresentationTests.Released(source));
        BattleSession cooldown = NativeBattleEffect31PresentationTests.Restore(source, game);
        for (int callback = 0; callback < 4 && !cooldown.NativeEntryReady; callback++)
        {
            cooldown.Tick();
            Assert.True(NativeBattleEffect31PresentationTests.Released(cooldown.Capture()));
        }
        Assert.True(cooldown.NativeEntryReady);
        source = cooldown.Capture();
        NativeBattleEffect31SourceAdmissionTests.ValidateSource(source, game, assets, false);
        NativeBattleEffect31State warm = source.NativeEffectMemory!.Effect31!;
        Assert.True(warm.StaticSourcesBound);
        Assert.NotEmpty(warm.ReadCallbackStack().Observations);
        BattleSnapshot prepared = NativeBattleEffect31PresentationTests.QueueComponent(source, game, assets);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(prepared.NativeEffectMemory!.Effect31));
        Assert.Equal(JsonSerializer.Serialize(source.NativeFileStaging), JsonSerializer.Serialize(prepared.NativeFileStaging));
        Assert.Equal(JsonSerializer.Serialize(source.NativeModelCpuResources), JsonSerializer.Serialize(prepared.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGraphics!.Models), JsonSerializer.Serialize(prepared.NativeGraphics!.Models));
        Assert.Equal(source.NativeCrtRandom, prepared.NativeCrtRandom);
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(prepared.Random));
        NativeBattleRestorationPresentationTests.Publish(output!, "following-before-entry", prepared, null);
        BattleSnapshot completed = NativeBattleEffect31PresentationTests.RunPrepared(prepared, game, assets, output!);
        Assert.True(NativeBattleEffect31PresentationTests.Restore(completed, game).NativeEntryReady);
        Assert.True(completed.Frame > source.Frame);
        Assert.NotEqual(JsonSerializer.Serialize(warm.ReadCallbackStack()), JsonSerializer.Serialize(completed.NativeEffectMemory!.Effect31!.ReadCallbackStack()));
        File.WriteAllText(Path.Combine(output!, "following-result.json"), JsonSerializer.Serialize(new { Status = "completed", Effect = 31, SameEffect = true, NoHistoryReset = true, completed.Frame, Callback = completed.NativeGraphics!.Callback }));
    }
}
