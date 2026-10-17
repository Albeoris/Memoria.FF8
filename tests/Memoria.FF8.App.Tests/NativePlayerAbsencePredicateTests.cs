using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerAbsencePredicateTests
{
    [Fact]
    public void PersistentFingerprintSurvivesAnotherFieldAndDistinguishesChangedPartyAndFlags()
    {
        EventEngine destination = Create(600, 1);
        EventEngine source = Create(601, 1);
        ImmutableArray<NativePersistentPredicateRead> reads = [new("PSHM_B", 720, 1), new("PSHSM_B", 721, 1)];
        JsonElement before = destination.Capture();
        string fingerprint = Assert.IsType<string>(destination.ReadPersistentPredicateFingerprint(reads));

        Assert.Equal(fingerprint, source.ReadPersistentPredicateFingerprint(reads.Reverse().Concat(reads).ToArray()));
        Assert.True(JsonElement.DeepEquals(before, destination.Capture()));
        Assert.NotEqual(fingerprint, Create(601, 0).ReadPersistentPredicateFingerprint(reads));
        source.Scene.Party[1] = 1;
        Assert.NotEqual(fingerprint, source.ReadPersistentPredicateFingerprint(reads));
        NativePlayerAbsenceReceipt receipt = new(1, 600, new string('A', 64), reads, fingerprint);
        NativePlayerAbsenceReceipt restored = JsonSerializer.Deserialize<NativePlayerAbsenceReceipt>(JsonSerializer.Serialize(receipt))!;
        Assert.Equal(receipt.Fingerprint, destination.ReadPersistentPredicateFingerprint(restored.Reads));
    }

    [Theory]
    [InlineData("PSHM_B", 1024, 1)]
    [InlineData("PSHM_B", 194, 1)]
    [InlineData("PSHM_W", 84, 2)]
    [InlineData("PSHM_B", 1300, 1)]
    [InlineData("PSHM_B", 720, 2)]
    [InlineData("NOP", 720, 1)]
    public void LocalUnrepresentedAndMalformedReadsDoNotCreatePortableGuards(string opcode, int address, int width)
    {
        EventEngine engine = Create(600, 1);
        JsonElement before = engine.Capture();

        Assert.Null(engine.ReadPersistentPredicateFingerprint([new NativePersistentPredicateRead(opcode, address, width)]));

        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
    }

    [Fact]
    public void SyntheticUninitializedFieldCannotCertifyNativePlayerAbsence()
    {
        EventEngine engine = Create(600, 1);
        JsonElement before = engine.Capture();

        Assert.Null(engine.InspectNativePlayerAbsence());

        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
    }

    private static EventEngine Create(int field, byte flag)
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(720, 1, NativeScalarValue.Exact(flag)).Write(721, 1, NativeScalarValue.Exact(254));
        EventEngine engine = new(new GameSession(new GameState()), memory);
        engine.LoadField(field, Program(field == 601), []);
        engine.Scene.Party[0] = 0;
        engine.Scene.Party[1] = 255;
        engine.Scene.Party[2] = 255;
        return engine;
    }

    private static byte[] Program(bool extraOperation)
    {
        int count = extraOperation ? 3 : 2;
        byte[] bytes = new byte[14 + count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)count));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), (int)Jsm.Opcode.LBL << 24);
        if (extraOperation)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), (int)Jsm.Opcode.NOP);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14 + (count - 1) * 4), (int)Jsm.Opcode.RET << 24 | 8);
        return bytes;
    }
}
