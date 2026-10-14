using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class MinimalBattleAndGuardianForceTests
{
    private static readonly Lazy<NativeCharacterVitals> InitialVitals = new(() =>
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        using GameAssets assets = new(directory);
        return assets.ReadNewGameCharacterVitals();
    });
    [Fact]
    public void Reached_battle_yields_returns_to_same_vm_and_names_original_gf2_after_restore()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 65536 + 94), (Jsm.Opcode.PSHN_L, 65536 + 4), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.BATTLERESULT, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 800), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.MENUNAME, 0), (Jsm.Opcode.PSHN_L, 321), (Jsm.Opcode.POPM_W, 900));
        EventEngine original = Engine(source, NativeWorldControlState.CreateNewGame());
        original.Scene.Names[5] = "Existing character name";
        original.Step();
        NativeBattleCompletion request = Assert.Single(original.Scene.NativeBattleCompletions!);
        Assert.Equal((94, 4), ((int)request.Encounter, (int)request.Flags));
        Assert.Equal((65536 + 94, 65536 + 4), (request.RawEncounter, request.RawFlags));
        Assert.Equal(NativeBattleBackend.AutomaticVictory, request.Backend);
        Assert.Equal(0, request.EmulatedCombatCallbacks);
        Assert.Null(request.Result);
        Assert.Null(original.Scene.NativeBattleResult);
        Assert.Empty(original.AcquiredGuardianForces!);
        JsonElement before = original.Capture();
        EventEngine restored = Engine(source, NativeWorldControlState.CreateNewGame());
        restored.Restore(before);
        Assert.True(JsonElement.DeepEquals(before, restored.Capture()));
        original.Step();
        restored.Step();
        Assert.True(JsonElement.DeepEquals(original.Capture(), restored.Capture()));
        Assert.Equal((byte)0, original.Scene.NativeBattleResult);
        Assert.Equal((byte)0, original.Scene.NativeBattleCompletions![0].Result);
        Assert.Equal(2, original.Scene.NativeBattleCompletions![0].ReturnFrame);
        Assert.Equal(0, original.Capture().GetProperty("Variables").GetProperty("800").GetInt32());
        Assert.Equal(new[] { 2 }, original.AcquiredGuardianForces!);
        MinimalMenuCompletion naming = Assert.Single(original.Scene.MinimalMenuCompletions);
        Assert.Equal((5, 7, 2), (naming.RawArgument, naming.MenuId, naming.GuardianForceId));
        Assert.False(original.IsDialogueOpen);
        Assert.Equal("Existing character name", original.Scene.Names[5]);
        original.Step();
        restored.Step();
        Assert.True(JsonElement.DeepEquals(original.Capture(), restored.Capture()));
        Assert.Equal(321, original.Capture().GetProperty("Variables").GetProperty("900").GetInt32());
        Assert.Equal(999, original.FieldId);
        Assert.Equal(3, original.Frame);
    }

    [Fact]
    public void Suppressed_battle_consumes_and_yields_without_winning_or_acquiring_any_gf()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPM_W, 900));
        EventEngine engine = Engine(source, NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x38 });
        engine.Step();
        NativeBattleCompletion request = Assert.Single(engine.Scene.NativeBattleCompletions!);
        Assert.True(request.Suppressed);
        Assert.Null(request.Backend);
        Assert.Null(engine.Scene.NativeBattleResult);
        Assert.Empty(engine.AcquiredGuardianForces!);
        engine.Step();
        Assert.Equal(123, engine.Capture().GetProperty("Variables").GetProperty("900").GetInt32());
        Assert.Null(engine.Scene.NativeBattleResult);
    }

    [Fact]
    public void Native_addparty_flag_branch_cannot_leave_the_known_battle_mask_stale()
    {
        EventEngine engine = Engine(Program((Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDPARTY, 0)), NativeWorldControlState.CreateNewGame());
        int[] party = engine.Scene.Party.ToArray();
        Assert.Throws<EventScriptException>(() => engine.Step());
        Assert.Equal("ADDPARTY", engine.LastFault!.Opcode);
        JsonElement snapshot = engine.Capture();
        Assert.Equal(2, snapshot.GetProperty("Fibers")[0].GetProperty("Position").GetInt32());
        Assert.Equal(new[] { 13 }, snapshot.GetProperty("Fibers")[0].GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(0x18u, snapshot.GetProperty("WorldControls").GetProperty("EventFlags104").GetUInt32());
        Assert.Equal(party, engine.Scene.Party);
    }

    [Fact]
    public void Unknown_battle_flag_faults_before_consuming_original_operands()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0));
        EventEngine engine = Engine(source, null);
        Assert.Throws<EventScriptException>(() => engine.Step());
        JsonElement snapshot = engine.Capture();
        Assert.Equal(3, snapshot.GetProperty("Fibers")[0].GetProperty("Position").GetInt32());
        Assert.Equal(new[] { 4, 94 }, snapshot.GetProperty("Fibers")[0].GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Contains("104", engine.LastFault!.Message);
        Assert.Null(engine.Scene.NativeBattleCompletions);
    }

    [Theory]
    [InlineData(3, 0, 5)]
    [InlineData(4, 1, 6)]
    [InlineData(5, 2, 7)]
    [InlineData(6, 3, 8)]
    [InlineData(7, 4, 9)]
    [InlineData(8, 6, 11)]
    [InlineData(9, 5, 10)]
    [InlineData(10, 7, 12)]
    [InlineData(11, 8, 13)]
    [InlineData(12, 9, 14)]
    [InlineData(13, 10, 15)]
    [InlineData(14, 12, 17)]
    [InlineData(15, 11, 16)]
    [InlineData(16, 13, 18)]
    [InlineData(17, 14, 19)]
    [InlineData(18, 15, 20)]
    public void Native_guardian_force_table_preserves_crossed_argument_mapping(int argument, int guardianForce, int menuId)
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.MENUNAME, 0));
        EventEngine engine = Engine(source, NativeWorldControlState.CreateNewGame());
        engine.Step();
        Assert.Equal(new[] { guardianForce }, engine.AcquiredGuardianForces!);
        MinimalMenuCompletion completion = Assert.Single(engine.Scene.MinimalMenuCompletions);
        Assert.Equal(menuId, completion.MenuId);
        EventEngine restored = Engine(source, NativeWorldControlState.CreateNewGame());
        restored.Restore(engine.Capture());
        Assert.True(JsonElement.DeepEquals(engine.Capture(), restored.Capture()));
        engine.Step();
        restored.Step();
        Assert.True(JsonElement.DeepEquals(engine.Capture(), restored.Capture()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Existing_character_name_prompt_cannot_acquire_a_negative_gf(int argument)
    {
        EventEngine engine = Engine(Program((Jsm.Opcode.PSHN_L, argument), (Jsm.Opcode.MENUNAME, 0)), NativeWorldControlState.CreateNewGame());
        engine.Step();
        Assert.True(engine.CurrentDialogue!.IsNamePrompt);
        Assert.Empty(engine.AcquiredGuardianForces!);
        Assert.Empty(engine.Scene.MinimalMenuCompletions);
    }

    [Fact]
    public void Invalid_battle_completion_restore_is_atomic()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0));
        EventEngine engine = Engine(source, NativeWorldControlState.CreateNewGame());
        engine.Step();
        JsonElement before = engine.Capture();
        foreach (string property in new[] { "Encounter", "Flags", "EmulatedCombatCallbacks", "Operation" })
        {
            JsonNode invalid = JsonNode.Parse(before.GetRawText())!;
            invalid["Scene"]!["NativeBattleCompletions"]![0]![property] = 123;
            Assert.Throws<InvalidDataException>(() => engine.Restore(JsonSerializer.SerializeToElement(invalid)));
            Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        }
    }

    private static EventEngine Engine(byte[] source, NativeWorldControlState? controls)
    {
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 999, WorldControls = controls, CharacterVitals = InitialVitals.Value.DeepCopy() });
        var engine = new EventEngine(session, session.State.Variables);
        engine.LoadField(999, source, []);
        return engine;
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[] body)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), .. body, (Jsm.Opcode.RET, 0)];
        const int operationsOffset = 14;
        byte[] bytes = new byte[operationsOffset + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
