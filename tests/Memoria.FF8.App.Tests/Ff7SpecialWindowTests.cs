namespace Memoria.FF8.Tests;

public sealed class Ff7SpecialWindowTests
{
    [Theory]
    [InlineData(600u, "10:00")]
    [InlineData(59u, "00:59")]
    [InlineData(0u, "00:00")]
    [InlineData(uint.MaxValue, "99:59")]
    public void CountdownUsesTheOriginalFourDigitDisplayAndUnsignedClamp(uint seconds, string expected)
    {
        Assert.Equal(expected, Ff7CountdownText.Format(seconds));
    }
}
