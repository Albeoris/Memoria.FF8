using System.Buffers.Binary;
using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Globalization;
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
    public void Saved_before_entry_component_enters_through_the_game_host_and_materializes_Blizzard_on_its_native_request()
    {
        var (saved, gameDirectory, evidence, id) = CreateBeforeEntryComponent(false);
        GameSession session = new(saved);
        using GameWorld world = RestoreWorld(session, gameDirectory);
        RunBlizzard(world, session, evidence, id);
    }

    [Fact]
    public void Saved_before_entry_component_uses_its_assigned_GF_command_to_materialize_Quezacotl()
    {
        var (saved, gameDirectory, evidence, id) = CreateBeforeEntryComponent(true);
        GameSession session = new(saved);
        using GameWorld world = RestoreWorld(session, gameDirectory);
        RunQuezacotl(world, session, evidence, id);
    }

    private static (GameState State, string GameDirectory, string? Evidence, string Id) CreateBeforeEntryComponent(bool guardianCommand)
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
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
        if (guardianCommand)
        {
            int gfAbility = vitals.MenuDefinitions.CommandAbilityCommands.Select((command, index) => (command, index)).Single(item => item.command == 3).index + 20;
            vitals = vitals.SetJunctionAbility(0, 1, gfAbility, true);
        }
        NativeBattleRetainedMemory nativeMemory = assets.ReadColdProcessNativeBattleMemory();
        NativeBattleExecutionStack? selectedStack = null;
        if (guardianCommand)
        {
            selectedStack = ReadSelectedExecutionStack(gameDirectory);
            NativeBattleEffectMemory effectMemory = Assert.IsType<NativeBattleEffectMemory>(nativeMemory.EffectMemory);
            Assert.Null(effectMemory.ExecutionStack);
            nativeMemory = nativeMemory with { EffectMemory = effectMemory with { ExecutionStack = selectedStack } };
        }
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
            NativeBattleMemory = nativeMemory,
            GlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            WorldEntryMemory = selectorDocument.RootElement.GetProperty("WorldEntryMemory").Deserialize<NativeWorldEntryMemory>(sourceOptions)!,
            EventRandom = selectorDocument.RootElement.GetProperty("EventRandom").Deserialize<NativeEventRandomState>(sourceOptions)!
        });
        source.SetWorldControls(NativeWorldControlState.CreateNewGame());
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        string id = (guardianCommand ? "production-gf-" : "production-blizzard-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        using (GameWorld initial = new(source, gameDirectory))
        {
            initial.Events.LoadField(999, Program(initializeBattleMode: true), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            JsonNode scene = JsonNode.Parse(initial.Events.Capture().GetRawText())!;
            // Only observed selector/bootstrap are supplied from the preserved source; other owners are explicit component inputs.
            scene["Scene"]!["MusicProcess"] = JsonSerializer.SerializeToNode(new NativeMusicProcessState(1, null, null, null, sourceBank, null, bootstrap));
            scene["Scene"]!["MusicChannel108"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(-1, null));
            scene["Scene"]!["NativeBattleMusic203"] = 5;
            initial.Events.Restore(JsonSerializer.SerializeToElement(scene));
            source.SetEventState(initial.Events.Capture());
            Publish(evidence, id + "-inputs", new { SelectorSource = Path.GetFullPath(selectorSource!), SelectorSourceSha256 = Convert.ToHexString(SHA256.HashData(selectorBytes)), CopiedFields = new[] { "EventState.Scene.MusicProcess.Bank201", "EventState.Scene.MusicProcess.Bootstrap", "WorldEntryMemory", "EventRandom" }, AssignedGuardianCommand = guardianCommand, SelectedExecutionEnvironment = selectedStack, HistoricalStackRecovered = false, ControlledInputs = "Original initial party/stock, learned Magic assigned to command slot0, optionally learned GF to slot1 before entry with an explicitly selected captured execution environment, cold graphics/sound, explicit BATTLEMODE0 before BATTLE94/80, source203=5/channel108=-1; old buffers and pending operands unknown; not an old-save migration or story continuation" });
            Publish(evidence, id + "-before-event", source.State);
            initial.Events.Step();
            initial.Events.Step();
            Assert.Equal((ushort)94, initial.Events.PendingInteractiveBattle!.Encounter);
            Assert.Equal((ushort)0, initial.Events.PendingInteractiveBattle.FlagConsumer!.PersistentModeFlags);
            Assert.Contains(initial.Events.Scene.NativeBattleFlagWrites!, write => write.Writer == NativeBattleFlagWriter.BattleMode && write.RawOperand == 0 && write.PersistentFlags == 0 && write.ActiveFlags == 0);
            Assert.Equal((ushort)0x80, initial.Events.Scene.NativeBattleFlagWrites![^1].ActiveFlags);
            source.SetEventState(initial.Events.Capture());
            Publish(evidence, id + "-before-entry", source.State);
        }
        GameState saved = string.IsNullOrWhiteSpace(evidence) ? JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(source.State))! : GameSession.Load(Path.Combine(evidence, id + "-before-entry.json"));
        return (saved, gameDirectory, evidence, id);
    }

    private static NativeBattleExecutionStack ReadSelectedExecutionStack(string gameDirectory)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_CAPTURE");
        string? expected = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_CAPTURE_SHA256");
        Assert.SkipUnless(path is not null && File.Exists(path) && expected is not null, "Explicitly select an authenticated captured execution environment; missing stack ownership remains unsupported.");
        byte[] bytes = File.ReadAllBytes(path!);
        string captureHash = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(expected, captureHash);
        string executableHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"))));
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, executableHash);
        using JsonDocument capture = JsonDocument.Parse(bytes);
        Assert.Equal(0, capture.RootElement.GetProperty("exitCode").GetInt32());
        JsonElement result = capture.RootElement.GetProperty("result");
        JsonElement observation = Assert.Single(result.GetProperty("breaks").EnumerateArray().SelectMany(point => point.GetProperty("recorded").EnumerateArray()), point => Hex(point.GetProperty("address").GetString()!) == 0x4A24B0 && point.TryGetProperty("callerAddress", out JsonElement caller) && Hex(caller.GetString()!) == 0x569C3D);
        uint esp = Hex(Assert.Single(observation.GetProperty("registers").EnumerateArray(), register => register.GetProperty("name").GetString() == "esp").GetProperty("value").GetString()!);
        Assert.Equal(0x4A24B0u, Hex(Assert.Single(observation.GetProperty("registers").EnumerateArray(), register => register.GetProperty("name").GetString() == "eip").GetProperty("value").GetString()!));
        uint callerAddress = Hex(Assert.Single(observation.GetProperty("stack").EnumerateArray(), slot => slot.GetProperty("offset").GetString() == "+0x0").GetProperty("value").GetString()!);
        Assert.Equal(0x569C3Du, callerAddress);
        NativeBattleExecutionStack selected = new(esp, callerAddress, executableHash, captureHash, result.GetProperty("processId").GetInt32(), observation.GetProperty("thread").GetInt32());
        selected.Validate();
        Assert.Equal(captureHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path!))));
        return selected;

        static uint Hex(string value)
        {
            Assert.StartsWith("0x", value);
            return uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    private static void RunQuezacotl(GameWorld world, GameSession session, string? evidence, string id)
    {
        try
        {
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.Normal, NativeSeed: 7));

            world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
            session.SetEventState(world.Events.Capture());
            Publish(evidence, id + "-before-current-gf", session.State);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.NotNull(battle.Presentation?.Graphics);
            Assert.NotNull(battle.NativeMusicProcess!.Commands);
            BattleSnapshot initial = battle.Capture();
            bool requested = initial.ActiveAction is { Ability: BattleAbility.GuardianForce, ActionId: 0 } || battle.QueuedActions.Any(action => action.Ability == BattleAbility.GuardianForce && action.ActionId == 0) || initial.GuardianForceCasts.Length != 0;
            if (!requested)
            {
                for (int callback = 0; callback < 2000 && (!battle.NativeEntryReady || !battle.Combatants.Any(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.GuardianForce) && actor.EquipmentIds.Contains(0))); callback++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                Assert.True(battle.NativeEntryReady);
                BattleCombatant caster = battle.Combatants.First(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.GuardianForce) && actor.EquipmentIds.Contains(0));
                BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive);
                world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.GuardianForce, ActionId: 0));
                session.SetEventState(world.Events.Capture());
                Publish(evidence, id + "-requested-gf", session.State);
            }
            int completed = battle.Events.Count(item => item.Kind == "ActionComplete");
            bool materialized = false;
            bool restored = false;
            bool packets = false;
            bool finished = false;
            for (int callback = 0; callback < 4096; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.Presentation!.Graphics.OriginalEffect is { Binding.Effect: 116 } effect)
                {
                    Assert.NotNull(effect.Binding.NativeQuezacotlResources);
                    materialized = true;
                    packets |= battle.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                    if (!restored && packets && effect.ReturnedDescriptor > 0)
                    {
                        session.SetEventState(world.Events.Capture());
                        Publish(evidence, id + "-mid-gf", session.State);
                        BattleSession clone = battle.ForkForContinuation();
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        clone.Tick();
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                        Assert.Equal(RenderBattle(world), RenderBattle(clone, world.BattleResources));
                        PublishPixels(evidence, id + "-mid-gf", RenderBattle(world));
                        restored = true;
                    }
                    if (effect.ReturnedDescriptor == 0 && battle.Presentation!.Memory.Active == 0)
                    {
                        finished = true;
                        break;
                    }
                }
            }
            Assert.True(materialized);
            Assert.True(packets);
            Assert.True(restored);
            Assert.True(finished);
            for (int callback = 0; callback < 128; callback++)
            {
                bool guardianPending = battle.Capture().GuardianForceCasts.Length != 0 || battle.QueuedActions.Any(action => action.Ability == BattleAbility.GuardianForce && action.ActionId == 0) || battle.ActiveAction is { Ability: BattleAbility.GuardianForce, ActionId: 0 };
                if (!guardianPending && battle.Events.Count(item => item.Kind == "ActionComplete") > completed)
                {
                    break;
                }
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(battle.Events.Count(item => item.Kind == "ActionComplete") > completed);
            Assert.Empty(battle.Capture().GuardianForceCasts);
            Assert.DoesNotContain(battle.QueuedActions, action => action.Ability == BattleAbility.GuardianForce && action.ActionId == 0);
            Assert.False(battle.ActiveAction is { Ability: BattleAbility.GuardianForce, ActionId: 0 });
            session.SetEventState(world.Events.Capture());
            Publish(evidence, id + "-after-gf", session.State);
            PublishPixels(evidence, id + "-after-gf", RenderBattle(world));
        }
        catch (Exception exception)
        {
            session.SetEventState(world.Events.Capture());
            Publish(evidence, id + "-unsupported", session.State);
            if (world.Battle is not null)
            {
                Publish(evidence, id + "-failed-live.battle", world.Battle.Capture());
            }
            Publish(evidence, id + "-failure", new { Error = exception.ToString(), BattleFrame = world.Battle?.Frame, Callback = world.Battle?.Presentation?.Graphics?.Callback });
            throw;
        }
    }

    private static void RunBlizzard(GameWorld world, GameSession session, string? evidence, string id)
    {
        try
        {
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.Normal, NativeSeed: 7));

            // Normal host ticks expose individual frames; diagnostic TurnBased ticks may wait through an entire event.
            world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
            session.SetEventState(world.Events.Capture());
            Publish(evidence, id + "-before-current-cast", session.State);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.NotNull(battle.Presentation?.Graphics);
            Assert.NotNull(battle.NativeMusicProcess!.Commands);
            Assert.Null(battle.Capture().Definition.NativeEffectCatalog[143].BlizzardResources);
            BattleSnapshot initial = battle.Capture();
            bool alreadyRequested = initial.ActiveAction is { Ability: BattleAbility.Magic, ActionId: 4 } || battle.QueuedActions.Any(action => action.Ability == BattleAbility.Magic && action.ActionId == 4);
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
                if (battle.Presentation!.Graphics.OriginalEffect is { Binding.Effect: 144 } effect)
                {
                    Assert.NotNull(effect.Binding.BlizzardResources);
                    materialized = true;
                    packets |= battle.Presentation!.Graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8");
                    if (!restored && effect.ReturnedDescriptor > 0 && packets)
                    {
                        session.SetEventState(world.Events.Capture());
                        Publish(evidence, id + "-mid", session.State);
                        BattleSession clone = battle.ForkForContinuation();
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        clone.Tick();
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                        Assert.Equal(RenderBattle(world), RenderBattle(clone, world.BattleResources));
                        restored = true;
                    }
                    if (effect.ReturnedDescriptor == 0 && battle.Presentation!.Memory.Active == 0)
                    {
                        break;
                    }
                }
            }
            Assert.True(materialized);
            Assert.True(packets);
            Assert.True(restored);
            Assert.Equal(0u, battle.Presentation!.Graphics.OriginalEffect!.ReturnedDescriptor);
            Assert.Single(battle.Events, item => item.NativeModuleSound?.Sound == 300012);
            for (int callback = 0; callback < 64 && battle.ActiveAction is { Ability: BattleAbility.Magic, ActionId: 4 }; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.False(battle.ActiveAction is { Ability: BattleAbility.Magic, ActionId: 4 }, "Native event completion left its managed action pending.");
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
            Publish(evidence, id + "-failure", new { Error = exception.ToString(), BattleFrame = world.Battle?.Frame, Callback = world.Battle?.Presentation?.Graphics?.Callback });
            throw;
        }
    }

    internal static GameWorld RestoreWorld(GameSession session, string directory)
    {
        Assert.Null(session.State.Battle);
        JsonElement events = session.State.EventState!.Value;
        GameWorld world = new(session, directory);
        if (session.State.FieldId != 999)
        {
            world.EnsureField();
            return world;
        }
        byte[] controlledProgram = Program(initializeBattleMode: true);
        bool explicitMode = events.GetProperty("ProgramHash").GetString() == Convert.ToHexString(SHA256.HashData(controlledProgram));
        // The test program has no field-table row from which to load particles.
        world.Events.ReadNativeFieldParticleDefinition = null;
        world.Events.LoadField(999, explicitMode ? controlledProgram : Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        world.Events.Restore(events);
        return world;
    }

    internal static byte[] RenderBattle(GameWorld world)
    {
        return RenderBattle(world.Battle!, world.BattleResources);
    }

    private static byte[] RenderBattle(BattleSession battle, BattleAssets assets)
    {
        return new BattleScenePresentation(assets, battle.Capture().Definition).Render(battle, 320, 224).ToArray();
    }

    internal static void Publish(string? directory, string name, object value)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".json");
        using (FileStream stream = new(path, FileMode.CreateNew))
        {
            JsonSerializer.Serialize(stream, value, value.GetType());
        }
    }

    private static void PublishPixels(string? directory, string name, byte[] pixels)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(pixels[offset + 3], pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        }
        using FileStream stream = new(Path.Combine(directory, name + ".png"), FileMode.CreateNew);
        bitmap.SavePng(stream);
    }

    private static byte[] Program(bool initializeBattleMode = false)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 0x80), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        if (initializeBattleMode)
        {
            // This fresh component declares its saved return mode through the actual full-word writer.
            operations = [.. operations.Take(3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BATTLEMODE, 0), .. operations.Skip(3)];
        }
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
