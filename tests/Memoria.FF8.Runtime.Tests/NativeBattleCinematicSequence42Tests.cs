using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicSequence42Tests
{
    [Theory]
    [InlineData(0x42, false, 0u)]
    [InlineData(0x442, false, 0u)]
    [InlineData(0x442, true, 0xFFFFFFF8u)]
    public void Sequence_selection_and_pose_branch_read_only_the_reached_operands_and_wrap_PC(ushort instruction, bool complete, uint next)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0xFFFFFFFC);
        frame = frame with { Arena = frame.Arena.Write(0x1000, [0x42, 4, 0xFC, 0xFF]).Write(0x201B, [3]) };
        if (instruction == 0x442 && !complete)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        int selections = 0;
        int polls = 0;
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicSequence42.Execute(current, new(0x1000, 4, 0xFFFFFFFC), new((actor, sequence) =>
        {
            selections++;
            Assert.Equal((byte)3, actor);
            Assert.Equal((short)-4, sequence);
        }, actor =>
        {
            polls++;
            Assert.Equal((byte)3, actor);
            return complete;
        }));
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(next, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(instruction == 0x42 ? 1 : 0, selections);
        Assert.Equal(instruction == 0x442 ? 1 : 0, polls);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }

    [Fact]
    public void Other_modes_return_without_reading_PC_actor_or_an_operand()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0xF042);
        frame = frame with { Core = frame.Core.MarkUnknown(0x2797450, 4), Arena = frame.Arena.MarkUnknown(0x201B, 1) };
        Assert.Same(frame, NativeBattleCinematicSequence42.Execute(frame, new(-1, 0, 0), new((_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException())));
    }
}
