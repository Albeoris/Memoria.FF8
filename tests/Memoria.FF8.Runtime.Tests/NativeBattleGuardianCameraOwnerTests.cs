using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianCameraOwnerTests
{
    [Fact]
    public void WarmCameraBanksSurviveModuleLoadingAndIndependentJsonContinuation()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleGuardianCameraProcess process = memory.GuardianCameraProcess!;
        process = process with { Eye = process.Eye with { Current = new(123, -456, 789), Remaining = 17, Accumulator = 37 }, Target = process.Target with { Current = new(-700, 200, 40), Remaining = 9 }, Sign = -1 };
        memory = (memory with { GuardianCameraProcess = process }).BeginLoad(198);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(process, restored.GuardianCameraProcess);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(restored.GuardianCameraProcess!, [0, 0x80], 0);
        Assert.Equal(process.Eye, task.Process.Eye);
        Assert.Equal(process.Target, task.Process.Target);
        Assert.Equal(1, task.Process.Sign);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), new(new(1, 2, 3), new(4, 5, 6), 288, 0), false);
        Assert.True(step.Task.Complete);
        Assert.Equal((ushort)0, step.Task.Process.Eye.Remaining);
        Assert.Equal((ushort)0, step.Task.Process.Target.Remaining);
        Assert.Equal(process.Eye.Current, step.Task.Process.Eye.Current);
        Assert.Equal(process.Target.Current, step.Task.Process.Target.Current);
        Assert.Equal(process.PreviousPose, step.Task.Process.PreviousPose);
    }

    [Fact]
    public void MissingImportedCameraOwnerStaysMissingThroughJsonAndModuleLoading()
    {
        NativeBattleEffectMemory original = NativeBattleEffectMemory.FromColdProcess() with { GuardianCameraProcess = null, Cactuar199 = null };
        string json = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("GuardianCameraProcess", json);
        Assert.DoesNotContain("Cactuar199", json);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        Assert.Null(restored.GuardianCameraProcess);
        Assert.Null(restored.Cactuar199);
        Assert.Null(restored.BeginLoad(198).GuardianCameraProcess);
        Assert.Null(restored.BeginLoad(198).Cactuar199);
    }
}
