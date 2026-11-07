using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattleSavedFireBreathTests
{
    [Fact]
    public void Selected_player_save_casts_Fire_Breath_with_its_retained_party_resources()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIRE_BREATH_SAVE");
        Assert.SkipUnless(input is not null && File.Exists(input), "Select a standalone player save for Fire Breath continuation.");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(input!, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        BattleDefinition definition = world.CreateEncounterBattle(79, activeFlags: 128);
        definition = NativeBattleComponentInputs.PrepareMusic(definition, directory, 0, 5, -1);
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.TurnBased);
        battle.AttachNativeEffectResourceResolver(id => assets.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        battle.ConfigureDiagnosticScheduling(true, true, "party-3");
        for (int tick = 0; tick < 1000 && !battle.NativeEntryReady; tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.IsPlayer && actor.ModelId == 3);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive);
        for (int tick = 0; tick < 2000 && !battle.CanChoosePlayerCommand(caster.Id); tick++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand(caster.Id));
        // Isolate the reported crisis-1 command while retaining the selected save's party and resources.
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new(caster.Id, 1)] });
        battle.AttachNativeEffectResourceResolver(id => assets.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        battle.QueueNativePlayerCommand(caster.Id, target.Id, 15, 9);
        bool entered = false;
        bool restored = false;
        using BattleAssets battleAssets = new(directory);
        BattleScenePresentation scene = new(battleAssets, battle.Capture().Definition, true);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        for (int display = 0; display < 2400; display++)
        {
            battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(166667));
            if ((display & 1) != 0)
            {
                battle.Tick();
            }
            scene.Render(battle, 960, 720, gpu);
            if (battle.NativeGraphics!.OriginalEffect is { Binding.Effect: 230, ReturnedDescriptor: > 0 })
            {
                entered = true;
                if (!restored && display > 200)
                {
                    BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
                    BattleSession copy = BattleSession.Restore(snapshot);
                    copy.AttachNativeEffectResourceResolver(id => assets.ReadNativeEffectCatalogForEffects([id])[id - 1]);
                    battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(166667));
                    copy.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(166667));
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(copy.Capture()));
                    restored = true;
                }
            }
            else if (entered && battle.NativeEffectMemory!.Active == 0 && battle.ActiveAction is null)
            {
                Assert.True(restored);
                return;
            }
        }
        Assert.Fail($"Fire Breath did not complete: entered={entered}, restored={restored}.");
    }
}
