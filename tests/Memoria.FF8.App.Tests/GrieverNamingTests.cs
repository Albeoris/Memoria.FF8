using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GrieverNamingTests
{
    [Theory]
    [InlineData(null, "Griever")]
    [InlineData("Lionheart", "Lionheart")]
    public void Actual_ring_name_prompt_uses_distinct_saved_subject_and_restores_before_confirmation(string? input, string expected)
    {
        string game = GameDirectory();
        string source = TestResources.FixturePath("App.Tests.GrieverNamingTests.1");
        Assert.True(File.Exists(source), "Actual reached MENUNAME675 standalone prompt is required.");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        world.Rename(GameNames.GuardianForceSubject(4), "BrothersX");
        string beforeVitals = JsonSerializer.Serialize(session.CharacterVitals);
        Assert.True(world.Events.CurrentDialogue!.IsNamePrompt);
        Assert.Equal("Griever", world.Events.PendingNameDefault);
        Assert.Equal(GameNames.GrieverSubject, world.Events.Capture().GetProperty("NameEntity").GetInt32());
        Assert.Equal("Griever", world.Events.Scene.Names[GameNames.GrieverSubject]);
        Assert.Equal("BrothersX", world.Events.Scene.Names[20]);
        Assert.Equal(beforeVitals, JsonSerializer.Serialize(session.CharacterVitals));
        session.SetEventState(world.Events.Capture());
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, game);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal("Griever", restored.Events.PendingNameDefault);
        long promptFrame = world.Events.Frame;
        string prior = world.Events.Capture().GetRawText();
        Assert.Throws<ArgumentException>(() => world.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: "Bad\nName")));
        Assert.Equal(prior, world.Events.Capture().GetRawText());
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: input)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: input)).Accepted);
        Assert.Equal(promptFrame, world.Events.Frame);
        Assert.Equal(expected, world.Events.Scene.Names[GameNames.GrieverSubject]);
        Assert.Equal("BrothersX", world.Events.Scene.Names[20]);
        Assert.Equal(beforeVitals, JsonSerializer.Serialize(session.CharacterVitals));
        Assert.Null(world.Events.PendingNameDefault);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        // Explicit field-allocation component: reuse the established global names owner without replaying further story.
        world.Events.LoadField(600, Program(20, push: true), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Assert.Equal(expected, world.Events.Scene.Names[GameNames.GrieverSubject]);
        Assert.Equal("BrothersX", world.Events.Scene.Names[20]);
    }

    [Fact]
    public void Original_newgame_bytes_and_saved_name_token_keep_native_identity()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(Repository(), "ws/original/FF8_EN.exe"));
        Assert.Equal("Griever", FF8Encoding.CreateEuropean().GetString(executable.AsSpan(0x7887C8, 7).ToArray()));
        Assert.Equal([9, 12, 54, 0, 252, 10, 0, 0], executable.AsSpan(0x7887E8 + 20 * 8, 8).ToArray());
        Assert.Equal("Griever", GameNames.DefaultName(GameNames.GrieverSubject));
        Assert.True(GameNames.IsValidSubject(GameNames.GrieverSubject));
        Dictionary<int, string> names = new() { [GameNames.GrieverSubject] = "Lionheart", [20] = "BrothersX" };
        Assert.Equal("Lionheart/Griever/BrothersX", DialogueTextFormatter.Format("{Char UnknownCharacter3}/{Char Griever}/{Name Brothers}", names));
        Assert.Equal("Griever", DialogueTextFormatter.Format("{Char UnknownCharacter3}"));
    }

    [Theory]
    [InlineData(19, true)]
    [InlineData(276, true)]
    [InlineData(20, false)]
    public void Unsupported_or_missing_native_name_argument_preserves_pc_and_operands(int argument, bool push)
    {
        string game = GameDirectory();
        using GameAssets assets = new(game);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        EventEngine engine = new(session);
        engine.LoadField(600, Program(argument, push), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        string vitals = JsonSerializer.Serialize(session.CharacterVitals);
        Assert.Throws<EventScriptException>(() =>
        {
            engine.Step();
            engine.Step();
        });
        Assert.Equal("MENUNAME", engine.LastFault!.Opcode);
        JsonElement fiber = engine.Capture().GetProperty("Fibers")[1];
        Assert.Equal(engine.LastFault.Operation, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(push ? 1 : 0, fiber.GetProperty("Stack").GetArrayLength());
        if (push)
        {
            Assert.Equal(argument, fiber.GetProperty("Stack")[0].GetInt32());
        }
        Assert.Empty(engine.Scene.Names);
        Assert.Null(engine.CurrentDialogue);
        Assert.Equal(vitals, JsonSerializer.Serialize(session.CharacterVitals));
    }

    private static byte[] Program(int argument, bool push)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1)];
        if (push)
        {
            operations.Add((Jsm.Opcode.PSHN_L, argument));
        }
        operations.AddRange([(Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        byte[] bytes = new byte[16 + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int value = parameter != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + 4 * index), value);
        }
        return bytes;
    }

    private static string GameDirectory()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field resources are required.");
        return game;
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("External fixture directory was not found.");
    }
}
