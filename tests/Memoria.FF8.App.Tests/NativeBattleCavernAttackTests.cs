using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCavernAttackTests
{

    [Theory]
    [InlineData(87, 0)]
    [InlineData(87, 3)]
    [InlineData(89, 0)]
    [InlineData(89, 3)]
    [InlineData(91, 0)]
    [InlineData(91, 3)]
    [InlineData(92, 0)]
    [InlineData(92, 3)]
    public void Original_cavern_encounters_complete_player_attack_and_continue_a_private_fork(ushort formation, int character)
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
        using GameAssets game = new(directory);
        using GameWorld world = new(session, directory);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-formations", encounters.Formations.Distinct().Select(id => new { Formation = id, Enemies = world.BattleResources.LoadEncounter(id).Enemies.Select(enemy => new { enemy.ModelId, Name = world.BattleResources.ReadEnemyName(enemy.ModelId) }).ToArray() }).ToArray());
        Assert.Contains(formation, encounters.Formations);
        BattleDefinition definition = world.CreateEncounterBattle(formation);
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        NativeMusicProcessState buffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, buffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
        definition = definition with { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess };
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition, new GameplayPreferences { AtbMode = BattleAtbMode.TurnBased }, game, world.BattleResources);
        session.SetLiveBattle(battle);
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-initial", session.State);
        int largestPacketEnd = 0;
        int largestProxyCount = 0;
        try
        {
            for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
            {
                battle.Tick();
                AuditSceneCapacity();
            }
            Assert.True(battle.NativeEntryReady);
            battle = battle.ForkForContinuation();
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
                AuditSceneCapacity();
                completed = battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == actor.Id);
            }
            Assert.True(completed, $"Player Attack did not complete in encounter {formation}.");
            BattleScenePresentation scene = new(world.BattleResources, definition);
            Assert.NotEmpty(scene.Render(battle, 320, 224));
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-after", session.State);
            BattleSession restored = battle.ForkForContinuation();
            restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(world.BattleResources, definition).Render(restored, 320, 224));
            string beforePresentation = JsonSerializer.Serialize(battle.Capture());
            foreach ((int width, int height) in new[] { (640, 480), (1280, 720), (800, 1200) })
            {
                Assert.Equal(scene.Render(battle, width, height), new BattleScenePresentation(world.BattleResources, definition).Render(restored, width, height));
            }
            Assert.Equal(beforePresentation, JsonSerializer.Serialize(battle.Capture()));
            Console.WriteLine($"Expanded scene formation {formation}, actor {character}: packet end {largestPacketEnd}, proxies {largestProxyCount}.");
        }
        catch (Exception exception)
        {
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-failure-state", session.State);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"cavern-{formation}-actor-{character}-failure", new { Error = exception.ToString(), battle.Frame, Callback = battle.Presentation?.Graphics?.Callback });
            throw;
        }

        void AuditSceneCapacity()
        {
            NativeBattlePrimitiveFrame frame = battle.Presentation!.Graphics.PrimitiveFrame!;
            NativeBattlePrimitiveProcess process = battle.Presentation!.Memory.PrimitiveProcess!;
            int proxyCount = frame.LastCommands.Select(item => item.Command.Packet).Distinct().Count();
            largestProxyCount = Math.Max(largestProxyCount, proxyCount);
            Assert.True(proxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity);
            foreach (NativeBattleLogicalCommand command in frame.LastCommands.Select(item => item.Command.ToCommand()).Where(command => command.Packet.Bank is "off_B6D070" or "off_B6D094"))
            {
                int end = command.Packet.ByteOffset + (command.WordOffset + command.Words.Length) * 4;
                largestPacketEnd = Math.Max(largestPacketEnd, end);
                Assert.InRange(end, 0, (2 - command.Packet.Buffer) * process.CommonPacketNominalCapacity);
            }
        }
    }
}
