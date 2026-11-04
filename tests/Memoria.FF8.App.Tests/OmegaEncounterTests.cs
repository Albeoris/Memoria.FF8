using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OmegaEncounterTests
{
    [Fact]
    public void Installed_Omega_enters_completes_an_original_action_and_restores_at_the_boundary()
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
            SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(),
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
        battle.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        Assert.Equal(462, battle.EncounterId);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.NotNull(battle.NativeGraphics);
        Assert.False(battle.NativeEntryReady);
        BattleSnapshot saved = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(restored.Capture()));
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        List<BattleEvent> completions = [];
        battle.DiagnosticEffectDispatched += dispatch => dispatches.Add(dispatch);
        battle.DiagnosticEventPublished += entry =>
        {
            if (entry.Kind == "ActionComplete" && entry.ActorId == omega.Id)
            {
                completions.Add(entry);
            }
        };
        for (int callback = 0; callback < 6000 && (completions.Count == 0 || battle.ActiveAction is not null); callback++)
        {
            battle.Tick();
            audio.Update(battle);
        }
        Assert.True(battle.NativeEntryReady);
        Assert.Contains(dispatches, dispatch => dispatch.ActorId == omega.Id && dispatch.ActionId is 2 or 16 or 124 or 308 or 309 or 310 or 311 or 378);
        Assert.NotEmpty(completions);
        Assert.Null(battle.ActiveAction);
        saved = battle.Capture();
        restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        BattleAudioPresentation restoredAudio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        battle.Tick();
        restored.Tick();
        audio.Update(battle);
        restoredAudio.Update(restored);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        // Original host action/continuation coverage; this diagnostic party does not establish a field route or a completed fight.
    }
}
