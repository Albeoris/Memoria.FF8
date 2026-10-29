using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticShotCycleDriverTests
{
    private static BattleInput Input => new(BattleInputKind.Action, ActorId: "party-2", TargetId: "enemy-0", Ability: BattleAbility.LimitBreak, ActionId: 14 << 16);
    private static DiagnosticShotCycleDeclaration Declaration => new(0, 188, 188);
    private static DiagnosticBattleDriverRecipe Recipe => new(Input, 188) { ExpectedNativeAction = 0, ExpectedNativeCommand = 237, LimitSelection = new(1, 4, 256), ShotCycle = Declaration, BaselineSeconds = 0 };
    private static DiagnosticShotObservation Point(long callback, int inventory, BattleNativeShotPhase? phase, byte learned = 0) => new(callback, inventory, learned, phase is { } value ? new(0, 8, 120, value == BattleNativeShotPhase.Ending ? (short)-1 : value == BattleNativeShotPhase.Opening ? (short)120 : (short)100, checked((byte)inventory), value) : null);
    private static DiagnosticShotCycleReceipt Receipt(int execution, long callback, int inventory) => new(execution, "party-2", "enemy-0", 0, 3, Declaration, 188, new("party-2", 2, 14, true, [1], NativeBattleRandomState.Initialize(0), NativeBattleRandomState.Initialize(0)), Point(callback, inventory, null, execution == 1 ? (byte)0 : (byte)1));
    private static BattleDiagnosticEffectDispatch Dispatch(int serial, long callback, byte command) => new(serial, callback, "party-2", BattleAbility.LimitBreak, 0, 188, 188, command) { NativeActorIndex = 0, NativeTargetMask = command == 238 ? (ushort)0x80F8 : (ushort)8, NativeHeaderIndex = 0, NativeHeaderGeneration = serial };

    private static void Complete(DiagnosticShotCycleDriver cycle, long callback, int inventory, int serial, Action<BattleDiagnosticEffectDispatch>? observe = null)
    {
        void Emit(BattleDiagnosticEffectDispatch dispatch)
        {
            if (observe is not null)
            {
                observe(dispatch);
            }
            else
            {
                cycle.ObserveDispatch(dispatch);
            }
        }
        cycle.Opened(Point(callback, inventory, BattleNativeShotPhase.Opening, cycle.Receipt.BeforeOpening.LearnedFlags));
        Emit(Dispatch(serial, callback + 1, 14));
        Assert.Equal(DiagnosticShotOperation.Trigger, cycle.Observe(Point(callback + 1, inventory, BattleNativeShotPhase.Ready, cycle.Receipt.BeforeOpening.LearnedFlags), false));
        cycle.Triggered(Point(callback + 1, inventory - 1, BattleNativeShotPhase.AwaitingEffect, cycle.Receipt.BeforeOpening.LearnedFlags));
        Emit(Dispatch(serial + 1, callback + 2, 237));
        Assert.Equal(DiagnosticShotOperation.None, cycle.Observe(Point(callback + 2, inventory - 1, BattleNativeShotPhase.AwaitingEffect, 1), false));
        Assert.Equal(DiagnosticShotOperation.End, cycle.Observe(Point(callback + 3, inventory - 1, BattleNativeShotPhase.Ready, 1), false));
        cycle.Ended(Point(callback + 3, inventory - 1, BattleNativeShotPhase.Ending, 1));
        Emit(Dispatch(serial + 2, callback + 4, 238));
        Assert.Equal(DiagnosticShotOperation.None, cycle.Observe(Point(callback + 4, inventory - 1, null, 1), false));
        Assert.False(cycle.Completed);
        cycle.Observe(Point(callback + 4, inventory - 1, null, 1), true);
        Assert.True(cycle.Completed);
    }

    [Fact]
    public void Two_real_cycles_require_two_openings_triggers_cancellations_and_complete_drains()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        DiagnosticDriverObservation Observe(long callback, bool drained = true) => new(callback / 100d, callback, callback, true, drained, false, []);
        driver.Advance(Observe(0));
        Assert.True(driver.Advance(Observe(10)));
        driver.BeginShotCycle(Receipt(1, 10, 3));
        Complete(driver.ShotCycle!, 10, 3, 1, driver.ObserveDispatch);
        Assert.Single(driver.Dispatches);
        driver.Advance(Observe(14));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observe(15));
        driver.Advance(Observe(16));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
        Assert.True(driver.Advance(Observe(20)));
        driver.BeginShotCycle(Receipt(2, 20, 2));
        Complete(driver.ShotCycle!, 20, 2, 4, driver.ObserveDispatch);
        driver.Advance(Observe(24));
        driver.Advance(Observe(25));
        driver.Advance(Observe(26));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(2, driver.Dispatches.Count);
        DiagnosticShotCycleReceipt second = driver.ShotReceipts[1];
        Assert.Equal(1, second.Completed!.Inventory);
        string json = JsonSerializer.Serialize(second);
        DiagnosticShotCycleReceipt restored = JsonSerializer.Deserialize<DiagnosticShotCycleReceipt>(json)!;
        DiagnosticShotCycleValidation.ValidateReceipt(restored, Input, 188, restored.Dispatches, 20, 24);
    }

    [Theory]
    [InlineData("wrong_command")]
    [InlineData("wrong_actor")]
    [InlineData("wrong_target")]
    [InlineData("wrong_action")]
    [InlineData("missing_generation")]
    [InlineData("fallback")]
    public void Unrequested_opening_dispatches_are_rejected(string fault)
    {
        DiagnosticShotCycleDriver cycle = new(Receipt(1, 10, 3), Input);
        cycle.Opened(Point(10, 3, BattleNativeShotPhase.Opening));
        BattleDiagnosticEffectDispatch dispatch = Dispatch(1, 11, 14);
        dispatch = fault switch
        {
            "wrong_command" => dispatch with { NativeCommand = 237 },
            "wrong_actor" => dispatch with { ActorId = "other" },
            "wrong_target" => dispatch with { NativeTargetMask = 16 },
            "wrong_action" => dispatch with { ActionId = 1 },
            "missing_generation" => dispatch with { NativeHeaderGeneration = null },
            _ => dispatch with { ExecutedEffect = 192 }
        };
        Assert.Throws<InvalidDataException>(() => cycle.ObserveDispatch(dispatch));
    }

    [Fact]
    public void Early_or_duplicate_trigger_and_expired_or_exhausted_processes_cannot_advance()
    {
        DiagnosticShotCycleDriver cycle = new(Receipt(1, 10, 3), Input);
        Assert.Throws<InvalidDataException>(() => cycle.Triggered(Point(10, 2, BattleNativeShotPhase.AwaitingEffect)));
        cycle.Opened(Point(10, 3, BattleNativeShotPhase.Opening));
        cycle.ObserveDispatch(Dispatch(1, 11, 14));
        Assert.Equal(DiagnosticShotOperation.Trigger, cycle.Observe(Point(11, 3, BattleNativeShotPhase.Ready), false));
        cycle.Triggered(Point(11, 2, BattleNativeShotPhase.AwaitingEffect));
        Assert.Throws<InvalidDataException>(() => cycle.Triggered(Point(11, 1, BattleNativeShotPhase.AwaitingEffect)));
        Assert.Throws<InvalidDataException>(() => cycle.Observe(Point(12, 2, BattleNativeShotPhase.Ending), false));
        Assert.Throws<InvalidDataException>(() => cycle.Observe(Point(12, 0, BattleNativeShotPhase.Ready), false));
    }

    [Theory]
    [InlineData("ammo")]
    [InlineData("learning")]
    [InlineData("header")]
    [InlineData("selection")]
    [InlineData("missing_cancel")]
    public void Completed_receipt_forgery_is_rejected(string fault)
    {
        DiagnosticShotCycleDriver cycle = new(Receipt(1, 10, 3), Input);
        Complete(cycle, 10, 3, 1);
        DiagnosticShotCycleReceipt receipt = cycle.Receipt;
        receipt = fault switch
        {
            "ammo" => receipt with { Completed = receipt.Completed! with { Inventory = 3 } },
            "learning" => receipt with { Completed = receipt.Completed! with { LearnedFlags = 2 } },
            "header" => receipt with { Dispatches = [receipt.Dispatches[0], receipt.Dispatches[1] with { NativeHeaderGeneration = 1 }, receipt.Dispatches[2]] },
            "selection" => receipt with { Selection = receipt.Selection with { Accepted = false } },
            _ => receipt with { Ending = null }
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticShotCycleValidation.ValidateReceipt(receipt, Input, 188, receipt.Dispatches, 10, 14));
    }

    [Fact]
    public void Second_cycle_cannot_refill_ammo_reset_learning_or_reuse_headers()
    {
        DiagnosticShotCycleDriver first = new(Receipt(1, 10, 3), Input);
        DiagnosticShotCycleDriver second = new(Receipt(2, 20, 2), Input);
        Complete(first, 10, 3, 1);
        Complete(second, 20, 2, 4);
        Assert.Throws<InvalidDataException>(() => DiagnosticShotCycleValidation.ValidateChain(first.Receipt, second.Receipt with { BeforeOpening = second.Receipt.BeforeOpening with { Inventory = 3 } }));
        Assert.Throws<InvalidDataException>(() => DiagnosticShotCycleValidation.ValidateChain(first.Receipt, second.Receipt with { BeforeOpening = second.Receipt.BeforeOpening with { LearnedFlags = 0 } }));
        Assert.Throws<InvalidDataException>(() => DiagnosticShotCycleValidation.ValidateChain(first.Receipt, second.Receipt with { Dispatches = first.Receipt.Dispatches }));
    }

    [Fact]
    public void Shot_recipe_disallows_supporting_bypasses_repairs_and_wrong_focal_command()
    {
        Recipe.Validate();
        Assert.Throws<InvalidDataException>(() => (Recipe with { SupportingEffects = [3] }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { ExpectedNativeCommand = 14 }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { Input = Input with { ActionId = 14 << 16 | 1 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { LimitSelection = null }).Validate());
    }
}
