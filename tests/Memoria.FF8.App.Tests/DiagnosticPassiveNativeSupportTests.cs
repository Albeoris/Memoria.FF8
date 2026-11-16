using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticPassiveNativeSupportTests
{
    [Theory]
    [InlineData((byte)240, (ushort)93, 12)]
    [InlineData((byte)240, (ushort)94, 13)]
    [InlineData((byte)240, (ushort)92, 14)]
    [InlineData((byte)245, (ushort)187, 0)]
    [InlineData((byte)245, (ushort)329, 7)]
    [InlineData((byte)245, (ushort)330, 8)]
    [InlineData((byte)245, (ushort)328, 9)]
    [InlineData((byte)245, (ushort)327, 10)]
    public void Passive_entry_is_armed_before_readiness_and_waits_for_its_exact_native_dispatch(byte command, ushort effect, int action)
    {
        DiagnosticBattleDriverRecipe recipe = CreateRecipe(command, effect, action);
        DiagnosticBattleDriver driver = new(recipe, 10);
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        Assert.False(driver.Advance(new(11, 1, 1, true, true, false, [])));
        Assert.Empty(driver.Dispatches);

        BattleDiagnosticEffectDispatch dispatch = new(1, 2, "party-0", BattleAbility.NativeCommand, action, effect, effect, command);
        driver.ObserveDispatch(dispatch);
        Assert.Equal(dispatch, Assert.Single(driver.Dispatches));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
    }

    [Fact]
    public void Passive_entry_rejects_a_different_kernel_effect_or_a_legacy_repeat()
    {
        DiagnosticBattleDriverRecipe recipe = CreateRecipe(245, 187, 0);
        Assert.Throws<InvalidDataException>(() => (recipe with { ExpectedEffect = 327 }).Validate());
        Assert.Throws<InvalidDataException>(() => (recipe with { IndependentRosterEntry = false }).Validate());
    }

    private static DiagnosticBattleDriverRecipe CreateRecipe(byte command, ushort effect, int action)
    {
        BattleInput input = new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.NativeCommand, ActionId: action);
        return new(input, effect)
        {
            BaselineSeconds = 0,
            ExpectedNativeAction = action,
            ExpectedNativeCommand = command,
            IndependentRosterEntry = true,
            PassiveNativeSupport = true
        };
    }
}
