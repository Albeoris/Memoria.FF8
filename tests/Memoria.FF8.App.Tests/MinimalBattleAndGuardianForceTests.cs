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
        Assert.Equal(3, original.Scene.NativeBattleCompletions![0].ReturnFrame);
        Assert.Equal(0, original.Variables[800]);
        Assert.Equal(new[] { 2 }, original.AcquiredGuardianForces!);
        MinimalMenuCompletion naming = Assert.Single(original.Scene.MinimalMenuCompletions);
        Assert.Equal((5, 7, 2), (naming.RawArgument, naming.MenuId, naming.GuardianForceId));
        Assert.False(original.IsDialogueOpen);
        Assert.Equal("Existing character name", original.Scene.Names[5]);
        original.Step();
        restored.Step();
        Assert.True(JsonElement.DeepEquals(original.Capture(), restored.Capture()));
        Assert.Equal(321, original.Variables[900]);
        Assert.Equal(999, original.FieldId);
        Assert.Equal(4, original.Frame);
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
        Assert.Equal(123, engine.Variables[900]);
        Assert.Null(engine.Scene.NativeBattleResult);
    }

    [Fact]
    public void Native_addparty_guest_branch_updates_the_known_battle_mask()
    {
        EventEngine engine = Engine(Program((Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDPARTY, 0)), NativeWorldControlState.CreateNewGame());
        engine.Step();
        JsonElement snapshot = engine.Capture();
        Assert.Equal(0x38u, snapshot.GetProperty("WorldControls").GetProperty("EventFlags104").GetUInt32());
        Assert.Contains(13, engine.Scene.Party);
        Assert.Null(engine.LastFault);
    }

    [Fact]
    public void Unknown_native_control_owner_faults_before_dispatching_battle()
    {
        byte[] source = Program((Jsm.Opcode.PSHN_L, 94), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BATTLE, 0));
        EventEngine engine = Engine(source, null);
        JsonElement before = engine.Capture();
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => engine.Step());
        Assert.Contains("canonical memory and atomic current-owner publication", error.Message);
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
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
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(InitialVitals.Value.DeepCopy());
        session.Restore(session.State with { FieldId = 999, FieldName = "synthetic-bare-battle-menu-component", WorldControls = controls });
        var engine = new EventEngine(session);
        engine.LoadField(999, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        return engine;
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[] body)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), .. body, (Jsm.Opcode.RET, 0)];
        const int operationsOffset = 16;
        byte[] bytes = new byte[operationsOffset + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
