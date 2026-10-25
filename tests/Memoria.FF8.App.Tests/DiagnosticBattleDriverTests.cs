using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticBattleDriverTests
{
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, ActionTimeoutSeconds = 5, ReadinessTimeoutSeconds = 3, OverallTimeoutSeconds = 20 };
    private static DiagnosticDriverObservation Observation(double seconds, long frame, bool drained = true, bool terminal = false) => new(seconds, frame, frame, true, drained, terminal, drained ? [] : ["pending-contact"]);
    private static BattleDiagnosticEffectDispatch Dispatch(int serial, ushort executed = 2) => new(serial, serial, "party-0", BattleAbility.Magic, 1, 2, executed);

    [Fact]
    public void Native_identity_can_differ_from_the_semantic_guardian_force_id()
    {
        DiagnosticBattleDriverRecipe recipe = Recipe with { Input = Recipe.Input with { Ability = BattleAbility.GuardianForce, ActionId = 0 }, ExpectedNativeAction = 64, ExpectedNativeCommand = 254 };
        recipe.Validate();
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.GuardianForce, 64, 2, 2, 254));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Null(driver.Blocker);
    }

    [Fact]
    public void Declared_supporting_dispatch_does_not_replace_the_focal_dispatch()
    {
        DiagnosticBattleDriver driver = new(Recipe with { SupportingEffects = [3] }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.Magic, 99, 3, 3));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        Assert.Empty(driver.Dispatches);
        driver.ObserveDispatch(Dispatch(2));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Single(driver.Dispatches);
    }

    [Fact]
    public void Two_dispatches_require_two_independent_drains_and_presented_tails()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        Assert.False(driver.Advance(Observation(0, 0)));
        Assert.True(driver.Advance(Observation(0.1, 1)));
        driver.ObserveDispatch(Dispatch(1));
        Assert.False(driver.Advance(Observation(0.2, 2)));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        Assert.False(driver.Advance(Observation(0.3, 3)));
        Assert.False(driver.Advance(Observation(0.4, 4)));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
        Assert.True(driver.Advance(Observation(0.5, 5)));
        driver.ObserveDispatch(Dispatch(2));
        driver.Advance(Observation(0.6, 6));
        driver.Advance(Observation(0.7, 7));
        driver.Advance(Observation(0.8, 8));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(2, driver.Dispatches.Count);
        Assert.False(driver.Advance(Observation(1, 9)));
    }

    [Fact]
    public void Late_work_restarts_the_complete_presented_tail()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(Dispatch(1));
        driver.Advance(Observation(0.2, 2));
        driver.Advance(Observation(0.3, 3, false));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(0.4, 4));
        driver.Advance(Observation(0.5, 5));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observation(0.6, 6));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
    }

    [Fact]
    public void Fallback_dispatch_is_an_explicit_blocker()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(Dispatch(1, 3));
        Assert.Equal("REQUESTED_EXECUTED_EFFECT_MISMATCH", driver.Blocker);
        Assert.Empty(driver.Dispatches);
    }

    [Fact]
    public void An_action_that_never_dispatches_cannot_complete()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.Advance(Observation(6, 2));
        Assert.Equal("ACTION_OR_DRAIN_TIMEOUT", driver.Blocker);
    }

    [Fact]
    public void Terminal_or_rewound_owner_history_blocks_repetition()
    {
        DiagnosticBattleDriver terminal = new(Recipe, 0);
        terminal.Advance(Observation(0, 0, terminal: true));
        Assert.Equal("REPEAT_PRECONDITION_UNSATISFIED", terminal.Blocker);
        DiagnosticBattleDriver rewound = new(Recipe, 0);
        rewound.Advance(Observation(0, 5));
        rewound.Advance(Observation(0.1, 4));
        Assert.Equal("CALLBACK_LINEAGE_REGRESSED", rewound.Blocker);
    }

    [Fact]
    public void Unsupported_repairs_and_unbounded_deadlines_fail_before_launch()
    {
        Assert.Throws<NotSupportedException>(() => (Recipe with { RequiresCombatRepairs = true }).Validate());
        Assert.Throws<NotSupportedException>(() => (Recipe with { RequiresCapabilityAdapter = true }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { ActionTimeoutSeconds = double.PositiveInfinity }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { TailFrames = 1 }).Validate());
    }
}
