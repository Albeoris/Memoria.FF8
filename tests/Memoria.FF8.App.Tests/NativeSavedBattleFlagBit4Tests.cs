using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSavedBattleFlagBit4Tests
{
    [Fact]
    public void OriginalRaw358DispatchAndBit4HelpersMatchTheAuthenticatedExecutable()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(LaunchOptions.DefaultGameDirectory, "FF8_EN.exe"));
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(executable)));
        Assert.Equal(0x529EB0u, BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(0x78DE94 + 358 * 4)));
        Assert.Equal("800D7AE9CF0110C3", Convert.ToHexString(executable.AsSpan(0x16DAC0, 8)));
        Assert.Equal("80257AE9CF01EFC3", Convert.ToHexString(executable.AsSpan(0x16DAD0, 8)));
    }

    [Fact]
    public void ActualLunarGate358PublishesOnlyKnownBit4AndColdRestores()
    {
        GameSession session = new(GameSession.Load(Source()));
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.EnsureField();
        Assert.Null(session.State.SavedBattleFlags);
        bool dream = session.State.CharacterVitals!.DreamEnabled;
        List<ScriptCommand> writes = [];
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.Unknown1))
            {
                writes.Add(command);
            }
        };
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        ScriptCommand write = Assert.Single(writes);
        Assert.Equal((541, 10, 82, 1085), (write.FieldId, write.ObjectId, write.ScriptId, write.Operation));
        Assert.Equal([1], write.Arguments);
        NativeSavedBattleFlags flags = session.State.SavedBattleFlags!;
        Assert.Equal((byte)0x11, flags.EffectiveKnownMask);
        Assert.Equal((byte)0x10, flags.Value);
        Assert.Equal(dream, session.State.CharacterVitals.DreamEnabled);
        session.SetEventState(world.Events.Capture());
        GameSession coldSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld cold = new(coldSession, LaunchOptions.DefaultGameDirectory);
        cold.EnsureField();
        Assert.Equal(flags, coldSession.State.SavedBattleFlags);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(cold.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(256, 16)]
    [InlineData(-65536, 16)]
    public void Native529Eb0PopsOneFullDwordWithoutYieldOrChangingOtherResults(int raw, int expected)
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session);
        byte[] source = Encode([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 12345), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.Unknown1, 0), (Jsm.Opcode.POPM_L, 6000), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
        engine.LoadField(600, source, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        Assert.Equal(expected, session.State.SavedBattleFlags!.Value);
        Assert.Equal((byte)0x11, session.State.SavedBattleFlags.EffectiveKnownMask);
        Assert.Equal(raw, session.State.SavedBattleFlags.FieldWrite!.RawOperand);
        Assert.Equal(12345, engine.EventMemory.Read(6000, 4).RequireExact());
        Assert.Equal(901, engine.EventMemory.Read(6004, 4).RequireExact());
        Assert.Equal(1, engine.Frame);
    }

    [Fact]
    public void FullLegacyOwnerKeepsExactMeaningAndBit4WritePreservesAllOtherBits()
    {
        NativeSavedBattleFlags full = JsonSerializer.Deserialize<NativeSavedBattleFlags>("{\"InitOutSha256\":\"" + NativeInputConfiguration.OriginalInitHash + "\",\"Value\":239}")!;
        full.Validate();
        Assert.Equal(byte.MaxValue, full.EffectiveKnownMask);
        NativeSavedBattleFlagFieldWrite write = new(541, new string('A', 64), 10, 82, 1085, 1, 256);
        NativeSavedBattleFlags set = full.WithFieldBit4(write);
        Assert.Equal((byte)255, set.Value);
        Assert.Equal((byte)239, set.WithFieldBit4(write with { RawOperand = 0 }).Value);
        NativeSavedBattleFlags partial = new(NativeInputConfiguration.OriginalInitHash, 16, 0x11, write);
        Assert.Throws<NotSupportedException>(() => partial.CompleteBattle());
        Assert.Throws<InvalidDataException>(() => (partial with { KnownMask = 255 }).Validate());
        Assert.Throws<InvalidDataException>(() => (partial with { Value = 0x14 }).Validate());
    }

    private static string Source()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        return Path.Combine(root, "../fixtures/external-fixture.json");
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
