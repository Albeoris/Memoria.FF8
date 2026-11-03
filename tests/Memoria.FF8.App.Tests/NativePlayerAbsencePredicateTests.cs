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
    public void IrrelevantCharacterGuardsRemainUnrepresentedWhilePartyChangesReopenTheirDependencies()
    {
        EventEngine engine = GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0));
        JsonElement before = engine.Capture();
        NativePersistentPredicateRead[] reads = Assert.IsType<NativePersistentPredicateRead[]>(InitializationGuards(engine));
        Assert.DoesNotContain(reads, read => read.Address == 721);
        Assert.False(engine.EventMemory.HasRepresentedExactBytes(721, 1));
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        engine.Scene.Party[1] = 1;
        Assert.Null(InitializationGuards(engine));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(-256)]
    public void LiteralPartyRelevanceUsesTheNativeByteOperand(int character)
    {
        EventEngine engine = GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0));
        Assert.Null(InitializationGuards(engine));
    }

    [Fact]
    public void DynamicOrBypassedLiteralsCannotExcludeAPartyPublisher()
    {
        Assert.Null(InitializationGuards(GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.SETPC, 0))));
        Assert.Null(InitializationGuards(GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JMP, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0))));
    }

    [Fact]
    public void AnIrrelevantBindingTemplateStillCannotOverwriteACollectedGuard()
    {
        EventEngine engine = GuardEngine((Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.POPM_B, 104), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0));
        Assert.Null(InitializationGuards(engine));
    }

    [Fact]
    public void EveryInitializerBranchMustFinishInactiveBeforeItsGuardsCanBeExcluded()
    {
        EventEngine activeBranch = GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0));
        Assert.Null(InitializationGuards(activeBranch));

        EventEngine unusedBranch = GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.JPF, 4), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.UNUSE, 0));
        Assert.NotNull(InitializationGuards(unusedBranch));
    }

    [Fact]
    public void ReactivationAndUnprovedCyclesCannotExcludeInitializerGuards()
    {
        Assert.Null(InitializationGuards(GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0))));
        Assert.Null(InitializationGuards(GuardEngine((Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.JMP, 0))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(256, false)]
    [InlineData(-256, false)]
    [InlineData(255, false)]
    public void OnlyAProvenAbsentPartyTestCanExcludeACounterSideEffect(int query, bool excludes)
    {
        EventEngine engine = GuardEngine(CounterInitializer(query));
        Assert.Equal(excludes, InitializationGuards(engine) is not null);
    }

    [Fact]
    public void EnteringAPartyTestAfterItsLiteralCannotPruneTheCounterWrite()
    {
        Assert.Null(InitializationGuards(GuardEngine(CounterInitializer(1, enterAfterLiteral: true))));
    }

    [Fact]
    public void AnInactiveTemplateCannotHideDependenciesSharedByAnotherInitializer()
    {
        (Jsm.Opcode, int)[] inactive = [(Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0)];
        (Jsm.Opcode, int)[] npcRead = [(Jsm.Opcode.PSHM_W, 720), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 1)];
        Assert.Null(InitializationGuards(GuardEngineFor(inactive, npcRead)));
        (Jsm.Opcode, int)[] npcWrite = [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 721)];
        Assert.Null(InitializationGuards(GuardEngineFor(inactive, npcWrite)));
        Assert.Null(InitializationGuards(GuardEngineForObjects([[inactive], [[], npcRead]])));
    }

    [Fact]
    public void APhysicalNonactorRecordCannotUseTheActorCompactionProof()
    {
        (Jsm.Opcode, int)[] body = [(Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0)];
        Assert.Null(InitializationGuards(GuardEngineForObjects([[body]], doors: true)));
    }

    [Theory]
    [InlineData(194)]
    [InlineData(1025)]
    [InlineData(70000)]
    public void ExcludingAnActorDoesNotAdmitUnsupportedDependencyOwners(int address)
    {
        Assert.Null(InitializationGuards(GuardEngine((Jsm.Opcode.PSHM_B, address), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0))));
    }

    [Fact]
    public void PersistentReadsFollowTheActualRestoredImmutableMemoryOwner()
    {
        EventEngine engine = Create(600, 1);
        EventEngine alternative = Create(600, 0);
        NativePersistentPredicateRead[] reads = [new("PSHM_B", 720, 1), new("PSHSM_B", 721, 1)];
        JsonElement original = engine.Capture();
        string first = Assert.IsType<string>(engine.ReadPersistentPredicateFingerprint(reads));
        string second = Assert.IsType<string>(alternative.ReadPersistentPredicateFingerprint(reads));
        Assert.NotEqual(first, second);

        engine.Restore(alternative.Capture());
        Assert.Equal(second, engine.ReadPersistentPredicateFingerprint(reads));
        engine.Restore(original);
        Assert.Equal(first, engine.ReadPersistentPredicateFingerprint(reads));
        Assert.True(JsonElement.DeepEquals(original, engine.Capture()));
    }

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

    private static NativePersistentPredicateRead[]? InitializationGuards(EventEngine engine)
    {
        object? result = typeof(EventEngine).GetMethod("ReadAbsenceInitializationGuards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(engine, null);
        return result is ImmutableArray<NativePersistentPredicateRead> reads ? reads.ToArray() : null;
    }

    private static EventEngine GuardEngine(params (Jsm.Opcode Opcode, int Parameter)[] body)
    {
        return GuardEngineFor(body);
    }

    private static EventEngine GuardEngineFor(params (Jsm.Opcode Opcode, int Parameter)[][] bodies)
    {
        return GuardEngineForObjects(bodies.Select(body => new[] { body }).ToArray());
    }

    private static EventEngine GuardEngineForObjects((Jsm.Opcode Opcode, int Parameter)[][][] objects, bool doors = false)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [];
        int scriptCount = objects.Sum(group => group.Length);
        int scriptsOffset = 8 + objects.Length * 2;
        int operationsOffset = scriptsOffset + (scriptCount + 1) * 2;
        byte[] bytes = new byte[operationsOffset + objects.SelectMany(group => group).Sum(body => body.Length + 2) * 4];
        bytes[doors ? 1 : 3] = checked((byte)objects.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), checked((ushort)scriptsOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)operationsOffset));
        int scriptIndex = 0;
        for (int index = 0; index < objects.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + index * 2), checked((ushort)((index << 7) | (objects[index].Length - 1))));
            foreach (var body in objects[index])
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + scriptIndex * 2), checked((ushort)operations.Count));
                operations.Add((Jsm.Opcode.LBL, scriptIndex++));
                operations.AddRange(body);
                operations.Add((Jsm.Opcode.RET, 8));
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + scriptCount * 2), checked((ushort)operations.Count));
        for (int index = 0; index < operations.Count; index++)
        {
            int encoded = (int)operations[index].Opcode > sbyte.MaxValue ? (int)operations[index].Opcode : (int)operations[index].Opcode << 24 | operations[index].Parameter & 0xFFFFFF;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        EventEngine engine = new(new GameSession(new GameState()), NativeEventMemory.Empty);
        engine.LoadField(600, bytes, []);
        engine.Scene.Party.Clear();
        engine.Scene.Party.AddRange([0, 255, 255]);
        return engine;
    }

    private static (Jsm.Opcode Opcode, int Parameter)[] CounterInitializer(int query, bool enterAfterLiteral = false)
    {
        List<(Jsm.Opcode, int)> body = [(Jsm.Opcode.PSHM_B, 721), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0)];
        if (enterAfterLiteral)
        {
            body.Add((Jsm.Opcode.JMP, 2));
        }
        body.AddRange([(Jsm.Opcode.PSHN_L, query), (Jsm.Opcode.ISPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 8), (Jsm.Opcode.JPF, 5), (Jsm.Opcode.PSHM_B, 1024), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1024)]);
        return body.ToArray();
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
