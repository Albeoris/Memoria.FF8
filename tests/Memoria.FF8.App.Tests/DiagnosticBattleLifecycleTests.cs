using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticBattleLifecycleTests
{
    [Fact]
    public void Installed_direct_battle_disposes_without_serializing_an_exploration_continuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the direct diagnostic lifecycle check.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        GameWorld world = new(session, directory);
        try
        {
            world.StartDiagnosticBattle(new(94, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1 });
            Assert.True(world.IsDiagnosticBattle);
            Assert.NotNull(world.Battle);
            Assert.Null(world.Field);
            Assert.Null(world.WorldScene);
            Assert.Throws<NotSupportedException>(() => session.PrepareSnapshot());
        }
        finally
        {
            world.Dispose();
        }

        Assert.False(world.IsDiagnosticBattle);
        Assert.Null(world.Battle);
        Assert.Null(session.State.Battle);
        Assert.Null(session.State.EventState);
        Assert.Equal(SceneKind.Title, session.State.Scene);
        Assert.Equal(SceneKind.Title, session.PrepareSnapshot().Scene);
    }
}
