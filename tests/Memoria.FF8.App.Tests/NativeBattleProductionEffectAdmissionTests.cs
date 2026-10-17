using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleProductionEffectAdmissionTests
{
    [Fact]
    public void Continue_the_named_production_checkpoint_through_Blizzard_or_the_next_native_boundary()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A concrete production-host standalone checkpoint is required.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = RestoreWorld(session, directory);
        string id = "production-continuation-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        RunBlizzard(world, session, directory, Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE"), id, false);
    }

    [Fact]
    public void Resume_the_named_production_battle_checkpoint_for_one_native_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A concrete production-host standalone checkpoint is required.");
        GameSession session = new(GameSession.Load(source!));
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        using GameWorld world = RestoreWorld(session, directory);
        GameSession independent = new(GameSession.Load(source!));
        using GameWorld clone = RestoreWorld(independent, directory);
        long callback = world.Battle!.NativeGraphics!.Callback;
        try
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        catch (Exception exception)
        {
            string id = "production-resume-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
            Publish(evidence, id + "-failed-live.battle", world.Battle.Capture());
            Publish(evidence, id + "-failure", new { Error = exception.ToString(), SavedCallback = callback, LiveCallback = world.Battle.NativeGraphics!.Callback });
            throw;
        }
        Assert.True(world.Battle.NativeGraphics!.Callback > callback);
        clone.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(JsonSerializer.Serialize(world.Battle.Capture()), JsonSerializer.Serialize(clone.Battle!.Capture()));
        Assert.Equal(RenderBattle(world), RenderBattle(clone));
        session.SetEventState(world.Events.Capture());
        Publish(Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE"), "production-resume-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-after", session.State);
    }

    [Fact]
    public void Saved_before_entry_component_enters_through_the_game_host_and_materializes_Blizzard_on_its_native_request()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original battle resources are required.");
        using GameAssets assets = new(gameDirectory);
        string? selectorSource = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_MUSIC_SOURCE");
        Assert.SkipUnless(selectorSource is not null && File.Exists(selectorSource), "The named standalone source of the physical music selector is required.");
        byte[] selectorBytes = File.ReadAllBytes(selectorSource!);
        Assert.Equal("0391E6CD0E67E8378DE70DD21A18A9708D1571AB1C8BB92E150C2443A53F49EE", Convert.ToHexString(SHA256.HashData(selectorBytes)));
        using JsonDocument selectorDocument = JsonDocument.Parse(selectorBytes);
        JsonSerializerOptions sourceOptions = new() { Converters = { new JsonStringEnumConverter() } };
        JsonElement selector = selectorDocument.RootElement.GetProperty("EventState").GetProperty("Scene").GetProperty("MusicProcess");
        NativeMusicBootstrap bootstrap = selector.GetProperty("Bootstrap").Deserialize<NativeMusicBootstrap>()!;
        byte sourceBank = selector.GetProperty("Bank201").GetByte();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0);
        int magicAbility = vitals.MenuDefinitions.CommandAbilityCommands.Select((command, index) => (command, index)).Single(item => item.command == 2).index + 20;
        vitals = vitals.SetJunctionAbility(0, 0, magicAbility, true).AddMagicUnit(0, 4, out int capacity).RebuildParty().Rest();
        Assert.Equal(0, capacity);
        // Supplied component inputs; this is not a story route or a migration from an old field save.
        GameSession source = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "controlled-native-battle-effect-entry",
            FullBattlePlaythrough = true,
            CharacterVitals = vitals,
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0),
            PocketStation = assets.ReadNewGamePocketStation(),
            ScannedModels = assets.ReadNewGameScannedModels(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            KnownMagic = assets.ReadNewGameKnownMagic(),
            CrtRandom = new(1),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory(),
            GlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            WorldEntryMemory = selectorDocument.RootElement.GetProperty("WorldEntryMemory").Deserialize<NativeWorldEntryMemory>(sourceOptions)!,
            EventRandom = selectorDocument.RootElement.GetProperty("EventRandom").Deserialize<NativeEventRandomState>(sourceOptions)!
        });
        source.SetWorldControls(NativeWorldControlState.CreateNewGame());
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        string id = "production-blizzard-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        using (GameWorld initial = new(source, gameDirectory))
        {
            initial.Events.LoadField(999, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            JsonNode scene = JsonNode.Parse(initial.Events.Capture().GetRawText())!;
            // Only observed selector/bootstrap are supplied from the preserved source; other owners are explicit component inputs.
            scene["Scene"]!["MusicProcess"] = JsonSerializer.SerializeToNode(new NativeMusicProcessState(1, null, null, null, sourceBank, null, bootstrap));
            scene["Scene"]!["MusicChannel108"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(-1, null));
            scene["Scene"]!["NativeBattleMusic203"] = 5;
            initial.Events.Restore(JsonSerializer.SerializeToElement(scene));
            source.SetEventState(initial.Events.Capture());
            Publish(evidence, id + "-inputs", new { SelectorSource = Path.GetFullPath(selectorSource!), SelectorSourceSha256 = Convert.ToHexString(SHA256.HashData(selectorBytes)), CopiedFields = new[] { "EventState.Scene.MusicProcess.Bank201", "EventState.Scene.MusicProcess.Bootstrap", "WorldEntryMemory", "EventRandom" }, ControlledInputs = "Original initial party/stock/commands, cold graphics/sound, source203=5/channel108=-1; old buffers and pending operands unknown; not an old-save migration or story continuation" });
            Publish(evidence, id + "-before-event", source.State);
            initial.Events.Step();
            initial.Events.Step();
            Assert.Equal((ushort)94, initial.Events.PendingInteractiveBattle!.Encounter);
            source.SetEventState(initial.Events.Capture());
            Publish(evidence, id + "-before-entry", source.State);
        }
        GameState saved = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(source.State))! : GameSession.Load(Path.Combine(evidence, id + "-before-entry.json"));
        GameSession session = new(saved);
        using GameWorld world = RestoreWorld(session, gameDirectory);
        RunBlizzard(world, session, gameDirectory, evidence, id, true);
    }

    private static void RunBlizzard(GameWorld world, GameSession session, string gameDirectory, string? evidence, string id, bool startEncounter)
    {
        try
        {
            if (startEncounter)
            {
                world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 7));
            }
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.NotNull(battle.NativeGraphics);
            Assert.NotNull(battle.NativeMusicProcess!.Commands);
            Assert.Null(battle.Capture().Definition.NativeEffectCatalog[143].BlizzardResources);
            BattleSnapshot initial = battle.Capture();
            bool alreadyRequested = initial.ActiveAction is { Ability: BattleAbility.Magic, ActionId: 4 } || initial.QueuedActions.Any(action => action.Ability == BattleAbility.Magic && action.ActionId == 4);
            if (!alreadyRequested)
            {
                for (int callback = 0; callback < 2000 && (!battle.NativeEntryReady || !battle.Combatants.Any(actor => actor.IsPlayer && actor.IsReady && actor.MagicStocks.Any(stock => stock.Id == 4 && stock.Count > 0))); callback++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                Assert.True(battle.NativeEntryReady);
                BattleCombatant caster = battle.Combatants.First(actor => actor.IsPlayer && actor.IsReady && actor.MagicStocks.Any(stock => stock.Id == 4 && stock.Count > 0));
                BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive);
                world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.Magic, ActionId: 4));
            }
            bool materialized = false;
            bool packets = false;
            bool restored = false;
            for (int callback = 0; callback < 2000; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.NativeGraphics!.OriginalEffect is { Binding.Effect: 144 } effect)
                {
                    Assert.NotNull(effect.Binding.BlizzardResources);
                    materialized = true;
                    packets |= battle.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                    if (!restored && effect.ReturnedDescriptor > 0 && packets)
                    {
                        session.SetEventState(world.Events.Capture());
                        Publish(evidence, id + "-mid", session.State);
                        GameState mid = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))! : GameSession.Load(Path.Combine(evidence, id + "-mid.json"));
                        GameSession independent = new(mid);
                        using GameWorld clone = RestoreWorld(independent, gameDirectory);
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Battle!.Capture()));
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        clone.ApplyBattleInput(new(BattleInputKind.Tick));
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Battle.Capture()));
                        Assert.Equal(RenderBattle(world), RenderBattle(clone));
                        restored = true;
                    }
                    if (effect.ReturnedDescriptor == 0 && battle.NativeEffectMemory!.Active == 0)
                    {
                        break;
                    }
                }
            }
            Assert.True(materialized);
            Assert.True(packets);
            Assert.True(restored);
            Assert.Equal(0u, battle.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
            Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == 300012);
            session.SetEventState(world.Events.Capture());
            Publish(evidence, id + "-after", session.State);
        }
        catch (Exception exception)
        {
            session.SetEventState(world.Events.Capture());
            Publish(evidence, id + "-unsupported", session.State);
            if (world.Battle is not null)
            {
                Publish(evidence, id + "-failed-live.battle", world.Battle.Capture());
            }
            Publish(evidence, id + "-failure", new { Error = exception.ToString(), BattleFrame = world.Battle?.Frame, Callback = world.Battle?.NativeGraphics?.Callback });
            throw;
        }
    }

    private static GameWorld RestoreWorld(GameSession session, string directory)
    {
        JsonElement events = session.State.EventState!.Value;
        GameWorld world = new(session, directory);
        world.Events.LoadField(999, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        world.Events.Restore(events);
        return world;
    }

    private static byte[] RenderBattle(GameWorld world)
    {
        BattleSession battle = world.Battle!;
        return new BattleScenePresentation(world.BattleResources, battle.Capture().Definition, true).Render(battle, 320, 224).ToArray();
    }

    private static void Publish(string? directory, string name, object value)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        using FileStream stream = new(Path.Combine(directory, name + ".json"), FileMode.CreateNew);
        JsonSerializer.Serialize(stream, value, value.GetType());
    }

    private static byte[] Program()
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 0x80), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        const int operationsOffset = 16;
        byte[] bytes = new byte[operationsOffset + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
