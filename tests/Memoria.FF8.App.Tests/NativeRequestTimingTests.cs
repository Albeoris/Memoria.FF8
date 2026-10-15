using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRequestTimingTests
{
    [Theory]
    [InlineData(Jsm.Opcode.REQ)]
    [InlineData(Jsm.Opcode.REQSW)]
    [InlineData(Jsm.Opcode.REQEW)]
    public void NativeRequestsYieldAndAnOccupiedSlotDropsWithoutCreatingADuplicate(Jsm.Opcode secondRequest)
    {
        byte[] content = Modules(secondRequest);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(999, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        Assert.Equal(-1, live.BackgroundStates[0]);
        JsonElement snapshot = live.Capture();
        Assert.Equal("None", snapshot.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1).GetProperty("WaitReason").GetString());
        EventEngine restored = Create();
        restored.Restore(snapshot);
        live.Step();
        restored.Step();
        Assert.Equal(1, live.BackgroundStates[0]);
        Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
        Assert.Single(live.Capture().GetProperty("Fibers").EnumerateArray(), item => item.GetProperty("ScriptId").GetInt32() == 10);
        for (int callback = 0; callback < 5; callback++)
        {
            live.Step();
            restored.Step();
            Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
        }
        Assert.Equal(1, live.BackgroundStates[1]);
    }

    [Fact]
    public void UnprovedSelfRequestRefusesBeforeConsumingOperandsOrTransferringItsSlot()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session);
        engine.LoadField(999, Modules(Jsm.Opcode.REQ, selfRequest: true), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        Assert.Throws<EventScriptException>(() => engine.Step());
        Assert.Contains("current-slot switching", engine.LastFault!.Message);
        JsonElement fiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        Assert.Equal(5, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(2, fiber.GetProperty("Stack").GetArrayLength());
    }

    [Theory]
    [InlineData(Jsm.Opcode.REQSW, 3)]
    [InlineData(Jsm.Opcode.REQEW, 7)]
    public void AcceptedRequestWaitCompletionYieldsBeforeTheSuccessor(Jsm.Opcode request, int successorFrame)
    {
        byte[] content = Modules(Jsm.Opcode.REQ, firstRequest: request);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(999, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (int frame = 3; frame <= successorFrame; frame++)
        {
            live.Step();
            restored.Step();
            Assert.Equal(frame < successorFrame ? -1 : 1, live.BackgroundStates[0]);
            Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
        }
    }

    private static byte[] Modules(Jsm.Opcode secondRequest, bool selfRequest = false, Jsm.Opcode firstRequest = Jsm.Opcode.REQ)
    {
        ushort[] groups = [1, (8 << 7) | 2];
        (Jsm.Opcode Opcode, int Parameter)[][] scripts =
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, selfRequest ? 1 : 10), (firstRequest, selfRequest ? 0 : 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BGDRAW, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 10), (secondRequest, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 10), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BGDRAW, 0), (Jsm.Opcode.RET, 8)]
        ];
        int scriptsOffset = 8 + groups.Length * 2;
        int operationsOffset = scriptsOffset + (scripts.Length + 1) * 2;
        byte[] bytes = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        bytes[2] = checked((byte)groups.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), checked((ushort)scriptsOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)operationsOffset));
        for (int index = 0; index < groups.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + index * 2), groups[index]);
        }
        int operationIndex = 0;
        for (int index = 0; index < scripts.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + index * 2), checked((ushort)operationIndex));
            foreach ((Jsm.Opcode opcode, int parameter) in scripts[index])
            {
                int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + operationIndex++ * 4), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + scripts.Length * 2), checked((ushort)operationIndex));
        return bytes;
    }
}
