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

    [Theory]
    [InlineData("none")]
    [InlineData("contact")]
    [InlineData("retention")]
    [InlineData("post-frame")]
    [InlineData("post-duplicate")]
    [InlineData("post-callback")]
    [InlineData("post-simulation")]
    [InlineData("post-graphics")]
    [InlineData("post-foreign")]
    [InlineData("post-reuse")]
    [InlineData("post-no-removal")]
    [InlineData("post-live")]
    [InlineData("returned-task")]
    [InlineData("descriptor")]
    [InlineData("generation")]
    [InlineData("no-ack")]
    [InlineData("post-action")]
    public void Optional_focal_observer_preserves_native_completion_and_seals_only_exact_released_owners(string fault)
    {
        var ordinary = Fixture();
        var captured = Fixture();
        NativeBattleGraphicsState Prepare(BattleSession session, NativeBattleGraphicsState graphics)
        {
            Property(nameof(BattleSession.NativeCamera)).SetValue(session, NativeBattleCameraState.Initialize());
            Property(nameof(BattleSession.NativeFileIo)).SetValue(session, NativeBattleFileIoState.FromColdProcess());
            Property(nameof(BattleSession.NativeEffectMemory)).SetValue(session, NativeBattleEffectMemory.FromColdProcess() with { ReturnedTaskPointer1D96AAC = fault == "returned-task" ? 1u : 0u, SoundPublishedFlags = 0 });
            Property(nameof(BattleSession.NativeHostCallbacks)).SetValue(session, 20L);
            if (fault == "no-ack")
            {
                SetAction(session, null!);
            }
            if (fault == "post-action")
            {
                Field("nativeActionScratch").SetValue(session, session.Capture().NativeActionScratch! with { Command = 28 });
            }
            var descriptors = graphics.Descriptors.Enqueue(10, 128, fault == "post-action" ? 0x47DE70u : 0x47DDA0u).State;
            if (fault == "descriptor")
            {
                descriptors = descriptors.Enqueue(10, 128, 0x47DDA0).State;
            }
            if (fault.StartsWith("post-", StringComparison.Ordinal) && fault != "post-action")
            {
                graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendDeferredCompletion5086C0(NativeBattleDescriptorState.CompletionAddress503040(0), (_, _) =>
                {
                }) };
            }
            return graphics with { OriginalEffect = new(1, 0, new(1, 1, 1), null, 0), Descriptors = descriptors, PendingContacts = fault is "contact" or "retention" ? [session.NativeActionPackets.ReadContact(0)] : [] };
        }
        NativeBattleGraphicsState plainGraphics = Prepare(ordinary.Session, ordinary.Graphics);
        NativeBattleGraphicsState capturedGraphics = Prepare(captured.Session, captured.Graphics);
        captured.Session.DiagnosticEffectDispatched += _ =>
        {
        };
        captured.Session.ConfigureDiagnosticInvocationCompletion(1, "party", 252, 1);
        Field("diagnosticDispatchSerial").SetValue(captured.Session, 1L);
        Method("ObserveDiagnosticPreparedHeader").Invoke(captured.Session, [0]);
        BattleDiagnosticEffectDispatch focal = new(1, 10, "party", BattleAbility.EnemyAbility, 1, 1, 1, 252) { NativeActorIndex = 0, NativeHeaderIndex = 0, NativeHeaderGeneration = fault == "generation" ? 2 : 1 };
        Method("PublishDiagnosticEffectDispatch").Invoke(captured.Session, [new Action<BattleDiagnosticEffectDispatch>(_ =>
        {
        }), focal]);
        List<BattleDiagnosticInvocationRelease> releases = [];
        captured.Session.DiagnosticInvocationReleased += receipt =>
        {
            Assert.Null(captured.Session.ActiveAction);
            Assert.Contains(captured.Session.Events, value => value.Kind == "ActionComplete");
            Assert.Equal((byte)0, captured.Session.NativeActionPackets.ActionCount);
            releases.Add(receipt);
        };
        uint callback = fault == "post-action" ? 0x47DE70u : 0x47DDA0u;
        bool frameEndCase = fault.StartsWith("post-", StringComparison.Ordinal) && fault != "post-action";
        NativeBattleGraphicsState CompleteThroughPump(BattleSession session, NativeBattleGraphicsState graphics, bool observeRemoval)
        {
            NativeBattleGraphicsState current = graphics;
            NativeBattleDescriptorState descriptors = graphics.Descriptors.Advance((state, _) =>
            {
                current = Complete(session, current with { Descriptors = state }, callback);
                return new(current.Descriptors, 15);
            }, (_, _) => throw new InvalidOperationException("Unexpected descriptor task in the completion fixture."), observeRemoval: observeRemoval ? (state, slot, packet) => Method("ObserveDiagnosticInvocationPacketRemoved").Invoke(session, [state, slot, packet]) : null);
            return current with { Descriptors = descriptors };
        }
        NativeBattleGraphicsState plainResult = frameEndCase ? CompleteThroughPump(ordinary.Session, plainGraphics, false) : Complete(ordinary.Session, plainGraphics, callback);
        NativeBattleGraphicsState capturedResult = frameEndCase ? CompleteThroughPump(captured.Session, capturedGraphics, fault != "post-no-removal") : Complete(captured.Session, capturedGraphics, callback);
        Assert.Equal(NativeBattleTestSnapshots.Json(plainResult), NativeBattleTestSnapshots.Json(capturedResult));
        Assert.Equal(NativeBattleTestSnapshots.Json(ordinary.Session.Capture()), NativeBattleTestSnapshots.Json(captured.Session.Capture()));
        Assert.Equal(ordinary.Session.Events, captured.Session.Events);
        if (fault == "none")
        {
            BattleDiagnosticInvocationRelease release = Assert.Single(releases);
            Assert.True(release.Released);
            Assert.Equal(1, release.Owners.DescriptorPackets);
            Assert.Equal((ushort)10, release.CompletionPacket!.Command);
            Assert.True(release.HeaderCapturedBeforeReset);
            Assert.Equal(1, release.Dispatch.NativeHeaderGeneration);
        }
        else
        {
            Assert.Empty(releases);
            Assert.False(captured.Session.LastDiagnosticInvocationCompletion!.Released);
        }
        Assert.Null(ordinary.Session.LastDiagnosticInvocationCompletion);
        if (frameEndCase)
        {
            BattleDiagnosticInvocationRelease original = captured.Session.LastDiagnosticInvocationCompletion!;
            Assert.True(original.HeaderCapturedBeforeReset);
            Assert.True(original.NativeEventCompleted);
            Assert.Equal(BattleDiagnosticInvocationRelease.ExecutingBoundary, original.Boundary);
            Assert.False(original.Released);
            Assert.Empty(capturedResult.Descriptors.Queue);
            List<byte> completionWrites = [];
            NativeBattleAuxiliaryStep retiredAuxiliary = capturedResult.Auxiliary.Advance(new() { WriteCompletionByte = (_, value) => completionWrites.Add(value) });
            Assert.Equal((byte)255, Assert.Single(completionWrites));
            NativeBattleGraphicsState post = capturedResult with { Auxiliary = retiredAuxiliary.State, Callback = capturedResult.Callback + 1 };
            if (fault == "post-reuse")
            {
                NativeBattleDescriptorPacket originallyRetired = post.Descriptors.Packets[original.CompletionPacketSlot];
                NativeBattleDescriptorState reused = post.Descriptors;
                for (int index = 0; index < 256; index++)
                {
                    reused = reused.Enqueue(10, 128, 0x47DDA0).State;
                    reused = reused.Advance((state, _) => new(state, 15), (_, _) => throw new InvalidOperationException("Unexpected reused descriptor task."));
                }
                Assert.True(originallyRetired.Bytes.AsSpan().SequenceEqual(reused.Packets[original.CompletionPacketSlot].Bytes.AsSpan()));
                Assert.NotSame(originallyRetired, reused.Packets[original.CompletionPacketSlot]);
                post = post with { Descriptors = reused };
            }
            if (fault == "post-live")
            {
                post = post with { Auxiliary = post.Auxiliary.AppendDeferredCompletion5086C0(NativeBattleDescriptorState.CompletionAddress503040(0), (_, _) =>
                {
                }) };
            }
            if (fault == "post-callback")
            {
                Property(nameof(BattleSession.NativeHostCallbacks)).SetValue(captured.Session, 21L);
            }
            if (fault == "post-simulation")
            {
                Property(nameof(BattleSession.Frame)).SetValue(captured.Session, captured.Session.Frame + 1);
            }
            if (fault == "post-graphics")
            {
                post = post with { Callback = post.Callback + 1 };
            }
            if (fault == "post-foreign")
            {
                Field("diagnosticDispatchSerial").SetValue(captured.Session, 2L);
                Method("PublishDiagnosticEffectDispatch").Invoke(captured.Session, [new Action<BattleDiagnosticEffectDispatch>(_ =>
                {
                }), new BattleDiagnosticEffectDispatch(2, 20, "enemy", BattleAbility.Attack, 0, 9, 9)]);
            }
            Property(nameof(BattleSession.NativeGraphics)).SetValue(captured.Session, post);
            Method("ObserveDiagnosticInvocationGraphicsFrameEnd").Invoke(captured.Session, null);
            if (fault is "post-frame" or "post-duplicate")
            {
                BattleDiagnosticInvocationRelease released = Assert.Single(releases);
                Assert.True(released.PacketRemoved);
                Assert.Equal(BattleDiagnosticInvocationRelease.GraphicsFrameBoundary, released.Boundary);
                Assert.Equal(0, released.Owners.DescriptorPackets);
                Assert.Same(original, released.ExecutingCompletion);
                Assert.Equal(original.Callback, released.Callback);
                Assert.Equal(original.SimulationFrame, released.SimulationFrame);
                Assert.Equal(original.GraphicsCallback + 1, released.GraphicsCallback);
                Assert.True(original.CompletionPacket!.Bytes.SetItem(1, 0).AsSpan().SequenceEqual(released.RemovedCompletionPacket!.Bytes.AsSpan()));
                Method("ObserveDiagnosticInvocationGraphicsFrameEnd").Invoke(captured.Session, null);
                Assert.Single(releases);
            }
            else
            {
                Assert.Empty(releases);
                Assert.False(captured.Session.LastDiagnosticInvocationCompletion!.Released);
                if (fault is "post-live" or "post-no-removal" or "post-reuse")
                {
                    Assert.Equal(BattleDiagnosticInvocationRelease.GraphicsFrameBoundary, captured.Session.LastDiagnosticInvocationCompletion.Boundary);
                    Assert.NotNull(captured.Session.LastDiagnosticInvocationPendingOwners);
                }
            }
        }
        if (fault == "retention")
        {
            BattleDiagnosticInvocationRelease exactFocal = captured.Session.LastDiagnosticInvocationCompletion!;
            Assert.True(exactFocal.HeaderCapturedBeforeReset);
            Assert.True(exactFocal.NativeEventCompleted);
            Assert.Contains("contact-or-descriptor", exactFocal.Blockers);
            NativeBattleActionPacketsState laterPackets = captured.Session.NativeActionPackets;
            (laterPackets, _) = laterPackets.Prepare(new(0, 1, 13, -1, 0, 0, null));
            (laterPackets, _) = laterPackets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (laterPackets, _) = laterPackets.Publish(1);
            Field("nativeActionPackets").SetValue(captured.Session, laterPackets);
            Method("ObserveDiagnosticPreparedHeader").Invoke(captured.Session, [0]);
            Property(nameof(BattleSession.NativeHostCallbacks)).SetValue(captured.Session, 40L);
            SetAction(captured.Session, new("party", "enemy", BattleActionPhase.Windup, 1, BattleAbility.Attack, 0) { NativeHeaderIndex = 0, NativeQueuedCommand = 1, NativeGraphic = new(1) });
            NativeBattleDescriptorState laterDescriptors = capturedResult.Descriptors.RemoveFirst().Enqueue(10, 128, 0x47DDA0).State;
            NativeBattleGraphicsState laterResult = Complete(captured.Session, capturedResult with { Descriptors = laterDescriptors }, 0x47DDA0);
            Assert.Null(captured.Session.ActiveAction);
            Assert.Equal((byte)1, captured.Session.NativeActionPackets.Headers[0].ReadByte(1));
            Assert.Equal(2, captured.Session.Events.Count(value => value.Kind == "ActionComplete"));
            Assert.Same(exactFocal, captured.Session.LastDiagnosticInvocationCompletion);
            Assert.Equal(20, exactFocal.Callback);
            Assert.False(exactFocal.Released);
            Assert.Equal(laterResult.PendingContacts, capturedResult.PendingContacts);
            Assert.Empty(releases);
        }
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
