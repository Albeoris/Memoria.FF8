namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDuel274CastTests
{
    [Fact]
    public void Installed_Duel274_PunchRush_preserves_original_scene_and_closes_into_finisher()
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(112, true, 0);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    public void Installed_Duel_basic_move_preserves_its_original_scene_and_closes_into_a_finisher(byte action)
    {
        NativeBattleDuel112CastTests.RunOriginalDuelEffect(112, true, action);
    }
}
