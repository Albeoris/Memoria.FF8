using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatisticsTests
{
    [Theory]
    [InlineData(1, 10, 21, 30)]
    [InlineData(2, 10, 20, 31)]
    [InlineData(3, 10, 21, 30)]
    [InlineData(4, 11, 20, 30)]
    [InlineData(5, 10, 20, 30)]
    public void CompletionUsesTheNativeResultJumpTable(byte result, uint victories, ushort defeats, ushort escapes)
    {
        Assert.Equal(new NativeBattleStatistics(victories, defeats, escapes), new NativeBattleStatistics(10, 20, 30).Complete(result));
    }

    [Fact]
    public void CountersWrapAtTheirOriginalWidths()
    {
        NativeBattleStatistics full = new(uint.MaxValue, ushort.MaxValue, ushort.MaxValue);
        Assert.Equal(0u, full.Complete(4).Victories);
        Assert.Equal((ushort)0, full.Complete(1).Defeats);
        Assert.Equal((ushort)0, full.Complete(2).Escapes);
        Assert.Throws<InvalidDataException>(() => full.Complete(0));
    }
}
