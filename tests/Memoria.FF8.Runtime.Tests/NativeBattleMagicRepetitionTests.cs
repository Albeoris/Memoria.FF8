namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagicRepetitionTests
{
    [Theory]
    [InlineData(0u, new[] { 1 })]
    [InlineData(0x20000u, new[] { 1, 2 })]
    [InlineData(0x40000u, new[] { 1, 3 })]
    [InlineData(0x60000u, new[] { 1, 2, 3 })]
    public void NativeSelectionPreservesTheDistinctDoubleAndTripleModes(uint statuses, int[] expected)
    {
        Assert.Equal(expected, NativeBattleMagicRepetition.NativeCounts(statuses, 1));
        Assert.Equal(expected, NativeBattleMagicRepetition.NativeCounts(statuses, 100));
        Assert.Empty(NativeBattleMagicRepetition.NativeCounts(statuses, 0));
    }

    [Fact]
    public void PlayerSelectionClipsTripleToTwoWhenOnlyTwoCopiesRemain()
    {
        Assert.Equal(new[] { 1, 3 }, NativeBattleMagicRepetition.NativeCounts(0x40000, 2));
        Assert.Equal(new[] { 1, 2 }, NativeBattleMagicRepetition.PlayerCounts(0x40000, 2, 0, false, false));
        Assert.Equal(new[] { 1 }, NativeBattleMagicRepetition.PlayerCounts(0x60000, 1, 0, false, false));
        Assert.Empty(NativeBattleMagicRepetition.PlayerCounts(0x60000, 0, 0x60, false, false));
    }

    [Theory]
    [InlineData(2, 0x20u, 1)]
    [InlineData(2, 0x40u, 2)]
    [InlineData(3, 0x20u, 3)]
    [InlineData(3, 0x40u, 1)]
    [InlineData(2, 0x60u, 1)]
    [InlineData(3, 0x60u, 1)]
    [InlineData(1, 0x60u, 1)]
    [InlineData(0, 0x60u, 0)]
    [InlineData(4, 0x60u, 4)]
    public void EconomicalPassivesApplyOnlyToTheirOwnSelectedMode(int casts, uint passives, int expected)
    {
        Assert.Equal(expected, NativeBattleMagicRepetition.Cost(casts, passives, false, false));
        Assert.Equal(expected, NativeBattleMagicRepetition.Cost(casts, passives, false, true));
        Assert.Equal(casts, NativeBattleMagicRepetition.Cost(casts, passives, true, false));
        Assert.Equal(expected, NativeBattleMagicRepetition.Cost(casts, passives, true, true));
    }

    [Theory]
    [InlineData(false, false, new[] { 1, 2, 3 })]
    [InlineData(false, true, new[] { 1, 2, 3 })]
    [InlineData(true, false, new[] { 1 })]
    [InlineData(true, true, new[] { 1, 2, 3 })]
    public void SingleCopyEconomicalSelectionPreservesInfiniteAndUnlimitedMagicSemantics(bool infiniteMagic, bool unlimitedMagic, int[] expected)
    {
        Assert.Equal(expected, NativeBattleMagicRepetition.PlayerCounts(0x60000, 1, 0x60, infiniteMagic, unlimitedMagic));
    }

    [Theory]
    [InlineData(0u, true, false, false)]
    [InlineData(0x20000u, true, true, false)]
    [InlineData(0x40000u, true, true, true)]
    [InlineData(0x60000u, true, true, true)]
    public void EachRepeatChecksTheCastersCurrentStatusInsteadOfTheOriginalSelection(uint statuses, bool first, bool second, bool third)
    {
        Assert.Equal(first, NativeBattleMagicRepetition.AdmitsRepeat(statuses, 1));
        Assert.Equal(second, NativeBattleMagicRepetition.AdmitsRepeat(statuses, 2));
        Assert.Equal(third, NativeBattleMagicRepetition.AdmitsRepeat(statuses, 3));
        Assert.False(NativeBattleMagicRepetition.AdmitsRepeat(statuses, 0));
        Assert.False(NativeBattleMagicRepetition.AdmitsRepeat(statuses, 4));
    }

    [Fact]
    public void AStatusRemovedBetweenCastsStopsTheNextRepeatedAction()
    {
        Assert.True(NativeBattleMagicRepetition.AdmitsRepeat(0x40000, 2));
        Assert.False(NativeBattleMagicRepetition.AdmitsRepeat(0, 2));
        Assert.False(NativeBattleMagicRepetition.AdmitsRepeat(0x20000, 3));
    }
}
