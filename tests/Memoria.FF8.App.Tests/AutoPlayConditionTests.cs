using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayConditionTests
{
    [Fact]
    public void ChangedPartyAndExecutedFlagReopenAnEventWithoutCreatingGraphVisits()
    {
        byte[] program = Program(
            [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 1300)],
            [],
            [(Jsm.Opcode.PSHM_B, 1300)]);
        GameSession session = new(new GameState());
        EventEngine engine = new(session);
        engine.LoadField(600, program, []);
        engine.Scene.Party[0] = 0;
        int[] reads = GameWorld.DiscoverAutoPlayConditionReads(JsmProgram.Read(program), "600:talk:0", 0);
        string initial = engine.ReadPredicateState(reads).Fingerprint;
        AutoPlayPlanner planner = new();
        Observe(planner, initial);
        planner.BeginEvent("600:0", "talk");
        planner.Completed("600:0", "talk", exitSignature: initial);
        Assert.Null(planner.Plan("600:0"));

        // These declared party slots test observation; they do not claim native menu execution authority.
        engine.Scene.Party[1] = 1;
        string partyChanged = engine.ReadPredicateState(reads).Fingerprint;
        Observe(planner, partyChanged);
        Assert.NotNull(planner.Plan("600:0"));
        planner.BeginEvent("600:0", "talk");
        planner.Completed("600:0", "talk", exitSignature: partyChanged);

        // Execute an original synthetic init script; scheduler inspection never writes the flag itself.
        engine.Step();
        Assert.Null(engine.LastFault);
        string after = engine.ReadPredicateState(reads).Fingerprint;
        Assert.NotEqual(partyChanged, after);
        Observe(planner, after);
        Assert.NotNull(planner.Plan("600:0"));
        Assert.Single(planner.State.Visits);
        Assert.True(Assert.Single(planner.State.Visits[0].Events).Completed);
    }

    [Fact]
    public void EntryAndExitConsumptionSurvivesRestoreAndStopsSelfWrittenTokens()
    {
        AutoPlayPlanner planner = new();
        Observe(planner, "token-before");
        planner.BeginEvent("600:0", "talk");
        planner.Completed("600:0", "talk", exitSignature: "token-after");
        AutoPlayPlannerState restored = JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!;
        planner = new AutoPlayPlanner(restored);
        foreach (string signature in new[] { "token-after", "token-before", "token-after" })
        {
            Observe(planner, signature);
            Assert.Null(planner.Plan("600:0"));
        }
        AutoPlayEventCoverage coverage = Assert.Single(Assert.Single(planner.State.Visits).Events);
        Assert.Equal(new[] { "token-before" }, coverage.Conditions!.ConsumedEntries);
        Assert.Equal(new[] { "token-after" }, coverage.Conditions.ConsumedExits);
        Assert.True(coverage.Completed);
        Observe(planner, "external-change");
        Assert.NotNull(planner.Plan("600:0"));
    }

    [Fact]
    public void LegacyCompletedEventsLearnCurrentBaselineWithoutBlindReplay()
    {
        AutoPlayPlannerState legacy = JsonSerializer.Deserialize<AutoPlayPlannerState>("{\"Visits\":[{\"Visit\":{\"Key\":\"600:0\",\"Region\":\"region\"},\"Events\":[{\"Event\":{\"Id\":\"talk\",\"Kind\":0},\"Available\":true,\"Completed\":true}]}]}")!;
        AutoPlayPlanner planner = new(legacy);
        Observe(planner, "current");
        Assert.Null(planner.Plan("600:0"));
        AutoPlayConditionCoverage conditions = legacy.Visits[0].Events[0].Conditions!;
        Assert.Empty(conditions.ConsumedEntries);
        Assert.Equal(new[] { "current" }, conditions.ConsumedExits);
        Observe(planner, "naturally-changed");
        Assert.NotNull(planner.Plan("600:0"));
    }

    [Fact]
    public void PassiveReadsPreserveTheSnapshotAndDeferUnknownOrVolatileOwners()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(1300, 1, NativeScalarValue.Exact(255));
        EventEngine engine = new(GameSession.CreateColdProcess(), memory);
        engine.LoadField(600, Program([(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHSM_B, 1300), (Jsm.Opcode.PSHM_L, 108), (Jsm.Opcode.PSHM_B, 216)]), []);
        JsonElement before = engine.Capture();
        int callbacks = 0;
        engine.OperationCompleted += () => callbacks++;
        EventPredicateState state = engine.ReadPredicateState([1, 2, 3, 4]);
        Assert.Equal(new[] { 255, -1 }, state.Values.Select(value => value.Value));
        Assert.Equal(new[] { 3, 4 }, state.DeferredOperations);
        Assert.Equal(0, callbacks);
        Assert.Null(engine.LastFault);
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        Assert.Equal(state.Fingerprint, engine.ReadPredicateState([4, 3, 2, 1]).Fingerprint);
    }

    [Theory]
    [InlineData(1300, 1, 255)]
    [InlineData(1299, 2, 65280)]
    [InlineData(1297, 4, -16777216)]
    public void ExactCellLookupIncludesTheLastByteOfEverySupportedCellWidth(int start, int width, int value)
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(start, width, NativeScalarValue.Exact(value));
        EventEngine engine = new(new GameSession(new GameState()), memory);
        engine.LoadField(600, Program([(Jsm.Opcode.PSHM_B, 1300)]), []);
        Assert.Equal(255, Assert.Single(engine.ReadPredicateState([1]).Values).Value);
    }

    [Fact]
    public void PersistentPreviousFieldOwnerUsesItsActualWidthWithoutPublishingOrWidening()
    {
        EventEngine engine = new(new GameSession(new GameState()));
        engine.LoadField(600, Program([(Jsm.Opcode.PSHM_W, 84), (Jsm.Opcode.PSHSM_W, 84), (Jsm.Opcode.PSHM_B, 84)]), [], previousFieldId: ushort.MaxValue);
        JsonElement before = engine.Capture();
        EventPredicateState state = engine.ReadPredicateState([1, 2, 3]);
        Assert.Equal(new[] { 65535, -1 }, state.Values.Select(value => value.Value));
        Assert.Equal(new[] { 3 }, state.DeferredOperations);
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        Assert.Null(engine.LastFault);
    }

    [Theory]
    [InlineData(Jsm.Opcode.REQ, 3)]
    [InlineData(Jsm.Opcode.PREQ, 3)]
    public void DependencyClosureIncludesOnlyTheRequestedOriginalScript(Jsm.Opcode request, int label)
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, label), (request, 0)], [(Jsm.Opcode.PSHM_W, 1300)], [(Jsm.Opcode.PSHM_B, 1310)]);
        JsmProgram program = JsmProgram.Read(bytes);
        int[] reads = GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:0", 0);
        Assert.Equal(1300, program.Operations[Assert.Single(reads)].Parameter);
    }

    private static void Observe(AutoPlayPlanner planner, string signature)
    {
        planner.Observe(new AutoPlayVisit("600:0", "region"), [new AutoPlayEvent("talk", AutoPlayEventKind.Interaction, ConditionSignature: signature)]);
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
