using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWhoAmITests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 3)]
    [InlineData(7, 7)]
    [InlineData(0, 255)]
    [InlineData(-1, 255)]
    [InlineData(265, 1)]
    [InlineData(266, 3)]
    [InlineData(0x1234FF, 255)]
    public void LowByteSelectorScansPhysicalRowsAndZeroExtendsResult(int argument, int expected)
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] rows = initial.Records.ToArray();
        rows[0] = rows[0] with { CharacterId = 8 };
        rows[1] = rows[1] with { CharacterId = 9 };
        rows[3] = rows[3] with { CharacterId = 10 };
        rows[6] = rows[6] with { CharacterId = 9 };
        NativeCharacterVitals vitals = initial with { Records = rows, DreamEnabled = true };
        byte[] source = Encode([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 12345), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.WHOAMI, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(vitals);
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session);
            engine.LoadField(600, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        int calls = 0;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.WHOAMI))
            {
                calls++;
                Assert.Equal([argument], command.Arguments);
                JsonElement fiber = live.Capture().GetProperty("Fibers")[0];
                Assert.Equal(12345, fiber.GetProperty("Stack")[0].GetInt32());
                Assert.Equal(expected, fiber.GetProperty("Results").GetProperty("0").GetInt32());
                Assert.Equal(901, fiber.GetProperty("Results").GetProperty("1").GetInt32());
                Assert.Equal("None", fiber.GetProperty("WaitReason").GetString());
            }
        };
        live.Step();
        Assert.Equal(1, calls);
        Assert.Equal(12345, live.EventMemory.Read(6000, 4).RequireExact());
        Assert.Equal(expected, live.EventMemory.Read(6004, 4).RequireExact());
        Assert.Equal(901, live.EventMemory.Read(6008, 4).RequireExact());
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        live.Step();
        restored.Step();
        Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOperandOrSavedOwnerRefusesBeforeMutation(bool missingOperand)
    {
        using GameAssets assets = new(GameDirectory);
        GameSession session = new(new GameState { CharacterVitals = missingOperand ? assets.ReadNewGameCharacterVitals() : null });
        EventEngine engine = new(session);
        List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0)];
        if (!missingOperand)
        {
            operations.Add((Jsm.Opcode.PSHN_L, 9));
        }
        operations.AddRange([(Jsm.Opcode.WHOAMI, 0), (Jsm.Opcode.RET, 8)]);
        engine.LoadField(600, Encode(operations.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Assert.Throws<EventScriptException>(() => engine.Step());
        Assert.Equal(nameof(Jsm.Opcode.WHOAMI), engine.LastFault!.Opcode);
        JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
        Assert.Equal(missingOperand ? 3 : 4, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(missingOperand ? 0 : 1, fiber.GetProperty("Stack").GetArrayLength());
        Assert.Equal(901, fiber.GetProperty("Results").GetProperty("0").GetInt32());
    }

    [Fact]
    public void Actual549GuestSelectionReturnUsesCurrentJunctionRowsAndRestores()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, GameDirectory);
        List<(int Selector, int Result)> queries = [];
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.WHOAMI))
            {
                queries.Add((command.Arguments.Single(), world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 9 && fiber.GetProperty("ScriptId").GetInt32() == 97).GetProperty("Results").GetProperty("0").GetInt32()));
            }
        };
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 120)).Accepted);
        Assert.Contains((9, 1), queries);
        Assert.Contains((10, 3), queries);
        Assert.Null(world.Events.LastFault);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-WHOAMI-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, GameDirectory);
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static byte[] Encode((Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }
}
