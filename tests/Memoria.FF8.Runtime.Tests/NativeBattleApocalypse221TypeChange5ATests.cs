namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221TypeChange5ATests
{
    [Fact]
    public void Type_change_dispatches_actual_selected_kind_after_operand_writes_and_advances_current_PC_after_callback()
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x5A);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, basis + 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1002, new byte[] { 11 }).MarkUnknown(0x1003, 1).Write(0x1004, BitConverter.GetBytes((short)-7)), Core = frame.Core.MarkUnknown(0x2797454, 128) };
        int calls = 0;
        NativeBattleWaterPresentationServices services = new(null!, null!, null!)
        {
            UpdateSlot = (owner, kind) =>
            {
                calls++;
                Assert.Equal((byte)11, kind);
                Assert.Equal((byte)11, owner.Arena.Read(0x2018, 1)[0]);
                Assert.Equal(unchecked((ushort)-7), WaterSchedulerFixture.ReadSlotWord(owner, 0, 0xB0));
                Assert.Equal(basis + 0x1000, WaterSchedulerFixture.ReadCoreDword(owner, 0x2797450));
                owner = WaterSchedulerFixture.SlotWord(owner, 0, 0x94, 0xABCD);
                return WaterSchedulerFixture.CoreDword(owner, 0x2797450, basis + 0x1008);
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterPresentationCommands.Execute(frame, new(0x1000, 6, basis + 0x1000), new(0x2000, 0x400, 4, 0x2000), 0, services);
        Assert.Equal(1, calls);
        Assert.Equal(basis + 0x100E, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0xABCD, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x94));
        Assert.Equal(frame.Random, result.Random);
    }
}
