using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianSkipPresentationTests
{
    [Theory]
    [InlineData(0, 116)]
    [InlineData(1, 185)]
    [InlineData(2, 201)]
    public void Installed_guardian_skip_yields_between_callbacks_holds_pixels_and_stops_at_the_synchronous_finale(int guardian, ushort effectId)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, guardian, effectId);
        BattleSession sliced = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 400 && (!sliced.NativeEntryReady || !sliced.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
        {
            sliced.Tick();
        }
        Assert.True(sliced.NativeEntryReady);
        Assert.True(sliced.Combatants.Single(actor => actor.Id == "party").IsReady);
        sliced.QueueAction("party", "enemy", BattleAbility.GuardianForce, guardian);
        for (int callback = 0; callback < 4096 && !sliced.CanSkipGuardianFormFinisher; callback++)
        {
            sliced.Tick();
        }
        Assert.True(sliced.CanSkipGuardianFormFinisher);
        BattleScenePresentation scene = new(assets, definition, true);
        byte[] visible = scene.Render(sliced, 320, 224).ToArray();
        Assert.True(sliced.SkipGuardianFormFinisher());
        Assert.True(sliced.GuardianAnimationSkipPending);
        BattleSession synchronous = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(sliced.Capture()))!);
        synchronous.Tick();
        Assert.False(synchronous.GuardianAnimationSkipPending);

        long initial = sliced.NativeGraphics!.Callback;
        sliced.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1), TimeSpan.Zero);
        Assert.Equal(initial + 1, sliced.NativeGraphics!.Callback);
        Assert.True(sliced.GuardianAnimationSkipPending);
        Assert.Equal(visible, scene.Render(sliced, 320, 224).ToArray());
        BattleScenePresentation restoredScene = new(assets, definition, true);
        byte[] blank = restoredScene.Render(sliced, 320, 224).ToArray();
        Assert.True(blank.Where((value, index) => index % 4 != 3).All(value => value == 0));
        Assert.True(blank.Where((value, index) => index % 4 == 3).All(value => value == 255));

        sliced = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(sliced.Capture()))!);
        Assert.True(sliced.GuardianAnimationSkipPending);

        int slices = 1;
        while (slices < 4096 && sliced.GuardianAnimationSkipPending)
        {
            long before = sliced.NativeGraphics!.Callback;
            sliced.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1), TimeSpan.Zero);
            Assert.Equal(before + 1, sliced.NativeGraphics!.Callback);
            slices++;
        }
        Assert.True(slices > 1);
        Assert.False(sliced.GuardianAnimationSkipPending);
        Assert.True(NativeBattleGuardianAnimationFinale.HasReachedFinale(sliced.NativeGraphics!.OriginalEffect!, sliced.NativeEffectMemory!));
        Assert.Equal(JsonSerializer.Serialize(synchronous.Capture()), JsonSerializer.Serialize(sliced.Capture()));
        byte[] finale = new BattleScenePresentation(assets, definition, true).Render(synchronous, 320, 224).ToArray();
        Assert.Equal(finale, scene.Render(sliced, 320, 224).ToArray());
        Assert.Equal(finale, restoredScene.Render(sliced, 320, 224).ToArray());

        long firstFinale = sliced.NativeGraphics!.Callback;
        sliced.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30), TimeSpan.Zero);
        Assert.Equal(firstFinale + 1, sliced.NativeGraphics!.Callback);
    }
}
