using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeEyesTests
{
    [Fact]
    public void Actual_689_closeeyes_copies_original_bank_and_roundtrips_without_replaying_arrival()
    {
        GameSession session = new(GameSession.Load(Path.Combine(Repository(), "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, GameDirectory());
        world.EnsureField();
        for (int count = 0; count < 60 && world.Events.Actors[3].NativeEyes?.Mode != 0x200000; count++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        NativeActorEyes eyes = Assert.IsType<NativeActorEyes>(world.Events.Actors[3].NativeEyes);
        Assert.Equal(0x200000u, eyes.Mode);
        Assert.Equal((byte)0x20, eyes.BackendHighNibble114);
        Assert.Equal(3, eyes.BoundModelId);
        NativeEyeCopy patch = Assert.IsType<NativeEyeCopy>(eyes.Patch);
        Assert.Equal("D274511026A7A7D2A9B2BD1FEDC5DDD641E08A10FFAFEC7473AA233A5EB628B9", patch.ModelHash);
        Assert.Equal((25, 94, 35, 81, 6, 8), (patch.SourceXWords, patch.SourceY, patch.DestinationXWords, patch.DestinationY, patch.WidthWords, patch.Height));
        Assert.Equal(96, patch.Words.Length);
        session.SetEventState(world.Events.Capture());
        using GameWorld restored = new(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!), GameDirectory());
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        JsonNode tampered = JsonNode.Parse(world.Events.Capture().GetRawText())!;
        tampered["Actors"]![3]!["NativeEyes"]!["BackendHighNibble114"] = 0x40;
        Assert.Throws<InvalidDataException>(() => restored.Events.Restore(JsonSerializer.SerializeToElement(tampered)));
    }

    [Fact]
    public void Original_open_and_closed_sources_are_authenticated_disjoint_packed_words()
    {
        using GameAssets assets = new(GameDirectory());
        NativeFieldEyeDefinition definition = assets.LoadField(689).Models[3].NativeEyes!;
        Assert.Equal("0002080C080246005100000032005E0032005600", Convert.ToHexString(definition.Bank));
        NativeEyeCopy closed = definition.Copy(true)!;
        NativeEyeCopy open = definition.Copy(false)!;
        Assert.Equal((25, 86, 6, 8), (open.SourceXWords, open.SourceY, open.WidthWords, open.Height));
        Assert.NotEqual(Convert.ToHexString(closed.Words), Convert.ToHexString(open.Words));
        Assert.Equal(closed.DestinationXWords, open.DestinationXWords);
        Assert.Null((definition with { Bank = [] }).Copy(true));
        byte[] unsupported = definition.Bank.ToArray();
        unsupported[2] = 4;
        Assert.Throws<NotSupportedException>(() => (definition with { Bank = unsupported }).Copy(true));
        byte[] overlap = definition.Bank.ToArray();
        overlap[12] = overlap[6];
        overlap[14] = overlap[8];
        Assert.Throws<NotSupportedException>(() => (definition with { Bank = overlap }).Copy(true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Commands_consume_no_operands_preserve_caller_ownership_and_finish_immediately(bool missingResource)
    {
        GameSession session = new(GameSession.Load(Path.Combine(Repository(), "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, GameDirectory());
        world.EnsureField();
        EventEngine engine = world.Events;
        engine.LoadField(689, Program(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        if (missingResource)
        {
            engine.ReadNativeEyes = _ => null;
            Assert.Throws<EventScriptException>(() =>
            {
                engine.Step();
                engine.Step();
            });
            Assert.Equal("CLOSEEYES", engine.LastFault!.Opcode);
            Assert.Equal(0u, engine.Actors[0].NativeEyes!.Mode);
        }
        else
        {
            engine.Step();
            engine.Step();
            Assert.Equal(0x100000u, engine.Actors[0].NativeEyes!.Mode);
            Assert.Equal((byte)0x40, engine.Actors[0].NativeEyes!.BackendHighNibble114);
        }
        JsonElement fiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(f => f.GetProperty("ScriptId").GetInt32() == 1);
        Assert.Equal(1234, fiber.GetProperty("Stack")[0].GetInt32());
        Assert.Single(fiber.GetProperty("Stack").EnumerateArray());

    }

    private static byte[] Program()
    {
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 3), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.CLOSEEYES, 0), (Jsm.Opcode.OPENEYES, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), parameter != 0 ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode);
        }
        return bytes;
    }

    private static string GameDirectory()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field resources are required.");
        return game;
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "../fixtures/external-fixture.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research checkout was not found.");
    }
}
