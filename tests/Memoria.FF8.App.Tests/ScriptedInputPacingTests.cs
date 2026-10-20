using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ScriptedInputPacingTests
{
    [Fact]
    public void PollingOwnershipSurvivesInstructionBudgetsBetweenQueries()
    {
        List<(Jsm.Opcode Opcode, int Parameter)> commands = [(Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.KEYSCAN2, 0)];
        commands.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 40));
        commands.Add((Jsm.Opcode.JMP, 1 - commands.Count));
        EventEngine engine = Create(commands.ToArray());
        int queries = 0;
        int callbacksWithoutQueries = 0;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "KEYSCAN2")
            {
                queries++;
            }
        };

        for (int callback = 0; callback < 24; callback++)
        {
            int before = queries;
            engine.Step();
            if (queries == before)
            {
                callbacksWithoutQueries++;
            }
            Assert.True(engine.IsScriptedInterface);
            Assert.Null(engine.LastFault);
        }

        Assert.True(queries > 0);
        Assert.True(callbacksWithoutQueries > 0);
    }

    [Fact]
    public void RestoringDuringARepeatWaitRetainsOrdinaryPacingUntilPollingResumes()
    {
        (Jsm.Opcode Opcode, int Parameter)[] commands = [(Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.KEYSCAN2, 0), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.JMP, -4)];
        EventEngine original = Create(commands);
        original.Step();
        JsonElement snapshot = original.Capture();
        JsonElement waiting = DefaultFiber(snapshot);
        Assert.Equal("Frames", waiting.GetProperty("WaitReason").GetString());
        long waitUntil = waiting.GetProperty("WaitUntil").GetInt64();
        Assert.True(waitUntil > original.Frame + 1);
        Assert.True(original.IsScriptedInterface);

        EventEngine restored = Create(commands);
        restored.Restore(snapshot);
        Assert.True(restored.IsScriptedInterface);
        bool polledAgain = false;
        restored.CommandExecuted += command => polledAgain |= command.Name == "KEYSCAN2";
        while (restored.Frame <= waitUntil)
        {
            original.Step();
            restored.Step();
            Assert.True(original.IsScriptedInterface);
            Assert.True(restored.IsScriptedInterface);
            Assert.Null(restored.LastFault);
            Assert.True(JsonElement.DeepEquals(original.Capture(), restored.Capture()));
        }
        Assert.True(polledAgain);
    }

    [Fact]
    public void LeavingThePollingBranchReleasesOwnershipOnTheGuardLoop()
    {
        EventEngine engine = Create([(Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.KEYSCAN2, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.JPF, -3), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.JMP, -1)]);
        Assert.True(engine.IsScriptedInterface);
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 64)).Accepted);
        for (int callback = 0; callback < 8 && engine.IsScriptedInterface; callback++)
        {
            engine.Step();
        }

        Assert.False(engine.IsUserControlEnabled);
        Assert.NotNull(engine.DiscoverScriptedInput());
        Assert.False(engine.IsScriptedInterface);
        for (int callback = 0; callback < 8; callback++)
        {
            engine.Step();
            Assert.False(engine.IsScriptedInterface);
            Assert.Null(engine.LastFault);
        }
    }

    private static JsonElement DefaultFiber(JsonElement snapshot)
    {
        return snapshot.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
    }

    private static EventEngine Create((Jsm.Opcode Opcode, int Parameter)[] commands)
    {
        NativeInputConfiguration configuration = new(NativeInputConfiguration.OriginalInitHash, 0x00020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray());
        GameSession coldProcess = GameSession.CreateColdProcess();
        coldProcess.SetEventRandom(coldProcess.EventRandom!.ClearSavedStateForNewGame());
        GameSession session = new(coldProcess.State with { Scene = SceneKind.Field, FieldId = 600, WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), InputConfiguration = configuration });
        EventEngine engine = new(session)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 0,
            ResolvePartyTraceTriangle = (_, _, _) => 0
        };
        engine.LoadField(600, Program(commands), [], new FieldTransition(600, 0, 0, 0, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Scene.Party[0] = 0;
        engine.Scene.Party[1] = 255;
        engine.Scene.Party[2] = 255;
        for (int callback = 0; callback < 8; callback++)
        {
            engine.Step();
            JsonElement saved = engine.Capture();
            if (saved.GetProperty("DefaultsStarted").GetBoolean() && DefaultFiber(saved).GetProperty("Started").GetBoolean())
            {
                break;
            }
        }
        Assert.Null(engine.LastFault);
        Assert.False(engine.IsUserControlEnabled);
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
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.LBL or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }
}
