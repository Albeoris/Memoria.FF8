using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticVictoryHoldTests
{
    [Fact]
    public void Native_ko_preserves_its_actual_hp_status_and_rng_while_victory_is_prospectively_held()
    {
        BattleSession held = Create();
        BattleSession natural = BattleSession.Restore(held.Capture());
        held.ConfigureDiagnosticTerminalHold(Hold);
        foreach (BattleSession battle in new[] { held, natural })
        {
            battle.QueueAction("party", "enemy", BattleAbility.Magic, 1);
        }
        for (int callback = 0; callback < 256 && natural.Outcome == BattleOutcome.Active; callback++)
        {
            NativeTick(held);
            NativeTick(natural);
        }
        Assert.Equal(BattleOutcome.Victory, natural.Outcome);
        Assert.Equal(BattleOutcome.Active, held.Outcome);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Held, held.DiagnosticTerminalHold!.Phase);
        Assert.Equal(0, held.Combatants[1].CurrentHp);
        Assert.Equal(natural.Combatants[1].NativeStatus0, held.Combatants[1].NativeStatus0);
        Assert.Equal(JsonSerializer.Serialize(natural.Capture().Random), JsonSerializer.Serialize(held.Capture().Random));
        Assert.Equal(BattleOutcome.Victory, Assert.Single(held.DiagnosticTerminalHoldReceipts).Candidate);
        Assert.Throws<InvalidOperationException>(() => held.RearmDiagnosticTerminalHold(held.NativeHostCallbacks ?? held.Frame));

        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(held.Capture()))!;
        BattleSession cold = BattleSession.Restore(snapshot);
        Complete(held);
        Complete(cold);
        Assert.Equal(JsonSerializer.Serialize(held.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Assert.Single(cold.DiagnosticTerminalHoldReceipts);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = null }));
        BattleDiagnosticTerminalHoldState wrong = snapshot.DiagnosticTerminalHold! with { Declaration = Hold with { Candidate = BattleOutcome.Defeat } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { DiagnosticTerminalHold = wrong }));

        cold.ReleaseDiagnosticTerminalHold(cold.NativeHostCallbacks ?? cold.Frame);
        Complete(cold);
        Assert.Equal(BattleOutcome.Victory, cold.Outcome);
        Assert.Equal(0, cold.Combatants[1].CurrentHp);
        Assert.Equal(new[] { "held", "released" }, cold.DiagnosticTerminalHoldReceipts.Select(receipt => receipt.Kind));
    }

    [Fact]
    public void Victory_hold_expires_without_changing_the_natural_ko()
    {
        BattleSession battle = Create();
        battle.ConfigureDiagnosticTerminalHold(Hold with { MaximumCallbacks = 1 });
        BattleCombatant enemy = battle.Combatants[1];
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.CurrentHp))!.SetValue(enemy, 0);
        typeof(BattleCombatant).GetProperty("NativeStatus0", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(enemy, (ushort)1);
        Complete(battle);
        NativeTick(battle);
        NativeTick(battle);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(BattleDiagnosticTerminalHoldPhase.Expired, battle.DiagnosticTerminalHold!.Phase);
        Assert.Equal(new[] { "held", "expired" }, battle.DiagnosticTerminalHoldReceipts.Select(receipt => receipt.Kind));
        Assert.Equal(0, enemy.CurrentHp);
    }

    [Fact]
    public void Victory_policy_rejects_a_different_natural_terminal_candidate()
    {
        BattleSession battle = Create();
        battle.ConfigureDiagnosticTerminalHold(Hold);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.CurrentHp))!.SetValue(battle.Combatants[0], 0);
        typeof(BattleCombatant).GetProperty("NativeStatus0", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle.Combatants[0], (ushort)1);
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => Complete(battle));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Empty(battle.DiagnosticTerminalHoldReceipts);
    }

    [Fact]
    public void Absent_candidate_keeps_the_original_defeat_contract()
    {
        JsonObject declaration = JsonSerializer.SerializeToNode(Hold)!.AsObject();
        declaration.Remove(nameof(BattleDiagnosticTerminalHoldDeclaration.Candidate));
        Assert.Equal(BattleOutcome.Defeat, declaration.Deserialize<BattleDiagnosticTerminalHoldDeclaration>()!.Candidate);
        BattleSession old = Create();
        old.ConfigureDiagnosticTerminalHold(Hold with { Candidate = BattleOutcome.Defeat });
        JsonObject snapshot = JsonSerializer.SerializeToNode(old.Capture())!.AsObject();
        snapshot[nameof(BattleSnapshot.DiagnosticTerminalHold)]![nameof(BattleDiagnosticTerminalHoldState.Declaration)]!.AsObject().Remove(nameof(BattleDiagnosticTerminalHoldDeclaration.Candidate));
        Assert.Equal(BattleOutcome.Defeat, BattleSession.Restore(snapshot.Deserialize<BattleSnapshot>()!).DiagnosticTerminalHold!.Declaration.Candidate);
        Assert.Throws<InvalidDataException>(() => (Hold with { Candidate = BattleOutcome.Active }).Validate());
    }

    private static BattleDiagnosticTerminalHoldDeclaration Hold => new("Single-use native victory presentation cleanup", ["party"], 100) { Candidate = BattleOutcome.Victory };

    private static BattleSession Create()
    {
        BattleNativeAction ko = new(2, 1, 1, 1) { MagicId = 1, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0x40, AttackFlags = 3, DamagePresentation = 0, Element = 0, StatusAttack = 255, Statuses0 = 1, Statuses1 = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        return new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Native KO", BattleActionEffect.Native, 0, false) { NativeAction = ko }],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 2)], NativeDeathCount = 0 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 100, Speed = 1 }]
        }, BattleAtbMode.TurnBased);
    }

    private static void Complete(BattleSession battle)
    {
        typeof(BattleSession).GetMethod("CompleteBattleIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
    }

    private static void NativeTick(BattleSession battle)
    {
        // This nongraphical unit fixture has no47CF60 pump; use its actual host-clock producer before the ordinary tick.
        typeof(BattleSession).GetMethod("AdvanceNativeHostTime", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
        battle.Tick();
    }
}
