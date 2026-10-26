using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticTerminalHoldTests
{
    [Theory]
    [InlineData((ushort)1, 0)]
    [InlineData((ushort)4, 100)]
    public void Explicit_hold_preserves_the_actual_defeat_and_repeated_admission_has_no_owner_mutations(ushort status, int hp)
    {
        BattleSession session = Create();
        session.ConfigureDiagnosticTerminalHold(new("Controlled defeat repetition", ["party"], 100));
        DisableParty(session, status, hp);
        Complete(session);
        Assert.Equal(BattleOutcome.Active, session.Outcome);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Held, session.DiagnosticTerminalHold!.Phase);
        Assert.Equal(status, session.ObserveDiagnosticCombatants()[0].Statuses0);
        Assert.Equal(hp, session.Combatants[0].CurrentHp);
        string before = JsonSerializer.Serialize(session.Capture());
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Complete(session);
        }
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Single(session.DiagnosticTerminalHoldReceipts);
    }

    [Fact]
    public void Unconfigured_defeat_and_explicit_release_follow_the_existing_terminal_path()
    {
        BattleSession ordinary = Create();
        DisableParty(ordinary, 1, 0);
        Complete(ordinary);
        Assert.Equal(BattleOutcome.Defeat, ordinary.Outcome);

        BattleSession held = Create();
        held.ConfigureDiagnosticTerminalHold(new("Controlled defeat repetition", ["party"], 100));
        DisableParty(held, 1, 0);
        Complete(held);
        held.ReleaseDiagnosticTerminalHold(held.NativeHostCallbacks ?? held.Frame);
        Complete(held);
        Assert.Equal(BattleOutcome.Defeat, held.Outcome);
        Assert.Equal(new[] { "held", "released" }, held.DiagnosticTerminalHoldReceipts.Select(receipt => receipt.Kind));
        Assert.Throws<InvalidOperationException>(() => held.ReleaseDiagnosticTerminalHold(held.Frame));
    }

    [Fact]
    public void Callback_budget_expiry_releases_the_genuine_defeat_without_repairing_party()
    {
        BattleSession session = Create();
        session.ConfigureDiagnosticTerminalHold(new("Bounded defeat repetition", ["party"], 1));
        DisableParty(session, 1, 0);
        Complete(session);
        typeof(BattleSession).GetProperty(nameof(BattleSession.Frame))!.SetValue(session, 2L);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(session, 2L);
        Complete(session);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Expired, session.DiagnosticTerminalHold!.Phase);
        Assert.Equal(BattleOutcome.Defeat, session.Outcome);
        Assert.Equal(0, session.Combatants[0].CurrentHp);
        Assert.Equal(new[] { "held", "expired" }, session.DiagnosticTerminalHoldReceipts.Select(receipt => receipt.Kind));
    }

    [Fact]
    public void Repairing_the_party_cannot_rearm_or_release_a_hold_beyond_its_callback_budget()
    {
        BattleSession session = Create();
        session.ConfigureDiagnosticTerminalHold(new("Bounded defeat repetition", ["party"], 1));
        DisableParty(session, 1, 0);
        Complete(session);
        DisableParty(session, 0, 100);
        typeof(BattleSession).GetProperty(nameof(BattleSession.Frame))!.SetValue(session, 2L);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(session, 2L);
        Complete(session);
        Assert.Equal(BattleOutcome.Active, session.Outcome);
        Assert.Throws<InvalidOperationException>(() => session.RearmDiagnosticTerminalHold(2));
        session.ReleaseDiagnosticTerminalHold(2);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Expired, session.DiagnosticTerminalHold!.Phase);
        Assert.Equal(new[] { "held", "expired" }, session.DiagnosticTerminalHoldReceipts.Select(receipt => receipt.Kind));
        BattleSession restored = BattleSession.Restore(session.Capture());
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Expired, restored.DiagnosticTerminalHold!.Phase);
        Assert.Equal(BattleOutcome.Active, restored.Outcome);
    }

    [Fact]
    public void Saved_rearm_requires_genuine_party_recovery_within_the_declared_callback_budget()
    {
        BattleSession session = Create();
        session.ConfigureDiagnosticTerminalHold(new("Bounded defeat repetition", ["party"], 1));
        DisableParty(session, 1, 0);
        Complete(session);
        BattleDiagnosticTerminalHoldState held = session.DiagnosticTerminalHold!;
        DisableParty(session, 0, 100);
        typeof(BattleSession).GetProperty(nameof(BattleSession.Frame))!.SetValue(session, 2L);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeHostCallbacks))!.SetValue(session, 2L);
        BattleDiagnosticTerminalHoldReceipt rearmed = held.Receipts[0] with { Kind = "rearmed", Frame = 1, Callback = 1, Drained = true, Party = session.ObserveDiagnosticCombatants().Where(actor => actor.Id == "party").ToArray() };
        BattleDiagnosticTerminalHoldState state = held with { Phase = BattleDiagnosticTerminalHoldPhase.Armed, Rearms = 1, Receipts = held.Receipts.Add(rearmed) };
        BattleSnapshot snapshot = session.Capture() with { DiagnosticTerminalHold = state };
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Armed, BattleSession.Restore(snapshot).DiagnosticTerminalHold!.Phase);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = state with { Receipts = held.Receipts.Add(rearmed with { Callback = 2, Frame = 2 }) } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = state with { Receipts = held.Receipts.Add(rearmed with { Party = held.Receipts[0].Party }) } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = state with { Receipts = held.Receipts.SetItem(0, held.Receipts[0] with { Party = rearmed.Party }).Add(rearmed) } }));
    }

    [Fact]
    public void Held_all_ko_snapshot_requires_its_explicit_consistent_hold_state()
    {
        BattleSession session = Create();
        session.ConfigureDiagnosticTerminalHold(new("Controlled defeat repetition", ["party"], 100));
        DisableParty(session, 1, 0);
        Complete(session);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(session.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(BattleOutcome.Active, restored.Outcome);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Held, restored.DiagnosticTerminalHold!.Phase);
        Assert.Equal(0, restored.Combatants[0].CurrentHp);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = null }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = snapshot.DiagnosticTerminalHold! with { Receipts = [] } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = snapshot.DiagnosticTerminalHold! with { HeldCallback = 1 } }));
    }

    [Fact]
    public void Configuration_rejects_late_unknown_enemy_and_repeated_declarations()
    {
        BattleSession session = Create();
        Assert.Throws<InvalidDataException>(() => session.ConfigureDiagnosticTerminalHold(new("Wrong owner", ["enemy"], 100)));
        session.ConfigureDiagnosticTerminalHold(new("Controlled defeat repetition", ["party"], 100));
        Assert.Throws<InvalidOperationException>(() => session.ConfigureDiagnosticTerminalHold(new("Repeated hold", ["party"], 100)));
        Assert.Throws<InvalidOperationException>(() => session.RearmDiagnosticTerminalHold(0));
        BattleSession late = Create();
        typeof(BattleSession).GetProperty(nameof(BattleSession.Frame))!.SetValue(late, 1L);
        Assert.Throws<InvalidOperationException>(() => late.ConfigureDiagnosticTerminalHold(new("Late hold", ["party"], 100)));
    }

    private static BattleSession Create()
    {
        return new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, CurrentHp = 100, MaximumHp = 100 },
                new() { Id = "enemy", Name = "Enemy", CurrentHp = 100, MaximumHp = 100 }
            ]
        }, BattleAtbMode.Normal);
    }

    private static void DisableParty(BattleSession session, ushort status, int hp)
    {
        BattleCombatant actor = session.Combatants[0];
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.CurrentHp))!.SetValue(actor, hp);
        typeof(BattleCombatant).GetProperty("NativeStatus0", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor, status);
    }

    private static void Complete(BattleSession session)
    {
        typeof(BattleSession).GetMethod("CompleteBattleIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
    }
}
