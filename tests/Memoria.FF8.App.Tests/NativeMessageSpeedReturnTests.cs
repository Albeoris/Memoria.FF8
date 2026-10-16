using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMessageSpeedReturnTests
{
    [Fact]
    public void PeekedMessageWordsBecomeActualReturnBankAndLowerPhysicalOperands()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 111), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] upper = [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0x11800), (Jsm.Opcode.SETMESSPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.SETMESSPEED, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = Program(init, main, upper);
        EventEngine live = Engine(content);
        live.Scene.MessageChannelTiming[1] = new(77, 55);
        while (!live.Scene.MessageChannelTiming.ContainsKey(0) && live.Frame < 10)
        {
            live.Step();
        }
        Assert.Equal(new NativeMessageChannelTiming(6144, 0), live.Scene.MessageChannelTiming[1]);
        Assert.Equal(new NativeMessageChannelTiming(65535, 0), live.Scene.MessageChannelTiming[0]);
        Assert.Equal(12, live.Actors[0].NativeOperandMemory!.LiveDepth);
        JsonElement before = live.Capture();
        Assert.Equal([-1, 0, 0x11800, 1], Fiber(before, 2).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        EventEngine restored = Engine(content);
        restored.Restore(before);
        for (int callback = 0; callback < 7; callback++)
        {
            live.Step();
            restored.Step();
            Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
        }
        JsonElement after = live.Capture();
        JsonElement lower = Fiber(after, 1);
        Assert.Equal([0, 0, 0, 111, 99], lower.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        Assert.Equal(4, live.Actors[0].NativeOperandMemory!.LiveDepth);
        JsonElement results = lower.GetProperty("Results");
        Assert.Equal(-1, results.GetProperty("0").GetInt32());
        Assert.Equal(0, results.GetProperty("1").GetInt32());
        Assert.Equal(0x11800, results.GetProperty("2").GetInt32());
        Assert.Equal(1, results.GetProperty("3").GetInt32());
        EventEngine afterRestore = Engine(content);
        afterRestore.Restore(after);
        live.Step();
        afterRestore.Step();
        Assert.True(JsonElement.DeepEquals(live.Capture(), afterRestore.Capture()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(65537)]
    public void UnrepresentedFullDwordChannelRefusesBeforeConsumingOperands(int channel)
    {
        EventEngine engine = Engine(Program([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, channel), (Jsm.Opcode.PSHN_L, 6144), (Jsm.Opcode.SETMESSPEED, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]));
        engine.Step();
        Assert.Throws<EventScriptException>(() => engine.Step());
        Assert.Equal("SETMESSPEED", engine.LastFault!.Opcode);
        Assert.Equal([6144, channel], Fiber(engine.Capture(), 1).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        Assert.Empty(engine.Scene.MessageChannelTiming);
        Assert.Equal(1, engine.Actors[0].NativeOperandMemory!.LiveDepth);
    }

    [Fact]
    public void Original828PeeksBothChannelsAndRestoresTheirRetainedWords()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeMessageSpeedReturnTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(new NativeMessageChannelTiming(6144, 0), world.Events.Scene.MessageChannelTiming[0]);
        Assert.Equal(new NativeMessageChannelTiming(6144, 0), world.Events.Scene.MessageChannelTiming[1]);
        JsonElement before = world.Events.Capture();
        using GameWorld restored = new(new GameSession(session.State with { EventState = before }), TestResources.GameDirectory);
        world.Apply(new(PlayerActionKind.Wait));
        restored.Apply(new(PlayerActionKind.Wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    private static EventEngine Engine(byte[] content)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        engine.LoadField(828, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static JsonElement Fiber(JsonElement snapshot, int script)
    {
        return snapshot.GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == script);
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
        int operationsOffset = 10 + 2 * (scripts.Length + 1);
        byte[] bytes = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)(scripts.Length - 1));
        int operationIndex = 0;
        for (int script = 0; script < scripts.Length; script++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + script * 2), (ushort)operationIndex);
            foreach ((Jsm.Opcode opcode, int parameter) in scripts[script])
            {
                int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.POPI_L or Jsm.Opcode.REQ ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + operationIndex++ * 4), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + scripts.Length * 2), (ushort)operationIndex);
        return bytes;
    }

    private static string RootFile(string relative)
    {
        return TestResources.FixtureFile(relative);
    }
}
