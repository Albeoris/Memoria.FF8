namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect326ModelDirectorTests
{
    [Fact]
    public void AnimationTwoAdvancesTwiceBeforeDrawing()
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(40);
        Assert.Equal(new[] { NativeBattleEffect326ModelService.Select, NativeBattleEffect326ModelService.Advance, NativeBattleEffect326ModelService.Advance, NativeBattleEffect326ModelService.Draw }, operations.Select(operation => operation.Service));
        Assert.Equal(2, operations[0].First);
    }

    [Fact]
    public void InitializationStillRunsWhenTheClockIsPaused()
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(0, 0x201);
        Assert.Equal(new[] { NativeBattleEffect326ModelService.Initialize, NativeBattleEffect326ModelService.Draw }, operations.Select(operation => operation.Service));
    }

    [Theory]
    [InlineData(175)]
    [InlineData(198)]
    [InlineData(199)]
    public void HeldSplitAlwaysWritesDistanceAfterDrawingEvenWhenPaused(int age)
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(age, 0x201);
        Assert.Equal(new[] { NativeBattleEffect326ModelService.SplitHeld, NativeBattleEffect326ModelService.SetDistance }, operations.Select(operation => operation.Service));
        Assert.Equal(128, operations[1].First);
    }

    [Fact]
    public void ApproachCapturesTheAdvancedPoseAfterDrawing()
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(132);
        Assert.Equal(new[] { NativeBattleEffect326ModelService.InitializeChild, NativeBattleEffect326ModelService.Draw, NativeBattleEffect326ModelService.Advance, NativeBattleEffect326ModelService.CaptureBone }, operations.Select(operation => operation.Service));
        Assert.Equal(0x630A70, operations[0].First);
    }

    [Fact]
    public void AttackUpdatesOnlyXAndZThenMovesBeforeDrawing()
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(143, 0, 0xFFFF);
        Assert.Equal(new[] { NativeBattleEffect326ModelService.TranslateXZ, NativeBattleEffect326ModelService.Select, NativeBattleEffect326ModelService.Flash, NativeBattleEffect326ModelService.AddZ, NativeBattleEffect326ModelService.Draw, NativeBattleEffect326ModelService.Advance }, operations.Select(operation => operation.Service));
        Assert.Equal((210, 1199), (operations[0].First, operations[0].Second));
        Assert.Equal(-450, operations[3].First);
    }

    [Fact]
    public void PairFlagClearAndDistanceAdvancePrecedeAnimatedSplit()
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(246);
        Assert.Equal(new[] { NativeBattleEffect326ModelService.InitializeChild, NativeBattleEffect326ModelService.TargetPairFlags, NativeBattleEffect326ModelService.AddDistance, NativeBattleEffect326ModelService.SplitAnimated }, operations.Select(operation => operation.Service));
        Assert.Equal(0x630CB0, operations[0].First);
        Assert.Equal(0, operations[1].First);
        Assert.Equal(18, operations[2].First);
        Assert.Equal(46, operations[3].First);
    }

    [Fact]
    public void LastTwoCallbacksDoNotDrawAndCompletionIsAt256()
    {
        List<NativeBattleEffect326ModelOperation> operations = [];
        NativeBattleEffect326ModelDirectorStep first = NativeBattleEffect326ModelDirector.Advance62CEE0(Task(254), new(0, operations.Add));
        Assert.Empty(operations);
        Assert.Equal((ushort)255, first.Counter);
        NativeBattleEffect326ModelDirectorStep last = NativeBattleEffect326ModelDirector.Advance62CEE0(Task(255), new(0, operations.Add));
        Assert.Equal(NativeBattleEffect326ModelService.Complete, Assert.Single(operations).Service);
        Assert.Equal((ushort)256, last.Counter);
        Assert.Equal(2, last.Result);
    }

    [Fact]
    public void PausedAnimatedSplitDoesNotMutatePairFlagsOrDistance()
    {
        List<NativeBattleEffect326ModelOperation> operations = Run(246, 0x200);
        Assert.Equal(NativeBattleEffect326ModelService.SplitAnimated, Assert.Single(operations).Service);
    }

    private static List<NativeBattleEffect326ModelOperation> Run(int age, uint flags = 0, ushort capturedZ = 0)
    {
        List<NativeBattleEffect326ModelOperation> operations = [];
        _ = NativeBattleEffect326ModelDirector.Advance62CEE0(Task(age).WriteWord(0x14, capturedZ), new(flags, operations.Add));
        return operations;
    }

    private static NativeBattleModulePoolTask Task(int age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleEffect326Profile.ChildDescriptor, NativeBattleEffect326Profile.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(0x62CEE0);
        return pool.Slots[slot].WriteWord(12, checked((ushort)age));
    }
}
