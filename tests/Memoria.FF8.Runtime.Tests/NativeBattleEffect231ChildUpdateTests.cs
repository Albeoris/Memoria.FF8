using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect231ChildUpdateTests
{
    private const uint Pc = 0x80001000;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Fact]
    public void InheritedUpdate4RunsWithChildCurrentAndRetainsFullFrameBeforeCacheAndParentRestore()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        int calls = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Spawn(frame, new(0x1000, 4, Pc), Bank, 0x12345678, Update);
        Assert.Equal(1, calls);
        Assert.Equal(Address(0x2000), WaterSchedulerFixture.ReadCoreDword(result, 0x27973E8));
        Assert.Equal(Address(0x2100), result.ReadTemporaryDword(0xFC));
        Assert.Equal(0xAABBCCDDu, result.ReadTemporaryDword(0xF8));
        Assert.Equal(0x10203040u, WaterSchedulerFixture.ReadCoreDword(result, 0x27973C4));
        Assert.Equal((ushort)47, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x8C));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((byte)4, result.Arena.Read(0x2118, 1)[0]);
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 1, 0xA0));

        NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame child, byte kind)
        {
            calls++;
            Assert.Equal((byte)4, kind);
            Assert.Equal(Address(0x2100), WaterSchedulerFixture.ReadCoreDword(child, 0x27973E8));
            Assert.Equal(Pc, WaterSchedulerFixture.ReadCoreDword(child, 0x2797450));
            Assert.Equal(new byte[] { 0, 1, 0xFF }, child.Core.Read(0x2797454, 3));
            Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(child, 1, 0x8C));
            child = NativeBattleCinematicBetweenUpdate4.Update(child, (_, _) => throw new InvalidOperationException("The new child has no reference block."));
            child = WaterSchedulerFixture.SlotDword(child, 1, 0x50, 47u << 16);
            child = WaterSchedulerFixture.CoreDword(child, 0x27973C4, 0x10203040);
            return child.WriteTemporaryDword(0xF8, 0xAABBCCDD).WriteTemporaryDword(0xFC, 0x11223344);
        }
    }

    [Fact]
    public void FullBankPreservesFailureEcxWithoutCallingUpdaterOrReadingOperand()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Core = frame.Core.Write(0x2797457, [2]), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterChildCommands.Spawn(frame, new(0x1000, 2, Pc), Bank, 0x12345678, (_, _) => throw new InvalidOperationException("A failed birth must not update a child."));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(0x12345678u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(Address(0x2000), WaterSchedulerFixture.ReadCoreDword(result, 0x27973E8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContactAndLineageFactoriesForwardTheActualUpdaterAndPreserveJsonContinuation(bool contact)
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x41, [1]).Write(NativeBattleScriptedEffectMemory.Address + 0x48, [6]) };
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        int calls = 0;
        NativeBattleWaterExecutionFrame expected = Run(frame);
        NativeBattleWaterExecutionFrame actual = Run(restored);
        Assert.Equal(2, calls);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
        Assert.Equal(Address(0x2000), WaterSchedulerFixture.ReadCoreDword(actual, 0x27973E8));
        Assert.Equal(Address(0x2100), actual.ReadTemporaryDword(0xFC));
        Assert.Equal(0xAABBCCDDu, actual.ReadTemporaryDword(0xF8));
        if (contact)
        {
            Assert.Equal((byte)6, actual.Arena.Read(0x211B, 1)[0]);
        }

        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame input) => contact
            ? NativeBattleWaterChildCommands.Execute5D(input, new(0x1000, 4, Pc), Bank, 0x12345678, Update)
            : NativeBattleWaterChildCommands.Execute9C(input, new(0x1000, 4, Pc), Bank, 0x12345678, Update);

        NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame child, byte kind)
        {
            calls++;
            Assert.Equal((byte)4, kind);
            Assert.Equal(Address(0x2100), WaterSchedulerFixture.ReadCoreDword(child, 0x27973E8));
            return NativeBattleCinematicBetweenUpdate4.Update(child, (_, _) => throw new InvalidOperationException()).WriteTemporaryDword(0xF8, 0xAABBCCDD);
        }
    }

    private static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x32);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973C0, 0x2797628);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 4);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, Pc);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, 11u << 16);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xA0, Address(0x3000));
        byte[] instruction = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(instruction, 0x32);
        BinaryPrimitives.WriteInt16LittleEndian(instruction.AsSpan(2), 8);
        return frame with { Arena = frame.Arena.Write(0x1000, instruction).Write(0x2018, [4]).MarkUnknown(0x3000, 4) };
    }

    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
}
