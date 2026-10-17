using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayConditionDependencyTests
{
    [Fact]
    public void OriginalConversationCountersDoNotHideTheOtherAuthoredPredicates()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayConditionTests.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(315, world.Events.FieldId);
        JsmProgram program = JsmProgram.Read(world.Field!.Jsm);
        foreach (int actor in new[] { 3, 4, 5 })
        {
            EventScript script = program.Objects[actor].Scripts[2];
            Assert.Contains(script.ReachableOperations, index => program.Operations[index].Opcode == Jsm.Opcode.PSHM_B && program.Operations[index].Parameter == 728);
            int[] reads = GameWorld.DiscoverAutoPlayConditionReads(program, $"315:talk:{actor}", actor);
            Assert.DoesNotContain(reads, index => program.Operations[index].Parameter == 728);
            Assert.Contains(reads, index => program.Operations[index].Opcode == Jsm.Opcode.PSHM_W && program.Operations[index].Parameter == 256);
            if (actor == 5)
            {
                Assert.Contains(reads, index => program.Operations[index].Opcode == Jsm.Opcode.PSHM_B && program.Operations[index].Parameter == 1024);
            }
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Theory]
    [InlineData(Jsm.Opcode.PSHM_B, Jsm.Opcode.POPM_B, 1, 255, 0)]
    [InlineData(Jsm.Opcode.PSHSM_B, Jsm.Opcode.POPM_B, 1, 255, 0)]
    [InlineData(Jsm.Opcode.PSHM_W, Jsm.Opcode.POPM_W, 2, 65535, 0)]
    [InlineData(Jsm.Opcode.PSHSM_W, Jsm.Opcode.POPM_W, 2, 65535, 0)]
    [InlineData(Jsm.Opcode.PSHM_L, Jsm.Opcode.POPM_L, 4, int.MaxValue, int.MinValue)]
    [InlineData(Jsm.Opcode.PSHSM_L, Jsm.Opcode.POPM_L, 4, int.MaxValue, 0x800000)]
    public void SelfUpdatePreservesNativeProjectionWithoutReopeningButAnotherEventsPredicateChanges(Jsm.Opcode read, Jsm.Opcode store, int width, int initial, int expected)
    {
        (Jsm.Opcode, int)[] update = [(read, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (store, 1300)];
        Jsm.Opcode predicateRead = read == Jsm.Opcode.PSHSM_L ? Jsm.Opcode.PSHM_L : read;
        byte[] bytes = Program(update, [], update, [(predicateRead, 1300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.POPM_B, 1400)]);
        JsmProgram program = JsmProgram.Read(bytes);
        int[] updates = GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:0", 0);
        int[] predicates = GameWorld.DiscoverAutoPlayConditionReads(program, "600:push:0", 0);
        Assert.Empty(updates);
        Assert.Equal(1300, program.Operations[Assert.Single(predicates)].Parameter);
        NativeEventMemory memory = NativeEventMemory.Empty.Write(1300, width, NativeScalarValue.Exact(initial));
        EventEngine engine = new(new GameSession(new GameState()), memory);
        engine.LoadField(600, bytes, []);
        string beforeUpdate = engine.ReadPredicateState(updates).Fingerprint;
        string beforePredicate = engine.ReadPredicateState(predicates).Fingerprint;
        AutoPlayPlanner planner = new();
        Observe(planner, beforeUpdate);
        planner.BeginEvent("600:0", "talk");
        planner.Completed("600:0", "talk", exitSignature: beforeUpdate);

        engine.Step();

        Assert.Null(engine.LastFault);
        Assert.Equal(expected, engine.EventMemory.Read(1300, width).RequireExact());
        string afterUpdate = engine.ReadPredicateState(updates).Fingerprint;
        Assert.Equal(beforeUpdate, afterUpdate);
        Assert.NotEqual(beforePredicate, engine.ReadPredicateState(predicates).Fingerprint);
        Observe(planner, afterUpdate);
        Assert.Null(planner.Plan("600:0"));
    }

    [Fact]
    public void AnotherReadOfTheUpdatedCellInTheSameEventRemainsADependency()
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1300), (Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.POPM_B, 1400)]);
        JsmProgram program = JsmProgram.Read(bytes);
        Assert.Equal(9, Assert.Single(GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:0", 0)));
    }

    [Theory]
    [InlineData(Jsm.Opcode.PSHM_B, Jsm.Opcode.POPM_W, 1300, 0)]
    [InlineData(Jsm.Opcode.PSHM_W, Jsm.Opcode.POPM_B, 1300, 0)]
    [InlineData(Jsm.Opcode.PSHM_B, Jsm.Opcode.POPM_B, 1301, 0)]
    [InlineData(Jsm.Opcode.PSHM_B, Jsm.Opcode.POPM_B, 1300, 1)]
    [InlineData(Jsm.Opcode.PSHM_B, Jsm.Opcode.POPM_B, 1300, 9)]
    public void DifferentWidthsAddressesOrCalculationsRemainDependencies(Jsm.Opcode read, Jsm.Opcode store, int address, int calculation)
    {
        byte[] bytes = Program([], [], [(read, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, calculation), (store, address)]);
        Assert.Single(GameWorld.DiscoverAutoPlayConditionReads(JsmProgram.Read(bytes), "600:talk:0", 0));
    }

    [Fact]
    public void NonliteralAndEscapedOperandsRemainDependencies()
    {
        byte[] dynamicOperand = Program([], [], [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHM_B, 1301), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1300)]);
        Assert.Equal(2, GameWorld.DiscoverAutoPlayConditionReads(JsmProgram.Read(dynamicOperand), "600:talk:0", 0).Length);
        byte[] consumedByCommand = Program([], [], [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.POPM_B, 1300)]);
        Assert.Single(GameWorld.DiscoverAutoPlayConditionReads(JsmProgram.Read(consumedByCommand), "600:talk:0", 0));
        byte[] unknownFlow = Program([], [], [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1300)]);
        Assert.Single(GameWorld.DiscoverAutoPlayConditionReads(JsmProgram.Read(unknownFlow), "600:talk:0", 0));
    }

    [Theory]
    [InlineData(Jsm.Opcode.REQ)]
    [InlineData(Jsm.Opcode.PREQ)]
    public void RequestedPredicateOfTheUpdatedCellRemainsADependency(Jsm.Opcode request)
    {
        byte[] bytes = Program([], [], [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_B, 1300), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 3), (request, 0)], [(Jsm.Opcode.PSHM_B, 1300), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.POPM_B, 1400)]);
        JsmProgram program = JsmProgram.Read(bytes);
        int read = Assert.Single(GameWorld.DiscoverAutoPlayConditionReads(program, "600:talk:0", 0));
        Assert.Contains(read, program.Objects[0].Scripts[3].ReachableOperations.AsEnumerable());
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
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.CAL or Jsm.Opcode.REQ or Jsm.Opcode.PREQ or Jsm.Opcode.LBL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + index * 4), encoded);
        }
        return bytes;
    }
}
