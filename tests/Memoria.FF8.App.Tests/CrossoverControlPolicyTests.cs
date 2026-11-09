namespace Memoria.FF8.App.Tests;

public sealed class CrossoverControlPolicyTests
{
    [Theory]
    [InlineData("advance")]
    [InlineData("action")]
    [InlineData("new-game")]
    [InlineData("battle-input")]
    [InlineData("ui-open")]
    [InlineData("load")]
    public void FF8_mutations_cannot_be_dispatched_to_a_suspended_world(string command)
    {
        Assert.Throws<InvalidOperationException>(() => CrossoverControlPolicy.EnsureAllowed(command, true, false));
        CrossoverControlPolicy.EnsureAllowed(command, false, false);
    }

    [Theory]
    [InlineData("world-step")]
    [InlineData("world-switch")]
    [InlineData("world-open")]
    [InlineData("world-snapshot")]
    [InlineData("save")]
    public void World_commands_cannot_bypass_a_prepared_update_pause(string command)
    {
        Assert.Throws<InvalidOperationException>(() => CrossoverControlPolicy.EnsureAllowed(command, true, true));
        CrossoverControlPolicy.EnsureAllowed(command, true, false);
    }

    [Theory]
    [InlineData("world-status")]
    [InlineData("status")]
    [InlineData("cancel-update")]
    [InlineData("quit")]
    public void Paused_host_keeps_status_cancellation_and_exit_responsive(string command)
    {
        CrossoverControlPolicy.EnsureAllowed(command, true, true);
    }
}
