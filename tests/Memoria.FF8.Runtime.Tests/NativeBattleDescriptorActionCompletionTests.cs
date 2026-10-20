using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDescriptorActionCompletionTests
{
    [Theory]
    [InlineData(0x47DDA0u)]
    [InlineData(0x47DE70u)]
    [InlineData(0x47E120u)]
    public void SuccessfulNativeEventReleasesItsPublishedHeaderPresentationWithoutAaOrRecoveryTimer(uint callback)
    {
        var fixture = Fixture();
        NativeBattleGraphicsState graphics = Complete(fixture.Session, fixture.Graphics, callback);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.False(graphics.ActionCallbackReady);
        Assert.Null(graphics.ActionActorId);
        Assert.False(fixture.Session.Combatants[0].IsBusy);
        Assert.Single(fixture.Session.Events, item => item.Kind == "ActionComplete");
        Assert.Equal((byte)0, fixture.Session.NativeActionPackets.ActionCount);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)3)]
    public void OtherEventActorDoesNotReleaseTheCurrentHeader(byte actor)
    {
        var fixture = Fixture();
        NativeBattleActionScratch scratch = fixture.Session.Capture().NativeActionScratch! with { EventActorB0F = actor };
        Field("nativeActionScratch").SetValue(fixture.Session, scratch);
        Complete(fixture.Session, fixture.Graphics, 0x47DDA0);
        Assert.Same(fixture.Action, fixture.Session.ActiveAction);
        Assert.DoesNotContain(fixture.Session.Events, item => item.Kind == "ActionComplete");
    }

    [Fact]
    public void ExplicitPreparedPresentationAcknowledgesAfterCountsResetWithoutAnActivatedCursor()
    {
        var fixture = Fixture();
        Field("nativeActionPackets").SetValue(fixture.Session, fixture.Session.NativeActionPackets.ResetCounts());
        Assert.Null(fixture.Session.NativeActionPackets.ActiveHeaderIndex);
        Complete(fixture.Session, fixture.Graphics, 0x47DDA0);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.False(fixture.Session.Combatants[0].IsBusy);
        Assert.Single(fixture.Session.Events, item => item.Kind == "ActionComplete");
        Complete(fixture.Session, fixture.Graphics, 0x47DDA0);
        Assert.Single(fixture.Session.Events, item => item.Kind == "ActionComplete");
    }

    [Fact]
    public void ActivatedPhysicalHeaderSurvivesIntermediateDd90CountsResetAndColdJsonUntilFinalEvent()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = fixture.Session.NativeActionPackets.ActivateHeader(0).ResetCounts();
        packets = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(packets))!;
        Field("nativeActionPackets").SetValue(fixture.Session, packets);
        Complete(fixture.Session, fixture.Graphics, 0x47DDA0);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Single(fixture.Session.Events, item => item.Kind == "ActionComplete");
        Assert.False(fixture.Session.Combatants[0].IsBusy);
    }

    [Fact]
    public void HeaderlessPresentationIsNotHydratedByAnUnrelatedNativeEvent()
    {
        var fixture = Fixture();
        BattleActionPresentation headerless = fixture.Action with { NativeHeaderIndex = null };
        SetAction(fixture.Session, headerless);
        Complete(fixture.Session, fixture.Graphics, 0x47DDA0);
        Assert.Same(headerless, fixture.Session.ActiveAction);
    }

    [Fact]
    public void AaContactAloneCannotFinishAPublishedFactualHeader()
    {
        var fixture = Fixture();
        Property(nameof(BattleSession.NativeGraphics)).SetValue(fixture.Session, fixture.Graphics with { ActionCallbackReady = true });
        Method("DispatchAction").Invoke(fixture.Session, null);
        Assert.Equal(BattleActionPhase.Windup, fixture.Session.ActiveAction!.Phase);
        Assert.Equal(1, fixture.Session.ActiveAction.RemainingFrames);
        Assert.DoesNotContain(fixture.Session.Events, item => item.Kind == "ActionComplete");
    }

    [Fact]
    public void UnknownEventActorCannotBeInferredFromTheManagedPresentation()
    {
        var fixture = Fixture();
        Field("nativeActionScratch").SetValue(fixture.Session, fixture.Session.Capture().NativeActionScratch! with { EventActorB0F = null });
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Complete(fixture.Session, fixture.Graphics, 0x47DDA0));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(fixture.Action, fixture.Session.ActiveAction);
        Assert.Equal((byte)1, fixture.Session.NativeActionPackets.ActionCount);
    }

    [Fact]
    public void PostActionEffectsDelayAcknowledgmentUntilTheActualFinalEvent()
    {
        var fixture = Fixture();
        Field("nativeActionScratch").SetValue(fixture.Session, fixture.Session.Capture().NativeActionScratch! with { Command = 28 });
        Method("BeginNativeBattleActionPause").Invoke(fixture.Session, null);
        NativeBattleGraphicsState post = Complete(fixture.Session, fixture.Graphics, 0x47DE70);
        Assert.Same(fixture.Action, fixture.Session.ActiveAction);
        Assert.DoesNotContain(fixture.Session.Events, item => item.Kind == "ActionComplete");
        Assert.True(fixture.Session.NativeActionPackets.ActionCount > 0);
        Assert.Equal(90, fixture.Session.Combatants[0].CurrentHp);
        Assert.Equal(1u, fixture.Session.Capture().NativeClock!.ActionPause);
        Complete(fixture.Session, post, 0x47E120);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
        //The original presentation and its already-produced0/6 mirror share the final event.
        Assert.Equal(2, fixture.Session.Events.Count(item => item.Kind == "ActionComplete"));
        Assert.Empty(fixture.Session.QueuedActions);
    }

    [Fact]
    public void MatchingHeaderOwnershipIsReadFromCurrentPhysicalPacketsAfterJson()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(fixture.Session.NativeActionPackets))!;
        Field("nativeActionPackets").SetValue(fixture.Session, packets);
        SetAction(fixture.Session, JsonSerializer.Deserialize<BattleActionPresentation>(JsonSerializer.Serialize(fixture.Action))!);
        Complete(fixture.Session, fixture.Graphics, 0x47DDA0);
        Assert.Null(fixture.Session.ActiveAction);
    }

    [Fact]
    public void ContradictoryPhysicalHeaderCannotAcknowledgeTheManagedActor()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = fixture.Session.NativeActionPackets;
        packets = packets with { Headers = packets.Headers.SetItem(0, packets.Headers[0] with { Payload = packets.Headers[0].Payload.SetItem(0, 3) }) };
        Field("nativeActionPackets").SetValue(fixture.Session, packets);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Complete(fixture.Session, fixture.Graphics, 0x47DDA0));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Same(fixture.Action, fixture.Session.ActiveAction);
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics, BattleActionPresentation Action) Fixture()
    {
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 100, CurrentHp = 100 }]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        //485460 resets the pending-order BYTE before dispatching the event represented by this fixture.
        Method("BeginNativeReactionBatch").Invoke(session, null);
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, 252, 0, 0, 1, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        Field("nativeActionPackets").SetValue(session, packets);
        Field("nativeActionScratch").SetValue(session, session.Capture().NativeActionScratch! with { EventActorB0F = 0, EventLane = 2, Command = 2 });
        BattleActionPresentation action = new("party", "enemy", BattleActionPhase.Windup, 1, BattleAbility.EnemyAbility, 1) { NativeHeaderIndex = 0, NativeQueuedCommand = 252, NativeGraphic = new(1) };
        SetAction(session, action);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.IsBusy))!.SetValue(session.Combatants[0], true);
        return (session, new([], [], 0, 0) { ActionCallbackReady = false, ActionActorId = "party" }, action);
    }

    private static NativeBattleGraphicsState Complete(BattleSession session, NativeBattleGraphicsState graphics, uint callback) => (NativeBattleGraphicsState)Method("RunNativeDescriptorActionCallback").Invoke(session, [graphics, callback])!;
    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static PropertyInfo Property(string name) => typeof(BattleSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!;
    private static void SetAction(BattleSession session, BattleActionPresentation action) => Property(nameof(BattleSession.ActiveAction)).SetValue(session, action);
}
