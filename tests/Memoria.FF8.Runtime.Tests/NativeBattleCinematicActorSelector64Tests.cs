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
}
