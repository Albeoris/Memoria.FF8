namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223FieldWait106Tests
{
    [Theory]
    [InlineData(-32768, 32767, true)]
    [InlineData(32767, -32768, false)]
    [InlineData(-1, 0, true)]
    [InlineData(0, -1, false)]
    [InlineData(12, 12, false)]
    public void Signed_fields_compare_selected_owner_and_wait_preserves_program_counter(short current, short selected, bool advance)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x106).WriteTemporaryWord(0x13E, 0xABCD);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, basis + 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes((short)-3)).Write(0x1004, BitConverter.GetBytes((short)-7)).Write(0x2089, BitConverter.GetBytes(current)).Write(0x2189, BitConverter.GetBytes(selected)) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 0xFEDC);
        int resolutions = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicFieldWait106.Execute(frame, new(0x1000, 6, basis + 0x1000), (owner, selector) =>
        {
            resolutions++;
            Assert.Same(frame, owner);
            Assert.Equal(-7, selector);
            return basis + 0x2100;
        });
        Assert.Equal(1, resolutions);
        Assert.Equal(basis + (advance ? 0x1006u : 0x1000u), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(advance ? (ushort)0xABCD : (ushort)0xFEDC, result.ReadTemporaryWord(0x13E));
        Assert.Equal(frame.Arena, result.Arena);
        Assert.Equal(frame.Random, result.Random);
    }
}
