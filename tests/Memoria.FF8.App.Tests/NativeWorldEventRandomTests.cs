using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldEventRandomTests
{
    [Fact]
    public void ActualWorldMotorAgesPackedBankWithSalarySuppressedAndCommitsAtomically()
    {
        var component = CreateNativeDeparture();
        using WorldMapScene world = component.World;
        GameSession session = component.Session;
        Assert.True(session.WorldControls!.SalarySuppressed);
        GameState before = session.State;
        NativeWorldSalaryState prospective = NativeWorldSalaryState.Begin(before).PrepareDrawPointAging(3);
        Assert.Equal(0x967EB0E7u, prospective.EventRandom!.State);
        Assert.Equal((ushort)0, prospective.EventRandom.DrawPoints!.RegenerationCounter180);
        Assert.Same(before, session.State);
        // A transport with no actual callback count cannot publish its prospective RNG.
        WorldState incomplete = before.World! with { Frame = 1, Actions = [new(1, 1, "Wait", 0, 0, 1, 1, before.World.X, before.World.Z, before.World.NativeHeight)] };
        Assert.Throws<InvalidDataException>(() => session.CommitWorldAction(incomplete, prospective));
        Assert.Same(before, session.State);
        Assert.Equal(1u, session.EventRandom!.State);

        world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(0x967EB0E7u, session.EventRandom!.State);
        Assert.Equal((ushort)6, session.EventRandom.DrawPoints!.RegenerationCounter180);
        Assert.Equal(0xC9, session.EventRandom.DrawPoints.Read(116, 1));
        Assert.Equal(0u, session.SeeD!.SalaryMovementCounter);
        Assert.NotSame(before, session.State);

        GameSession independent = new(before);
        using WorldMapScene singleInputs = new(independent, GameDirectory());
        for (int callback = 0; callback < 3; callback++)
        {
            singleInputs.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        }
        Assert.Equal(session.EventRandom, independent.EventRandom);
        Assert.Equal(JsonSerializer.Serialize(session.ExamTimer), JsonSerializer.Serialize(independent.ExamTimer));
        Assert.Equal(JsonSerializer.Serialize(world.State with { Actions = [] }), JsonSerializer.Serialize(singleInputs.State with { Actions = [] }));
    }

    [Fact]
    public void DormantFieldCaptureAndIndependentWorldReloadRetainCurrentSharedRandomOwner()
    {
        var component = CreateNativeDeparture();
        using WorldMapScene world = component.World;
        GameSession session = component.Session;
        EventEngine engine = component.Engine;
        JsonElement beforeWorld = engine.Capture();
        string frozen = LocalVm(beforeWorld);
        session.SetDepartingFieldState(beforeWorld);
        world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(frozen, LocalVm(engine.Capture()));
        Assert.Equal(1u, beforeWorld.GetProperty("EventRandom").GetProperty("State").GetUInt32());
        Assert.Equal(0x967EB0E7u, engine.Capture().GetProperty("EventRandom").GetProperty("State").GetUInt32());
        session.SetEventState(engine.Capture());
        string path = Path.Combine(Path.GetTempPath(), "ff8-native-world-random-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restored = new(GameSession.Load(path));
            Assert.Equal(1u, restored.State.DepartingFieldState!.Value.GetProperty("EventRandom").GetProperty("State").GetUInt32());
            Assert.Equal(0x967EB0E7u, restored.EventRandom!.State);
            EventEngine dormant = new(restored);
            dormant.LoadField(74, component.Program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            dormant.Restore(restored.State.EventState!.Value);
            Assert.Equal(session.EventRandom, restored.EventRandom);
            Assert.Equal(frozen, LocalVm(dormant.Capture()));
            using WorldMapScene other = new(restored, GameDirectory());
            PlayerAction next = new(PlayerActionKind.Move, Horizontal: 1, Frames: 2);
            Assert.Equal(world.Apply(next), other.Apply(next));
            Assert.Equal(session.EventRandom, restored.EventRandom);
            Assert.Equal(JsonSerializer.Serialize(world.State), JsonSerializer.Serialize(other.State));
            Assert.Equal(frozen, LocalVm(dormant.Capture()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (GameSession Session, EventEngine Engine, WorldMapScene World, byte[] Program) CreateNativeDeparture()
    {
        // Declared component JSM executes original-width stores and WORLDMAPJUMP.
        // It is not an original field script or an authentic research route journal.
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0xC9), (Jsm.Opcode.POPM_B, 116), (Jsm.Opcode.PSHN_L, 0x2800), (Jsm.Opcode.POPM_W, 180), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.RET, 0)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int operand) = operations[index];
            int encoded = operand != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | operand & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets assets = new(GameDirectory());
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
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
        session.MarkReplayUnavailable("Declared native world RNG component has no original route history.");
        Assert.NotNull(world);
        return (session, engine, world!, bytes);
    }

    private static string LocalVm(JsonElement snapshot)
    {
        JsonObject local = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        foreach (string owner in new[] { "ExamTimer", "SeeD", "Economy", "SalaryPresentation", "GlobalSoundProcess", "EventRandom" })
        {
            local.Remove(owner);
        }
        return local.ToJsonString();
    }

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        return directory;
    }
}
