using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleExecutionStackMigrationTests
{
    [Fact]
    public void Continue_named_Quezacotl_with_the_desktop_presentation_clock_and_saved_battle_mode()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_GF_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_EVIDENCE");
        Assert.SkipUnless(source is not null && File.Exists(source) && !string.IsNullOrWhiteSpace(evidence), "A concrete GF standalone and distinct evidence directory are required.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, directory);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleAtbMode mode = battle.Mode;
        TimeSpan elapsed = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond + 1);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.Zero);
        for (int frame = 0; frame < 2000 && (!battle.NativeEntryReady || !battle.Combatants.Any(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.GuardianForce) && actor.EquipmentIds.Contains(0))); frame++)
        {
            battle.AdvanceNativeGraphicsPresentation(elapsed);
            world.SynchronizeBattlePresentation();
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.Mode == BattleAtbMode.TurnBased)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
        }
        int completed = battle.Events.Count(item => item.Kind == "ActionComplete");
        BattleCombatant caster = battle.Combatants.First(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.GuardianForce) && actor.EquipmentIds.Contains(0));
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive);
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.GuardianForce, ActionId: 0));
        bool materialized = false;
        bool restored = false;
        try
        {
            for (int frame = 0; frame < 4096; frame++)
            {
                battle.AdvanceNativeGraphicsPresentation(elapsed);
                world.SynchronizeBattlePresentation();
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Equal(mode, battle.Mode);
                if (mode == BattleAtbMode.TurnBased && battle.Capture().GuardianForceCasts.Length != 0)
                {
                    world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                }
                if (battle.NativeGraphics!.OriginalEffect is { Binding.Effect: 116, ReturnedDescriptor: > 0 })
                {
                    materialized = true;
                    if (!restored && battle.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        string mid = Path.Combine(evidence!, "desktop-midcast.json");
                        session.Save(mid, compact: true);
                        GameSession independent = new(GameSession.Load(mid));
                        using GameWorld clone = NativeBattleProductionEffectAdmissionTests.RestoreWorld(independent, directory);
                        clone.Battle!.AdvanceNativeGraphicsPresentation(elapsed);
                        clone.SynchronizeBattlePresentation();
                        clone.ApplyBattleInput(new(BattleInputKind.Tick));
                        battle.AdvanceNativeGraphicsPresentation(elapsed);
                        world.SynchronizeBattlePresentation();
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Battle.Capture()));
                        Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(world), NativeBattleProductionEffectAdmissionTests.RenderBattle(clone));
                        restored = true;
                    }
                }
                if (materialized && battle.NativeGraphics.OriginalEffect is { Binding.Effect: 116, ReturnedDescriptor: 0 } && battle.NativeEffectMemory!.Active == 0 && battle.Capture().GuardianForceCasts.Length == 0 && battle.Events.Count(item => item.Kind == "ActionComplete") > completed)
                {
                    Assert.True(restored);
                    session.Save(Path.Combine(evidence!, "desktop-after-summon.json"), compact: true);
                    NativeBattleProductionEffectAdmissionTests.Publish(evidence, "desktop-proof", new { Source = source, Mode = mode, Frame = battle.Frame, Callback = battle.NativeGraphics.Callback, Materialized = materialized, IndependentRestore = restored, ActionCompleted = true });
                    return;
                }
            }
            Assert.Fail("Quezacotl did not complete within the bounded desktop continuation.");
        }
        catch (Exception exception)
        {
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, "desktop-failure", new { Error = exception.ToString(), Frame = battle.Frame, Callback = battle.NativeGraphics!.Callback });
            throw;
        }
    }

    [Fact]
    public void Selected_stack_survives_rotating_compressed_quick_save_and_independent_host_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_STACK_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_EVIDENCE");
        Assert.SkipUnless(source is not null && File.Exists(source) && !string.IsNullOrWhiteSpace(evidence), "An explicitly selected stack standalone and a distinct evidence directory are required.");
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(source!));
        NativeBattleExecutionStack profile = Assert.IsType<NativeBattleExecutionStack>(session.State.NativeBattleMemory?.EffectMemory?.ExecutionStack);
        if (session.State.Battle is { } active)
        {
            Assert.Equal(profile, active.NativeEffectMemory!.ExecutionStack);
        }
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, directory);
        string quickDirectory = Path.Combine(evidence!, "quick-roundtrip-" + Guid.NewGuid().ToString("N"));
        PlayerSaveStore saves = new(source!, quickDirectory);
        saves.Save(0, session);
        string quickPath = saves.GetPath(0);
        Assert.Equal("MFF8SAVE"u8.ToArray(), File.ReadAllBytes(quickPath).Take(8).ToArray());
        GameSession independent = new(saves.Load(0));
        if (independent.State.Battle is { } restoredBattle)
        {
            Assert.Equal(profile, restoredBattle.NativeEffectMemory!.ExecutionStack);
        }
        Assert.Equal(profile, independent.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        using GameWorld clone = NativeBattleProductionEffectAdmissionTests.RestoreWorld(independent, directory);
        if (world.Battle is null)
        {
            Assert.Null(clone.Battle);
            Assert.Equal(JsonSerializer.Serialize(session.PrepareSnapshot()), JsonSerializer.Serialize(independent.PrepareSnapshot()));
            Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), clone.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(JsonSerializer.Serialize(session.PrepareSnapshot()), JsonSerializer.Serialize(independent.PrepareSnapshot()));
            Assert.Equal(profile, session.State.NativeBattleMemory!.EffectMemory!.ExecutionStack);
            saves.Save(0, session);
            Assert.NotEqual(quickPath, saves.GetPath(0));
            Assert.True(File.Exists(quickPath));
            Assert.Equal(profile, saves.Load(0).NativeBattleMemory!.EffectMemory!.ExecutionStack);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, "selected-stack-quick-roundtrip-proof", new { Source = source, QuickPath = quickPath, NextQuickPath = saves.GetPath(0), Profile = profile, Frame = session.State.World?.Frame });
            return;
        }
        Assert.Equal(JsonSerializer.Serialize(world.Battle!.Capture()), JsonSerializer.Serialize(clone.Battle!.Capture()));
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        clone.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(JsonSerializer.Serialize(world.Battle.Capture()), JsonSerializer.Serialize(clone.Battle.Capture()));
        Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(world), NativeBattleProductionEffectAdmissionTests.RenderBattle(clone));
        saves.Save(0, session);
        Assert.NotEqual(quickPath, saves.GetPath(0));
        Assert.True(File.Exists(quickPath));
        Assert.Equal(profile, saves.Load(0).Battle!.NativeEffectMemory!.ExecutionStack);
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, "selected-stack-quick-roundtrip-proof", new { Source = source, QuickPath = quickPath, NextQuickPath = saves.GetPath(0), Profile = profile, Frame = world.Battle.Frame, Callback = world.Battle.NativeGraphics!.Callback });
    }

    [Fact]
    public void Select_a_captured_execution_stack_for_the_named_before_ribbon_checkpoint_and_check_its_next_callback()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_SNAPSHOT");
        string? expectedSourceHash = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_SNAPSHOT_SHA256");
        string? capturePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_CAPTURE");
        string? expectedCaptureHash = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_CAPTURE_SHA256");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_STACK_EVIDENCE");
        Assert.SkipUnless(sourcePath is not null && expectedSourceHash is not null && capturePath is not null && expectedCaptureHash is not null && !string.IsNullOrWhiteSpace(evidence), "Explicit standalone and native capture paths, exact SHA256 values and a distinct evidence folder are required.");
        byte[] sourceBytes = File.ReadAllBytes(sourcePath!);
        byte[] captureBytes = File.ReadAllBytes(capturePath!);
        string sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes));
        string captureHash = Convert.ToHexString(SHA256.HashData(captureBytes));
        Assert.Equal(expectedSourceHash, sourceHash);
        Assert.Equal(expectedCaptureHash, captureHash);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        string executableHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "FF8_EN.exe"))));
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, executableHash);
        using JsonDocument capture = JsonDocument.Parse(captureBytes);
        Assert.Equal(0, capture.RootElement.GetProperty("exitCode").GetInt32());
        JsonElement result = capture.RootElement.GetProperty("result");
        JsonElement observation = Assert.Single(result.GetProperty("breaks").EnumerateArray().SelectMany(point => point.GetProperty("recorded").EnumerateArray()), point => ReadHex(point.GetProperty("address").GetString()!) == 0x4A24B0 && point.TryGetProperty("callerAddress", out JsonElement caller) && ReadHex(caller.GetString()!) == 0x569C3D);
        uint esp = ReadHex(Assert.Single(observation.GetProperty("registers").EnumerateArray(), register => register.GetProperty("name").GetString() == "esp").GetProperty("value").GetString()!);
        uint eip = ReadHex(Assert.Single(observation.GetProperty("registers").EnumerateArray(), register => register.GetProperty("name").GetString() == "eip").GetProperty("value").GetString()!);
        Assert.Equal(0x4A24B0u, eip);
        uint returnAddress = ReadHex(Assert.Single(observation.GetProperty("stack").EnumerateArray(), slot => slot.GetProperty("offset").GetString() == "+0x0").GetProperty("value").GetString()!);
        Assert.Equal(0x569C3Du, returnAddress);
        NativeBattleExecutionStack profile = new(esp, returnAddress, executableHash, captureHash, result.GetProperty("processId").GetInt32(), observation.GetProperty("thread").GetInt32());
        profile.Validate();
        JsonNode original = JsonNode.Parse(sourceBytes)!;
        JsonNode migrated = original.DeepClone();
        JsonObject memory = Assert.IsType<JsonObject>(migrated["Battle"]!["NativeEffectMemory"]);
        Assert.False(memory.ContainsKey("ExecutionStack"));
        memory["ExecutionStack"] = JsonSerializer.SerializeToNode(profile);
        JsonObject retainedMemory = Assert.IsType<JsonObject>(migrated["NativeBattleMemory"]!["EffectMemory"]);
        Assert.False(retainedMemory.ContainsKey("ExecutionStack"));
        retainedMemory["ExecutionStack"] = JsonSerializer.SerializeToNode(profile);
        JsonNode reverted = migrated.DeepClone();
        Assert.True(Assert.IsType<JsonObject>(reverted["Battle"]!["NativeEffectMemory"]).Remove("ExecutionStack"));
        Assert.True(Assert.IsType<JsonObject>(reverted["NativeBattleMemory"]!["EffectMemory"]).Remove("ExecutionStack"));
        Assert.True(JsonNode.DeepEquals(original, reverted));
        string id = "production-stack-profile-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id, migrated);
        string migratedPath = Path.Combine(evidence!, id + ".json");
        GameState saved = GameSession.Load(migratedPath);
        Assert.Equal((byte)4, saved.Battle!.NativeGraphics!.EntryLifecycle);
        Assert.Equal((ushort)116, saved.Battle.NativeGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal(profile, saved.Battle.NativeEffectMemory!.ExecutionStack);
        NativeBattleStackFrame callbackStack = NativeBattleQuezacotlRibbon.ResolveEntryStack6C8850(profile, saved.Battle.NativeGraphics.EntryLifecycle);
        GameSession session = new(saved);
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, directory);
        BattleSnapshot originalBattle = GameSession.Load(sourcePath!).Battle!;
        BattleSession originalSession = BattleSession.Restore(originalBattle);
        Assert.Equal(new BattleScenePresentation(world.BattleResources, originalBattle.Definition, true).Render(originalSession, 320, 224).ToArray(), NativeBattleProductionEffectAdmissionTests.RenderBattle(world));
        GameSession independent = new(GameSession.Load(migratedPath));
        using GameWorld clone = NativeBattleProductionEffectAdmissionTests.RestoreWorld(independent, directory);
        long beforeCallback = world.Battle!.NativeGraphics!.Callback;
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        clone.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.True(world.Battle.NativeGraphics!.Callback > beforeCallback);
        session.SetEventState(world.Events.Capture());
        independent.SetEventState(clone.Events.Capture());
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(independent.State));
        Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(world), NativeBattleProductionEffectAdmissionTests.RenderBattle(clone));
        Assert.Equal(profile, world.Battle.NativeEffectMemory!.ExecutionStack);
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-checked-next", session.State);
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-proof", new
        {
            OriginalPath = Path.GetFullPath(sourcePath!),
            OriginalSha256 = sourceHash,
            CapturePath = Path.GetFullPath(capturePath!),
            CaptureSha256 = captureHash,
            CapturedSessionId = result.GetProperty("id").GetString(),
            MigratedPath = Path.GetFullPath(migratedPath),
            MigratedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(migratedPath))),
            SelectedExecutionStack = profile,
            CallbackEntryEsp = callbackStack.EntryEsp,
            DirectionScratchSlot = callbackStack.AddressAtOffset(-0x6C),
            DirectionScratchValue = callbackStack.AddressAtOffset(-0x38),
            ChangedPaths = new[] { "Battle.NativeEffectMemory.ExecutionStack", "NativeBattleMemory.EffectMemory.ExecutionStack: canonical retained mirror" },
            HistoricalNativeStackRecovered = false,
            Validation = "Explicitly selected captured execution environment before first dependent read; only profile and its mandatory canonical retained mirror added; original and capture unchanged; current factory load; initial pixels unchanged; independent next whole JSON and pixels agree"
        });
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))));
        Assert.Equal(captureHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(capturePath!))));
    }

    private static uint ReadHex(string value)
    {
        Assert.StartsWith("0x", value);
        return uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
