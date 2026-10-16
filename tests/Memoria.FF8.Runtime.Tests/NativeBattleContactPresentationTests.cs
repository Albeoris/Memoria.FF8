namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactPresentationTests
{
    [Theory]
    [InlineData(0, 0, 0, 3, 5)]
    [InlineData(0, 1, 0, 3, 0)]
    [InlineData(4, 0, 1, 3, 0)]
    [InlineData(255, 0, 2, 0, 0)]
    [InlineData(0, 0, 2, 0, 6)]
    [InlineData(4, 0, 4, 0, 9)]
    [InlineData(4, 0, 4, 3, 0)]
    [InlineData(4, 0, 6, 3, 6)]
    [InlineData(4, 1, 1, 3, 4)]
    public void SequenceUsesFormulaFlagsAndAttackClassWithoutGuessingFromDamage(byte sequence, byte flags, byte hitFlags, byte attackClass, byte expected)
    {
        NativeBattleContactPresentation result = NativeBattleContactPresentations.Complete(sequence, flags, hitFlags, attackClass, 255);
        Assert.Equal(expected, result.Sequence);
        Assert.Equal(flags, result.Flags2);
        Assert.Equal(hitFlags, result.HitFlags3);
    }

    [Theory]
    [InlineData(0, 0x08)]
    [InlineData(1, 0x48)]
    [InlineData(2, 0x88)]
    [InlineData(3, 0xC8)]
    public void LinkedSourceOwnsHighFlagsAndSequence22(byte source, byte expected)
    {
        NativeBattleContactPresentation result = NativeBattleContactPresentations.Complete(5, 0, 0xC0, 3, source);
        Assert.Equal((byte)22, result.Sequence);
        Assert.Equal(expected, result.HitFlags3);
    }
}
