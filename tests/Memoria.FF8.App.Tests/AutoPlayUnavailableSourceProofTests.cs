using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayUnavailableSourceProofTests
{
    [Fact]
    public void TypedStoryGateAndLiteralRequestHaveRepresentedInputsWithoutChangingBytes()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHM_W, 256), (Jsm.Opcode.PSHN_L, 70), (Jsm.Opcode.CAL, 8), (Jsm.Opcode.JPF, 4), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.PSHN_L, 121), (Jsm.Opcode.MAPJUMP3, 1)]);
        byte[] before = bytes.ToArray();

        Assert.True(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(bytes), "600:talk:0", 0));

        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(Jsm.Opcode.RND)]
    [InlineData(Jsm.Opcode.GETHP)]
    [InlineData(Jsm.Opcode.GETINFO)]
    [InlineData(Jsm.Opcode.PGETINFO)]
    [InlineData(Jsm.Opcode.KEYSCAN)]
    [InlineData(Jsm.Opcode.KEYSCAN2)]
    [InlineData(Jsm.Opcode.KEYON)]
    [InlineData(Jsm.Opcode.PSHI_L)]
    public void DynamicAndUnrepresentedInputProducersRefuseCertification(Jsm.Opcode opcode)
    {
        byte[] bytes = Program([], [], [(opcode, 0)]);
        byte[] before = bytes.ToArray();

        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(bytes), "600:talk:0", 0));

        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(Jsm.Opcode.REQ)]
    [InlineData(Jsm.Opcode.PREQ)]
    public void DynamicRequestSelectorRefusesInsteadOfBroadeningItsDependencyClosure(Jsm.Opcode request)
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHM_B, 720), (request, 0)], []);

        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(bytes), "600:talk:0", 0));
    }

    [Fact]
    public void LiteralRequestChecksTheRequestedScriptAndIgnoresAnUnrequestedGetter()
    {
        byte[] safe = Program([], [], [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.PSHM_B, 720)], [(Jsm.Opcode.GETHP, 0)]);
        byte[] unsafeProgram = Program([], [], [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.GETHP, 0)]);

        Assert.True(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(safe), "600:talk:0", 0));
        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(unsafeProgram), "600:talk:0", 0));
    }

    [Fact]
    public void ConditionalJumpCannotBypassTheLiteralRequestSelector()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0)], []);

        Assert.False(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(bytes), "600:talk:0", 0));
    }

    [Fact]
    public void CanonicalPartyResultMustComeFromItsAdjacentKnownProducer()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ISPARTY, 0), (Jsm.Opcode.PSHI_L, 0)]);

        Assert.True(GameWorld.CanCacheAutoPlayUnavailableSource(JsmProgram.Read(bytes), "600:talk:0", 0));
    }

    [Fact]
    public void ActualSourceCertificateOnlyInspectsTheCurrentAllocation()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNativeUnavailableTests.1");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, document.RootElement.GetProperty("Snapshot").GetString()!);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        byte[] program = world.Field!.Jsm.ToArray();

        Assert.True(world.CanCacheAutoPlayUnavailableSource("125:line:18", 18));

        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(program, world.Field.Jsm);
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [];
        List<ushort> positions = [];
        for (int index = 0; index < scripts.Length; index++)
        {
            positions.Add(checked((ushort)operations.Count));
            operations.Add((Jsm.Opcode.LBL, index));
            operations.AddRange(scripts[index]);
            operations.Add((Jsm.Opcode.RET, 8));
        }
        positions.Add(checked((ushort)operations.Count));
        int offset = 10 + positions.Count * 2;
        byte[] bytes = new byte[offset + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)offset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)(scripts.Length - 1)));
        for (int index = 0; index < positions.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), positions[index]);
        }
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.REQ or Jsm.Opcode.PREQ or Jsm.Opcode.LBL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
