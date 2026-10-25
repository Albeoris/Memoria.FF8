using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterChildCommandTests
{
    private const uint Pc = 0x80001000;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Fact]
    public void FullActiveOrderDoesNotReadOperandOrSlotAndPreservesIncomingEcxOnFailure()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x32);
        frame = frame with { Core = frame.Core.Write(0x2797457, new byte[] { 2 }).MarkUnknown(0x27973EC, 4), Arena = frame.Arena.MarkUnknown(0x1002, 2).MarkUnknown(0x2000, 0x400) };
        uint incoming = frame.TemporaryFrame.Address + 0x100;
        NativeBattleWaterExecutionFrame result = Run(frame, 2, incoming);
        Assert.Equal(incoming, result.ReadTemporaryDword(0xFC));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(frame.Core.Read(0x2797454, 4), result.Core.Read(0x2797454, 4));
        Assert.Equal(0x12345678u, Run(frame, 2, 0x12345678).ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void FirstZeroHeadWinsDespiteOccupiedPayloadAndScalarInheritanceUsesActualDefaults()
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x32, unchecked((ushort)-8)));
        frame = frame with { Arena = frame.Arena.Write(0x2100, Enumerable.Repeat((byte)0xA5, 0x100).ToArray()).Write(0x2100, new byte[4]).MarkUnknown(0x2200, 0x100) };
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x18, 0xFFFF);
        NativeBattleWaterExecutionFrame result = Run(frame, 4);
        Assert.Equal(new byte[] { 0, 1, 0xFF, 0xFF, 0xCC }, result.Core.Read(0x2797454, 5));
        Assert.Equal(0x80000FF8u, WaterSchedulerFixture.ReadSlotDword(result, 1, 0));
        Assert.Equal(new byte[8], result.Arena.Read(0x2104, 8));
        Assert.Equal((ushort)0xFFFF, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x12));
        Assert.Equal((ushort)0x1234, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x14));
        Assert.Equal((ushort)0x4321, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x16));
        Assert.Equal((ushort)0xBEEF, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x48));
        Assert.Equal((byte)6, result.Arena.Read(0x211B, 1)[0]);
        Assert.Equal(0x11223344u, WaterSchedulerFixture.ReadSlotDword(result, 1, 0xB0));
        Assert.Equal(0x55667788u, WaterSchedulerFixture.ReadSlotDword(result, 1, 0xB4));
        Assert.Equal((ushort)0x20, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x92));
        Assert.Equal((ushort)0x8899, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x9A));
        Assert.Equal((ushort)0x66, WaterSchedulerFixture.ReadSlotWord(result, 1, 0xC8));
        Assert.Equal((ushort)0x777, WaterSchedulerFixture.ReadSlotWord(result, 1, 0xCA));
        Assert.Equal(0x00808080u, WaterSchedulerFixture.ReadSlotDword(result, 1, 0xCC));
        Assert.Equal((ushort)0xFFFF, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x8C));
        Assert.Equal((ushort)2, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x8E));
        Assert.Equal((ushort)0xFFFD, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x90));
        Assert.Equal((ushort)100, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x94));
        Assert.Equal((ushort)200, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x96));
        Assert.Equal(unchecked((ushort)-300), WaterSchedulerFixture.ReadSlotWord(result, 1, 0x98));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, WaterSchedulerFixture.ReadCoreDword(result, 0x27973E8));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2100, result.ReadTemporaryDword(0xFC));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(NativeBattleScriptedEffectMemory.Address, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x18, 2)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2200, 1));
    }

    [Fact]
    public void ParentAliasIsInitializedBeforeAnyInheritanceInsteadOfUsingAnOldSnapshot()
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x32, 8));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, 0);
        frame = frame with { Arena = frame.Arena.Write(0x2018, new byte[] { 3 }) };
        NativeBattleWaterExecutionFrame result = Run(frame, 4);
        Assert.Equal(new byte[] { 0, 0, 0xFF }, result.Core.Read(0x2797454, 3));
        Assert.Equal(new byte[24], result.Arena.Read(0x2050, 24));
        Assert.Equal((byte)0, result.Arena.Read(0x2018, 1)[0]);
        Assert.Equal((byte)3, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x14));
        Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x16));
        Assert.Equal(Pc + 8, WaterSchedulerFixture.ReadSlotDword(result, 0, 0));
    }

    [Theory]
    [InlineData(0x26, 0x80000FF8u)]
    [InlineData(0x8B, 0x00000FF8u)]
    public void DirectStreamRetainsOrClearsSignAndOnlyResetsActualWordAndByte(ushort instruction, uint target)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 2, unchecked((ushort)-8));
        frame = frame with { Arena = frame.Arena.Write(0x2000, Enumerable.Repeat((byte)0xA5, 0x100).ToArray()) };
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(target, WaterSchedulerFixture.ReadSlotDword(result, 0, 8));
        Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x10));
        Assert.Equal((byte)0, result.Arena.Read(0x2046, 1)[0]);
        Assert.Equal((ushort)0xA5A5, WaterSchedulerFixture.ReadSlotWord(result, 0, 0xE));
        Assert.Equal((byte)0xA5, result.Arena.Read(0x2045, 1)[0]);
        Assert.Equal(2u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(Pc + 6, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AutomaticStreamUsesFirstZeroOfThreeRawFieldsIncludingWaitPairAlias(int selected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x26, 0, 8);
        for (int index = 1; index < selected; index++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 4 * index, 0x55555555);
        }
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 0xABCD);
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal((uint)selected, result.ReadTemporaryDword(0xF0));
        Assert.Equal(Pc + 8, WaterSchedulerFixture.ReadSlotDword(result, 0, 4 * selected));
        Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0xC + 2 * selected));
        if (selected == 3)
        {
            Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x12));
        }
    }

    [Fact]
    public void AutomaticFullStreamsDeferRelativeAndAllStreamStateWrites()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8B, 0, 0);
        for (int index = 1; index <= 3; index++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 4 * index, 1);
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame result = Run(frame, 4);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(frame.ReadTemporaryDword(0xF0), result.ReadTemporaryDword(0xF0));
        Assert.Equal(Pc + 6, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => Run(Fixture(0x26, 0xFFFF, 2)));
        Assert.Throws<NotSupportedException>(() => Run(Fixture(0x26, 64, 2)));
    }

    [Fact]
    public void ActiveAppendIsVisibleInSameSchedulerPassAndJsonContinuationMatches()
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x32, 0x10));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, 0x1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 3);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797634, 3);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        List<uint> fetched = [];
        NativeBattleWaterExecutionFrame a = Pass(frame, fetched);
        NativeBattleWaterExecutionFrame b = Pass(restored, []);
        Assert.Equal(new uint[] { 0x1000, 0x1004, 0x1010 }, fetched);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));

        static NativeBattleWaterExecutionFrame Pass(NativeBattleWaterExecutionFrame current, List<uint> fetched)
        {
            return NativeBattleWaterScriptScheduler.RunScriptPass(false, current, (_, pc) =>
            {
                fetched.Add(pc);
                return pc == 0x1000 ? (ushort)0x32 : (ushort)9;
            }, (step, selector) => selector == 0x32 ? NativeBattleWaterChildCommands.Execute(step, new(0x1000, 4, 0x1000), Bank, step.TemporaryFrame.Address + 0x100, UpdateSelected) : step.WriteTemporaryWord(0x13E, 3));
        }
    }

    [Fact]
    public void Child9CUsesPreviousPhysicalSlotAncestryInsteadOfCurrentParent()
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x9C, 8));
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 0xA1B2);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x14, 0xC3D4);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0, 1, 0xFF, 0xFF }) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Execute9C(frame, new(0x1000, 4, Pc), Bank, frame.TemporaryFrame.Address + 0x100, UpdateSelected);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2200, result.ReadTemporaryDword(0xFC));
        Assert.Equal((ushort)0xA1B2, WaterSchedulerFixture.ReadSlotWord(result, 2, 0x14));
        Assert.Equal((ushort)0xC3D4, WaterSchedulerFixture.ReadSlotWord(result, 2, 0x16));
        Assert.Equal((ushort)0x1234, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x12));
        Assert.Equal((ushort)100, WaterSchedulerFixture.ReadSlotWord(result, 2, 0x94));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x9C, result.ReadTemporaryWord(0x14A));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Child9CFullCapacityStillCopiesThroughActualTemporaryCoreOrArenaPointer(int owner)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x9C);
        frame = frame with { Core = frame.Core.Write(0x2797457, new byte[] { 2 }).MarkUnknown(0x27973EC, 4), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        uint pointer;
        if (owner == 0)
        {
            pointer = frame.TemporaryFrame.Address + 0x100;
            frame = frame.WriteTemporaryWord(0x12, 0x1234).WriteTemporaryWord(0x14, 0x5678);
        }
        else if (owner == 1)
        {
            pointer = NativeBattleScriptedEffectMemory.Address + 0x100;
            frame = WaterSchedulerFixture.CoreWord(frame, pointer - 0xEE, 0x1234);
            frame = WaterSchedulerFixture.CoreWord(frame, pointer - 0xEC, 0x5678);
        }
        else
        {
            pointer = NativeBattleEffectNodeMemory.ArenaAddress + 0x5000;
            frame = frame with { Arena = frame.Arena.Write(0x4F12, new byte[] { 0x34, 0x12, 0x78, 0x56 }).MarkUnknown(0x4F16, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Execute9C(frame, new(0x1000, 2, Pc), Bank, pointer, UpdateSelected);
        ushort first = owner == 0 ? result.ReadTemporaryWord(0x114) : owner == 1 ? BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(pointer + 0x14, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(0x5014, 2));
        ushort second = owner == 0 ? result.ReadTemporaryWord(0x116) : owner == 1 ? BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(pointer + 0x16, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(0x5016, 2));
        Assert.Equal((ushort)0x1234, first);
        Assert.Equal((ushort)0x5678, second);
        Assert.Equal(pointer, result.ReadTemporaryDword(0xFC));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1002, 1));
    }

    [Fact]
    public void Child9CFullCapacityDoesNotReplaceAnUnknownReturnedAddress()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x9C);
        frame = frame with { Core = frame.Core.Write(0x2797457, new byte[] { 2 }) };
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterChildCommands.Execute9C(frame, new(0x1000, 2, Pc), Bank, 0x12345678, UpdateSelected));
    }

    [Fact]
    public void ExtractedSpawnRetainsOrdinary32AndSnapshot9CSemantics()
    {
        NativeBattleWaterExecutionFrame ordinary = Parent(Fixture(0x32, 8));
        Assert.Equal(JsonSerializer.Serialize(Run(ordinary)), JsonSerializer.Serialize(NativeBattleWaterChildCommands.Spawn(ordinary, new(0x1000, 6, Pc), Bank, ordinary.TemporaryFrame.Address + 0x100, UpdateSelected)));
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x9C, 8));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame a = NativeBattleWaterChildCommands.Execute9C(frame, new(0x1000, 4, Pc), Bank, frame.TemporaryFrame.Address + 0x100, UpdateSelected);
        NativeBattleWaterExecutionFrame b = NativeBattleWaterChildCommands.Execute9C(restored, new(0x1000, 4, Pc), Bank, restored.TemporaryFrame.Address + 0x100, UpdateSelected);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    [Fact]
    public void Child5DRepeatsTheSameSpawnOperandAndWritesActorAfterItsUpdate()
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x5D, 8));
        uint header = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797624, frame.TemporaryFrame.Address);
        frame = frame with { Core = frame.Core.Write(header + 0x41, new byte[] { 3 }).Write(header + 0x48, new byte[] { 5, 2, 7 }) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Execute5D(frame, new(0x1000, 4, Pc), Bank, frame.TemporaryFrame.Address + 0x100, UpdateSelected);
        for (int child = 1; child <= 3; child++)
        {
            Assert.Equal(Pc + 8, WaterSchedulerFixture.ReadSlotDword(result, child, 0));
            Assert.Equal((ushort)100, WaterSchedulerFixture.ReadSlotWord(result, child, 0x94));
        }
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, result.Core.Read(0x2797454, 4));
        Assert.Equal((byte)5, result.Arena.Read(0x211B, 1)[0]);
        Assert.Equal((byte)2, result.Arena.Read(0x221B, 1)[0]);
        Assert.Equal((byte)7, result.Arena.Read(0x231B, 1)[0]);
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x5D, result.ReadTemporaryWord(0x14A));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Child5DNonpositiveSignedCountDoesNotReadActorArrayOperandOrSpawnOwners(byte count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x5D);
        uint header = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = frame with { Core = frame.Core.Write(header + 0x41, new byte[] { count }).MarkUnknown(header + 0x48, 16).MarkUnknown(0x2797624, 4).MarkUnknown(0x27973EC, 4), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Execute5D(frame, new(0x1000, 2, Pc), Bank, 0, UpdateSelected);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Child5DFullCapacityCarriesActualClobberedEcxBetweenIterations()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x5D);
        uint header = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797624, frame.TemporaryFrame.Address);
        frame = frame with { Core = frame.Core.Write(header + 0x41, new byte[] { 3 }).Write(header + 0x48, new byte[] { 5, 7, 9 }).Write(0x2797457, new byte[] { 2 }).MarkUnknown(0x27973EC, 4), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Execute5D(frame, new(0x1000, 2, Pc), Bank, frame.TemporaryFrame.Address + 0x100, UpdateSelected);
        Assert.Equal((byte)5, result.ReadTemporaryByte(0x11B));
        Assert.Equal((byte)7, result.ReadTemporaryByte(0x20));
        Assert.Equal((byte)9, result.ReadTemporaryByte(0x22));
        Assert.Equal(frame.TemporaryFrame.Address + 7, result.ReadTemporaryDword(0xFC));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
    }

    [Fact]
    public void Child5DStandaloneSnapshotKeepsTheSamePhysicalChildrenAndActorIds()
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x5D, 8));
        uint header = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797624, frame.TemporaryFrame.Address);
        frame = frame with { Core = frame.Core.Write(header + 0x41, new byte[] { 2 }).Write(header + 0x48, new byte[] { 4, 6 }) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame first = NativeBattleWaterChildCommands.Execute5D(frame, new(0x1000, 4, Pc), Bank, frame.TemporaryFrame.Address + 0x100, UpdateSelected);
        NativeBattleWaterExecutionFrame second = NativeBattleWaterChildCommands.Execute5D(restored, new(0x1000, 4, Pc), Bank, restored.TemporaryFrame.Address + 0x100, UpdateSelected);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChildInheritsUnknownMatrixPaddingWithoutConsumingIt(bool transactional)
    {
        NativeBattleWaterExecutionFrame frame = Parent(Fixture(0x32, 8));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20B2, 2).MarkUnknown(0x20B5, 1) };
        using NativeBattleEffectArenaTransaction? transaction = transactional ? new(frame.Arena) : null;
        if (transaction is not null)
        {
            frame = frame with { Arena = transaction.View };
            frame.Arena.Write(0x20B0, new byte[] { 0x78, 0x56 });
        }
        NativeBattleWaterExecutionFrame result = Run(frame, 4);
        if (transaction is not null)
        {
            result = result with { Arena = transaction.Freeze() };
        }
        var source = result.Arena.ReadSnapshot(0x20B0, 8);
        var child = result.Arena.ReadSnapshot(0x21B0, 8);
        Assert.Equal(source.Data, child.Data);
        Assert.Equal(source.Known, child.Known);
        Assert.Equal(new bool[] { true, true, false, false, true, false, true, true }, child.Known);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x21B0, 4));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(child.Data, restored.Arena.ReadSnapshot(0x21B0, 8).Data);
        Assert.Equal(child.Known, restored.Arena.ReadSnapshot(0x21B0, 8).Known);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, ushort first = 0, ushort second = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0, 0xFF, 0xFF, 0xFF, 0xCC }) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973C0, 0x2797628);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 4);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x1C, 0x8899);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address, 0xFFFF);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797634, 0x66);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797636, 0x777);
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), first);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), second);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }

    private static NativeBattleWaterExecutionFrame Parent(NativeBattleWaterExecutionFrame frame)
    {
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, Pc);
        int[] position = [-1, 2, -3, 100, 200, -300];
        for (int lane = 0; lane < position.Length; lane++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50 + 4 * lane, unchecked((uint)(position[lane] << 16)));
        }
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 0x1234);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 0x4321);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x48, 0xBEEF);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB0, 0x11223344);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB4, 0x55667788);
        return frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 6 }) };
    }

    private static ImmutableArray<byte> ActiveOrder(NativeBattleWaterExecutionFrame frame)
    {
        //The unused suffix is a transient argument placeholder, not a claim about physical bytes beyond FF.
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int index = 0; index < order.Length; index++)
        {
            order[index] = frame.Core.Read(0x2797454 + (uint)index, 1)[0];
            if (order[index] == 0xFF)
            {
                return order.ToImmutableArray();
            }
        }
        return order.ToImmutableArray();
    }

    private static NativeBattleWaterExecutionFrame UpdateSelected(NativeBattleWaterExecutionFrame frame, byte kind)
    {
        int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, Bank);
        return frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, Bank, index, kind is 3 or 5 or 7 ? ActiveOrder(frame) : []) };
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, int length = 6, uint? incoming = null) => NativeBattleWaterChildCommands.Execute(frame, new(0x1000, length, Pc), Bank, incoming ?? frame.TemporaryFrame.Address + 0x100, UpdateSelected);
}
