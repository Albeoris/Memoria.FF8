using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BalambEncounterStartupTests
{
    [Fact]
    public void NewGamePlusProjectsInstalledEnemyStatsAboveTheNativeLevelRange()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState native = GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            CardInventory = NativeCardInventory.CreateNewGame(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        };
        NewGamePlusDraft draft = new(native, native, new());
        GameplayLevelRules rules = new() { MaximumEnemyLevel = 300, MaximumCharacterLevel = 275, AdditionalPartyLevel = 150 };
        draft.SetLevelRules(rules);
        draft.SetGlobalLevel(275);
        using GameWorld world = new(new GameSession(draft.ApplyTo(native)), directory);

        BattleDefinition definition = world.CreateEncounterBattle(512);
        Assert.Equal(rules, definition.LevelRules);
        Assert.All(definition.Combatants.Where(actor => actor.IsPlayer), actor => Assert.Equal(275, actor.Level));
        BattleCombatantDefinition enemy = definition.Combatants.First(actor => !actor.IsPlayer && actor.InitialActive && actor.Level == 300);
        BattleEnemyNativeProjection stats = world.BattleResources.ReadEnemyNativeStats(enemy.ModelId).Project(300);
        Assert.Equal(stats.MaximumHp, enemy.MaximumHp);
        Assert.Equal(stats.Magic, enemy.Magic);
        Assert.Equal(world.BattleResources.ReadEnemyAi(enemy.ModelId, 300).LevelBand, enemy.NativeAi!.LevelBand);

        BattleNativeCommandBinding levelDown = world.BattleResources.ReadNativeCommand(33);
        BattleNativeCommandBinding levelUp = world.BattleResources.ReadNativeCommand(34);
        BattleCombatantDefinition player = definition.Combatants.First(actor => actor.IsPlayer);
        definition = world.BindExpandedEnemyLevels(definition with
        {
            Combatants = definition.Combatants.Select(actor => actor.Id == player.Id ? actor with { NativeCommands = [.. actor.NativeCommands, levelDown, levelUp] } : actor).ToArray()
        });
        NativeMusicProcessState sourceBuffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(512, definition.NativeFlags, sourceBuffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), new NativeMusicCatalog(directory).ReadBattleEntryResources());
        definition = definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, native.GameplayPreferences, assets, world.BattleResources);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: player.Id);

        TickUntil(battle, () => battle.CanChoosePlayerCommand(player.Id));
        battle.QueueNativePlayerCommand(player.Id, enemy.Id, 33, levelDown.ActionId);
        TickUntil(battle, () => battle.Events.Any(item => item.LevelChange is { NewLevel: 150 }));
        TickUntil(battle, () => battle.CanChoosePlayerCommand(player.Id));
        battle.QueueNativePlayerCommand(player.Id, enemy.Id, 34, levelUp.ActionId);
        BattleSession fork = battle.ForkForContinuation();
        foreach (BattleSession candidate in new[] { battle, fork })
        {
            TickUntil(candidate, () => candidate.Events.Any(item => item.LevelChange is { NewLevel: 300 }));
            BattleCombatant restoredEnemy = candidate.Combatants.Single(actor => actor.Id == enemy.Id);
            Assert.Equal(300, restoredEnemy.Level);
            Assert.Equal(stats.MaximumHp, restoredEnemy.MaximumHp);
        }
    }

    private static void TickUntil(BattleSession battle, Func<bool> condition)
    {
        for (int callback = 0; callback < 2000 && !condition(); callback++)
        {
            battle.Tick();
        }
        Assert.True(condition(), $"Battle did not reach the expected state after {battle.Frame} callbacks.");
    }

    [Theory]
    [InlineData(6)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(24)]
    public void Original_Balamb_formations_construct_raw_entry_and_restore_its_snapshot_through_the_current_game_host(byte ground)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original encounter and participant resources are required.");
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        // This isolated component owns explicit installed initial party records; it makes no route or saved gameplay claim.
        GameSession session = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "synthetic-balamb-encounter-startup-component",
            CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        });
        using GameWorld world = new(session, directory);
        NativeWorldEncounterDefinition encounters = Assert.IsType<NativeWorldEncounterDefinition>(worldAssets.ReadRandomEncounterDefinition(24576, -29406, ground, 1));
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();

        foreach (ushort formation in encounters.Formations.Distinct())
        {
            BattleDefinition definition = world.CreateEncounterBattle(formation);
            AssertEntryAllocationRandomOrder(world, definition);
            //Each independent component declares sourceBYTE201=0 with both old buffers unknown.
            //Actual52CE30 establishes this formation's selected buffer/return selectors before fresh501C60.
            NativeMusicProcessState sourceBuffers = new(1, null, null, null, 0, null);
            NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, sourceBuffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
            definition = definition with { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess };
            BattleSession battle = GameWorld.CreateLiveBattleSession(definition, new GameplayPreferences { AtbMode = BattleAtbMode.Normal }, assets, world.BattleResources);

            Assert.Equal(formation, battle.EncounterId);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Equal(0, battle.Frame);
            Assert.Contains(battle.Combatants, actor => actor.IsPlayer && actor.IsActive);
            Assert.Contains(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
            Assert.All(definition.Combatants.Where(actor => !actor.IsPlayer), actor => Assert.NotNull(actor.NativeAi));
            Assert.NotNull(battle.Presentation?.Graphics);
            Assert.NotNull(definition.NativeClockRules);
            Assert.False(battle.NativeEntryReady);
            Assert.Equal(0.8f, battle.Presentation!.Memory.PrimitiveProcess!.Depth!.BucketMix);
            Assert.Equal((float)(1d - (double)0.8f), battle.Presentation!.Memory.PrimitiveProcess.Depth.RemainingWeight);
            Assert.Equal(new NativeBattleMusicCommandState(false, null, null, null, 0x1CE075C, false), battle.NativeMusicProcess!.Commands);
            BattleSnapshot snapshot = battle.Capture();
            BattleSession restored = BattleSession.Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
    }

    private static void AssertEntryAllocationRandomOrder(GameWorld world, BattleDefinition definition)
    {
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(definition.NativeSeed);
        foreach (BattleCombatantDefinition party in definition.Combatants.Where(actor => actor.IsPlayer))
        {
            if (((party.NativeStatus0 | party.NativeRetainedStatusBits) & 5) != 0)
            {
                Assert.Equal(0, party.InitialAtb);
            }
            else if ((party.NativePlayerStatRules!.NativeStatusPassiveFlags!.Value & 0x10000) != 0)
            {
                Assert.Equal(BattleSession.AtbMaximum, party.InitialAtb);
            }
            else
            {
                expected = expected.Draw(out byte roll);
                Assert.Equal(Math.Clamp((party.Speed >> 2) + (roll & 0x7F) - 34, 0, 100) * 100, party.InitialAtb);
            }
        }
        foreach (BattleEnemyPlacement enemy in world.BattleResources.LoadEncounter(definition.EncounterId!.Value).Slots.Where(slot => slot.Enabled))
        {
            int levelDraws = BattleEnemyNativeStats.LevelRandomByteCount((byte)enemy.Level);
            for (int draw = 0; draw < levelDraws; draw++)
            {
                expected = expected.Draw(out _);
            }
            expected = expected.Draw(out byte entryRoll);
            BattleCombatantDefinition actor = definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == enemy.Slot);
            Assert.Equal(BattleInitialAtb.Calculate(actor.Speed, entryRoll), actor.InitialAtb);
        }
        Assert.Equal(expected.Indexes.ToArray(), definition.InitialRandom!.Indexes.ToArray());
        Assert.Equal(expected.Lane, definition.InitialRandom.Lane);
    }
}
