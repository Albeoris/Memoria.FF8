using System.Reflection;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.App.Tests;

public sealed class Pandora487LinearScrollTests
{
    [Fact]
    public void PhysicalRowsCarryOnlyProvedCurrentAndPreserveExplicitNonzeroPair()
    {
        SceneEventState scene = new();
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.NormalBackgroundScrollCenter))!.SetValue(scene, new NativeNormalBackgroundScrollCenter(1, 434, 0, 35891, 31, -52));
        Assert.Empty(Capture(scene));
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.NativeFieldLoadVersion))!.SetValue(scene, 1);
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.NativeFieldInitialization))!.SetValue(scene, new NativeFieldInitializationProof(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, 434, new string('A', 64), 20, NativeFieldInitializationStage.Completed));
        scene.BackgroundScrollPositions[2] = new(123, -456, 900, 800, 12, 34, 20, 4, 1, 800, NativeLinearVersion: 1);
        Dictionary<int, NativeBackgroundScrollCurrent> currents = Capture(scene);
        Assert.Equal(new(1, 0, 0), currents[1]);
        Assert.Equal(new(1, 123, -456), currents[2]);
        Assert.Equal(7, currents.Count);
        SceneEventState unknown = new();
        Assert.Empty(Capture(unknown));
        unknown.BackgroundScrollPositions[3] = new(null, null, 123, 456, null, null, null, 3, 0);
        Assert.Empty(Capture(unknown));
    }

    [Fact]
    public void NativeDuration800KeepsTruncationAndInclusiveEndpoint()
    {
        Assert.Equal((short)0, NativeLinearScrollPhase.Interpolate(0, -40, 800, 0));
        Assert.Equal((short)0, NativeLinearScrollPhase.Interpolate(0, -40, 800, 19));
        Assert.Equal((short)-1, NativeLinearScrollPhase.Interpolate(0, -40, 800, 20));
        Assert.Equal((short)-39, NativeLinearScrollPhase.Interpolate(0, -40, 800, 799));
        Assert.Equal((short)-40, NativeLinearScrollPhase.Interpolate(0, -40, 800, 800));
        Assert.True(NativeLinearScrollPhase.IsValid(new(0, -40, 0, -40, 0, 0, 800, 4, 2, 800, NativeLinearVersion: 1)));
        Assert.False(NativeLinearScrollPhase.IsValid(new(0, -39, 0, -40, 0, 0, 800, 4, 2, 800, NativeLinearVersion: 1)));
    }

    [Fact]
    public void ExplicitStartWritesPhysicalCurrentDuringInitializationWithoutRunningDriver()
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65548), (Jsm.Opcode.PSHN_L, -65560), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.LSCROLL3, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = new byte[16 + operations.Length * 4];
        program[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int operand) = operations[index];
            int encoded = operand != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | operand & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(16 + index * 4), encoded);
        }
        EventEngine engine = new(new GameSession(new GameState())) { CanSetLinearBackgroundScroll = _ => true };
        engine.LoadField(600, program, []);
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.NativeBackgroundScrollCurrents))!.SetValue(engine.Scene, new Dictionary<int, NativeBackgroundScrollCurrent> { [1] = new(1, 123, 456) });
        engine.Step();
        BackgroundScrollPosition row = engine.Scene.BackgroundScrollPositions[1];
        Assert.Equal(((short)12, (short)-24, (short)4, (byte)0), (row.X, row.Y, row.Duration, row.Status));
        Assert.Equal(new(1, 12, -24), engine.Scene.NativeBackgroundScrollCurrents![1]);
        Assert.Null(row.Counter);
    }

    [Fact]
    public void Actual487FirstLinearScrollUsesDeparting434CurrentAndRestoresInclusivePhase()
    {
        string root = AppContext.BaseDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.Pandora487LinearScrollTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.Null(world.Events.Scene.NormalBackgroundScrollCenter);
        Assert.Equal(new(1, 0, 0), world.Events.Scene.NativeBackgroundScrollCurrents![1]);
        world.Apply(new(PlayerActionKind.Wait, Frames: 2));
        Assert.Null(world.Events.LastFault);
        BackgroundScrollPosition first = world.Events.Scene.BackgroundScrollPositions[1];
        Assert.Equal(((short)0, (short)0, (short)800, (short)1, (byte)1), (first.PreviousX, first.PreviousY, first.Duration, first.Counter, first.Status));
        Assert.Equal(((short)0, (short)-40), (first.TargetX, first.TargetY));
        session.SetEventState(world.Events.Capture());
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-Pandora487-scroll-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession coldSession = new(GameSession.Load(path));
            using GameWorld cold = new(coldSession, TestResources.GameDirectory);
            world.Apply(new(PlayerActionKind.Wait));
            cold.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
            Assert.Equal((short)2, cold.Events.Scene.BackgroundScrollPositions[1].Counter);
            JsonNode malformed = JsonNode.Parse(cold.Events.Capture().GetRawText())!;
            malformed["Scene"]!["NativeBackgroundScrollCurrents"]!["1"]!["X"] = 123;
            using JsonDocument document = JsonDocument.Parse(malformed.ToJsonString());
            Assert.Throws<InvalidDataException>(() => cold.Events.Restore(document.RootElement));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Dictionary<int, NativeBackgroundScrollCurrent> Capture(SceneEventState scene)
    {
        return (Dictionary<int, NativeBackgroundScrollCurrent>)typeof(EventEngine).GetMethod("CaptureNativeBackgroundScrollCurrents", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [scene, false])!;
    }
}
