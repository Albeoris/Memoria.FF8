using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectPreviewArenaInitializationTests
{
    [Fact]
    public void Installed_common_arena_initializes_selected_visual_models_without_enemy_AI_or_ATB_growth()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select original resources for graphical arena initialization.");
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession active = world.StartEffectPreview(new(1, 0, 2));
        Assert.True(active.NativeEntryReady);
        Assert.Empty(active.ObserveDiagnosticDrainBlockers());
        BattleCombatant ally = Assert.Single(active.Combatants, actor => actor.IsPlayer);
        BattleCombatant enemy = Assert.Single(active.Combatants, actor => !actor.IsPlayer);
        Assert.Equal(0, ally.ModelId);
        Assert.Equal(1, enemy.ModelId);
        Assert.Equal(0, ally.NativeIndex);
        Assert.Equal(3, enemy.NativeIndex);
        Assert.Null(enemy.NativeAi);
        Assert.Equal(ally.NativeAtb!.Maximum, ally.NativeAtb.Current);
        Assert.Equal(0, enemy.NativeAtb!.Current);
        int allyHp = ally.CurrentHp;
        int enemyHp = enemy.CurrentHp;
        for (int callback = 0; callback < 4; callback++)
        {
            active.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
        }
        Assert.Equal(ally.NativeAtb.Maximum, ally.NativeAtb.Current);
        Assert.Equal(0, enemy.NativeAtb.Current);
        Assert.Equal(allyHp, ally.CurrentHp);
        Assert.Equal(enemyHp, enemy.CurrentHp);
        Assert.Empty(active.QueuedActions);
    }
}
