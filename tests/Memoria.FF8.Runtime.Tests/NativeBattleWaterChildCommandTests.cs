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
            }, (step, selector) => selector == 0x32 ? NativeBattleWaterChildCommands.Execute(step, new(0x1000, 4, 0x1000), Bank, step.TemporaryFrame.Address + 0x100) : step.WriteTemporaryWord(0x13E, 3));
        }
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

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, int length = 6, uint? incoming = null) => NativeBattleWaterChildCommands.Execute(frame, new(0x1000, length, Pc), Bank, incoming ?? frame.TemporaryFrame.Address + 0x100);
}
