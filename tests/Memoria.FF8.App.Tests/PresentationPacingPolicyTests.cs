namespace Memoria.FF8.Tests;

public sealed class PresentationPacingPolicyTests
{
    [Theory]
    [InlineData(60, true, 60, 0)]
    [InlineData(60, true, 59, 0)]
    [InlineData(120, true, 60, 0)]
    [InlineData(60, true, 144, 60)]
    [InlineData(30, true, 60, 30)]
    [InlineData(15, true, 60, 15)]
    [InlineData(60, false, 60, 60)]
    [InlineData(60, true, null, 60)]
    [InlineData(60, true, 0, 60)]
    [InlineData(60, true, 1, 60)]
    [InlineData(0, true, 60, 0)]
    [InlineData(0, false, null, 0)]
    public void Only_a_known_synchronized_display_rate_can_replace_the_software_cap(int requested, bool synchronized, int? refreshRate, int expected)
    {
        Assert.Equal(expected, PresentationPacingPolicy.SoftwareFrameLimit(requested, synchronized, refreshRate));
    }
}
