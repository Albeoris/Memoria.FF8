using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OmegaEncounterTests
{
    [Fact]
    public void Installed_level_100_Omega_keeps_native_HP_and_AI_in_an_expanded_encounter()
    {
        string directory = TestResources.GameDirectory;
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, field, 462, prepareVitals: vitals =>
            vitals with { Records = vitals.Records.Select(actor => actor with { Experience = 99_000 }).ToArray() });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer).ToArray();
        Assert.Equal(11, enemies.Length);
        Assert.All(enemies, actor =>
        {
            Assert.Equal(114, actor.ModelId);
            Assert.Equal(100, actor.Level);
            Assert.Equal(1_161_000, actor.MaximumHp);
            Assert.Equal(actor.MaximumHp, actor.CurrentHp);
            Assert.NotNull(actor.NativeAi);
        });
        world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
        BattleSnapshot start = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(start))!);
        for (int tick = 0; tick < 640 && battle.Outcome == BattleOutcome.Active; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            restored.Tick();
        }
        Assert.Contains(battle.Events, entry => entry is { Kind: "EffectContact", Message: "LV5 Death" });
        Assert.Contains(battle.Events, entry => entry.Kind == "ActionComplete" && enemies.Any(actor => actor.Id == entry.ActorId));
        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.Equal(BattleOutcome.Defeat, restored.Outcome);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Installed_Omega_death_action_finishes_before_its_allocation_is_released()
    {
        string directory = TestResources.GameDirectory;
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), 462);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleSnapshot baseline = battle.Capture();
        BattleCombatant omega = battle.Combatants.Last(actor => !actor.IsPlayer);
        // Prepare an isolated before-hit boundary, preserving the installed death bytecode and reward owner.
        omega.CurrentHp = 1;
        BattleCombatant attacker = battle.Combatants.First(actor => actor.IsPlayer);
        attacker.Atb = BattleSession.AtbMaximum;
        battle.QueueAttack(attacker.Id, omega.Id);
        for (int tick = 0; tick < 180 && !battle.QueuedActions.Any(action => action.ActorId == omega.Id && action.NativeAiCallbackStage == 3); tick++)
        {
            battle.Tick();
        }
        BattleAttackCommand death = Assert.Single(battle.QueuedActions, action => action.ActorId == omega.Id && action.NativeAiCallbackStage == 3);
        Assert.Equal(114, omega.ModelId);
        Assert.True(omega.IsActive);
        Assert.Equal(0, omega.CurrentHp);
        Assert.Contains(127, battle.NativeScriptState.UnlockedTutorials);
        BattleSnapshot pending = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(pending))!);
        int start = battle.Events.Count;
        for (int tick = 0; tick < 180 && omega.IsActive; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.False(omega.IsActive);
        BattleEvent[] events = battle.Events.Skip(start).ToArray();
        int contact = Array.FindIndex(events, entry => entry.ActorId == omega.Id && entry.Kind == "EffectContact" && entry.NativeSequence == death.NativeSequence);
        int removal = Array.FindIndex(events, entry => entry.ActorId == omega.Id && entry.Kind == "NativeModelRemoved");
        Assert.True(contact >= 0 && removal > contact);
        Assert.Equal(new[] { 127 }, battle.NativeScriptState.UnlockedTutorials);
        Assert.Single(battle.EnemyDefeats, defeat => !baseline.EnemyDefeats.Contains(defeat));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

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
        using GameWorld world = new(session, directory) { UsePhysicalBattlePresentation = true };
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
