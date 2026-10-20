namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223Branch55Tests
{
    [Theory]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(0, 1, 0, 0, true)]
    [InlineData(1, 0, 0, 0, false)]
    [InlineData(1, 1, 0, 0, true)]
    [InlineData(1, 255, 255, 0, false)]
    [InlineData(2, 0, 0, 0, false)]
    [InlineData(2, 0, 0, 1, true)]
    [InlineData(2, 0, 0, 2, false)]
    [InlineData(2, 0, 0, 3, false)]
    [InlineData(3, 0, 0, 1, false)]
    [InlineData(3, 0, 0, 2, true)]
    [InlineData(3, 0, 0, 3, true)]
    [InlineData(127, 255, 0, 0, true)]
    public void Selectors_read_their_actual_owner_and_branch_from_opcode_with_signed_WORD(int selector, byte current, byte count, byte flags, bool branch)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, checked((ushort)(0x55 | selector << 9)));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, basis + 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0xC0, 0x1D99A78);
        frame = frame with { Core = frame.Core.Write(manager + 0xD0, new byte[] { current, count }), Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes((short)-6)) };
        if (selector != 1)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(manager + 0xD1, 1) };
        }
        if (selector is 2 or 3)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(manager + 0xD0, 1) };
        }
        int reads = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActionBranch55.Execute(frame, new(0x1000, 16, basis + 0x1000), (_, address, length) =>
        {
            reads++;
            Assert.Equal(0x1D99A79u, address);
            Assert.Equal(1, length);
            return new byte[] { flags };
        });
        Assert.Equal(selector is 2 or 3 ? 1 : 0, reads);
        Assert.Equal(branch ? basis + 0xFFA : basis + 0x1004, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Arena, result.Arena);
        Assert.Equal(frame.Random, result.Random);
    }
}
