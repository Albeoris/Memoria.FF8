using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100AuxTests
{
    [Theory]
    [InlineData(0x716A60u, true)]
    [InlineData(0x716B50u, false)]
    public void Stage_fades_keep_first_publish_and_RGB_timing_then_terminal_depth(uint callback, bool rising)
    {
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0x70 - 12].ToImmutableArray()).WriteWord(0x24, ushort.MaxValue);
        for (int index = 0; index < 13; index++)
        {
            NativeBattleModulePoolTask cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
            NativeBattleChoco97StageFadeStep step = NativeBattleChoco100Aux.AdvanceStage(task);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleChoco100Aux.AdvanceStage(cold)));
            Assert.Equal(index == 0 && rising, step.ClearRgb);
            Assert.Equal(index == 12, step.ReadyToComplete);
            Assert.Equal(unchecked((ushort)index), step.Task.ReadWord(0x24));
            if (index == 0 && !rising)
            {
                Assert.Null(step.Depth);
            }
            else
            {
                Assert.Equal((short)(rising ? index * 128 : 1536 - index * 128), step.Depth);
            }
            task = step.Task;
        }
        Assert.False(NativeBattleChoco100Aux.AdvanceStage(task.WriteByte(0x28, 1)).ReadyToComplete);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco100Aux.AdvanceStage(task.UpdateHeader(task.Flags, task.State, task.Next, 0x731A50)));
    }

    [Fact]
    public void Delayed_camera_Y_has_no_transition_execution_and_preserves_child_completion_and_cold_next()
    {
        NativeBattleChoco99CameraYProgram program = new(0x1529F70, [88, -80, 72, -64, 56, -48, 56, -48, 127]);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x716300, new byte[0x40 - 12].ToImmutableArray()).WriteDword(0x34, program.Address).WriteWord(0x38, 15).WriteByte(0x28, 1);
        List<short> values = [];
        for (int index = 0; index < 24; index++)
        {
            NativeBattleModulePoolTask cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
            NativeBattleChoco99CameraYStep step = NativeBattleChoco100Aux.Advance716300(task, program);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleChoco100Aux.Advance716300(cold, program)));
            Assert.False(step.ReadyToComplete);
            if (index < 15)
            {
                Assert.Null(step.Y);
            }
            if (step.Y is short y)
            {
                values.Add(y);
            }
            task = step.Task;
        }
        Assert.Equal(program.Values.Take(8), values);
        Assert.Equal((ushort)9, task.ReadWord(0x38));
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.True(NativeBattleChoco100Aux.Advance716300(task.WriteByte(0x28, 0), program).ReadyToComplete);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco100Aux.Advance716300(task.UpdateHeader(task.Flags, task.State, task.Next, 0x71F880), program));
    }
}
