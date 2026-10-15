using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDoorInitializationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Original_layout_mid_init_save_finishes_with_preserved_loader_proof(int frame)
    {
        byte[] source = DoorProgram(DelayedLine(), []);
        EventEngine original = Engine(source);
        for (int index = 0; index < frame; index++)
        {
            original.Step();
        }
        JsonElement snapshot = original.Capture();
        Assert.Null(original.Scene.NativeDoorDriverVersion);
        Assert.Equal(frame == 0 ? NativeFieldInitializationStage.Allocated : NativeFieldInitializationStage.Initializing, original.Scene.NativeFieldInitialization!.Stage);
        Assert.Equal(frame >= 5, original.Actors[0].NativeSetLineReceipt is not null);
        EventEngine restored = Engine(source);
        restored.Restore(snapshot);
        Assert.True(JsonElement.DeepEquals(snapshot, restored.Capture()));
        while (original.Scene.NativeDoorDriverVersion is null && original.Frame < 12)
        {
            original.Step();
            restored.Step();
            Assert.True(JsonElement.DeepEquals(original.Capture(), restored.Capture()));
        }
        Assert.Equal(2, original.Scene.NativeDoorDriverVersion);
        Assert.Equal(NativeFieldInitializationStage.Completed, original.Scene.NativeFieldInitialization!.Stage);
        Assert.Equal(new Vector3(1, -2, 3), original.Actors[0].LineStart);
        Assert.Equal(new Vector3(3, 4, 5), original.Actors[0].LineEnd);
        Assert.Equal(7929, original.Actors[0].MovementSpeed);
        Assert.True(original.Actors[0].LineEnabled);
        Assert.Equal((999, 0, 0), (original.Actors[0].NativeSetLineReceipt!.FieldId, original.Actors[0].NativeSetLineReceipt!.ObjectId, (int)original.Actors[0].NativeSetLineReceipt!.NativeDoorOrdinal));
    }

    [Fact]
    public void Unset_native_door_is_disabled_and_legacy_mid_init_does_not_gain_loader_history()
    {
        byte[] unset = DoorProgram([], []);
        EventEngine fresh = Engine(unset);
        fresh.Step();
        Assert.Equal(2, fresh.Scene.NativeDoorDriverVersion);
        Assert.False(fresh.Actors[0].LineEnabled);
        Assert.Null(fresh.Actors[0].LineStart);
        EventEngine restored = Engine(unset);
        restored.Restore(fresh.Capture());
        Assert.True(JsonElement.DeepEquals(fresh.Capture(), restored.Capture()));

        byte[] source = DoorProgram(DelayedLine(), []);
        EventEngine legacy = Engine(source, false);
        legacy.Step();
        EventEngine resumed = Engine(source);
        resumed.Restore(legacy.Capture());
        for (int index = 0; index < 10; index++)
        {
            resumed.Step();
        }
        Assert.Null(resumed.Scene.NativeFieldInitialization);
        Assert.Null(resumed.Scene.NativeDoorDriverVersion);
        Assert.NotNull(resumed.Actors[0].NativeSetLineReceipt);
        Assert.Equal(1000, resumed.Actors[0].MovementSpeed);
    }

    [Fact]
    public void Reached_original_door_script_faults_at_unsupported_opcode_without_hash_admission()
    {
        byte[] source = DoorProgram(Line(), [(Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, 456), (Jsm.Opcode.BATTLE, 0)]);
        EventEngine engine = Engine(source);
        engine.Step();
        engine.Step();
        Assert.Equal(2, engine.Scene.NativeDoorDriverVersion);
        engine.Scene.NativeDoorPendingEvents!.Add(0, 2);
        Assert.Throws<EventScriptException>(() => engine.Step());
        Assert.Equal((999, 0, 3, "BATTLE"), (engine.LastFault!.FieldId, engine.LastFault.ObjectId, engine.LastFault.ScriptId, engine.LastFault.Opcode));
        JsonElement fiber = Assert.Single(engine.Capture().GetProperty("Fibers").EnumerateArray(), item => !item.GetProperty("Complete").GetBoolean() && item.GetProperty("ScriptId").GetInt32() == 3);
        Assert.Equal(new[] { 456, 123 }, fiber.GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()));
    }

    [Fact]
    public void Setline_reenables_line_preserves_contact_and_validates_receipt_atomically()
    {
        byte[] source = DoorProgram(DelayedLine(), Line());
        EventEngine engine = Engine(source);
        for (int index = 0; index < 10; index++)
        {
            engine.Step();
        }
        engine.Scene.NativeDoorPendingEvents!.Add(0, 2);
        engine.CommandExecuted += command =>
        {
            if (command.ScriptId == 3 && command.Name == Jsm.Opcode.LBL.ToString())
            {
                typeof(ActorState).GetProperty(nameof(ActorState.LineEnabled))!.SetValue(engine.Actors[0], false);
                typeof(ActorState).GetProperty(nameof(ActorState.LineContact))!.SetValue(engine.Actors[0], true);
                typeof(ActorState).GetProperty(nameof(ActorState.NativeLineCrossingPending))!.SetValue(engine.Actors[0], true);
                engine.Scene.NativeDoorPendingEvents![0] = 4;
            }
        };
        engine.Step();
        Assert.True(engine.Actors[0].LineEnabled);
        Assert.True(engine.Actors[0].LineContact);
        Assert.True(engine.Actors[0].NativeLineCrossingPending);
        Assert.Equal((byte)4, engine.Scene.NativeDoorPendingEvents![0]);
        Assert.Equal(3, engine.Actors[0].NativeSetLineReceipt!.ScriptId);
        typeof(ActorState).GetProperty(nameof(ActorState.NativeLineCrossingPending))!.SetValue(engine.Actors[0], false);
        engine.Scene.NativeDoorPendingEvents.Clear();
        JsonElement before = engine.Capture();
        foreach (string property in new[] { "FieldId", "ObjectId", "Operation", "Frame", "NativeDoorOrdinal" })
        {
            JsonNode invalid = JsonNode.Parse(before.GetRawText())!;
            invalid["Actors"]![0]!["NativeSetLineReceipt"]![property] = property == "NativeDoorOrdinal" ? 123 : 12345;
            Assert.Throws<InvalidDataException>(() => engine.Restore(JsonSerializer.SerializeToElement(invalid)));
            Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        }
    }

    private static EventEngine Engine(byte[] source, bool verified = true)
    {
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 999 });
        var engine = new EventEngine(session);
        engine.LoadField(999, source, [], nativeRuntimeProfile: verified ? NativeFieldRuntimeProfile.FF8En : null);
        return engine;
    }

    private static (Jsm.Opcode, int)[] Line()
    {
        return [(Jsm.Opcode.PSHN_L, 65537), (Jsm.Opcode.PSHN_L, -65538), (Jsm.Opcode.PSHN_L, 131075), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETLINE, 0)];
    }

    private static (Jsm.Opcode, int)[] DelayedLine()
    {
        return [(Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), .. Line(), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0)];
    }

    private static byte[] DoorProgram((Jsm.Opcode, int)[] initialization, (Jsm.Opcode, int)[] facing)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        for (int local = 0; local < 8; local++)
        {
            (Jsm.Opcode, int)[] body = local == 0 ? initialization : local == 3 ? facing : [];
            scripts.Add([(Jsm.Opcode.LBL, local), .. body, (Jsm.Opcode.RET, 0)]);
        }
        const int scriptsOffset = 10;
        const int operationsOffset = 28;
        byte[] bytes = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        bytes[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), scriptsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 7);
        int operationIndex = 0;
        for (int index = 0; index < scripts.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + index * 2), checked((ushort)operationIndex));
            foreach ((Jsm.Opcode opcode, int parameter) in scripts[index])
            {
                int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + operationIndex++ * 4), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + scripts.Count * 2), checked((ushort)operationIndex));
        return bytes;
    }
}
