namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel28CastTests
{
    [Fact]
    public void Installed_original212_raw239_completes_action_and_independent_standalones()
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(212, false);
    }

    [Fact]
    public void Installed_original215_raw239_completes_action_and_independent_standalones()
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(215, false);
    }
}
