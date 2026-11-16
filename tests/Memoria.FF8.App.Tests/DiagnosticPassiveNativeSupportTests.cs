using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticPassiveNativeSupportTests
{
    [Theory]
    [InlineData((ushort)187, 0)]
    [InlineData((ushort)329, 7)]
    [InlineData((ushort)330, 8)]
    [InlineData((ushort)328, 9)]
    [InlineData((ushort)327, 10)]
    public void Passive_entry_is_armed_before_readiness_and_waits_for_its_exact_native_dispatch(ushort effect, int action)
    {
        DiagnosticBattleDriverRecipe recipe = CreateRecipe(effect, action);
        DiagnosticBattleDriver driver = new(recipe, 10);
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        Assert.False(driver.Advance(new(11, 1, 1, true, true, false, [])));
        Assert.Empty(driver.Dispatches);

        BattleDiagnosticEffectDispatch dispatch = new(1, 2, "party-0", BattleAbility.NativeCommand, action, effect, effect, 245);
        driver.ObserveDispatch(dispatch);
        Assert.Equal(dispatch, Assert.Single(driver.Dispatches));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
    }

    [Fact]
    public void Passive_entry_rejects_a_different_kernel_effect_or_a_legacy_repeat()
    {
        DiagnosticBattleDriverRecipe recipe = CreateRecipe(187, 0);
        Assert.Throws<InvalidDataException>(() => (recipe with { ExpectedEffect = 327 }).Validate());
        Assert.Throws<InvalidDataException>(() => (recipe with { IndependentRosterEntry = false }).Validate());
    }

    private static DiagnosticBattleDriverRecipe CreateRecipe(ushort effect, int action)
    {
        BattleInput input = new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.NativeCommand, ActionId: action);
        return new(input, effect)
        {
            BaselineSeconds = 0,
            ExpectedNativeAction = action,
            ExpectedNativeCommand = 245,
            IndependentRosterEntry = true,
            PassiveNativeSupport = true
        };
    }
}
