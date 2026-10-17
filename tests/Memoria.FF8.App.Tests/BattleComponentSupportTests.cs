using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleComponentSupportTests
{
    [Fact]
    public void Live_turn_based_component_automatically_refills_after_queued_action_completion_and_restores_at_the_boundary()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using GameWorld world = new(CreateSession(assets, 0), assets.GameDirectory);
        BattleDefinition seed = ComponentDefinition(world, 512);
        BattleCombatantDefinition party = seed.Combatants.First(actor => actor.IsPlayer) with { Speed = 1_000 };
        BattleCombatantDefinition enemy = seed.Combatants.First(actor => !actor.IsPlayer) with { MaximumHp = 50_000, CurrentHp = 50_000 };
        BattleSession battle = GameWorld.CreateLiveBattleSession(seed with { Combatants = [party, enemy] }, BattleAtbMode.TurnBased);
        Assert.Null(battle.NativeGraphics);
        for (int callback = 0; callback < 128 && battle.QueuedActions.Count != 0; callback++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        long before = battle.Frame;

        battle.Tick();

        Assert.True(battle.Combatants[0].IsReady);
        Assert.True(battle.Frame > before + 1);
        long decision = battle.Frame;
        battle.Tick();
        Assert.Equal(decision, battle.Frame);
        battle.QueueAttack(party.Id, enemy.Id);
        battle.Tick();
        Assert.NotNull(battle.ActiveAction);
        for (int callback = 0; callback < 256 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0 || battle.Capture().NativeActionScratch!.ComponentCompletion is not null); callback++)
        {
            battle.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.Capture().NativeActionScratch!.ComponentCompletion);
        Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == party.Id);
        Assert.False(battle.Combatants[0].IsReady);
        before = battle.Frame;
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);

        battle.Tick();
        restored.Tick();

        Assert.True(battle.Combatants[0].IsReady);
        Assert.True(battle.Frame > before + 1);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(8, 10)]
    public void Live_component_entry_uses_saved_guardian_flags_once_and_restores_without_another_draw(byte flags, byte expectedAction)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = CreateSession(assets, flags);
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition definition = ComponentDefinition(world, 512);
        Assert.NotNull(definition.NativeSupportRules);
        Assert.Null(definition.NativeClockRules);
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.TurnBased);
        NativeBattleRandomState expected = definition.InitialRandom!.Draw(out _);
        if (flags == 8)
        {
            expected = expected.Draw(out _);
        }

        Assert.True(battle.Capture().NativeSupport!.EntryInitialized);
        Assert.Equal(expectedAction, battle.Capture().NativeSupport!.SelectedAction);
        AssertRandom(expected, battle.Capture().Random!);
        Assert.Equal(new BattleNativeCallback(0, 7), battle.QueuedActions.Last().NativeCallback);
        string before = JsonSerializer.Serialize(battle.Capture());

        battle.CompleteComponentBattleEntry();
        BattleScenePresentation scene = new(world.BattleResources, definition, true);
        scene.AdvancePresentation(TimeSpan.FromSeconds(1));
        _ = scene.Render(battle, 320, 224);

        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        session.SetBattle(battle.Capture());
        using GameWorld restored = new(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!), assets.GameDirectory);
        Assert.Equal(before, JsonSerializer.Serialize(restored.Battle!.Capture()));
    }

    [Fact]
    public void Periodic_Angelo_uses_admitted_component_callbacks_and_waiting_or_rendering_cannot_consume_its_cooldown()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = CreateSession(assets, 0, rinoa: true);
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition definition = ComponentDefinition(world, 512);
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 128 && (battle.QueuedActions.Count != 0 || battle.ActiveAction is not null || battle.Capture().NativeActionScratch!.ComponentCompletion is not null); callback++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        Assert.Null(battle.Capture().NativeActionScratch!.ComponentCompletion);
        BattleSnapshot admitted = battle.Capture();
        Assert.True(admitted.NativeSupport!.Cooldown < definition.NativeSupportRules!.Cooldown);
        // Independent boundary inputs isolate the final cooldown callback and a ready player's command wait.
        BattleSnapshot boundary = admitted with { Random = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0), NativeSupport = admitted.NativeSupport with { Cooldown = 0 } };
        BattleSession waiting = BattleSession.Restore(boundary with { Combatants = boundary.Combatants.Select((actor, index) => index == 0 ? actor with { Atb = BattleSession.AtbMaximum } : actor).ToArray() });
        string before = JsonSerializer.Serialize(waiting.Capture());

        waiting.Tick();
        BattleScenePresentation scene = new(world.BattleResources, definition, true);
        scene.AdvancePresentation(TimeSpan.FromSeconds(1));
        _ = scene.Render(waiting, 320, 224);

        Assert.Equal(before, JsonSerializer.Serialize(waiting.Capture()));
        battle = BattleSession.Restore(boundary);
        NativeBattleRandomState expected = boundary.Random!.Draw(out _);
        battle.Tick();

        Assert.Equal(boundary.Frame + 1, battle.Frame);
        Assert.Equal(new BattleNativeCallback(0, 8), Assert.Single(battle.QueuedActions).NativeCallback);
        Assert.Equal((byte)14, battle.Capture().NativeSupport!.SelectedAction);
        Assert.Equal(definition.NativeSupportRules.Cooldown, battle.Capture().NativeSupport!.Cooldown);
        AssertRandom(expected, battle.Capture().Random!);
        string queued = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(queued)!);
        Assert.Equal(queued, JsonSerializer.Serialize(restored.Capture()));
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal((byte)240, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        Assert.Equal(14, Assert.Single(battle.QueuedActions).ActionId);
    }

    private static BattleDefinition ComponentDefinition(GameWorld world, int formation)
    {
        BattleDefinition original = world.CreateEncounterBattle(formation);
        // Explicit component inputs isolate support from encounter advantage, ready gauges and unrelated enemy decisions.
        return original with
        {
            NativeGraphics = null,
            NativeClockRules = null,
            NativeEntry = null,
            InitialRandom = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0),
            Combatants = original.Combatants.Select(actor => actor.IsPlayer ? actor with { InitialAtb = 0, Speed = 1 } : actor with { InitialAtb = 0, Speed = 1, NativeStatusResistances = new byte[40], NativeAi = actor.NativeAi! with { Scripts = [[0], [0], [0], [0], [0]] } }).ToArray()
        };
    }

    private static void AssertRandom(NativeBattleRandomState expected, NativeBattleRandomState actual)
    {
        Assert.Equal(expected.Lane, actual.Lane);
        Assert.Equal(expected.Indexes.ToArray(), actual.Indexes.ToArray());
    }

    private static GameSession CreateSession(GameAssets assets, byte flags, bool rinoa = false)
    {
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        if (rinoa)
        {
            vitals = vitals with { CombatParty = [4, 255, 255] };
        }
        vitals = vitals.RebuildParty().Rest();
        return new(GameSession.CreateColdProcess().State with { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), InputConfiguration = assets.ReadNewGameInputConfiguration(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame(), LimitProgress = assets.ReadNewGameLimitProgress() with { AngeloFlags = 8 }, SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, flags), PocketStation = assets.ReadNewGamePocketStation(), ScannedModels = assets.ReadNewGameScannedModels(), ShotAvailability = assets.ReadNewGameShotAvailability(), GlobalRandom = assets.ReadNewGameGlobalRandom(), KnownMagic = assets.ReadNewGameKnownMagic() });
    }
}
