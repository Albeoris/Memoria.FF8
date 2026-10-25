using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OmegaEncounterTests
{
    [Fact]
    public void Installed_Omega_constructs_original_entry_resources_and_restores_the_raw_host_snapshot()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        // Explicit cold-process diagnostic party records; this does not represent a route to the encounter.
        GameSession session = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 593,
            FieldName = "diagnostic-omega-entry-component",
            CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        });
        using GameWorld world = new(session, directory);
        BattleDefinition definition = world.CreateEncounterBattle(462);
        BattleCombatantDefinition omega = Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.InitialActive);
        Assert.Equal(114, omega.ModelId);
        Assert.NotNull(omega.NativeAi);
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(462, definition.NativeFlags, new(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
        definition = definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.Normal);
        Assert.Equal(462, battle.EncounterId);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.NotNull(battle.NativeGraphics);
        Assert.False(battle.NativeEntryReady);
        BattleSnapshot saved = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(restored.Capture()));
        // Raw original-resource entry only: no effects, field trigger, playable fight or completed victory is claimed.
    }
}
