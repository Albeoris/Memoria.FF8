namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187DirectorTests
{
    [Fact]
    public void OriginalTimelinePreservesFilesStreamsPlaybackAndLateContacts()
    {
        List<(int Age, NativeBattleEffect187DirectorOperation Operation)> operations = [];
        NativeBattleModulePoolTask task = Task(0);
        NativeBattleEffect187DirectorStep? last = null;
        int viewPublications = 0;
        for (int age = 0; age < 289; age++)
        {
            int selectedAge = age;
            last = NativeBattleEffect187Director.Advance6474C0(task, new(0, () => viewPublications++, () => 0, () => true, operation => operations.Add((selectedAge, operation))));
            task = last.Task;
        }
        Assert.Equal(289, viewPublications);
        Assert.NotNull(last);
        Assert.Equal(2, last.Result);
        Assert.Equal((ushort)289, last.Task.ReadWord(12));
        Assert.Equal(new[] { (1, 0x225, 0x14000, false), (13, 0x226, 0, false), (22, 0x227, 0, true), (117, 0x228, 0x14000, false), (141, 0x229, 0, true), (170, 0x22A, 0, true) }, operations.Where(item => item.Operation.Service == NativeBattleEffect187DirectorService.File).Select(item => (item.Age, item.Operation.First, item.Operation.Second, item.Operation.UseWorkBuffer)));
        Assert.Equal(new[] { 0, 133, 163, 208 }, operations.Where(item => item.Operation.Service == NativeBattleEffect187DirectorService.Stream).Select(item => item.Age));
        Assert.Equal(new[] { 56, 138, 173, 185, 197, 212 }, operations.Where(item => item.Operation.Service == NativeBattleEffect187DirectorService.Play).Select(item => item.Age));
        Assert.Equal(287, Assert.Single(operations, item => item.Operation.Service == NativeBattleEffect187DirectorService.Contacts).Age);
        Assert.Equal(new NativeBattleEffect187DirectorOperation(NativeBattleEffect187DirectorService.Stage, 0), operations[^1].Operation);
    }

    [Fact]
    public void PausePublishesWorldViewWithoutPollingOrAdvancing()
    {
        int views = 0;
        int polls = 0;
        List<NativeBattleEffect187DirectorOperation> operations = [];
        NativeBattleEffect187DirectorStep step = NativeBattleEffect187Director.Advance6474C0(Task(13), new(0x201, () => views++, Poll, () => true, operations.Add));
        Assert.Equal(1, views);
        Assert.Equal(0, polls);
        Assert.Equal((ushort)13, step.Task.ReadWord(12));
        Assert.Empty(operations);

        int Poll()
        {
            polls++;
            return 0;
        }
    }

    [Fact]
    public void ActualFrameWaitCanYieldAfterTheInitialGlobalPoll()
    {
        Queue<int> pending = new([0, -1]);
        List<NativeBattleEffect187DirectorOperation> operations = [];
        NativeBattleEffect187DirectorStep step = NativeBattleEffect187Director.Advance6474C0(Task(13), new(0x200, () => { }, pending.Dequeue, () => true, operations.Add));
        Assert.Empty(pending);
        Assert.Equal((ushort)13, step.Task.ReadWord(12));
        Assert.Empty(operations);
    }

    [Fact]
    public void StreamAcknowledgementControlsTheSixthFileBranch()
    {
        List<NativeBattleEffect187DirectorOperation> operations = [];
        NativeBattleEffect187DirectorStep step = NativeBattleEffect187Director.Advance6474C0(Task(170), new(0, () => { }, () => 0, () => false, operations.Add));
        Assert.Equal((ushort)171, step.Task.ReadWord(12));
        Assert.Empty(operations);
    }

    [Theory]
    [InlineData(5, 3410)]
    [InlineData(273, 2560)]
    [InlineData(288, 160)]
    public void StageAmountUsesOriginalIntegerArithmetic(int age, int expected)
    {
        List<NativeBattleEffect187DirectorOperation> operations = [];
        _ = NativeBattleEffect187Director.Advance6474C0(Task(age), new(0, () => { }, () => 0, () => true, operations.Add));
        Assert.Equal(expected, operations.First(operation => operation.Service == NativeBattleEffect187DirectorService.Stage).First);
    }

    private static NativeBattleModulePoolTask Task(int age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleEffect187Profile.ChildDescriptor, NativeBattleEffect187Profile.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(NativeBattleEffect187Profile.MainCallback);
        return pool.Slots[slot].WriteWord(12, checked((ushort)age));
    }
}
