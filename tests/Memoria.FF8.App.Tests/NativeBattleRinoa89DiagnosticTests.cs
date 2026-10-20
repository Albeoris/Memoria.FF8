using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleRinoa89CastTests
{
    [Fact]
    public void Capture_named_Wishing_Star_standalone_at_target_phase()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(input is not null && File.Exists(input) && output is not null, "Select a standalone and a separate diagnostic output directory.");
        int phase = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF8_RINOA89_TARGET_PHASE") ?? "110");
        Assert.InRange(phase, 1, 270);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input!))!;
        Assert.Equal((ushort)89, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = BattleSession.Restore(snapshot);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSnapshot before = snapshot;
        try
        {
            for (int callback = 0; callback < 512; callback++)
            {
                NativeBattleModulePoolTask? body = battle.NativeGraphics!.OriginalEffect!.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleRinoa89Controller.Body)?.Slots[0];
                if (body is not null && (body.Flags & 1) != 0 && body.ReadWord(12) >= phase)
                {
                    BattleSnapshot selected = battle.Capture();
                    NativeBattleRestorationPresentationTests.Publish(output!, "selected", selected, scene.Render(battle, 320, 224).ToArray());
                    BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, "selected.battle.json")))!);
                    independent.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                    Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(independent.Capture()));
                    before = selected;
                    battle.Tick();
                    independent.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(independent, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output!, "selected-next", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                    File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "passed", Input = input, TargetPhase = phase, ActualPhase = body.ReadWord(12), selected.Frame, selected.NativeGraphics!.Callback }));
                    return;
                }
                before = battle.Capture();
                battle.Tick();
            }
            Assert.Fail("The selected standalone did not reach its active Wishing Star phase.");
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            throw;
        }
    }
}
