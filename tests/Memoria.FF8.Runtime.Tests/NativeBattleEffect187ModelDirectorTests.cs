namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187ModelDirectorTests
{
    [Fact]
    public void AnimationTwoAdvancesTwiceBeforeItsFirstDraw()
    {
        List<NativeBattleEffect187ModelOperation> operations = [];
        NativeBattleEffect187ModelDirectorStep step = NativeBattleEffect187ModelDirector.Advance649D00(Task(40), new(0, () => 1, operations.Add));
        Assert.Equal(new[] { NativeBattleEffect187ModelService.Select, NativeBattleEffect187ModelService.Advance, NativeBattleEffect187ModelService.Advance, NativeBattleEffect187ModelService.Draw }, operations.Select(operation => operation.Service));
        Assert.Equal(2, operations[0].First);
        Assert.Equal((ushort)41, step.Counter);
    }

    [Fact]
    public void ApproachDrawPrecedesMovementPoseAdvanceAndBoneCapture()
    {
        List<NativeBattleEffect187ModelOperation> operations = [];
        _ = NativeBattleEffect187ModelDirector.Advance649D00(Task(108), new(0, () => 1, operations.Add));
        Assert.Equal(new[] { NativeBattleEffect187ModelService.Select, NativeBattleEffect187ModelService.Draw, NativeBattleEffect187ModelService.AddZ, NativeBattleEffect187ModelService.Advance, NativeBattleEffect187ModelService.CaptureBone }, operations.Select(operation => operation.Service));
        Assert.Equal(90, operations[2].First);
    }

    [Fact]
    public void FirstCallbackInitializesEvenWhenTheAnimationClockIsPaused()
    {
        List<NativeBattleEffect187ModelOperation> operations = [];
        NativeBattleEffect187ModelDirectorStep step = NativeBattleEffect187ModelDirector.Advance649D00(Task(0), new(0x201, () => 1, operations.Add));
        Assert.Equal(new[] { NativeBattleEffect187ModelService.Initialize, NativeBattleEffect187ModelService.Draw }, operations.Select(operation => operation.Service));
        Assert.Equal((ushort)0, step.Counter);
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(131)]
    [InlineData(143)]
    [InlineData(155)]
    [InlineData(156)]
    [InlineData(157)]
    [InlineData(167)]
    public void HeldPosesDrawWithoutAdvancing(int age)
    {
        List<NativeBattleEffect187ModelOperation> operations = [];
        _ = NativeBattleEffect187ModelDirector.Advance649D00(Task(age), new(0, () => 1, operations.Add));
        Assert.Single(operations, operation => operation.Service == NativeBattleEffect187ModelService.Draw);
        Assert.DoesNotContain(operations, operation => operation.Service == NativeBattleEffect187ModelService.Advance);
    }

    [Fact]
    public void CompleteTimelineUsesAllSixOriginalSpawnRowsAndStopsAt198()
    {
        List<(int Age, NativeBattleEffect187ModelOperation Operation)> operations = [];
        NativeBattleEffect187ModelDirectorStep? last = null;
        for (int age = 0; age < 198; age++)
        {
            int selected = age;
            last = NativeBattleEffect187ModelDirector.Advance649D00(Task(age), new(0, () => 1, operation => operations.Add((selected, operation))));
        }
        Assert.NotNull(last);
        Assert.Equal((ushort)198, last.Counter);
        Assert.Equal(2, last.Result);
        Assert.Equal(new[] { (0, 0), (12, 1), (40, 2), (65, 3), (108, 4), (120, 5), (132, 5), (144, 5), (157, 6) }, operations.Where(item => item.Operation.Service == NativeBattleEffect187ModelService.Select).Select(item => (item.Age, item.Operation.First)));
        Assert.Equal(new[] { 3, 4, 5 }, operations.Where(item => item.Operation.Service == NativeBattleEffect187ModelService.InitializeChild && item.Operation.First == 0x64D850).Select(item => item.Operation.Second));
        Assert.Contains(operations, item => item.Age == 155 && item.Operation.Service == NativeBattleEffect187ModelService.InitializeChild && item.Operation.First == 0x64C710);
        Assert.Equal(197, Assert.Single(operations, item => item.Operation.Service == NativeBattleEffect187ModelService.Complete).Age);
    }

    [Theory]
    [InlineData(-1, 1900)]
    [InlineData(1, -1900)]
    public void ExitTranslationUsesTheCurrentCameraSign(int sign, int expectedX)
    {
        List<NativeBattleEffect187ModelOperation> operations = [];
        _ = NativeBattleEffect187ModelDirector.Advance649D00(Task(157), new(0, () => sign, operations.Add));
        NativeBattleEffect187ModelOperation translation = Assert.Single(operations, operation => operation.Service == NativeBattleEffect187ModelService.Translate);
        Assert.Equal((expectedX, 700, -8900), (translation.First, translation.Second, translation.Third));
    }

    [Fact]
    public void AttackTranslationUsesTheSignedPreviouslyCapturedBoneZ()
    {
        List<NativeBattleEffect187ModelOperation> operations = [];
        NativeBattleModulePoolTask task = Task(120).WriteWord(0x14, ushort.MaxValue);
        _ = NativeBattleEffect187ModelDirector.Advance649D00(task, new(0, () => 1, operations.Add));
        NativeBattleEffect187ModelOperation translation = Assert.Single(operations, operation => operation.Service == NativeBattleEffect187ModelService.Translate);
        Assert.Equal(-101, translation.Third);
    }

    private static NativeBattleModulePoolTask Task(int age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleEffect187Profile.ChildDescriptor, NativeBattleEffect187Profile.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(0x649D00);
        return pool.Slots[slot].WriteWord(12, checked((ushort)age));
    }
}
