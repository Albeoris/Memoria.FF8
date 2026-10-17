using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCavernAttackTests
{
    [Fact]
    public void Named_cavern_standalone_continues_one_callback_without_reconstructing_a_field_host()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAVERN_ATTACK_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A named standalone cavern component is required.");
        GameState state = GameSession.Load(source!);
        Assert.NotNull(state.Battle);
        BattleSnapshot snapshot = state.Battle!;
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        long callback = battle.NativeGraphics!.Callback;
        battle.Tick();
        restored.Tick();
        Assert.True(battle.NativeGraphics.Callback > callback);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        using BattleAssets assets = new(TestResources.GameDirectory);
        Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(battle, 320, 224), new BattleScenePresentation(assets, snapshot.Definition, true).Render(restored, 320, 224));
        GameSession session = new(state);
        session.SetLiveBattle(battle);
        string id = "cavern-resume-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        NativeBattleProductionEffectAdmissionTests.Publish(Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE"), id, session.State);
    }

    [Theory]
    [InlineData(87, 0)]
    [InlineData(87, 3)]
    [InlineData(89, 0)]
    [InlineData(89, 3)]
    [InlineData(91, 0)]
    [InlineData(91, 3)]
    [InlineData(92, 0)]
    [InlineData(92, 3)]
    public void Original_cavern_encounters_complete_player_attack_and_continue_a_standalone_snapshot(ushort formation, int character)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field and battle resources are required.");
        using GameAssets assets = new(directory);
        NativeFieldEncounterDefinition encounters = Assert.IsType<NativeFieldEncounterDefinition>(assets.LoadField(133).RandomEncounters);
        // Explicit installed initial party and bufferless audio inputs define an isolated encounter component.
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        GameSession session = new(initial.State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "cavern-attack-component",
            FieldLoadContext = null,
            FieldRequestPacket = null,
            CharacterVitals = (assets.ReadNewGameCharacterVitals() with { CombatParty = [0, 3, 255] }).RebuildParty().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            KnownMagic = assets.ReadNewGameKnownMagic(),
            ScannedModels = assets.ReadNewGameScannedModels(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0),
            PocketStation = assets.ReadNewGamePocketStation(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        });
        using GameWorld world = new(session, directory);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-formations", encounters.Formations.Distinct().Select(id => new { Formation = id, Enemies = world.BattleResources.LoadEncounter(id).Enemies.Select(enemy => new { enemy.ModelId, Name = world.BattleResources.ReadEnemyName(enemy.ModelId) }).ToArray() }).ToArray());
        Assert.Contains(formation, encounters.Formations);
        BattleDefinition definition = world.CreateEncounterBattle(formation);
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        NativeMusicProcessState buffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, buffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
        definition = definition with { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.TurnBased);
        session.SetLiveBattle(battle);
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-initial", session.State);
        try
        {
            for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
            {
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            BattleCombatant actor = battle.Combatants.Single(actor => actor.IsPlayer && actor.ModelId == character);
            for (int turn = 0; turn < 16 && !actor.IsReady; turn++)
            {
                battle.AdvanceTurn();
            }
            Assert.True(actor.IsReady);
            BattleCombatant target = battle.Combatants.First(enemy => !enemy.IsPlayer && enemy.IsActive && enemy.IsAlive);
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-before", session.State);
            battle.QueueAttack(actor.Id, target.Id);
            bool completed = false;
            for (int callback = 0; callback < 300 && !completed; callback++)
            {
                battle.Tick();
                completed = battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == actor.Id);
            }
            Assert.True(completed, $"Player Attack did not complete in encounter {formation}.");
            BattleScenePresentation scene = new(world.BattleResources, definition, true);
            Assert.NotEmpty(scene.Render(battle, 320, 224));
            BattleSnapshot after = battle.Capture();
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-after", session.State);
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(after))!);
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(world.BattleResources, definition, true).Render(restored, 320, 224));
        }
        catch (Exception exception)
        {
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-failure-state", session.State);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-failure", new { Error = exception.ToString(), battle.Frame, Callback = battle.NativeGraphics?.Callback });
            throw;
        }
    }
}
