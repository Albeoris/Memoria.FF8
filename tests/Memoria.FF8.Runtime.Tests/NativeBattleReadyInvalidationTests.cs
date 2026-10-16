namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReadyInvalidationTests
{
    [Fact]
    public void Publication_preserves_each_original_status_transition_and_terminal_UI_removal()
    {
        NativeBattleReadyInvalidation result = NativeBattleReadyInvalidations.Evaluate(0, 0x21, 1, 0, 2, 0, 0);
        Assert.Equal((byte)3, result.RemoveUiCommands);
        Assert.True(result.ClearReadyBusyFlags);
        Assert.True(result.ResetGauge);
        Assert.True(result.RemoveQueuedCommands);
    }

    [Fact]
    public void Queued_actions_and_current_actor_suppress_only_their_original_nonterminal_branches()
    {
        Assert.Equal((byte)1, NativeBattleReadyInvalidations.Evaluate(0, 0x21, 1, 1, 2, null, null).RemoveUiCommands);
        Assert.Equal((byte)1, NativeBattleReadyInvalidations.Evaluate(0, 0x20, 1, 0, 0, null, null).RemoveUiCommands);
        Assert.Equal((byte)0, NativeBattleReadyInvalidations.Evaluate(0, 0x20, 0, 0, 0, null, null).RemoveUiCommands);
        Assert.Equal((byte)1, NativeBattleReadyInvalidations.Evaluate(0, 1, 0x80000009, 0, 2, null, null).RemoveUiCommands);
    }

    [Fact]
    public void Unknown_prior_status_is_not_replaced_by_current_formula_statuses()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleReadyInvalidations.Evaluate(0, 0, 0, 0, 2, null, null));
        Assert.Equal((byte)0, NativeBattleReadyInvalidations.Evaluate(0, 0, 0, 0, 2, 0, 0).RemoveUiCommands);
    }
}
