using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeItemPresenceTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(164, 1)]
    [InlineData(0, 1)]
    [InlineData(255, 1)]
    [InlineData(163, 0)]
    [InlineData(-1, 0)]
    [InlineData(420, 0)]
    [InlineData(65536, 0)]
    public void QueryUsesFullDwordIdAndAllOriginalPairsRegardlessOfQuantity(int argument, int expected)
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy initial = assets.ReadNewGameEconomy();
        NativeItemStock[] items = initial.Items.ToArray();
        items[0] = new(164, 0);
        items[^1] = new(255, 0);
        NativeEconomy economy = initial with { Items = items };
        byte[] source = Encode([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 12345), (Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.Unknown5, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(economy);
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
            if (command.Name == nameof(Jsm.Opcode.Unknown5))
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
        Assert.Equal(items, live.Economy!.Items);
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        live.Step();
        restored.Step();
        Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
    }

    [Fact]
    public void MissingOperandRefusesBeforePcAndResultMutation()
    {
        using GameAssets assets = new(GameDirectory);
        GameSession session = new(new GameState { Economy = assets.ReadNewGameEconomy() });
        EventEngine engine = new(session);
        engine.LoadField(600, Encode([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.Unknown5, 0), (Jsm.Opcode.RET, 8)]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Assert.Throws<EventScriptException>(() => engine.Step());
        JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
        Assert.Equal(3, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(0, fiber.GetProperty("Stack").GetArrayLength());
        Assert.Equal(901, fiber.GetProperty("Results").GetProperty("0").GetInt32());
    }

    [Fact]
    public void MissingCanonicalInventoryRefusesBeforeOperandConsumption()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session);
        engine.LoadField(600, Encode([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 164), (Jsm.Opcode.Unknown5, 0), (Jsm.Opcode.RET, 8)]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Assert.Throws<EventScriptException>(() => engine.Step());
        JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
        Assert.Equal(4, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(164, fiber.GetProperty("Stack")[0].GetInt32());
        Assert.Equal(901, fiber.GetProperty("Results").GetProperty("0").GetInt32());
    }

    [Fact]
    public void ActualWhiteSeeD857InitializationAndStandaloneContinuation()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeItemPresenceTests.1")));
        using GameWorld world = new(session, GameDirectory);
        int calls = 0;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.Unknown5))
            {
                calls++;
                Assert.Equal([164], command.Arguments);
            }
        };
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(857, session.State.FieldId);
        Assert.Equal(1, calls);
        Assert.Null(world.Events.LastFault);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-item-presence-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, GameDirectory);
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
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

    private static string FindRootFile(string relative)
    {
        return TestResources.FixtureFile(relative);
    }
}
