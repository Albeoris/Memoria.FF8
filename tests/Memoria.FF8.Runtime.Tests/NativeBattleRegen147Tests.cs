using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRegen147Tests
{
    [Theory]
    [InlineData(0, 0, 4096)]
    [InlineData(3, 0, 3328)]
    [InlineData(4, 0, 0)]
    [InlineData(14, 0, 0)]
    [InlineData(29, 0, 3840)]
    [InlineData(0, 1, 4096)]
    [InlineData(3, 1, 3712)]
    [InlineData(4, 1, 2048)]
    [InlineData(14, 1, 2048)]
    [InlineData(29, 1, 3968)]
    public void Envelope_fade_keeps_its_initial_discontinuity_and_distinct_kind_baseline(short counter, short kind, int expected)
    {
        Assert.Equal(expected, NativeBattleRegen147Math.EnvelopeFade(counter, kind));
    }
}
