using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestLimitProcessTests
{
    [Theory]
    [InlineData("Duel", (byte)1, (byte)11)]
    [InlineData("Slot", (byte)5, (byte)16)]
    public void Original_limit_selection_and_input_complete_one_owned_host_turn(string name, byte character, byte command)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new(name, character, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(command);
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Every_original_Shot_ammunition_selection_opens_fires_and_closes(byte ammunition)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Shot", 2, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(14, (ushort)(0x0E00 | ammunition));
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Combine_selects_each_original_learned_Angelo_finisher(byte action)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Combine", 4, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(19, (ushort)(0x1300 | action), angeloFlags: (byte)(1 << (4 + action)));
        fixture.FollowWithFire();
    }

    [Fact]
    public void Automatic_Duel_uses_source_UI_random_and_finishes_its_native_timer()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Duel", 1, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(11, automaticDuel: true);
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Every_original_Duel_move_is_reached_through_its_native_button_recipe(byte action)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Duel", 1, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(11, duelAction: action);
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(0x20000u, 2)]
    [InlineData(0x40000u, 3)]
    public void Angel_Wing_activates_then_uses_later_native_compelled_free_magic_and_stops_on_KO(uint repetitionStatus, int casts)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Combine", 4, 4, nativeProcess: true);
        fixture.ExecuteAngelWingTurns(repetitionStatus, casts);
    }

    [Theory]
    [InlineData("Duel", (byte)1, false)]
    [InlineData("Shot", (byte)2, true)]
    public void KO_finishes_inflight_native_phase_cancels_unstarted_input_and_preserves_accepted_ammunition(string name, byte character, bool acceptedShot)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new(name, character, 4, nativeProcess: true);
        fixture.AssertProcessCancellation(acceptedShot);
    }

    [Fact]
    public void Retained_Duel_capability_probe_preserves_live_native_pools_source_random_and_stock()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Duel", 1, 4, nativeProcess: true);
        fixture.AssertRetainedPreflightIsolation();
    }

    [Theory]
    [InlineData(0x80u)]
    [InlineData(0x800000u)]
    [InlineData(0x40u)]
    public void Source_execution_add_immunity_precedes_forced_chance_and_immunity_bypass_but_allows_curing(uint mask)
    {
        Ff7BattleDamageActor stats = new(10, 10, 10, 10, 10, 0, 0, 10, 10);
        Ff7BattleActionVitals target = new(100, 100, 10, 10, 0) { AbsoluteAddStatusImmunities = mask };
        Ff7BattleDamageAction action = new(0, 0, 255, HasStatusOperation: true, AddsStatuses: true) { AlwaysHits = true };
        Ff7BattleAttack attack = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 63, byte.MaxValue, 0, mask, 0, 0);
        Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
        Ff7BattleActionReceipt denied = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, target, stats, target, action, attack, Random(), sameActor: true);
        Assert.Equal(0u, denied.Target.Statuses & mask);
        Ff7BattleActionVitals unprotected = target with { AbsoluteAddStatusImmunities = 0 };
        Ff7BattleActionReceipt allowed = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, unprotected, stats, unprotected, action, attack, Random(), sameActor: true);
        Assert.Equal(mask, allowed.Target.Statuses & mask);
        Ff7BattleActionVitals existing = target with { Statuses = mask };
        Ff7BattleActionReceipt cured = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, existing, stats, existing, action, attack with { StatusChange = 127 }, Random(), sameActor: true);
        Assert.Equal(0u, cured.Target.Statuses & mask);
    }
}
