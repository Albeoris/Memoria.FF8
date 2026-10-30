namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel216CastTests
{
    [Fact]
    public void Installed_original216_raw239_completes_action_and_independent_standalones()
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(216, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Different_Beat_completes_original_action_and_independent_standalones(bool duelCommandRoute)
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(216, duelCommandRoute);
    }
}
