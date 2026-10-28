using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticReflectionDriverTests
{
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "enemy", TargetId: "enemy", Ability: BattleAbility.EnemyAbility, ActionId: 31), 106) { ExpectedNativeCommand = 2, ExpectedNativeAction = 31, BaselineSeconds = 0 };
    private static DiagnosticDriverObservation Observation(double seconds, long frame, bool drained = true) => new(seconds, frame, frame, true, drained, false, drained ? [] : ["action-or-command"]);
    private static BattleDiagnosticEffectDispatch Source(int serial, long generation) => new(serial, serial, "enemy", BattleAbility.EnemyAbility, 31, 106, 106, 2) { NativeActorIndex = 3, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = generation };
    private static BattleDiagnosticEffectDispatch Child(BattleDiagnosticEffectDispatch source) => new(source.Serial + 1, source.Callback + 1, "enemy", BattleAbility.EnemyAbility, 31, 106, 106, 247) { NativeActorIndex = 3, NativeTargetMask = 1, NativeHeaderIndex = 1, NativeHeaderGeneration = source.NativeHeaderGeneration + 1, Reflection = new(0x487DF0, source.Serial, 0, source.NativeHeaderGeneration!.Value, 1, source.NativeHeaderGeneration.Value + 1, "enemy", 3, "enemy", 3, 0x80, "party", 0, 2, 31, 106, 8, 0x10, 31, 106, 0, 1) };

    [Fact]
    public void Second_Reflect_cast_keeps_one_focal_dispatch_and_drains_its_reflected_child()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        Assert.True(driver.Advance(Observation(0.1, 1)));
        driver.ObserveDispatch(Source(1, 1));
        driver.Advance(Observation(0.2, 2));
        driver.Advance(Observation(0.3, 3));
        driver.Advance(Observation(0.4, 4));
        Assert.True(driver.Advance(Observation(0.5, 5)));
        BattleDiagnosticEffectDispatch second = Source(2, 2);
        driver.ObserveDispatch(second);
        driver.Advance(Observation(0.6, 6));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.ObserveDispatch(Child(second));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(0.7, 7, false));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(0.8, 8));
        driver.Advance(Observation(0.9, 9));
        driver.Advance(Observation(1, 10));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(2, driver.Dispatches.Count);
        Assert.Single(driver.ReflectionDispatches);
        Assert.Null(driver.Blocker);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_unlinked_or_wrong_parent_child_cannot_hide_as_a_supporting_effect(bool wrongParent)
    {
        DiagnosticBattleDriver driver = new(Recipe with { SupportingEffects = [107] }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        BattleDiagnosticEffectDispatch source = Source(1, 1);
        driver.ObserveDispatch(source);
        BattleDiagnosticEffectDispatch child = Child(source);
        child = wrongParent ? child with { Reflection = child.Reflection! with { SourceHeaderGeneration = 9 } } : child with { Reflection = null, RequestedEffect = 107, ExecutedEffect = 107 };
        driver.ObserveDispatch(child);
        Assert.Equal(wrongParent ? "REFLECTION_LINEAGE_MISMATCH" : "REFLECTION_LINEAGE_UNAVAILABLE", driver.Blocker);
        Assert.Single(driver.Dispatches);
        Assert.Empty(driver.ReflectionDispatches);
    }

    [Fact]
    public void Reusing_a_child_header_receipt_with_a_new_serial_is_rejected()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        BattleDiagnosticEffectDispatch source = Source(1, 1);
        driver.ObserveDispatch(source);
        BattleDiagnosticEffectDispatch child = Child(source);
        driver.ObserveDispatch(child);
        driver.ObserveDispatch(child with { Serial = 3 });
        Assert.Equal("REFLECTION_LINEAGE_MISMATCH", driver.Blocker);
        Assert.Single(driver.ReflectionDispatches);
    }

    [Fact]
    public void A_recipe_that_explicitly_requests_native247_remains_an_ordinary_focal_dispatch()
    {
        DiagnosticBattleDriver driver = new(Recipe with { ExpectedNativeCommand = 247 }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(Source(1, 1) with { NativeCommand = 247 });
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Single(driver.Dispatches);
        Assert.Empty(driver.ReflectionDispatches);
    }
}
