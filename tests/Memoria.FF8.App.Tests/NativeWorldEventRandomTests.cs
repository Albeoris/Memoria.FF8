using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldEventRandomTests
{
    [Fact]
    public void OrdinaryCallbacksRunOffEntrancesAfterDelayAndReadStagedWritesAcrossBatch()
    {
        var component = CreateNativeDeparture();
        using WorldMapScene world = component.World;
        GameSession session = component.Session;
        WorldAssets assets = InjectWorldEvents(world,
            [(0xFF01, 0), (0xFF27, 0), (0xFF2D, 0), (0xFF04, 0), (0xFF28, 0x0100), (0xFF2E, 0x0100), (0xFF05, 0)],
            [(0xFF01, 0), (0xFF27, 0x0100), (0xFF2D, 0x0100), (0xFF04, 0), (0xFF2E, 0x0201), (0xFF05, 0)]);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(world.State.ReplacementMask);
        Assert.All(world.State.NativeFootRuntime!.GroundCache!.Alternatives, alternative => Assert.Equal(0, assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).TextureFlags & 8));
        world.Apply(new(PlayerActionKind.Wait, Frames: 23));
        Assert.Equal((byte)0, session.State.SavedWorld!.WorldEventVariable124);
        Assert.Equal((byte)0, session.State.SavedWorld.WorldEventVariable125);
        world.Apply(new(PlayerActionKind.Wait, Frames: 2));
        Assert.Equal(25, world.State.Frame);
        Assert.True(session.State.SavedWorld.WorldEventFlags116!.Matches(0x0100));
        Assert.Equal((byte)1, session.State.SavedWorld.WorldEventVariable124);
        Assert.Equal((byte)2, session.State.SavedWorld.WorldEventVariable125);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalAfterAnExecutedWriteRollsBackTheWholeWorldAction(bool unknown)
    {
        var component = CreateNativeDeparture();
        using WorldMapScene world = component.World;
        GameSession session = component.Session;
        session.SetSavedWorldState(session.State.SavedWorld! with { WorldEventVariable125 = null });
        (ushort Opcode, ushort Operand)[] tail = unknown ? [(0xFF0A, 0), (0xFF2D, 0x0001), (0xFF0B, 0)] : [(0xFF26, 0)];
        InjectWorldEvents(world, [(0xFF01, 0), (0xFF04, 0), (0xFF28, 0x0100), (0xFF2E, 0x0100), .. tail, (0xFF05, 0)]);
        GameState before = session.State;
        string worldBefore = JsonSerializer.Serialize(world.State);
        Assert.Throws<NotSupportedException>(() => world.Apply(new(PlayerActionKind.Wait, Frames: 24)));
        Assert.Same(before, session.State);
        Assert.Equal(worldBefore, JsonSerializer.Serialize(world.State));
        Assert.Equal((byte)0, before.SavedWorld!.WorldEventVariable124);
        Assert.Null(before.SavedWorld.WorldEventVariable125);
        Assert.True(before.SavedWorld.WorldEventFlags116!.Matches(0));
    }

    private static WorldAssets InjectWorldEvents(WorldMapScene world, params (ushort Opcode, ushort Operand)[][] clauses)
    {
        // Declared component WMSET, deliberately injected without changing installed gameplay resources.
        int position = (clauses.Length + 1) * 4;
        byte[] program = new byte[position + clauses.Sum(clause => clause.Length * 4)];
        for (int index = 0; index < clauses.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(index * 4), position);
            foreach ((ushort opcode, ushort operand) in clauses[index])
            {
                BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(position), opcode);
                BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(position + 2), operand);
                position += 4;
            }
        }
        WorldAssets assets = (WorldAssets)typeof(WorldMapScene).GetField("assets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        WorldFieldEntryReader reader = WorldFieldEntryReader.Read(program, new byte[24]).ReadWorldEvents(program);
        typeof(WorldAssets).GetField("worldEvents", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(assets, reader);
        return assets;
    }

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
        WorldState incomplete = before.World! with { Frame = 1 };
        Assert.Throws<InvalidDataException>(() => session.CommitWorldAction(incomplete, prospective));
        Assert.Same(before, session.State);
        Assert.Equal(1u, session.EventRandom!.State);

        world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(0x967EB0E7u, session.EventRandom!.State);
        Assert.Equal((ushort)0, session.EventRandom.DrawPoints!.RegenerationCounter180);
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
        Assert.Equal(JsonSerializer.Serialize(world.State), JsonSerializer.Serialize(singleInputs.State));
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
        // It is not an original field script or an authentic research route snapshot.
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 135), (Jsm.Opcode.POPM_W, 256), (Jsm.Opcode.PSHN_L, 0xC9), (Jsm.Opcode.POPM_B, 116), (Jsm.Opcode.PSHN_L, 0x2800), (Jsm.Opcode.POPM_W, 180), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.RET, 0)];
        operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), .. operations];
        operations[2] = (Jsm.Opcode.LBL, 1);
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)operations.Length);
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
        for (int callback = 0; callback < 4 && world is null; callback++)
        {
            engine.Step();
        }
        Assert.NotNull(world);
        return (session, engine, world!, bytes);
    }

    private static string LocalVm(JsonElement snapshot)
    {
        JsonObject local = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        foreach (string owner in new[] { "ExamTimer", "SeeD", "Economy", "SalaryPresentation", "GlobalSoundProcess", "EventRandom", "VibrationProcess", "Playtime" })
        {
            local.Remove(owner);
        }
        return local.ToJsonString();
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        return directory;
    }
}
