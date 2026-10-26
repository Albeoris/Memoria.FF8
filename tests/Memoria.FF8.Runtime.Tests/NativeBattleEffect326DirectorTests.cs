using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect326DirectorTests
{
    [Fact]
    public void ActualTimelinePreservesAllEightFileDestinationsAndTheLateContact()
    {
        List<(int Age, NativeBattleEffect326DirectorOperation Operation)> operations = [];
        NativeBattleModulePoolTask task = Task(0);
        NativeBattleEffect326DirectorStep? last = null;
        int views = 0;
        for (int age = 0; age < 410; age++)
        {
            int currentAge = age;
            last = NativeBattleEffect326Director.Advance62A9C0(task, new(0, () => views++, () => 0, operation => operations.Add((currentAge, operation))));
            task = last.Task;
        }
        Assert.Equal(410, views);
        Assert.NotNull(last);
        Assert.Equal(2, last.Result);
        Assert.Equal((ushort)410, task.ReadWord(12));
        Assert.Equal(new[] { 1, 13, 22, 117, 141, 195, 218, 252 }, operations.Where(value => value.Operation.Service == NativeBattleEffect326DirectorService.File).Select(value => value.Age));
        Assert.Equal(Enumerable.Range(737, 8), operations.Where(value => value.Operation.Service == NativeBattleEffect326DirectorService.File).Select(value => value.Operation.First));
        Assert.Equal(new[] { NativeBattleEffect326SourceBase.Common, NativeBattleEffect326SourceBase.Common, NativeBattleEffect326SourceBase.Literal, NativeBattleEffect326SourceBase.Common, NativeBattleEffect326SourceBase.Work, NativeBattleEffect326SourceBase.Common, NativeBattleEffect326SourceBase.Work, NativeBattleEffect326SourceBase.Literal }, operations.Where(value => value.Operation.Service == NativeBattleEffect326DirectorService.File).Select(value => value.Operation.SourceBase));
        Assert.Equal(new[] { 0x14800, 0, 0xEECC2C, 0x14800, 0, 0x14800, 0, 0xEECC30 }, operations.Where(value => value.Operation.Service == NativeBattleEffect326DirectorService.File).Select(value => value.Operation.Second));
        Assert.Equal(new[] { 56, 138, 198, 352 }, operations.Where(value => value.Operation.Service == NativeBattleEffect326DirectorService.Play).Select(value => value.Age));
        Assert.Equal(new[] { (170, 52), (218, 53), (322, 1) }, operations.Where(value => value.Operation.Service == NativeBattleEffect326DirectorService.ActorAnimation).Select(value => (value.Age, value.Operation.First)));
        Assert.Equal(408, Assert.Single(operations, value => value.Operation.Service == NativeBattleEffect326DirectorService.Contacts).Age);
        Assert.Contains(operations, value => value.Age == 349 && value.Operation == new NativeBattleEffect326DirectorOperation(NativeBattleEffect326DirectorService.Flash, 2, 1, 2, 255));
        Assert.Equal(new NativeBattleEffect326DirectorOperation(NativeBattleEffect326DirectorService.Stage, 0), operations[^1].Operation);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(22)]
    [InlineData(117)]
    [InlineData(127)]
    [InlineData(163)]
    public void ActualIoWaitKeepsTheTaskClockAndAvoidsLaterSideEffects(int age)
    {
        List<NativeBattleEffect326DirectorOperation> operations = [];
        NativeBattleEffect326DirectorStep step = NativeBattleEffect326Director.Advance62A9C0(Task(age), new(0, () => { }, () => -1, operations.Add));
        Assert.Equal((ushort)age, step.Task.ReadWord(12));
        Assert.Empty(operations);
    }

    [Theory]
    [InlineData(218, NativeBattleEffect326DirectorService.ActorAnimation)]
    [InlineData(252, NativeBattleEffect326DirectorService.Sound)]
    public void NativePrePollSideEffectStillOccursWhenTheLaterIoGateWaits(int age, NativeBattleEffect326DirectorService first)
    {
        List<NativeBattleEffect326DirectorOperation> operations = [];
        NativeBattleEffect326DirectorStep step = NativeBattleEffect326Director.Advance62A9C0(Task(age), new(0, () => { }, () => -1, operations.Add));
        Assert.Equal((ushort)age, step.Task.ReadWord(12));
        Assert.Equal(first, Assert.Single(operations).Service);
    }

    [Fact]
    public void PauseStillPublishesTheCurrentWorldViewBeforeReturning()
    {
        int views = 0;
        NativeBattleEffect326DirectorStep step = NativeBattleEffect326Director.Advance62A9C0(Task(13), new(0x201, () => views++, () => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
        Assert.Equal(1, views);
        Assert.Equal((ushort)13, step.Task.ReadWord(12));
    }

    [Theory]
    [InlineData(5, 3410)]
    [InlineData(354, 2560)]
    [InlineData(369, 160)]
    public void StageFadeUsesItsActualBoundedInterval(int age, int amount)
    {
        List<NativeBattleEffect326DirectorOperation> operations = [];
        _ = NativeBattleEffect326Director.Advance62A9C0(Task(age), new(0, () => { }, () => 0, operations.Add));
        Assert.Equal(amount, Assert.Single(operations, operation => operation.Service == NativeBattleEffect326DirectorService.Stage).First);
    }

    private static NativeBattleModulePoolTask Task(int age) => new NativeBattleModulePoolTask(1, 0, null, NativeBattleEffect326Profile.MainCallback, new byte[24].ToImmutableArray()).WriteWord(12, checked((ushort)age));
}
