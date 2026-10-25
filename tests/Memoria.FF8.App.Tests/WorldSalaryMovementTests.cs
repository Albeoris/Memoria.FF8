using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldSalaryMovementTests
{
    [Fact]
    public void Car_accepted_raw_velocity_supplies_units_with_current_terrain_and_saved_vehicle_state()
    {
        GameSession session = CreateNativeSession();
        session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), session.SeeD!.WithSalaryUiEnabled(true));
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int>() { [256] = 40, [265] = 128 }) });
        using WorldMapScene scene = new(session, GameDirectory());
        session.SetWorldState(scene.State with { NativeCarRuntime = scene.State.NativeCarRuntime! with { Speed = 24 } });
        GameState initial = session.State;
        scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal(20, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(2u, session.SeeD!.SalaryMovementCounter);
        GameSession restored = new(initial);
        using WorldMapScene other = new(restored, GameDirectory());
        other.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
    }

    [Fact]
    public void Raw_accepted_velocity_counts_before_ground_height_changes_and_matches_restored_batches()
    {
        GameSession session = CreateNativeSession();
        session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), session.SeeD!.WithSalaryUiEnabled(true));
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene scene = new(session, GameDirectory());
        session.SetWorldState(scene.State with { CameraYaw = 0, BodyYaw = 1024 });
        GameState initial = session.State;
        scene.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.NotEqual(initial.World!.NativeHeight, scene.State.NativeHeight);
        Assert.Equal(0u, session.SeeD!.SalaryMovementCounter);
        GameState before = session.State;
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(9u, session.SeeD!.SalaryMovementCounter);
        GameSession independent = new(before);
        using WorldMapScene other = new(independent, GameDirectory());
        for (int callback = 0; callback < 3; callback++)
        {
            other.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        }
        Assert.Equal(session.SeeD, independent.SeeD);
        Assert.Equal(JsonSerializer.Serialize(scene.State), JsonSerializer.Serialize(other.State));
        GameState serialized = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(serialized);
        using WorldMapScene resumed = new(restored, GameDirectory());
        PlayerAction next = new(PlayerActionKind.Move, Vertical: -1, Frames: 2);
        Assert.Equal(scene.Apply(next), resumed.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
        Assert.Equal(scene.Render(320, 224), resumed.Render(320, 224));
    }

    [Fact]
    public void A_later_payment_without_its_output_producer_refuses_the_entire_world_batch()
    {
        GameSession session = CreateNativeSession();
        session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), session.SeeD!.WithSalaryUiEnabled(true) with { SalaryMovementCounter = 0x6000 - 7 });
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene scene = new(session, GameDirectory());
        GameState before = session.State;
        NotSupportedException refusal = Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)));
        Assert.Contains("UI and global sound producer", refusal.Message);
        Assert.Same(before, session.State);
        Assert.Equal(0, scene.State.Frame);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void One_world_action_pays_then_uses_the_new_owners_on_later_callbacks_and_matches_single_inputs()
    {
        GameSession session = CreateNativeSession();
        session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true).WithSalaryDisplayEnabled(true), session.SeeD!.WithSalaryUiEnabled(true) with { RankPoints = 650, SalaryMovementCounter = 0x6000 - 3 });
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        EventEngine output = CreateSalaryEngine(session);
        using WorldMapScene scene = new(session, GameDirectory()) { PrepareSalaryEffects = output.PrepareSalaryEffects };
        GameState before = session.State;
        int publications = 0;
        scene.PublishSalaryEffects = effects =>
        {
            Assert.NotSame(before, session.State);
            Assert.Equal(3, session.State.World!.Frame);
            Assert.Equal(6u, session.SeeD!.SalaryMovementCounter);
            publications++;
            output.PublishSalaryEffects(effects);
        };
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(1, publications);
        Assert.Equal((ushort)640, session.SeeD!.RankPoints);
        Assert.Equal((ushort)650, session.SeeD.PreviousRank186);
        Assert.Equal(6u, session.SeeD.SalaryMovementCounter);
        Assert.Equal((ushort)147, session.SeeD.SalaryDisplayCountdown184);
        Assert.Equal(9000u, session.State.Economy!.Gil);
        Assert.Equal(3000u, session.State.Economy.PastGil);
        Assert.Equal(0u, session.State.Economy.EventBankGil72);
        Assert.Equal((short)1792, session.SalaryPresentation!.Scale9A);
        Assert.Equal(3, output.Scene.GlobalSoundRequests.Count);

        var independent = new GameSession(before);
        EventEngine otherOutput = CreateSalaryEngine(independent);
        using WorldMapScene other = new(independent, GameDirectory()) { PrepareSalaryEffects = otherOutput.PrepareSalaryEffects, PublishSalaryEffects = otherOutput.PublishSalaryEffects };
        for (int callback = 0; callback < 3; callback++)
        {
            other.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        }
        Assert.Equal(session.SeeD, independent.SeeD);
        Assert.Equal(JsonSerializer.Serialize(session.State.Economy), JsonSerializer.Serialize(independent.State.Economy));
        Assert.Equal(JsonSerializer.Serialize(session.SalaryPresentation), JsonSerializer.Serialize(independent.SalaryPresentation));
        Assert.Equal(JsonSerializer.Serialize(session.GlobalSoundProcess), JsonSerializer.Serialize(independent.GlobalSoundProcess));
        Assert.Equal(JsonSerializer.Serialize(scene.State), JsonSerializer.Serialize(other.State));
    }

    [Fact]
    public void Declared_world_callback_batch_commits_two_payments_and_refuses_an_incomplete_transport()
    {
        // This explicit callback-scalar component does not claim a motor path or
        // authentic journey. It exercises multiple native payouts in one transport.
        GameSession session = CreateNativeSession();
        session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true).WithSalaryDisplayEnabled(true), session.SeeD!.WithSalaryUiEnabled(true) with { RankPoints = 650, SalaryMovementCounter = 0x6000 - 1 });
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        EventEngine output = CreateSalaryEngine(session);
        using WorldMapScene scene = new(session, GameDirectory());
        GameState before = session.State;
        NativeWorldSalaryState salary = NativeWorldSalaryState.Begin(before);
        int payments = 0;
        for (int callback = 0; callback < 6; callback++)
        {
            NativeSalaryResult result = GameSession.SampleWorldSalary(before, salary.SeeD, salary.Economy, 5792)!;
            NativeSalaryEffects effects = output.PrepareSalaryEffects(result, salary.Presentation, salary.SoundProcess);
            payments += result.Payment is null ? 0 : 1;
            salary = salary.Advance(result, effects.Presentation!.Advance(2), effects.SoundProcess, 5792, 2);
            Assert.Same(before, session.State);
        }
        WorldState final = before.World! with { Frame = 6 };
        Assert.Throws<InvalidDataException>(() => session.CommitWorldAction(final, NativeWorldSalaryState.Begin(before)));
        Assert.Same(before, session.State);
        Assert.Empty(session.State.PendingOriginalAchievementActions);
        session.CommitWorldAction(final, salary);
        Assert.Equal(OriginalAchievementActionKind.FirstSalary, Assert.Single(session.State.PendingOriginalAchievementActions).Kind);
        Assert.Equal(2, payments);
        Assert.Equal((ushort)630, session.SeeD!.RankPoints);
        Assert.Equal((ushort)640, session.SeeD.PreviousRank186);
        Assert.Equal(0u, session.SeeD.SalaryMovementCounter);
        Assert.Equal((ushort)149, session.SeeD.SalaryDisplayCountdown184);
        Assert.Equal(13000u, session.State.Economy!.Gil);
        Assert.Equal((byte)1, session.GlobalSoundProcess!.Rotation);
        Assert.Equal((short)768, session.SalaryPresentation!.Scale9A);
    }

    [Fact]
    public void Executed_native_departure_freezes_the_vm_while_global_salary_and_clock_survive_reload()
    {
        // A declared component program executes the real native handler. It is
        // not an original field script or an authentic research route snapshot.
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SARALYON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.RET, 0)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int operand) = operations[index];
            int encoded = opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | operand & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        GameSession session = CreateNativeSession();
        EventEngine engine = new(session);
        WorldMapScene? world = null;
        engine.PrepareWorldTransition = request =>
        {
            PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(session.State, GameDirectory(), request.Transition);
            return () =>
            {
                session.CommitPreparedWorldEntry(request.Transition, prepared.InitialState, request.ControlsAtDeparture, prepared.ValidateAdoption, prepared.InitialSavedWorld);
                world = prepared.Adopt(session);
                prepared.Dispose();
            };
        };
        engine.LoadField(74, bytes, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        using WorldMapScene current = world!;
        Assert.True(engine.Capture().GetProperty("TransitionRequested").GetBoolean());
        string LocalVm(EventEngine vm)
        {
            var captured = System.Text.Json.Nodes.JsonNode.Parse(vm.Capture().GetRawText())!.AsObject();
            captured.Remove("ExamTimer");
            captured.Remove("SeeD");
            captured.Remove("Economy");
            captured.Remove("SalaryPresentation");
            captured.Remove("GlobalSoundProcess");
            return captured.ToJsonString();
        }
        string frozen = LocalVm(engine);
        current.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(9u, session.SeeD!.SalaryMovementCounter);
        Assert.Equal(frozen, LocalVm(engine));
        session.SetEventState(engine.Capture());
        string path = Path.Combine(Path.GetTempPath(), "ff8-native-world-salary-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession independent = new(GameSession.Load(path));
            EventEngine restored = new(independent);
            restored.LoadField(74, bytes, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            restored.Restore(independent.State.EventState!.Value);
            using WorldMapScene other = new(independent, GameDirectory());
            Assert.Equal(frozen, LocalVm(restored));
            PlayerAction next = new(PlayerActionKind.Move, Vertical: -1, Frames: 2);
            Assert.Equal(current.Apply(next), other.Apply(next));
            Assert.Equal(session.SeeD, independent.SeeD);
            Assert.Equal(JsonSerializer.Serialize(session.ExamTimer), JsonSerializer.Serialize(independent.ExamTimer));
            Assert.Equal(JsonSerializer.Serialize(current.State), JsonSerializer.Serialize(other.State));
            Assert.Equal(current.Render(320, 224), other.Render(320, 224));
            Assert.Equal(frozen, LocalVm(restored));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static GameSession CreateNativeSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets assets = new(GameDirectory());
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        return session;
    }

    private static EventEngine CreateSalaryEngine(GameSession session)
    {
        // Declared empty field component supplies the verified native output handler;
        // it does not execute an original route or supply any saved process defaults.
        var engine = new EventEngine(session)
        {
            ResolveGlobalSoundResource = sample =>
            {
                GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(GameDirectory(), sample);
                return new GlobalSoundResource(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, null));
            }
        };
        byte[] bytes = new byte[24];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), (int)Jsm.Opcode.LBL);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), (int)Jsm.Opcode.RET);
        engine.LoadField(600, bytes, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        return directory;
    }
}
