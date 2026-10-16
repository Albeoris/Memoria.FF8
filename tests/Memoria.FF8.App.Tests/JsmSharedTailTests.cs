using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JsmSharedTailTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(65542)]
    public void ConditionalSharedTailRetainsIssuingScriptAcrossWaitReturnAndColdRestore(int displacement)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] source = SharedTail(displacement);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session);
            engine.LoadField(999, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        for (int callback = 0; callback < 8; callback++)
        {
            live.Step();
            if (live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2 && fiber.GetProperty("Position").GetInt32() == 20))
            {
                break;
            }
        }
        JsonElement saved = live.Capture();
        JsonElement owner = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2);
        Assert.Equal(0, owner.GetProperty("ObjectId").GetInt32());
        Assert.Equal(20, owner.GetProperty("Position").GetInt32());
        Assert.Equal("Frames", owner.GetProperty("WaitReason").GetString());
        Assert.False(live.EventMemory.Cells.ContainsKey(6000));
        EventEngine restored = Create();
        restored.Restore(saved);
        live.Step();
        restored.Step();
        Assert.Equal(42, live.EventMemory.Read(6004, 4).RequireExact());
        Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
        Assert.True(live.Capture().GetProperty("Fibers").EnumerateArray().All(fiber => fiber.GetProperty("ScriptId").GetInt32() != 3));

        JsonObject wrongOwner = JsonNode.Parse(saved.GetRawText())!.AsObject();
        JsonObject wrongFiber = wrongOwner["Fibers"]!.AsArray().Single(fiber => fiber!["ScriptId"]!.GetValue<int>() == 2)!.AsObject();
        wrongFiber["Position"] = 17;
        Assert.Throws<InvalidDataException>(() => Create().Restore(JsonSerializer.SerializeToElement(wrongOwner)));
    }

    [Fact]
    public void JumpEdgesRemainGlobalBoundedAndUnconditionalTransferDoesNotAdmitInterveningCode()
    {
        byte[] content = SharedTail(6);
        const int operationsOffset = 20;
        BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(operationsOffset + 11 * 4), (int)Jsm.Opcode.JMP << 24 | 7);
        EventScript script = JsmProgram.Read(content).Objects[0].Scripts[2];
        Assert.True(script.ContainsOperation(18));
        Assert.False(script.ContainsOperation(12));
        Assert.False(script.ContainsOperation(16));
        Assert.False(script.ContainsOperation(17));
        Assert.Equal((10, 16), (script.Start, script.End));
        BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(operationsOffset + 11 * 4), (int)Jsm.Opcode.JMP << 24 | 100);
        Assert.Throws<InvalidDataException>(() => JsmProgram.Read(content));
        Assert.Equal(65535, JsmProgram.JumpTarget(0, -1));
        Assert.Equal(1, JsmProgram.JumpTarget(65535, 2));
    }

    [Fact]
    public void ActualLunarGate541PreservesEntry79AndAuthenticatesItsSharedTail()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        JsmProgram program = JsmProgram.Read(assets.LoadField(541).Jsm);
        EventObject owner = program.Objects.Single(actor => actor.Scripts.Any(script => script.Id == 79));
        EventScript entry = owner.Scripts.Single(script => script.Id == 79);
        Assert.Equal((966, 976), (entry.Start, entry.End));
        Assert.True(entry.ContainsOperation(973));
        Assert.True(entry.ContainsOperation(1040));
        Assert.False(entry.ContainsOperation(976));
        Assert.False(entry.ContainsOperation(1003));
        Assert.Contains(owner.Scripts, script => script.Id == 82 && script.Start == 1003);
        Assert.Equal(1040, JsmProgram.JumpTarget(973, program.Operations[973].Parameter));

        string root = AppContext.BaseDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.JsmSharedTailTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Equal(541, session.State.FieldId);
        Assert.Null(world.Events.LastFault);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-shared-JSM-tail-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static byte[] SharedTail(int displacement)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQEW, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JPF, displacement), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[20 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 23);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.REQEW ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + index * 4), encoded);
        }
        return bytes;
    }
}
