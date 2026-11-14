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

    [Theory]
    [InlineData(0, 0, 0, 0, 0, false)]
    [InlineData(4, 0, 4, 0, 1, false)]
    [InlineData(0, 1, 0, 1, 1, false)]
    [InlineData(0, 8, 0, 8, 1, false)]
    [InlineData(0x20, 0, 0, 0, 1, false)]
    [InlineData(0, 0, 0x20, 0, 1, false)]
    [InlineData(0, 0x4000, 0, 0, 1, false)]
    [InlineData(0, 0, 0, 0x2000000, 1, false)]
    [InlineData(0, 0x2404000, 0, 0x2004000, 0, false)]
    [InlineData(1, 0, 0, 0, 1, true)]
    [InlineData(0, 0x10000, 0, 0, 1, true)]
    [InlineData(0x25, 0x2014009, 0, 0, 3, true)]
    public void Expanded_decision_uses_status_facts_without_a_physical_actor_index(ushort saved, uint battle, ushort previousSaved, uint previousBattle, byte removals, bool terminal)
    {
        NativeBattleReadyInvalidation expected = new(removals, removals != 0, terminal, terminal);
        Assert.Equal(expected, NativeBattleReadyInvalidations.EvaluateExpanded(saved, battle, false, false, previousSaved, previousBattle));
        Assert.Equal(expected, NativeBattleReadyInvalidations.Evaluate(2, saved, battle, 0, byte.MaxValue, previousSaved, previousBattle));
    }

    [Theory]
    [InlineData(0x20, 0x80004009u, false, false, 0, false)]
    [InlineData(0x21, 0x80004009u, false, false, 1, true)]
    [InlineData(0x20, 0x00004009u, true, false, 0, false)]
    [InlineData(0x20, 0x00004009u, false, true, 1, false)]
    [InlineData(0x20, 0x00004000u, false, true, 0, false)]
    [InlineData(0x20, 0x00014009u, true, true, 1, true)]
    public void Summon_and_logical_queue_or_current_actor_bypass_only_the_original_mirror_reads(ushort saved, uint battle, bool hasQueuedCommands, bool isCurrentActor, byte removals, bool terminal)
    {
        Assert.Equal(new NativeBattleReadyInvalidation(removals, removals != 0, terminal, terminal), NativeBattleReadyInvalidations.EvaluateExpanded(saved, battle, hasQueuedCommands, isCurrentActor, null, null));
    }

    [Fact]
    public void Expanded_comparison_requires_each_reached_previous_mirror()
    {
        NotSupportedException saved = Assert.Throws<NotSupportedException>(() => NativeBattleReadyInvalidations.EvaluateExpanded(0, 0, false, false, null, 0));
        NotSupportedException battle = Assert.Throws<NotSupportedException>(() => NativeBattleReadyInvalidations.EvaluateExpanded(0, 0, false, false, 0, null));
        Assert.Contains("WORD1D28E30", saved.Message);
        Assert.Contains("DWORD1D28E38", battle.Message);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(255, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 23)]
    public void Native_entry_keeps_its_physical_actor_and_queue_count_bounds(byte actor, int queuedCommands)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleReadyInvalidations.Evaluate(actor, 0, 0x80000000, queuedCommands, byte.MaxValue, null, null));
        Assert.Equal(new NativeBattleReadyInvalidation(0, false, false, false), NativeBattleReadyInvalidations.Evaluate(2, 0, 0, 22, byte.MaxValue, null, null));
    }
}
