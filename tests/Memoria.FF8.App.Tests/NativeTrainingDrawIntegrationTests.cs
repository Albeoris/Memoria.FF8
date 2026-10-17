using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTrainingDrawIntegrationTests
{
    [Fact]
    public void ActualTwoGratLiveComponentPublishesDrawAfterTheWholeAnimationAndRestoresWithoutRepeatingStock()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(215);
        Assert.Equal("bgmon_2", field.Name);
        Assert.Contains((ushort)59, field.RandomEncounters!.Formations);
        // Installed initial records are explicit component inputs; this does not reconstruct a gameplay route.
        GameSession session = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "installed-training-draw-component",
            CharacterVitals = assets.ReadNewGameCharacterVitals().RebuildParty().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(),
            PocketStation = assets.ReadNewGamePocketStation(),
            ScannedModels = assets.ReadNewGameScannedModels(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            KnownMagic = assets.ReadNewGameKnownMagic()
        });
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition installed = world.CreateEncounterBattle(59) with { NativeGraphics = null, NativeClockRules = null };
        Assert.Null(installed.NativeGraphics);
        BattleCombatantDefinition[] actors = installed.Combatants.ToArray();
        int casterIndex = Array.FindIndex(actors, actor => actor.IsPlayer);
        actors[casterIndex] = actors[casterIndex] with { InitialAtb = BattleSession.AtbMaximum, Abilities = [.. actors[casterIndex].Abilities.Append(BattleAbility.Draw).Distinct()] };
        BattleDefinition definition = installed with { Name = "Installed field215 encounter59 two-Grat Draw component", Combatants = actors };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.TurnBased);
        BattleCombatant caster = battle.Combatants[casterIndex];
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).ToArray();
        Assert.Equal(2, enemies.Length);
        Assert.All(enemies, enemy => Assert.Equal(15, enemy.ModelId));
        Assert.True(caster.IsReady);
        for (int callback = 0; callback < 20 && battle.QueuedActions.Count != 0; callback++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        string? snapshotPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_SNAPSHOT");
        if (snapshotPath is not null)
        {
            using FileStream output = new(snapshotPath, FileMode.CreateNew);
            JsonSerializer.Serialize(output, battle.Capture());
        }
        BattleCombatant source = enemies[0];
        byte selection = source.NativeDrawSlots.First(slot => slot.Id is > 0 and < 64).Id;
        int beforeStock = caster.MagicStocks.Where(stock => stock.Id == selection).Sum(stock => stock.Count);
        battle.QueueAction(caster.Id, source.Id, BattleAbility.Draw, selection);
        battle.Tick();
        BattleNativeDrawContext draw = Assert.IsType<BattleNativeDrawContext>(battle.ActiveAction!.NativeDraw);
        Assert.False(draw.Failed);
        Assert.Equal((byte)6, battle.NativeActionPackets.Headers[0].ReadByte(1));
        Assert.Equal((ushort)15, NativeBattleActionGraphics.Word(battle.NativeActionPackets.Headers[0], 6));
        Assert.Equal(beforeStock + draw.Added, caster.MagicStocks.Where(stock => stock.Id == selection).Sum(stock => stock.Count));
        Assert.DoesNotContain(battle.Events, item => item.Kind == "NativeText" && item.ActorId == caster.Id);
        BattleSession fromWindup = Restore(battle);
        BattleSession? fromRecovery = null;
        long windupFrame = Assert.Single(battle.Events, item => item.Kind == "Windup" && item.ActorId == caster.Id).Frame;
        int callbacks = 0;
        while (caster.IsBusy && callbacks++ < 100)
        {
            Assert.DoesNotContain(battle.Events, item => item.Kind == "NativeText" && item.ActorId == caster.Id);
            battle.Tick();
            fromWindup.Tick();
            fromRecovery?.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(fromWindup.Capture()));
            if (fromRecovery is not null)
            {
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(fromRecovery.Capture()));
            }
            if (fromRecovery is null && battle.ActiveAction is { Phase: BattleActionPhase.Recovery })
            {
                fromRecovery = Restore(battle);
            }
        }
        Assert.False(caster.IsBusy);
        Assert.NotNull(fromRecovery);
        BattleEvent result = Assert.Single(battle.Events, item => item.Kind == "NativeText" && item.ActorId == caster.Id);
        Assert.Equal(windupFrame + BattleSession.AttackWindupFrames + BattleSession.AttackRecoveryFrames, result.Frame);
        Assert.Equal(beforeStock + draw.Added, caster.MagicStocks.Where(stock => stock.Id == selection).Sum(stock => stock.Count));
        Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster.Id && item.Frame == result.Frame);
        BattleSession completed = Restore(battle);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(completed.Capture()));
    }

    private static BattleSession Restore(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }
}
