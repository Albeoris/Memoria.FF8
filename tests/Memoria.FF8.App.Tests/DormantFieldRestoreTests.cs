using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DormantFieldRestoreTests
{
    [Fact]
    public void FieldOnlyRestoreNeverPublishesHistoricalClockOrCanonicalMemory()
    {
        GameSession session = GameSession.CreateColdProcess();
        EventEngine original = new(session);
        byte[] program = EmptyProgram();
        original.LoadField(600, program, []);
        session.SetEventMemory(session.State.EventMemory.Write(1300, 1, NativeScalarValue.Exact(17)));
        JsonElement frozen = original.Capture();
        session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.World, 24));
        session.SetEventMemory(session.State.EventMemory.Write(1300, 1, NativeScalarValue.Exact(73)).Write(1100, 1, NativeScalarValue.Exact(9)));
        GameState current = session.State;
        EventEngine restored = new(session);
        int completions = 0;
        restored.OperationCompleted += () => completions++;
        restored.LoadField(600, program, [], restoringCheckpoint: true);
        Assert.Same(current, session.State);
        restored.RestoreFieldState(frozen);
        Assert.Same(current, session.State);
        Assert.Equal(48, session.ExamTimer.ClockCalls);
        Assert.Equal(48, session.State.Playtime!.ClockCalls);
        Assert.Equal(73, session.State.EventMemory.Read(1300, 1).RequireExact());
        Assert.Equal(9, session.State.EventMemory.Read(1100, 1).RequireExact());
        Assert.Equal(0, completions);
        JsonElement after = restored.Capture();
        Assert.True(JsonElement.DeepEquals(frozen.GetProperty("Scene"), after.GetProperty("Scene")));
        Assert.True(JsonElement.DeepEquals(frozen.GetProperty("Fibers"), after.GetProperty("Fibers")));
        Assert.Equal(frozen.GetProperty("Frame").GetInt64(), restored.Frame);
    }

    [Fact]
    public void FieldOnlyRestoreIncludesTheLocalAxisInterpolationOrigin()
    {
        GameSession session = GameSession.CreateColdProcess();
        EventEngine original = new(session);
        byte[] program = EmptyProgram();
        original.LoadField(600, program, []);
        JsonObject frozen = JsonNode.Parse(original.Capture().GetRawText())!.AsObject();
        frozen["AxisStart"] = 37;
        frozen["AxisTarget"] = 49;
        frozen["AxisDuration"] = 4;
        frozen["AxisElapsed"] = 1;
        frozen["InputAxisDirection"] = 40;
        EventEngine restored = new(session);
        restored.LoadField(600, program, [], restoringCheckpoint: true);
        restored.RestoreFieldState(JsonSerializer.SerializeToElement(frozen));
        JsonElement after = restored.Capture();
        Assert.Equal(37, after.GetProperty("AxisStart").GetInt32());
        Assert.Equal(49, after.GetProperty("AxisTarget").GetInt32());
        Assert.Equal(4, after.GetProperty("AxisDuration").GetInt32());
        Assert.Equal(1, after.GetProperty("AxisElapsed").GetInt32());
        Assert.Equal(40, restored.InputAxisDirection);
    }

    [Fact]
    public void WholeCheckpointRestoreStillPublishesItsSavedCanonicalMemory()
    {
        GameSession session = GameSession.CreateColdProcess();
        EventEngine engine = new(session);
        byte[] program = EmptyProgram();
        engine.LoadField(600, program, []);
        session.SetEventMemory(session.State.EventMemory.Write(1300, 1, NativeScalarValue.Exact(17)));
        JsonElement saved = engine.Capture();
        session.SetEventMemory(session.State.EventMemory.Write(1300, 1, NativeScalarValue.Exact(73)));
        engine.LoadField(600, program, [], restoringCheckpoint: true);
        engine.Restore(saved);
        Assert.Equal(17, session.State.EventMemory.Read(1300, 1).RequireExact());
        Assert.True(JsonElement.DeepEquals(saved, engine.Capture()));
    }

    private static byte[] EmptyProgram()
    {
        byte[] bytes = new byte[22];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), (int)Jsm.Opcode.LBL << 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), (int)Jsm.Opcode.RET << 24 | 8);
        return bytes;
    }
}
