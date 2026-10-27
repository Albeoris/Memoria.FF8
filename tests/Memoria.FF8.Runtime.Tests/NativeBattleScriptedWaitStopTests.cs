using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedWaitStopTests
{
    [Theory]
    [InlineData(0x001D)]
    [InlineData(0xFE1D)]
    public void Existing_selected_node_waits_without_advancing_PC_or_mutating_any_slot(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        NativeBattleWaterExecutionFrame result = Run(frame, NativeBattleEffectNodeMemory.ArenaAddress + 0x2100);
        Assert.Equal((ushort)0x80, result.ReadTemporaryWord(0x13E));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.Core, result.Core);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!, NativeBattleEffectNodeMemory.ArenaAddress + 0x2100)));
    }

    [Fact]
    public void Bank_zero_resolution_delegates_to_the_actual_current_stop_and_keeps_unconsumed_slot_bytes_unknown()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1D).WriteTemporaryByte(0x143, 1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2000, 0x200) };
        NativeBattleWaterExecutionFrame result = Run(frame, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x8000, result.ReadTemporaryWord(0x13E));
        Assert.Same(frame.Arena, result.Arena);
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction).WriteTemporaryWord(0x13E, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return frame with { Arena = frame.Arena.Write(0x1000, [0x1D, 0, 0xFF, 0xFF]) };
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, uint resolved)
    {
        return NativeBattleWaterStopCommand.WaitStop1D(frame, new(0x1000, 4, 0x1000), (_, selector) =>
        {
            Assert.Equal((short)-1, selector);
            return resolved;
        });
    }
}
