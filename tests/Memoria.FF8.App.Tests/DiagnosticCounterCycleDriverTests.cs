using System.Collections.Immutable;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticCounterCycleDriverTests
{
    private static BattleInput Input => new(BattleInputKind.Action, ActorId: "party0", TargetId: "enemy0", Ability: BattleAbility.Attack, ActionId: 0);
    private static DiagnosticCounterCycleDeclaration Declaration(int maximum = 2) => new(new("enemy0", 8, 230, 178, 2, DiagnosticCounterCycleDeclaration.SupportedScriptSha256), maximum);
    private static DiagnosticCounterCycleDriver Cycle(int maximum = 2) => new(new(1, Declaration(maximum), "party0", 0, "enemy0", 3, 10), Input);

    [Fact]
    public void Committed_effect_zero_attack_drains_without_a_fake_dispatch_then_requires_rearm_before_retry()
    {
        DiagnosticCounterCycleDriver cycle = Cycle();
        cycle.Submitted(10);
        cycle.ObserveContact(Contact());
        cycle.Drained(12);
        Assert.False(cycle.Completed);
        Assert.True(cycle.NeedsRearm);
        Assert.False(cycle.CanSubmit);
        Assert.Throws<InvalidOperationException>(() => cycle.Submitted(13));
        cycle.Rearmed();
        cycle.Submitted(13);
        Assert.Equal(2, cycle.Receipt.Attempts.Length);
        Assert.Equal(12L, cycle.Receipt.Attempts[0].DrainedCallback);
        Assert.Null(cycle.Receipt.Attempts[0].Dispatch);
        Assert.Null(cycle.Receipt.Attempts[0].DispatchPackets);
        Assert.Null(cycle.Receipt.Attempts[0].PreparationRepair);
        Assert.True(cycle.Observing);
    }

    [Fact]
    public void Final_bounded_nonfocal_attempt_fails_without_an_extra_trigger_or_claimed_completion()
    {
        DiagnosticCounterCycleDriver cycle = Cycle(1);
        cycle.Submitted(10);
        cycle.ObserveContact(Contact());
        Assert.Throws<NotSupportedException>(() => cycle.Drained(12));
        Assert.Single(cycle.Receipt.Attempts);
        Assert.False(cycle.Completed);
        Assert.Throws<InvalidOperationException>(() => cycle.Submitted(13));
    }

    [Fact]
    public void A_drained_attempt_without_actual_committed_primary_contact_cannot_rearm()
    {
        DiagnosticCounterCycleDriver cycle = Cycle();
        cycle.Submitted(10);
        Assert.Throws<InvalidDataException>(() => cycle.Drained(12));
        Assert.Null(cycle.Receipt.CompletedCallback);
        Assert.Throws<InvalidOperationException>(() => cycle.Rearmed());
    }

    [Fact]
    public void Contacts_cannot_be_reassigned_across_submission_and_drain_boundaries()
    {
        DiagnosticCounterCycleDriver cycle = Cycle();
        Assert.Throws<InvalidOperationException>(() => cycle.ObserveContact(Contact()));
        cycle.Submitted(10);
        Assert.Throws<InvalidOperationException>(() => cycle.Submitted(11));
        cycle.ObserveContact(Contact());
        cycle.Drained(12);
        Assert.Throws<InvalidOperationException>(() => cycle.ObserveContact(Contact()));
        Assert.Throws<InvalidOperationException>(() => cycle.Drained(13));
        Assert.Single(cycle.Receipt.Attempts[0].Contacts);
    }

    [Fact]
    public void A_focal_effect_without_its_observed_queued_header_is_not_accepted()
    {
        DiagnosticCounterCycleDriver cycle = Cycle();
        cycle.Submitted(10);
        BattleDiagnosticEffectDispatch dispatch = new(1, 11, "enemy0", BattleAbility.EnemyAbility, 230, 178, 178, 8) { NativeActorIndex = 3, NativeTargetMask = 1, NativeHeaderIndex = 0, NativeHeaderGeneration = 1, NativeAiCallbackStage = 2, NativeAiScriptSha256 = DiagnosticCounterCycleDeclaration.SupportedScriptSha256 };
        Assert.Throws<InvalidDataException>(() => cycle.ObserveDispatch(dispatch, new(1, 11, 0, 1, NativeBattleActionPacketsState.Fresh())));
        Assert.Null(cycle.Receipt.Attempts[0].Dispatch);
    }

    [Fact]
    public void Counter_waits_within_whole_execution_timeout_without_resetting_it_per_trigger()
    {
        DiagnosticBattleDriverRecipe recipe = new(Input, 178) { CounterCycle = Declaration(), ExpectedNativeCommand = 8, ExpectedNativeAction = 230, BaselineSeconds = 0, ActionTimeoutSeconds = 30, OverallTimeoutSeconds = 90 };
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(new(0, 0, 0, true, true, false, []));
        Assert.True(driver.Advance(new(0, 0, 1, true, true, false, [])));
        driver.Advance(new(11, 11, 2, false, false, false, ["native-action"]));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        Assert.False(driver.CounterWorkWithinDeadline(31));
        driver.Advance(new(31, 31, 3, false, false, false, ["native-action"]));
        Assert.Equal(DiagnosticDriverStage.Blocked, driver.Stage);
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
    }

    [Fact]
    public void Counter_focal_identity_cannot_be_credited_to_the_player_trigger_actor()
    {
        DiagnosticBattleDriverRecipe recipe = new(Input, 178) { CounterCycle = Declaration(), ExpectedNativeCommand = 8, ExpectedNativeAction = 230, BaselineSeconds = 0 };
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(new(0, 0, 0, true, true, false, []));
        driver.Advance(new(0, 0, 1, true, true, false, []));
        driver.ObserveDispatch(new(1, 1, "party0", BattleAbility.EnemyAbility, 230, 178, 178, 8));
        Assert.Equal("COUNTER_FOCAL_ACTOR_MISMATCH", driver.Blocker);
        Assert.Empty(driver.Dispatches);
    }

    private static BattleDiagnosticCounterContact Contact()
    {
        // Codec-only ordinary primary receipt; no installed AI/admission or RNG success is claimed.
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 1, 4, -1, 0, 0, null));
        (packets, _) = packets.AppendContact(new(new(3, 4, 0, 0, 0, 10, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1).ActivateHeader(0);
        BattleDiagnosticCounterActor recipient = new("enemy0", 3, 84, 90, true, 0, new(0, 0, 1, 0, 0, 0), new(2, 0), [1, 0, 0, 0, 0, 0, 0, 0]);
        BattleDiagnosticCounterPrimary primary = new(1, 11, "party0", 0, "enemy0", 3, 1, 0, 1, 100, 90, 0, 1, recipient);
        return new(primary, 0, false, packets);
    }
}
