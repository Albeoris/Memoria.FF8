namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect9BodyTests
{
    [Theory]
    [InlineData(-11, -10, 10, 128, 128)]
    [InlineData(-10, -10, 10, 128, 128)]
    [InlineData(0, -10, 10, 128, 64)]
    [InlineData(10, -10, 10, 128, 0)]
    [InlineData(11, -10, 10, 128, 0)]
    [InlineData(1, 0, 0, 128, 0)]
    [InlineData(1, 0, -1, 128, 0)]
    [InlineData(-32767, -32768, 32767, 128, 0)]
    [InlineData(256, 0, 1, 128, 128)]
    [InlineData(1024, 0, 1, 128, 128)]
    [InlineData(513, 0, 1, 128, 0)]
    [InlineData(1, 0, 3, 128, 86)]
    [InlineData(1, 0, 2, -128, 0)]
    public void Gradient_preserves_native_signed_WORD_span_ratio_and_clamp_order(int coordinate, int lower, int upper, int shade, int expected)
    {
        Assert.Equal(expected, NativeBattleEffect9Body.Gray((short)coordinate, (short)lower, (short)upper, (short)shade));
    }
}
