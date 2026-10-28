using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiagnosticPeriodicStatusTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public void Common_timer_receipt_preserves_self_attribution_and_state(bool zombie, bool doom, bool unrelatedAction)
    {
        BattleSession battle = new(new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "recipient", Name = "Recipient", MaximumHp = 1000, CurrentHp = 500, NativeStatus0 = zombie ? (ushort)0x40 : (ushort)0, NativeStatus1 = doom ? 0x400u : 0x10u }, new() { Id = "other-enemy", Name = "Other enemy", MaximumHp = 1000, CurrentHp = 1000 }]
        }, BattleAtbMode.TurnBased);
        BattleSnapshot snapshot = battle.Capture();
        snapshot.Combatants.Single(actor => actor.Id == "recipient").NativeStatus!.Timers[doom ? 10 : 4] = doom ? (short)0 : (short)60;
        battle = BattleSession.Restore(snapshot);
        BattleSession unobserved = BattleSession.Restore(snapshot);
        if (unrelatedAction)
        {
            BattleActionPresentation action = new("party", "party", BattleActionPhase.Recovery, 1, BattleAbility.Attack, 0);
            typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(battle, action);
            typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(unobserved, action);
        }
        List<BattleDiagnosticResolution> receipts = [];
        battle.DiagnosticResolution += receipts.Add;

        Dispatch(battle);
        Dispatch(unobserved);

        BattleDiagnosticResolution resolution = Assert.Single(receipts);
        Assert.Null(resolution.ActorId);
        Assert.Equal("recipient", resolution.OriginalTargetId);
        Assert.Equal("recipient", resolution.ActualTargetId);
        Assert.Null(resolution.NativeContact);
        BattleDiagnosticPeriodicStatus proof = Assert.IsType<BattleDiagnosticPeriodicStatus>(resolution.PeriodicStatus);
        Assert.Equal(0x483470u, proof.ProducerAddress);
        Assert.Equal((byte)3, proof.RecipientIndex);
        Assert.Equal(doom ? 10 : 4, proof.StatusBit);
        Assert.Equal(doom ? -1111 : 58, proof.TimerAfter);
        Assert.Equal(doom ? 500 : 50, proof.ResolvedAmount);
        Assert.Equal(doom || zombie ? "damage" : "healing", resolution.Kind);
        Assert.Equal(JsonSerializer.Serialize(unobserved.Capture()), JsonSerializer.Serialize(battle.Capture()));
        BattleDiagnosticResolution roundtrip = JsonSerializer.Deserialize<BattleDiagnosticResolution>(JsonSerializer.Serialize(resolution))!;
        Assert.Equal(proof, roundtrip.PeriodicStatus);
        JsonObject missing = JsonSerializer.SerializeToNode(proof)!.AsObject();
        missing.Remove(nameof(BattleDiagnosticPeriodicStatus.TimerBefore));
        Assert.Throws<JsonException>(() => missing.Deserialize<BattleDiagnosticPeriodicStatus>());
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    private static void Dispatch(BattleSession battle)
    {
        typeof(BattleSession).GetMethod("DispatchNativeStatusTimers", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
    }
}
