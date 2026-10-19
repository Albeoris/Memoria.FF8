using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorSelector64Tests
{
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);

    [Theory]
    [InlineData(0x0064, 7, 2)]
    [InlineData(0x1064, 0xEF, 4)]
    [InlineData(0x8064, 5, 2)]
    [InlineData(0x9064, 5, 2)]
    public void SelectorFlagsPreserveSourcePriorityByteWidthAndPcWidth(ushort instruction, byte expected, uint width)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0xC0, 0x1D99A78);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x48, [7]), Arena = frame.Arena.Write(0x1000, [0x64, 0x10, 0xEF, 0xAB]).Write(0x201A, [0xCC, 0, 0xDD]) };
        if ((instruction & 0x1000) == 0 || (instruction & 0x8000) != 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        int reads = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, new(0x1000, 4, Address(0x1000)), (_, pointer, count) =>
        {
            reads++;
            Assert.Equal(0x1D99A78u, pointer);
            Assert.Equal(1, count);
            return [5];
        });
        Assert.Equal(expected, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal((byte)0xCC, result.Arena.Read(0x201A, 1)[0]);
        Assert.Equal((byte)0xDD, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal(Address(0x1000) + width, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((instruction & 0x8000) != 0 ? 1 : 0, reads);
    }

    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 4, 0x1000);

    [Theory]
    [InlineData(0x8064)]
    [InlineData(0x9064)]
    public void ActionModeTakesPrecedenceAndReadsOnlyTheActualParticipantByte(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 4) };
        uint manager = WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0xC0, 0x30000000);
        List<(uint Address, int Length)> reads = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, (_, address, length) =>
        {
            reads.Add((address, length));
            return [0xFE];
        });
        Assert.Equal(new[] { (0x30000000u, 1) }, reads);
        Assert.Equal((byte)0xFE, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void InlineModeConsumesOneByteButAdvancesFourAndDoesNotClampTheActor()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1064);
        frame = frame with { Arena = frame.Arena.Write(0x1002, [0xFF]).MarkUnknown(0x1003, 1) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal((byte)0xFF, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void DefaultModeReadsTheCachedOwnerByteWithoutAnInlineOperand()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x64);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = frame with { Core = frame.Core.Write(owner + 0x48, [9]), Arena = frame.Arena.MarkUnknown(0x1000, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorSelector64.Execute(frame, Program, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal((byte)9, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
    }
}
