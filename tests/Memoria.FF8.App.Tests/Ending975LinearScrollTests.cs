using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ending975LinearScrollTests
{
    [Theory]
    [InlineData(4, true)]
    [InlineData(65540, false)]
    public void LayerUsesFullDwordAndCoordinatesAndDurationUseLowSignedWords(int layer, bool supported)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 66175), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 66175), (Jsm.Opcode.LSCROLL3, 0), (Jsm.Opcode.RET, 0)];
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
        EventEngine engine = new(new GameSession(new GameState())) { CanSetLinearBackgroundScroll = query => query == new NativeLinearScrollQuery(4, 0, 0, 639, 0, 639) };
        engine.LoadField(600, program, []);
        if (!supported)
        {
            Assert.Throws<EventScriptException>(() => engine.Step());
            Assert.Empty(engine.Scene.BackgroundScrollPositions);
            JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
            Assert.Equal(7, fiber.GetProperty("Position").GetInt32());
            Assert.Equal(new[] { 66175, -65536, 66175, -65536, 65536, layer }, fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()));
            return;
        }
        engine.Step();
        BackgroundScrollPosition issued = engine.Scene.BackgroundScrollPositions[4];
        Assert.Equal(((short)0, (short)0, (short)639, (short)0, (short)639, (byte)4, (byte)0), (issued.X, issued.Y, issued.TargetX, issued.TargetY, issued.Duration, issued.Mode, issued.Status));
        Assert.Null(issued.Counter);
        Assert.Null(issued.PreviousX);
    }

    [Fact]
    public void Original975RepeatsWrappedLinearScrollAndRestores()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 24));
        Assert.Null(world.Events.LastFault);
        Assert.Equal(1284, world.Events.Frame);
        Assert.Equal(1, world.Events.Scene.CameraIndex);
        BackgroundScrollPosition restarted = world.Events.Scene.BackgroundScrollPositions[4];
        Assert.Equal(((short)0, (short)0, (short)639, (short)639, (short)1, (byte)1), (restarted.X, restarted.PreviousX, restarted.TargetX, restarted.Duration, restarted.Counter, restarted.Status));
        Assert.Equal((short)0, world.Events.Scene.BackgroundScrollPositions[0].X);
        Assert.True(world.Events.CanRestoreLinearBackgroundScroll!(world.Events.Scene));

        BackgroundScrollFrame frame = Assert.IsType<BackgroundScrollFrame>(NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors));
        Assert.True(FieldNativeNormalBackgroundWrap.Validate(world.Field!, 1, frame.ActorOffset, frame.LayerOffsets, frame.NativeWrap!));
        BackgroundScrollPosition dormant = world.Events.Scene.BackgroundScrollPositions[0];
        world.Events.Scene.BackgroundScrollPositions[0] = dormant with { X = 1000, TargetX = 1000 };
        BackgroundScrollFrame changed = Assert.IsType<BackgroundScrollFrame>(NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors));
        Assert.Equal(frame.ActorOffset, changed.ActorOffset);
        Assert.Equal(frame.NativeWrap!.Layers, changed.NativeWrap!.Layers);
        NativeLinearScrollQuery query = new(4, 0, 0, 639, 0, 639);
        world.Events.Scene.BackgroundScrollPositions[0] = dormant with { X = 1000 };
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanBegin(world.Field!, query, world.Events.Scene));
        world.Events.Scene.BackgroundScrollPositions[0] = dormant;
        world.Events.Scene.BackgroundScrollPositions[1] = dormant;
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanBegin(world.Field!, query, world.Events.Scene));
        world.Events.Scene.BackgroundScrollPositions.Remove(1);

        session.SetEventState(world.Events.Capture());
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-Ending975-scroll-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession coldSession = new(GameSession.Load(path));
            using GameWorld cold = new(coldSession, TestResources.GameDirectory);
            cold.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
            world.Apply(new(PlayerActionKind.Wait, Frames: 36));
            cold.Apply(new(PlayerActionKind.Wait, Frames: 36));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
            Assert.Null(cold.Events.LastFault);
            Assert.True(NativeLinearScrollPhase.IsValid(cold.Events.Scene.BackgroundScrollPositions[4]));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
