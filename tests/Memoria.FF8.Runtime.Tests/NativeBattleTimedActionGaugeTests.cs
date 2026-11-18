using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTimedActionGaugeTests
{
    [Fact]
    public void Owned_Renzokuken_retains_native_gauge_growth_after_input_before_pause_publication()
    {
        var (battle, command, hosted) = Create();
        NativeBattleAtbState gauge = battle.Combatants[0].NativeAtb!;

        Restore(battle, new(command, 0), hosted);

        Assert.Same(gauge, battle.Combatants[0].NativeAtb);
        Assert.Equal(new NativeBattleAtbState(12000, 153), gauge);
        Assert.Equal(127, battle.Combatants[0].Atb);
        Assert.True(battle.Combatants[0].IsBusy);
        NativeBattleTimedAction timed = Assert.IsType<NativeBattleTimedAction>(typeof(BattleSession).GetField("nativeTimedAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle));
        Assert.Same(command, timed.Command);
        Assert.Equal(gauge, JsonSerializer.Deserialize<NativeBattleAtbState>(JsonSerializer.Serialize(gauge)));
    }

    [Theory]
    [InlineData("missing-event")]
    [InlineData("missing-owner")]
    [InlineData("foreign-owner")]
    [InlineData("foreign-actor-id")]
    [InlineData("different-active-command")]
    [InlineData("different-timed-command")]
    [InlineData("missing-gauge")]
    [InlineData("wrong-native-maximum")]
    [InlineData("invalid-native-maximum")]
    [InlineData("invalid-native-current")]
    [InlineData("missing-clock-rules")]
    [InlineData("wrong-command")]
    [InlineData("wrong-action")]
    [InlineData("wrong-crisis")]
    [InlineData("wrong-selection")]
    public void Nonzero_gauge_requires_exact_current_logical_owner_and_valid_native_transaction(string corruption)
    {
        var (battle, command, initialHosted) = Create(nativeClock: corruption != "missing-clock-rules");
        BattleSession.ExpandedHostedEvent? hosted = initialHosted;
        switch (corruption)
        {
            case "missing-event":
                hosted = null;
                break;
            case "missing-owner":
                hosted = hosted with { Renzokuken = null };
                break;
            case "foreign-owner":
                hosted = hosted with { Renzokuken = new(new BattlePartyActorModel(new("foreign")), "party", 141, null) };
                break;
            case "foreign-actor-id":
                hosted = hosted with { Renzokuken = new(new BattlePartyActorModel(new("party")), "foreign", 141, null) };
                break;
            case "different-active-command":
                SetActive(battle, command with { });
                break;
            case "different-timed-command":
                command = command with { };
                break;
            case "missing-gauge":
                battle.Combatants[0].NativeAtb = null;
                battle.Combatants[0].Atb = 127;
                break;
            case "wrong-native-maximum":
                battle.Combatants[0].NativeAtb = new(8000, 153);
                break;
            case "invalid-native-maximum":
                battle.Combatants[0].NativeAtb = new(12001, 153);
                break;
            case "invalid-native-current":
                battle.Combatants[0].NativeAtb = new(12000, 12001);
                break;
            case "wrong-command":
                command = command with { NativeQueuedCommand = 2 };
                break;
            case "wrong-action":
                command = command with { ActionId = 7 };
                break;
            case "wrong-crisis":
                command = command with { NativeRenzokuken = new(0) };
                break;
            case "wrong-selection":
                command = command with { TargetRequest = BattleTargetRequest.None };
                break;
        }

        Assert.Throws<InvalidDataException>(() => Restore(battle, new(command, 0), hosted));
    }

    [Fact]
    public void Zero_gauge_component_without_hosted_owner_keeps_its_existing_contract()
    {
        var (battle, command, _) = Create(nativeClock: false);
        battle.Combatants[0].NativeAtb = null;
        battle.Combatants[0].Atb = 0;

        Restore(battle, new(command, 0), null);

        Assert.True(battle.Combatants[0].IsBusy);
        Assert.Equal(0, battle.Combatants[0].Atb);
        Assert.Null(battle.Combatants[0].NativeAtb);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Snapshot_still_rejects_missing_native_gauge_or_wrong_published_projection(bool missing)
    {
        var (battle, _, _) = Create();
        BattleSnapshot snapshot = battle.Capture();
        BattleCombatantSnapshot actor = snapshot.Combatants[0];
        snapshot.Combatants[0] = missing ? actor with { NativeAtb = null } : actor with { Atb = actor.Atb + 1 };

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot));
    }

    private static (BattleSession Battle, BattleActionPresentation Command, BattleSession.ExpandedHostedEvent Hosted) Create(bool nativeClock = true)
    {
        BattleSession battle = new(new BattleDefinition
        {
            NativeClockRules = nativeClock ? new(10) : null,
            NativeBattleSpeed = nativeClock ? (byte)2 : null,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        battle.Combatants[0].NativeAtb = new(12000, 153);
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
        BattleActionPresentation command = new("party", "enemy", BattleActionPhase.Recovery, target, BattleAbility.LimitBreak, 5 << 16) { NativeQueuedCommand = 5, NativeRenzokuken = new(1) };
        SetActive(battle, command);
        BattleSession.ExpandedHostedEvent hosted = new(command, command, 0x47DE70, null) { Renzokuken = new(new BattlePartyActorModel(new("party")), "party", 141, null) };
        return (battle, command, hosted);
    }

    private static void SetActive(BattleSession battle, BattleActionPresentation command)
    {
        typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(battle, command);
    }

    private static void Restore(BattleSession battle, NativeBattleTimedAction timed, BattleSession.ExpandedHostedEvent? hosted)
    {
        try
        {
            typeof(BattleSession).GetMethod("RestoreNativeTimedAction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [timed, hosted]);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(error.InnerException);
        }
    }
}
