using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedMotionResetTests
{
    [Theory]
    [InlineData(0x0037)]
    [InlineData(0xFE37)]
    public void Clears_exact_motion_lanes_then_updates_and_caches_current_node_before_advancing_current_PC(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction).WriteTemporaryWord(0x13E, 0x1234);
        frame = frame with { Arena = frame.Arena.Write(0x2050, Enumerable.Repeat((byte)0xA5, 24).ToArray()).Write(0x2018, [3]).MarkUnknown(0x208C, 6) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0xBAD000);
        NativeBattleWaterSlotBank bank = new(0, 0x3000, 2, 0x2000);
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(0x9002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x208C, 6));
        Assert.Equal((ushort)0xFFFF, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x8C));
        Assert.Equal((ushort)0x8000, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x8E));
        Assert.Equal((ushort)0x1234, WaterSchedulerFixture.ReadSlotWord(result, 1, 0x90));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Execute(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));

        NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame current)
        {
            return NativeBattleScriptedMotionReset.Execute(current, bank, (updated, kind) =>
            {
                Assert.Equal((byte)3, kind);
                Assert.Equal(new byte[24], updated.Arena.Read(0x2050, 24));
                updated = WaterSchedulerFixture.CoreDword(updated, 0x27973E8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2100);
                updated = WaterSchedulerFixture.CoreDword(updated, 0x2797450, 0x9000);
                updated = WaterSchedulerFixture.SlotDword(updated, 1, 0x50, 0xFFFF4321);
                updated = WaterSchedulerFixture.SlotDword(updated, 1, 0x54, 0x8000FFFF);
                return WaterSchedulerFixture.SlotDword(updated, 1, 0x58, 0x12345678);
            });
        }
    }
}
