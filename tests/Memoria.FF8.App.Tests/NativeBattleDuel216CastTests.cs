namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel216CastTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Different_Beat_completes_original_action_and_independent_standalones(bool duelCommandRoute)
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(216, duelCommandRoute);
    }
}
