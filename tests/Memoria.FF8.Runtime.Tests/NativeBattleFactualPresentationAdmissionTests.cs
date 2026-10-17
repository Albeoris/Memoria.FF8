using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFactualPresentationAdmissionTests
{
    [Fact]
    public void CurrentAiHeaderMirrorsWhileItsOwn104And10RemainPendingWithoutReproduction()
    {
        var fixture = Fixture(3);
        BattleAttackCommand command = Command("enemy", 0);
        Queue(fixture.Session).Enqueue(command);
        string packets = JsonSerializer.Serialize(fixture.Session.NativeActionPackets);
        string descriptors = JsonSerializer.Serialize(fixture.Graphics.Descriptors);
        Invoke(fixture.Session, "BeginNativeBattleActionPause");
        Invoke(fixture.Session, "DispatchAction");
        BattleActionPresentation active = Assert.IsType<BattleActionPresentation>(fixture.Session.ActiveAction);
        Assert.Equal("enemy", active.ActorId);
        Assert.Equal((int?)0, active.NativeHeaderIndex);
        Assert.Same(command.NativeAction, active.NativeAction);
        Assert.Equal((byte?)1, active.NativeAiCallbackStage);
        Assert.Empty(fixture.Session.QueuedActions);
        Assert.Equal(packets, JsonSerializer.Serialize(fixture.Session.NativeActionPackets));
        Assert.Equal(descriptors, JsonSerializer.Serialize(fixture.Session.NativeGraphics!.Descriptors));
        Assert.DoesNotContain(fixture.Session.Events, item => item.Kind == "Damage");
        Assert.Equal(1u, fixture.Session.Capture().NativeClock!.ActionPause);
        Complete(fixture.Session);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
        Assert.Equal(0, fixture.Session.NativeActionPackets.ActionCount);
        Assert.Single(fixture.Session.Events, item => item.Kind == "ActionComplete");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingDescriptorsDoNotSearchPastUnproducedOrCallbackHead(bool callback)
    {
        var fixture = Fixture(3);
        BattleAttackCommand first = callback ? new("enemy", "enemy", BattleAbility.EnemyAbility, 0) { NativeCallback = new(3, 1) } : Command("enemy", null);
        Queue(fixture.Session).Enqueue(first);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        string before = JsonSerializer.Serialize(fixture.Session.Capture());
        Invoke(fixture.Session, "DispatchAction");
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.Capture()));
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhysicalCallbackBankStillRequiresItsClockAndDescriptorAdmission(bool pending)
    {
        var fixture = Fixture(3);
        if (!pending)
        {
            Property(nameof(BattleSession.NativeGraphics)).SetValue(fixture.Session, new NativeBattleGraphicsState([], [], 0, 0));
        }
        Method("QueueNativeCallback").Invoke(fixture.Session, [fixture.Session.Combatants.Single(actor => actor.Id == "enemy"), (ushort)1, (byte)1]);
        Invoke(fixture.Session, "BeginNativeBattleActionPause");
        string before = JsonSerializer.Serialize(fixture.Session.Capture());
        Invoke(fixture.Session, "DispatchAction");
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.Capture()));
        Assert.Null(fixture.Session.ActiveAction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoricalHeaderNeverReentersPauseWhetherDescriptorsArePending(bool pending)
    {
        var fixture = Fixture(3);
        SetField(fixture.Session, "nativeActionPackets", fixture.Session.NativeActionPackets.ResetCounts());
        if (!pending)
        {
            Property(nameof(BattleSession.NativeGraphics)).SetValue(fixture.Session, new NativeBattleGraphicsState([], [], 0, 0));
        }
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Invoke(fixture.Session, "DispatchAction"));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
    }

    [Fact]
    public void WrongPhysicalActorAndOutOfRangeHeaderRemainStrictBeforePause()
    {
        var fixture = Fixture(0);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Invoke(fixture.Session, "DispatchAction"));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);

        fixture = Fixture(3);
        Queue(fixture.Session).Enqueue(Command("enemy", NativeBattleActionPacketsState.HeaderCapacity));
        error = Assert.Throws<TargetInvocationException>(() => Invoke(fixture.Session, "DispatchAction"));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
    }

    [Fact]
    public void UnknownPublishedTargetByteCannotBeInferredFromQueuedMetadata()
    {
        var fixture = Fixture(3);
        NativeBattleActionPacketsState packets = fixture.Session.NativeActionPackets;
        packets = packets with { Headers = packets.Headers.SetItem(0, packets.Headers[0] with { UnknownByteMask = packets.Headers[0].UnknownByteMask | (1u << 16) }) };
        SetField(fixture.Session, "nativeActionPackets", packets);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Invoke(fixture.Session, "DispatchAction"));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
    }

    [Fact]
    public void OneSuccessfulEventReleasesActiveAndSecondCurrentHeaderButPreservesNewAndOtherActorCommands()
    {
        var fixture = Fixture(3, 3, 0);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        BattleAttackCommand second = Command("enemy", 1);
        Queue(fixture.Session).Enqueue(second);
        BattleAttackCommand other = Command("party", 2);
        BattleAttackCommand next = Command("enemy", null);
        Queue(fixture.Session).Enqueue(other);
        Queue(fixture.Session).Enqueue(next);
        Invoke(fixture.Session, "DispatchAction");
        Complete(fixture.Session);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(new[] { other, next }, Queue(fixture.Session).ToArray());
        Assert.Equal(2, fixture.Session.Events.Count(item => item.Kind == "ActionComplete"));
        Assert.True(fixture.Session.Combatants.Single(actor => actor.Id == "enemy").IsBusy);
        Assert.Equal(0, fixture.Session.NativeActionPackets.ActionCount);
    }

    [Fact]
    public void QueuedCurrentEventCompletionDoesNotClearUnrelatedActivePresentationOrGraphicsOwner()
    {
        var fixture = Fixture(3, 0);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        BattleActionPresentation unrelated = new("party", "enemy", BattleActionPhase.Windup, 1, BattleAbility.EnemyAbility, 1) { NativeHeaderIndex = 1 };
        Property(nameof(BattleSession.ActiveAction)).SetValue(fixture.Session, unrelated);
        Property(nameof(BattleSession.NativeGraphics)).SetValue(fixture.Session, fixture.Graphics with { ActionActorId = "party", ActionCallbackReady = true });
        Complete(fixture.Session);
        Assert.Same(unrelated, fixture.Session.ActiveAction);
        Assert.Equal("party", fixture.Session.NativeGraphics!.ActionActorId);
        Assert.True(fixture.Session.NativeGraphics.ActionCallbackReady);
        Assert.Empty(Queue(fixture.Session));
        Assert.Single(fixture.Session.Events, item => item.Kind == "ActionComplete");
    }

    [Fact]
    public void UnknownEventActorCannotConsumeQueuedPhysicalHeaders()
    {
        var fixture = Fixture(3);
        BattleAttackCommand command = Command("enemy", 0);
        Queue(fixture.Session).Enqueue(command);
        SetField(fixture.Session, "nativeActionScratch", fixture.Session.Capture().NativeActionScratch! with { EventActorB0F = null });
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Complete(fixture.Session));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(command, Assert.Single(Queue(fixture.Session)));
        Assert.Equal(1, fixture.Session.NativeActionPackets.ActionCount);
    }

    [Fact]
    public void InstantEventConsumesBothCurrentMirrorsWithoutInventingALateWindup()
    {
        var fixture = Fixture(3, 3);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        Queue(fixture.Session).Enqueue(Command("enemy", 1));
        Complete(fixture.Session);
        Assert.Empty(Queue(fixture.Session));
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Equal(2, fixture.Session.Events.Count(item => item.Kind == "ActionComplete"));
        Assert.DoesNotContain(fixture.Session.Events, item => item.Kind == "Windup");
        Invoke(fixture.Session, "DispatchAction");
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
    }

    [Fact]
    public void PostEffectEarlyReturnPreservesBothOriginalMirrorsUntilFinalEvent()
    {
        var fixture = Fixture(3, 3);
        Queue(fixture.Session).Enqueue(Command("enemy", 0));
        BattleAttackCommand second = Command("enemy", 1);
        Queue(fixture.Session).Enqueue(second);
        Invoke(fixture.Session, "DispatchAction");
        BattleActionPresentation first = fixture.Session.ActiveAction!;
        SetField(fixture.Session, "nativeActionScratch", fixture.Session.Capture().NativeActionScratch! with { Command = 28 });
        Complete(fixture.Session);
        Assert.Same(first, fixture.Session.ActiveAction);
        Assert.Same(second, Queue(fixture.Session).Peek());
        Assert.Equal(3, fixture.Session.NativeActionPackets.ActionCount);
        Assert.Equal(90, fixture.Session.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(1u, fixture.Session.Capture().NativeClock!.ActionPause);
        Assert.DoesNotContain(fixture.Session.Events, item => item.Kind == "ActionComplete");
        Method("RunNativeDescriptorActionCallback").Invoke(fixture.Session, [fixture.Session.NativeGraphics!, 0x47E120u]);
        Assert.Null(fixture.Session.ActiveAction);
        Assert.Empty(Queue(fixture.Session));
        Assert.Equal(3, fixture.Session.Events.Count(item => item.Kind == "ActionComplete"));
        Assert.Equal(0u, fixture.Session.Capture().NativeClock!.ActionPause);
    }

    [Fact]
    public void JsonCurrentQueuedHeadersAdmitAndCompleteIdentically()
    {
        var first = Fixture(3, 3);
        var second = Fixture(3, 3);
        foreach (var fixture in new[] { first, second })
        {
            Queue(fixture.Session).Enqueue(Command("enemy", 0));
            Queue(fixture.Session).Enqueue(Command("enemy", 1));
        }
        SetField(second.Session, "nativeActionPackets", JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(second.Session.NativeActionPackets))!);
        BattleAttackCommand[] restored = JsonSerializer.Deserialize<BattleAttackCommand[]>(JsonSerializer.Serialize(Queue(second.Session)))!;
        Queue(second.Session).Clear();
        foreach (BattleAttackCommand command in restored)
        {
            Queue(second.Session).Enqueue(command);
        }
        Invoke(first.Session, "DispatchAction");
        Invoke(second.Session, "DispatchAction");
        Complete(first.Session);
        Complete(second.Session);
        Assert.Equal(JsonSerializer.Serialize(first.Session.Capture()), JsonSerializer.Serialize(second.Session.Capture()));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(params byte[] actors)
    {
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 100 }]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        Invoke(session, "BeginNativeReactionBatch");
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        foreach (byte actor in actors)
        {
            (packets, _) = packets.Prepare(new(actor, 2, 11, -1, 1, 2, null));
            (packets, _) = packets.AppendContact(new(new(actor == 0 ? (byte)3 : (byte)0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        SetField(session, "nativeActionPackets", packets);
        SetField(session, "nativeActionScratch", session.Capture().NativeActionScratch! with { EventActorB0F = 3, EventLane = 0, Command = 2 });
        NativeBattleGraphicsState graphics = new([], [], 0, 0);
        graphics = graphics with { Descriptors = graphics.Descriptors.Enqueue(104, 128, 0x1D280C4).State };
        graphics = graphics with { Descriptors = graphics.Descriptors.Enqueue(10, 128, 0x47DE70).State };
        Property(nameof(BattleSession.NativeGraphics)).SetValue(session, graphics);
        return (session, graphics);
    }

    private static BattleAttackCommand Command(string actor, int? header)
    {
        BattleNativeAction native = new(2, 1, 1, 1) { Name = "Fire", MagicId = 2, AttackType = 2, Power = 18, HitCount = 1, TargetFlags = 84, AttackFlags = 49, DamagePresentation = 5, Element = 1, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        return new(actor, actor == "party" ? "enemy" : "party", BattleAbility.EnemyAbility, 1) { NativeHeaderIndex = header, NativeAction = native, NativeAiCallbackStage = 1, NativeQueuedCommand = 2, NativeSequence = 11, NativeTargetMask = actor == "party" ? (ushort)8 : (ushort)1 };
    }

    private static void Complete(BattleSession session) => Method("RunNativeDescriptorActionCallback").Invoke(session, [session.NativeGraphics!, 0x47DE70u]);
    private static Queue<BattleAttackCommand> Queue(BattleSession session) => (Queue<BattleAttackCommand>)typeof(BattleSession).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    private static void SetField(BattleSession session, string name, object value) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, value);
    private static void Invoke(BattleSession session, string name) => Method(name).Invoke(session, null);
    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static PropertyInfo Property(string name) => typeof(BattleSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!;
}
