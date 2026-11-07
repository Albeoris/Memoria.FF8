namespace Memoria.FF8.Tests;

public sealed class EffectPreviewDrainObservationBudgetTests
{
    [Fact]
    public void Only_live_to_returned_blocked_boundary_and_timeout_are_recorded_once()
    {
        EffectPreviewDrainObservationBudget budget = new();
        Assert.Null(budget.Observe(false, true, true, false));
        Assert.Null(budget.Observe(true, false, true, false));
        Assert.Null(budget.Observe(false, true, false, false));
        Assert.Equal("first-post-module-stall", budget.Observe(false, true, true, false));
        for (int frame = 0; frame < 100; frame++)
        {
            Assert.Null(budget.Observe(false, true, true, false));
        }
        Assert.Equal("timeout", budget.Observe(false, true, true, true));
        Assert.Null(budget.Observe(false, true, true, true));
    }

    [Fact]
    public void Timeout_is_observed_without_assuming_module_initialization_or_return()
    {
        EffectPreviewDrainObservationBudget budget = new();
        Assert.Equal("timeout", budget.Observe(false, false, true, true));
        Assert.Null(budget.Observe(false, false, true, true));
    }
}
