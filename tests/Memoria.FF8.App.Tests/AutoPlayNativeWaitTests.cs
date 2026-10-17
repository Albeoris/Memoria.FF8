using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNativeWaitTests
{
    [Fact]
    public void OrdinaryPlayableWaitRemainsOneCallbackAndOptInMatchesSeparateCallbacks()
    {
        EventEngine batched = Create([(Jsm.Opcode.HALT, 0)]);
        EventEngine separate = Create([(Jsm.Opcode.HALT, 0)]);
        Assert.Equal(1, batched.Apply(new(PlayerActionKind.Wait, Frames: 7)).FramesAdvanced);
        Assert.Equal(1, separate.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);

        PlayerActionResult result = batched.Apply(new(PlayerActionKind.Wait, Frames: 7, WaitWhilePlayable: true));
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(1, separate.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);
        }

        Assert.Equal(7, result.FramesAdvanced);
        Assert.Equal(PlayerTriggerKind.Playable, result.Trigger.Kind);
        Assert.False(result.NativeInputObserved);
        Assert.True(JsonElement.DeepEquals(batched.Capture(), separate.Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayableBatchStopsOnTheFirstControlOrDialogueOwnershipChange(bool dialogue)
    {
        List<(Jsm.Opcode, int)> commands = [(Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0)];
        if (dialogue)
        {
            commands.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.AMESW, 0)]);
        }
        else
        {
            commands.Add((Jsm.Opcode.UCOFF, 0));
        }
        commands.Add((Jsm.Opcode.HALT, 0));
        EventEngine engine = Create(commands.ToArray());
        long frame = engine.Frame;
        long boundary = DefaultWaitUntil(engine);

        PlayerActionResult result = engine.Apply(new(PlayerActionKind.Wait, Frames: 30, WaitWhilePlayable: true));

        Assert.Equal(boundary - frame, result.FramesAdvanced);
        Assert.Equal(boundary, engine.Frame);
        Assert.Equal(dialogue ? PlayerTriggerKind.Dialogue : PlayerTriggerKind.Waiting, result.Trigger.Kind);
        Assert.False(result.NativeInputObserved);
    }

    [Fact]
    public void OptInStartingWithoutControlStopsAtItsFirstPlayableCallback()
    {
        EventEngine engine = Create([(Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.HALT, 0)], requirePlayable: false);
        Assert.Equal(PlayerTriggerKind.Waiting, engine.NextTrigger.Kind);
        long frame = engine.Frame;
        long boundary = DefaultWaitUntil(engine);

        PlayerActionResult result = engine.Apply(new(PlayerActionKind.Wait, Frames: 30, WaitWhilePlayable: true));

        Assert.Equal(boundary - frame, result.FramesAdvanced);
        Assert.Equal(PlayerTriggerKind.Playable, result.Trigger.Kind);
        Assert.False(result.NativeInputObserved);
    }

    [Theory]
    [InlineData(Jsm.Opcode.KEYSCAN)]
    [InlineData(Jsm.Opcode.KEYSCAN2)]
    [InlineData(Jsm.Opcode.KEYON)]
    public void ARealKeyQueryEndsTheBatchBeforeAnotherCallback(Jsm.Opcode query)
    {
        EventEngine engine = Create([(Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 64), (query, 0), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0)]);
        long frame = engine.Frame;
        long boundary = DefaultWaitUntil(engine);
        List<ScriptCommand> observed = [];
        engine.CommandExecuted += observed.Add;

        PlayerActionResult result = engine.Apply(new(PlayerActionKind.Wait, Frames: 100, WaitWhilePlayable: true));

        Assert.Equal(boundary - frame, result.FramesAdvanced);
        Assert.Equal(boundary, engine.Frame);
        Assert.Equal(PlayerTriggerKind.Playable, result.Trigger.Kind);
        Assert.True(result.NativeInputObserved);
        Assert.Single(observed, command => command.Name == query.ToString());
        Assert.NotNull(engine.DiscoverScriptedInput());
        Assert.False(engine.Apply(new(PlayerActionKind.Wait)).NativeInputObserved);
    }

    [Fact]
    public void ActualResultsTimerContinuesNaturallyAndStopsWhenTheSceneTakesControl()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayNativeWaitTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(PlayerTriggerKind.Playable, world.NextTrigger.Kind);
        Assert.All(session.ExamTimer!.Alternatives, alternative => Assert.Equal(15, alternative.Remaining));
        long frame = world.Events.Frame;

        PlayerActionResult result = world.Apply(new(PlayerActionKind.Wait, Frames: 600, WaitWhilePlayable: true));

        Assert.InRange(result.FramesAdvanced, 2, 599);
        Assert.Equal(frame + result.FramesAdvanced, world.Events.Frame);
        Assert.Equal(PlayerTriggerKind.Waiting, result.Trigger.Kind);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.False(result.NativeInputObserved);
        Assert.All(session.ExamTimer!.Alternatives, alternative => Assert.True(alternative.Remaining < 10));
        Assert.Equal(1, world.Events.EventMemory.Read(1028, 1).RequireExact());
        Assert.Null(world.Events.LastFault);
    }

    private static long DefaultWaitUntil(EventEngine engine)
    {
        return engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("WaitUntil").GetInt64();
    }

    private static EventEngine Create((Jsm.Opcode Opcode, int Parameter)[] commands, bool requirePlayable = true)
    {
        NativeInputConfiguration configuration = new(NativeInputConfiguration.OriginalInitHash, 0x00020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray());
        GameSession coldProcess = GameSession.CreateColdProcess();
        coldProcess.SetEventRandom(coldProcess.EventRandom!.ClearSavedStateForNewGame());
        GameSession session = new(coldProcess.State with { Scene = SceneKind.Field, FieldId = 600, WorldControls = NativeWorldControlState.CreateNewGame(), InputConfiguration = configuration });
        EventEngine engine = new(session)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 0,
            ResolvePartyTraceTriangle = (_, _, _) => 0
        };
        engine.LoadField(600, Program(commands), ["A native scene message."], new FieldTransition(600, 0, 0, 0, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Scene.Party[0] = 0;
        engine.Scene.Party[1] = 255;
        engine.Scene.Party[2] = 255;
        for (int callback = 0; callback < 8; callback++)
        {
            engine.Step();
            JsonElement saved = engine.Capture();
            if (saved.GetProperty("DefaultsStarted").GetBoolean() && saved.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1 && fiber.GetProperty("Started").GetBoolean()))
            {
                break;
            }
        }
        Assert.Null(engine.LastFault);
        Assert.Equal(requirePlayable ? PlayerTriggerKind.Playable : PlayerTriggerKind.Waiting, engine.NextTrigger.Kind);
        return engine;
    }

    private static byte[] Program((Jsm.Opcode Opcode, int Parameter)[] commands)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1)];
        operations.AddRange(commands);
        operations.Add((Jsm.Opcode.RET, 8));
        byte[] bytes = new byte[16 + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Count));
        for (int index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            int encoded = operation.Parameter != 0 || operation.Opcode is Jsm.Opcode.LBL or Jsm.Opcode.PSHN_L ? (int)operation.Opcode << 24 | operation.Parameter & 0xFFFFFF : (int)operation.Opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }
}
