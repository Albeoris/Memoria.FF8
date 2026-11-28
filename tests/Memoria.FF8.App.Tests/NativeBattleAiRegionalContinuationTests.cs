using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAiRegionalContinuationTests
{
    [Fact]
    public void OriginalRegionalEncountersInitializeRunAnEnemyTurnAndContinueAfterFork()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original regional encounter and battle resources are required.");
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        ushort[] formations = new byte[] { 6, 4, 10, 24 }.SelectMany(ground => worldAssets.ReadRandomEncounterDefinition(24576, -29406, ground, 1)!.Formations).Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(512, 10).Select(id => (ushort)id), formations);
        AssertRegionalContinuation(directory, assets, formations, CreateSession(assets));
    }

    [Fact]
    public void OriginalDolletEncountersInitializeRunAnEnemyTurnAndContinueAfterFork()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        ushort[] formations = new[] { 309, 310 }.SelectMany(field => Assert.IsType<NativeFieldEncounterDefinition>(assets.LoadField(field).RandomEncounters).Formations).Distinct().Order().ToArray();
        Assert.Equal(new ushort[] { 30, 31, 32, 33 }, formations);
        AssertRegionalContinuation(directory, assets, formations, LoadDolletParty());
    }

    [Fact]
    public void Tower_removes_Wedges_queued_turn_and_continues_with_Elvoret()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        using GameWorld world = new(LoadDolletParty(), directory);
        BattleSession battle = StartBattle(world, assets, new NativeMusicCatalog(directory).ReadBattleEntryResources(), world.CreateEncounterBattle(29), BattleAtbMode.TurnBased);
        BattleCombatant biggs = battle.Combatants.Single(actor => actor.ModelId == 74 && !actor.IsPlayer);
        BattleCombatant wedge = battle.Combatants.Single(actor => actor.ModelId == 73 && !actor.IsPlayer);
        BattleCombatant attacker = battle.Combatants.First(actor => actor.IsPlayer);

        AdvanceUntil(() => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0, "native entry");
        StrikeBiggs();
        AdvanceUntil(() => !wedge.Suspended && battle.ActiveAction is null && battle.QueuedActions.Count == 0, "Wedge's arrival");
        Assert.True(biggs.IsAlive);
        byte turn = wedge.AiTurnCount;

        StrikeBiggs();
        AdvanceUntil(() => battle.ActiveAction?.ExpandedAiCallback is { Stage: 3 } request && request.Actor.ActorId == biggs.Id, "Biggs's death callback");
        // Arrange the already-registered turn while the death callback still owns the removal descriptors.
        MethodInfo queueCallback = typeof(BattleSession).GetMethod("QueueExpandedAiCallback", BindingFlags.Instance | BindingFlags.NonPublic)!;
        queueCallback.Invoke(battle, [wedge, 1, null]);
        Assert.Contains(battle.QueuedActions, command => command.ExpandedAiCallback is { Stage: 1 } request && request.Actor.ActorId == wedge.Id);
        AdvanceUntil(() => !wedge.IsActive && battle.ActiveAction is null && battle.QueuedActions.Count == 0, "retirement of the brothers' actions");
        Assert.False(biggs.IsActive);
        Assert.Equal(turn, wedge.AiTurnCount);
        BattleCombatant elvoret = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 91 && actor.IsActive);
        Assert.True(elvoret.IsAlive);
        Assert.False(elvoret.Suspended);

        BattleSession fork = battle.ForkForContinuation();
        battle.Tick();
        fork.Tick();
        Assert.Equal(battle.Outcome, fork.Outcome);
        Assert.Equal(battle.NativeHostCallbacks, fork.NativeHostCallbacks);
        Assert.Equal(battle.Capture().NativeCrtRandom, fork.Capture().NativeCrtRandom);
        Assert.Equal(battle.QueuedActions, fork.QueuedActions);

        void StrikeBiggs()
        {
            biggs.CurrentHp = 1;
            attacker.Atb = BattleSession.AtbMaximum;
            battle.QueueAttack(attacker.Id, biggs.Id);
        }

        void AdvanceUntil(Func<bool> reached, string boundary)
        {
            for (int frame = 0; frame < 1600 && !reached(); frame++)
            {
                battle.Tick();
            }
            Assert.True(reached(), $"The original tower battle did not reach {boundary}.");
        }
    }

    private static void AssertRegionalContinuation(string directory, GameAssets assets, ushort[] formations, GameSession session)
    {
        using GameWorld world = new(session, directory);
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        foreach (ushort formation in formations)
        {
            BattleDefinition definition = world.CreateEncounterBattle(formation);
            Assert.Equal(session.State.CharacterVitals!.CombatParty.Where(row => row != 255).Select(row => (int)row), definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.MagicRecordId!.Value));
            BattleSession battle = StartBattle(world, assets, musicResources, definition, BattleAtbMode.Normal);
            BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
            {
                battle.Tick();
            }
            Assert.True(process.EntryReady, $"Original formation{formation} did not complete native entry.");
            BattleCombatant enemy = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.NativeAi is not null);
            enemy.Atb = BattleSession.AtbMaximum;
            for (int callback = 0; callback < 600 && !HasEnemyTurn(battle); callback++)
            {
                battle.Tick();
            }
            Assert.True(HasEnemyTurn(battle), $"Original formation{formation} did not publish a bounded enemy turn for {enemy.Id} ({enemy.Atb} ATB at callback {process.Graphics.Callback}).");
            BattleSession restored = battle.ForkForContinuation();
            Assert.NotSame(battle.Presentation, restored.Presentation);
            battle.Tick();
            restored.Tick();
            Assert.Equal(battle.Outcome, restored.Outcome);
            Assert.Equal(battle.Presentation!.Graphics.Callback, restored.Presentation!.Graphics.Callback);
            Assert.Equal(battle.Capture().NativeCrtRandom, restored.Capture().NativeCrtRandom);
            Assert.Equal(battle.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.Atb, actor.PersistentStatus)), restored.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.Atb, actor.PersistentStatus)));
            Assert.Equal(battle.QueuedActions.Select(action => (action.ActorId, action.TargetId, action.Ability, action.ActionId)), restored.QueuedActions.Select(action => (action.ActorId, action.TargetId, action.Ability, action.ActionId)));
            Assert.Equal(battle.ActiveAction is { } activeAction ? (activeAction.ActorId, activeAction.TargetId, activeAction.Ability, activeAction.ActionId, activeAction.Phase) : default, restored.ActiveAction is { } continued ? (continued.ActorId, continued.TargetId, continued.Ability, continued.ActionId, continued.Phase) : default);
        }
    }

    private static BattleSession StartBattle(GameWorld world, GameAssets assets, NativeBattleMusicEntryResources musicResources, BattleDefinition definition, BattleAtbMode mode)
    {
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(checked((ushort)definition.EncounterId!.Value), definition.NativeFlags, new(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
        return GameWorld.CreateLiveBattleSession(definition with { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess }, new GameplayPreferences { AtbMode = mode }, assets, world.BattleResources);
    }

    private static GameSession LoadDolletParty()
    {
        // Use the saved exam party as component input, without replaying the surrounding field scripts.
        return new(GameSession.Load(TestResources.FixturePath("App.Tests.TowerEntry.1")));
    }

    private static bool HasEnemyTurn(BattleSession battle)
    {
        return battle.QueuedActions.Any(action => action.ExpandedAiCallback is { Stage: 1 }) ||
            battle.ActiveAction is { ExpandedAiCallback.Stage: 1 };
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
