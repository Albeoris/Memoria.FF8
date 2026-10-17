using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTraversalPolicyTests
{
    [Theory]
    [InlineData("bg", 700, 0)]
    [InlineData("bc", 700, 10)]
    [InlineData("bd", 700, 20)]
    [InlineData("world", 17, 17)]
    [InlineData("other", 700, 100)]
    public void OpeningRegionsHaveExplicitExplorationPriorities(string region, int eventOrder, int expected)
    {
        Assert.Equal(expected, AutoPlayTraversalPolicy.PhaseOrder(region, eventOrder));
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(30, true)]
    public void GuardFieldRequiresItsOriginalQualificationScene(int story, bool expected)
    {
        Assert.Equal(expected, AutoPlayTraversalPolicy.CanEnterField(136, 129, story, null));
    }

    [Fact]
    public void InitialCavernEntryRequiresAnExecutedPositiveTimer()
    {
        Assert.False(AutoPlayTraversalPolicy.CanEnterField(129, 131, 20, null));
        Assert.False(AutoPlayTraversalPolicy.CanEnterField(129, 131, 20, NativeExamTimerState.FromColdProcess()));
        Assert.False(AutoPlayTraversalPolicy.CanEnterField(129, 131, 21, NativeExamTimerState.FromReachedSet(0)));
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(129, 131, 21, NativeExamTimerState.FromReachedSet(600)));
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(129, 131, 28, NativeExamTimerState.FromReachedSet(1)));
        NativeExamTimerState ambiguous = NativeExamTimerState.FromReachedSet(1) with { Alternatives = [new NativeTimerAlternative(0, 0, 0), new NativeTimerAlternative(1, 1, 1)] };
        Assert.False(AutoPlayTraversalPolicy.CanEnterField(129, 131, 21, ambiguous));
    }

    [Fact]
    public void CompletedExamAndUnrelatedEdgesRetainTheirOriginalAvailability()
    {
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(129, 131, 30, null));
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(129, 131, 31, NativeExamTimerState.FromColdProcess()));
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(129, 136, 14, null));
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(131, 129, 21, null));
        Assert.True(AutoPlayTraversalPolicy.CanEnterField(232, 139, 14, null));
    }
}
