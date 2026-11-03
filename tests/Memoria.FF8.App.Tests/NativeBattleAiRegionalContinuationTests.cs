using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAiRegionalContinuationTests
{
    [Fact]
    public void OriginalRegionalEncountersInitializeRunAnEnemyTurnAndContinueAfterRestore()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original regional encounter and battle resources are required.");
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        GameSession session = CreateSession(assets);
        using GameWorld world = new(session, directory);
        ushort[] formations = new byte[] { 6, 4, 10, 24 }.SelectMany(ground => worldAssets.ReadRandomEncounterDefinition(24576, -29406, ground, 1)!.Formations).Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(512, 10).Select(id => (ushort)id), formations);
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        foreach (ushort formation in formations)
        {
            BattleDefinition definition = world.CreateEncounterBattle(formation);
            Assert.Equal(session.State.CharacterVitals!.CombatParty.Where(row => row != 255).Select(row => (int)row), definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.MagicRecordId!.Value));
            NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, new(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
            BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess }, BattleAtbMode.Normal);
            for (int callback = 0; callback < 2500 && !HasEnemyTurn(battle); callback++)
            {
                battle.Tick();
            }
            Assert.True(HasEnemyTurn(battle), $"Original formation{formation} did not publish a bounded enemy turn.");
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            battle.Tick();
            restored.Tick();
            Assert.Equal(battle.Outcome, restored.Outcome);
            Assert.Equal(battle.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.Atb, actor.PersistentStatus)), restored.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.Atb, actor.PersistentStatus)));
            Assert.Equal(battle.QueuedActions.Select(action => (action.ActorId, action.TargetId, action.Ability, action.ActionId)), restored.QueuedActions.Select(action => (action.ActorId, action.TargetId, action.Ability, action.ActionId)));
            Assert.Equal(battle.ActiveAction is { } activeAction ? (activeAction.ActorId, activeAction.TargetId, activeAction.Ability, activeAction.ActionId, activeAction.Phase) : default, restored.ActiveAction is { } continued ? (continued.ActorId, continued.TargetId, continued.Ability, continued.ActionId, continued.Phase) : default);
        }
    }

    private static bool HasEnemyTurn(BattleSession battle)
    {
        return battle.QueuedActions.Any(action => action.NativeAiCallbackStage == 1 && battle.Combatants.Any(actor => !actor.IsPlayer && actor.Id == action.ActorId)) || battle.ActiveAction is { NativeAiCallbackStage: 1 } active && battle.Combatants.Any(actor => !actor.IsPlayer && actor.Id == active.ActorId);
    }

    private static GameSession CreateSession(GameAssets assets)
    {
        return new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "battle-ai-component",
            CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            PocketStation = assets.ReadNewGamePocketStation(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            KnownMagic = assets.ReadNewGameKnownMagic(),
            ScannedModels = assets.ReadNewGameScannedModels(),
            CardInventory = NativeCardInventory.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        });
    }
}
