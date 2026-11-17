using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleClockTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expanded_queued_input_accepts_gauge_growth_only_with_the_owned_raw_clock(bool nativeClock)
    {
        BattleNativeAction attack = new(1, 0, -1, -1)
        {
            Name = "Attack", MagicId = 0, AttackType = 1, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0,
            DamagePresentation = 4, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleSession session = new(new BattleDefinition
        {
            NativeBattleSpeed = nativeClock ? (byte)0 : null,
            NativeClockRules = nativeClock ? new(1) : null,
            Combatants =
            [
                new() { Id = "ally", Name = "Ally", IsPlayer = true, NativeAttack = attack, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);
        session.QueueAttack("ally", "enemy");
        session.Combatants[0].Atb = 500;
        BattleSnapshot queued = session.Capture();
        if (nativeClock)
        {
            BattleSession restored = BattleSession.Restore(queued);
            Assert.Equal(queued.Combatants[0].NativeAtb, restored.Combatants[0].NativeAtb);
            Assert.Equal("ally", Assert.Single(restored.QueuedActions).ActorId);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => BattleSession.Restore(queued));
        }
    }

    [Fact]
    public void Terminal_logical_candidate_with_the_attached_ui_observation_clears_both_clock_owners()
    {
        BattleSession owner = new(new BattleDefinition
        {
            NativeBattleSpeed = 0,
            NativeClockRules = new(1),
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        BattleSession candidate = BattleSession.Restore(owner.Capture() with { NativeClock = new(1, 1, 1) });
        typeof(BattleSession).GetField("expandedTurnClockObservation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(candidate, NativeBattleUiServices.Initialize());

        candidate.InstantVictory();

        Assert.Equal(BattleOutcome.Victory, candidate.Outcome);
        Assert.Equal((byte)0, candidate.Capture().NativeClock!.Active);
        Assert.Equal(0u, candidate.Capture().NativeClock!.ActionPause);
        Assert.Equal(BattleOutcome.Active, owner.Outcome);
    }

    [Theory]
    [InlineData((byte)1, 8, false, true)]
    [InlineData((byte)0, 8, false, false)]
    [InlineData((byte)2, 8, false, false)]
    [InlineData((byte)3, 8, false, false)]
    [InlineData((byte)4, 8, false, false)]
    [InlineData(null, 1, false, true)]
    [InlineData(null, 2, false, true)]
    [InlineData(null, 1, true, false)]
    [InlineData(null, 0, false, false)]
    [InlineData(null, 247, false, false)]
    public void Hosted_event_resets_only_the_original_ordinary_clock_owner(byte? stage, byte command, bool reaction, bool reset)
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeBattleSpeed = 0,
            NativeClockRules = new(1),
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        BattleCombatant actor = session.Combatants[0];
        actor.Atb = 500;
        int raw = actor.NativeAtb!.Current;
        actor.NativeCombatantFlags = 0xCu | 1;
        BattleActionPresentation action = new("ally", "enemy", BattleActionPhase.Recovery, BattleTargetRequest.None)
        {
            NativeAiCallbackStage = stage,
            NativeQueuedCommand = command,
            NativePlayerReaction = reaction ? BattleNativePlayerReaction.Counter : null
        };

        Assert.Equal(reset, BattleSession.UsesNativeOrdinaryCompletion(action));
        typeof(BattleSession).GetMethod("CompleteExpandedHostedEventGauge", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [action]);

        Assert.Equal(reset ? 0 : raw, actor.NativeAtb.Current);
        Assert.Equal(reset ? 1u : 0xDu, actor.NativeCombatantFlags);
    }

    [Theory]
    [InlineData(1, true, false, false, 0, 1)]
    [InlineData(0, true, false, false, 0, 0)]
    [InlineData(1, false, false, false, 0, 0)]
    [InlineData(1, true, true, false, 0, 0)]
    [InlineData(1, true, false, true, 0, 0)]
    [InlineData(1, true, false, false, 1, 0)]
    public void NativeAdmissionResetsDebAndRequiresEveryOriginalGate(byte active, bool uiAllowsAtb, bool scriptWaitHeld, bool allocationPaused, uint actionPause, byte expected)
    {
        NativeBattleClockState before = new(active, 1, actionPause);
        NativeBattleClockState after = NativeBattleClockRules.Admit(before, uiAllowsAtb, scriptWaitHeld, allocationPaused);
        Assert.Equal(expected, after.AtbAdmitted);
        Assert.Equal(before.Active, after.Active);
        Assert.Equal(before.ActionPause, after.ActionPause);
        Assert.Equal((byte)1, before.AtbAdmitted);
        Assert.Equal(after, JsonSerializer.Deserialize<NativeBattleClockState>(JsonSerializer.Serialize(after)));
    }

    [Fact]
    public void UnknownOrInvalidNativeClockFieldsCannotBeSeededByDeserialization()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleClockState>("{\"Active\":1,\"AtbAdmitted\":0}"));
        Assert.Throws<InvalidDataException>(() => NativeBattleClockRules.Admit(new(1, 0, 2), true, false, false));
    }
}
