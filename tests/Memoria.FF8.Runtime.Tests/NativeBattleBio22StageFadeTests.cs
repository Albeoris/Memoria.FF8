using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22StageFadeTests
{
    [Fact]
    public void Actual_four_rows_clear_RGB_ramp_hold_wait_four_ticks_and_fall_before_release()
    {
        NativeBattleStageState original = Stage();
        NativeBattleModulePoolTask task = Task(0x38);
        NativeBattleModulePoolTask parent = Task(0x8C);
        NativeBattleBio22StageFadeStep step = NativeBattleBio22StageFade.Advance89B8A0(task, parent, original);
        Assert.All(step.Stage.Groups, group =>
        {
            Assert.Equal((short)0, group.ColorDepth);
            Assert.Equal(0xAB000000u, group.ColorFlags);
            Assert.Equal((byte)0x5A, group.Flags);
        });
        for (int index = 1; index <= 8; index++)
        {
            step = NativeBattleBio22StageFade.Advance89B8A0(step.Task, parent, step.Stage);
            Assert.All(step.Stage.Groups, group => Assert.Equal((short)(index * 128), group.ColorDepth));
        }
        Assert.Equal((byte)2, step.Task.ReadByte(0x29));
        NativeBattleBio22StageFadeStep waiting = NativeBattleBio22StageFade.Advance89B8A0(step.Task, parent, step.Stage);
        Assert.Equal((byte)2, waiting.Task.ReadByte(0x29));
        Assert.Same(step.Stage, waiting.Stage);
        parent = parent.WriteWord(0x80, 7);
        step = NativeBattleBio22StageFade.Advance89B8A0(waiting.Task, parent, waiting.Stage);
        Assert.Equal((byte)3, step.Task.ReadByte(0x29));
        for (int index = 1; index <= 4; index++)
        {
            step = NativeBattleBio22StageFade.Advance89B8A0(step.Task, parent, step.Stage);
            Assert.Equal(index, step.Task.ReadWord(0x1E));
            Assert.Equal(index < 4 ? (byte)3 : (byte)4, step.Task.ReadByte(0x29));
        }
        for (int index = 1; index <= 8; index++)
        {
            step = NativeBattleBio22StageFade.Advance89B8A0(step.Task, parent, step.Stage);
            Assert.All(step.Stage.Groups, group => Assert.Equal((short)(1024 - index * 128), group.ColorDepth));
            Assert.Equal(index < 8 ? 0u : 2u, step.Result);
        }
        Assert.Equal((byte)5, step.Task.ReadByte(0x29));
        Assert.All(step.Stage.Groups, group => Assert.Equal(0xAB000000u, group.ColorFlags));
        Assert.All(original.Groups, group => Assert.Equal(0xAB123456u, group.ColorFlags));
        Assert.Equal((short)77, original.Groups[0].ColorDepth);
    }

    [Theory]
    [InlineData((byte)0, 2u)]
    [InlineData((byte)1, 0u)]
    public void Terminal_phase_still_counts_and_defers_hierarchy_release_while_child_counter_is_nonzero(byte childCount, uint result)
    {
        NativeBattleModulePoolTask task = Task(0x38).WriteByte(0x29, 4).WriteByte(0x28, childCount).WriteWord(0x1C, 128).WriteWord(0x24, ushort.MaxValue);
        NativeBattleBio22StageFadeStep step = NativeBattleBio22StageFade.Advance89B8A0(task, Task(0x8C), Stage());
        Assert.Equal(result, step.Result);
        Assert.Equal((ushort)0, step.Task.ReadWord(0x24));
        Assert.Equal((byte)1, step.Task.ReadByte(0x26));
        Assert.All(step.Stage.Groups, group => Assert.Equal((short)0, group.ColorDepth));
    }

    [Fact]
    public void Independent_JSON_keeps_hold_counter_and_actual_parent_signal_for_the_next_phase()
    {
        NativeBattleModulePoolTask task = Task(0x38).WriteByte(0x29, 3).WriteWord(0x1E, 3);
        NativeBattleModulePoolTask parent = Task(0x8C);
        NativeBattleStageState stage = Stage();
        NativeBattleBio22StageFadeStep expected = NativeBattleBio22StageFade.Advance89B8A0(task, parent, stage);
        NativeBattleBio22StageFadeStep restored = NativeBattleBio22StageFade.Advance89B8A0(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!, JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(parent))!, JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restored));
        Assert.Equal((byte)4, expected.Task.ReadByte(0x29));
    }

    private static NativeBattleStageState Stage() => NativeBattleStageState.FromColdProcess() with { Groups = Enumerable.Range(0, 4).Select(_ => new NativeBattleStageGroup(0x5A, 77, 0xAB123456, 0, 0, 0, 0, null, [])).ToImmutableArray() };
    private static NativeBattleModulePoolTask Task(int length) => new(1, 0, null, 0x89B8A0, new byte[length - 12].ToImmutableArray());
}
