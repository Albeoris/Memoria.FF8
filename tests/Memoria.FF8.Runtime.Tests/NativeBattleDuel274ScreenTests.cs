namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274ScreenTests
{
    [Theory]
    [InlineData(0, -640, 0, 1728, 2368)]
    [InlineData(64, 0, 640, 1088, 1728)]
    public void TransitionDrawWritesActualTwoGradientQuadsAndModeBrackets(byte amount, short top, short middle, short lower, short bottom)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274Screen.Draw68D4B0(amount);
        Assert.Equal(88, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 8, 44, 80 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.Attempts, attempt => Assert.Equal(8, attempt.Primitive!.Bucket));
        Assert.Equal(0xE1000020u, batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0xE1000040u, batch.Attempts[3].Primitive!.Words[1]);
        System.Collections.Immutable.ImmutableArray<uint> upper = batch.Attempts[1].Primitive!.Words;
        System.Collections.Immutable.ImmutableArray<uint> bottomQuad = batch.Attempts[2].Primitive!.Words;
        Assert.Equal(top, unchecked((short)(upper[2] >> 16)));
        Assert.Equal(middle, unchecked((short)(upper[6] >> 16)));
        Assert.Equal(lower, unchecked((short)(bottomQuad[2] >> 16)));
        Assert.Equal(bottom, unchecked((short)(bottomQuad[6] >> 16)));
        Assert.Equal(new uint[] { 0x3A404040, 0x00404040, 0, 0 }, new[] { upper[1], upper[3], upper[5], upper[7] });
        Assert.Equal(new uint[] { 0x3A000000, 0, 0x00404040, 0x00404040 }, new[] { bottomQuad[1], bottomQuad[3], bottomQuad[5], bottomQuad[7] });
    }
}
