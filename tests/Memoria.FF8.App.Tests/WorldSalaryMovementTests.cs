using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldSalaryMovementTests
{
    [Fact]
    public void Car_accepted_raw_velocity_supplies_units_with_current_terrain_and_saved_vehicle_state()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetSeeD(session.SeeD!.WithSalaryEnabled(true));
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), Variables = new() { [256] = 40, [265] = 128 } });
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
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetSeeD(session.SeeD!.WithSalaryEnabled(true));
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
        Assert.Equal(JsonSerializer.Serialize(scene.State with { Actions = [] }), JsonSerializer.Serialize(other.State with { Actions = [] }));
        GameState serialized = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(serialized);
        using WorldMapScene resumed = new(restored, GameDirectory());
        PlayerAction next = new(PlayerActionKind.Move, Vertical: -1, Frames: 2);
        Assert.Equal(scene.Apply(next), resumed.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
        Assert.Equal(scene.Render(320, 224), resumed.Render(320, 224));
    }

    [Fact]
    public void A_later_payment_boundary_refuses_the_entire_world_batch()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetSeeD(session.SeeD!.WithSalaryEnabled(true) with { SalaryMovementCounter = 0x6000 - 7 });
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene scene = new(session, GameDirectory());
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3)));
        Assert.Same(before, session.State);
        Assert.Equal(0, scene.State.Frame);
        Assert.Empty(scene.State.Actions);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void Executed_native_departure_freezes_the_vm_while_global_salary_and_clock_survive_reload()
    {
        // A declared component program executes the real native handler. It is
        // not an original field script or an authentic research route journal.
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
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session, session.State.Variables);
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
            return captured.ToJsonString();
        }
        string frozen = LocalVm(engine);
        current.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(9u, session.SeeD!.SalaryMovementCounter);
        Assert.Equal(frozen, LocalVm(engine));
        session.SetEventState(engine.Capture());
        session.MarkReplayUnavailable("Declared native departure component has no original field route history.");
        string path = Path.Combine(Path.GetTempPath(), "ff8-native-world-salary-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession independent = new(GameSession.Load(path));
            EventEngine restored = new(independent, independent.State.Variables);
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

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        return directory;
    }
}
