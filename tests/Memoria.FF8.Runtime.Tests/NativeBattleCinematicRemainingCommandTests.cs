using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicRemainingCommandTests
{
    [Theory]
    [InlineData(-32768)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(32767)]
    public void Negate_B9_preserves_signed_selector_gates_and_conditional_refresh(short selector)
    {
        NativeBattleWaterExecutionFrame frame = Frame(0xB9);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(selector)).Write(0x2018, new byte[] { 9 }) };
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50 + 4 * lane, unchecked((uint)(-65537 * (lane + 1))));
        }
        int offset = 0x2050 + 4 * selector;
        if (selector > 0)
        {
            frame = frame with { Arena = frame.Arena.Write(offset, BitConverter.GetBytes(int.MinValue)) };
        }
        NativeBattleWaterSlotBank bank = new(0, 0x4000, 2, 0x2000);
        int updates = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNegateB9.Execute(frame, Program(), bank, 0, (current, kind) =>
        {
            updates++;
            Assert.Equal((byte)9, kind);
            Assert.Equal(int.MinValue, BitConverter.ToInt32(current.Arena.Read(offset, 4)));
            return WaterSchedulerFixture.SlotWord(current, 0, 0x94, 0x1234);
        });
        Assert.Equal(selector is >= 3 and <= 5 ? 1 : 0, updates);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1004, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        if (selector <= 0)
        {
            Assert.Same(frame.Arena, result.Arena);
        }
        if (selector is 1 or 2)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.Equal(unchecked((ushort)(BitConverter.ToInt32(result.Arena.Read(0x2050 + 4 * lane, 4)) >> 16)), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8C + 2 * lane));
            }
        }
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Context_AF_stalls_nonzero_without_reading_context_and_publishes_three_signed_payload_offsets_on_zero()
    {
        NativeBattleWaterExecutionFrame frame = Frame(0xAF);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes((ushort)1)) };
        Assert.Same(frame, NativeBattleCinematicContextCommandAF.Execute(frame, Program()));
        frame = frame with { Arena = frame.Arena.Write(0x1002, new byte[2]).Write(0x1004, BitConverter.GetBytes(short.MinValue)).Write(0x1006, BitConverter.GetBytes(short.MaxValue)).Write(0x1008, BitConverter.GetBytes((short)-1)) };
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicContextCommandAF.Execute(frame, Program());
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1004, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xB8));
        int[] values = [short.MinValue, short.MaxValue, -1];
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(unchecked(NativeBattleEffectNodeMemory.ArenaAddress + 0x1004u + (uint)values[index]), BitConverter.ToUInt32(result.Arena.Read(0x3008 + 4 * index, 4)));
        }
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1050, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicContextCommandAF.Execute(restored, Program())));
    }

    [Theory]
    [InlineData(0, 2048, 10, 4086)]
    [InlineData(0, 2047, 10, 10)]
    [InlineData(4090, 2, 20, 2)]
    [InlineData(2, 4090, 20, 4090)]
    [InlineData(1, 1, 10, 1)]
    [InlineData(0, 2048, -10, 2048)]
    public void Path_turn_preserves_half_turn_tie_wrap_and_signed_step(int current, int target, int step, int expected)
    {
        Assert.Equal(expected, NativeBattleCinematicPath179.TurnB67B80(current, target, step));
    }

    [Fact]
    public void Path_initialization_captures_signed_position_angles_and_last_planar_distance()
    {
        NativeBattleWaterExecutionFrame frame = PathFrame(true, 1200);
        short[] current = [-123, 75, -48];
        short[] target = [177, -125, 352];
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94 + 2 * lane, unchecked((ushort)current[lane]));
            frame = frame with { Arena = frame.Arena.Write(0x2494 + 2 * lane, BitConverter.GetBytes(target[lane])) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPath179.Execute(frame, Program(), (_, selector) =>
        {
            Assert.Equal((short)-32768, selector);
            return NativeBattleEffectNodeMemory.ArenaAddress + 0x2400;
        });
        Assert.Equal(new short[] { -123, 75, -48, 419, 3794 }, Enumerable.Range(0, 5).Select(index => unchecked((short)WaterSchedulerFixture.ReadSlotWord(result, 0, 0xA0 + 2 * index))).ToArray());
        Assert.Equal(447u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(frame.Arena.Read(0x2094, 6), result.Arena.Read(0x2094, 6));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1004, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(1200, true)]
    [InlineData(5, false)]
    public void Path_step_uses_signed_control_words_then_overwrites_position_and_retains_partial_FC_store(short limit, bool complete)
    {
        NativeBattleWaterExecutionFrame frame = PathFrame(false, limit);
        if (complete)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x204B, 1) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPath179.Execute(frame, Program(), (_, _) => NativeBattleEffectNodeMemory.ArenaAddress + 0x2400);
        Assert.Equal(new short[] { 100, 193, -307 }, Enumerable.Range(0, 3).Select(index => unchecked((short)WaterSchedulerFixture.ReadSlotWord(result, 0, 0x94 + 2 * index))).ToArray());
        Assert.Equal(result.Arena.Read(0x2094, 6), result.Arena.Read(0x20A0, 6));
        Assert.Equal(4064u, result.ReadTemporaryDword(0xE0));
        Assert.Equal(960u, result.ReadTemporaryDword(0xE4));
        Assert.Equal((ushort)0, result.ReadTemporaryWord(0xF8));
        Assert.Equal((ushort)0xFFF9, result.ReadTemporaryWord(0xFA));
        Assert.Equal(0xFFF9u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(complete ? (byte)0xA1 : (byte)0xA0, result.Arena.Read(0x204A, 1)[0]);
        if (complete)
        {
            Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x204B, 1));
        }
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1004, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame PathFrame(bool initialize, short limit)
    {
        NativeBattleWaterExecutionFrame frame = Frame(0xB3);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2300);
        frame = frame with { Arena = frame.Arena.Write(0x234B, new byte[] { initialize ? (byte)0x80 : (byte)0 }).Write(0x1002, BitConverter.GetBytes(short.MinValue)).Write(0x204A, new byte[] { 0xA1, 0x12 }) };
        short[] scratch = [100, 200, -300, 4000, 1024];
        short[] target = [500, -100, 800];
        short[] controls = [-7, 64, limit];
        for (int lane = 0; lane < scratch.Length; lane++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xA0 + 2 * lane, unchecked((ushort)scratch[lane]));
        }
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94 + 2 * lane, unchecked((ushort)controls[lane]));
            frame = frame with { Arena = frame.Arena.Write(0x2494 + 2 * lane, BitConverter.GetBytes(target[lane])) };
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame Frame(ushort opcode)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, opcode);
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000);
    }

    private static NativeBattleWaterScriptProgram Program()
    {
        return new(0x1000, 0x100, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000);
    }
}
