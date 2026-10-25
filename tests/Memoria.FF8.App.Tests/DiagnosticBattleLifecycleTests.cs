using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticBattleLifecycleTests
{
    [Fact]
    public void Installed_entry_preparation_advances_real_owners_before_presentation_without_warming_a_spell()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for direct entry preparation.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleRequest request = new(94, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, PrepareEntryBeforePresentation = true };
        BattleSession active = world.StartDiagnosticBattle(request);
        int dispatches = 0;
        active.DiagnosticEffectDispatched += _ => dispatches++;
        world.PrepareDiagnosticEntryBeforePresentation(request);
        Assert.True(active.NativeEntryReady);
        Assert.InRange(world.DiagnosticReceipt!.EntryPreparation!.NativeCallbacks, 1, request.EntryCallbackBudget);
        Assert.Equal(active.NativeHostCallbacks, world.DiagnosticReceipt.EntryPreparation.NativeCallbacks);
        Assert.Equal(0, dispatches);
        Assert.Null(world.Field);
        Assert.Null(world.WorldScene);
        Assert.Throws<InvalidOperationException>(() => world.PrepareDiagnosticEntryBeforePresentation(request));
    }

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
