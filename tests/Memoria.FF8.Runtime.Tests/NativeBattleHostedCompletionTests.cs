using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedCompletionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ActorId = "extended";
    private static readonly BattlePartyActorModel Owner = new(new(ActorId));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));
    private static readonly BattleEnemyActorModel Missing = new(new("missing"));
    private static readonly BattleActionHeaderReference First = new(0);
    private static readonly NativeBattleActionHeaderPreparation Header = new(Owner, 1, 5, -1, 0, 0, null);
    private static readonly NativeBattleContactPacket Contact = new(new(Enemy, 0, 1, 0, 0, 0, 0), null);

    [Theory]
    [InlineData(0x47DDA0u, true)]
    [InlineData(0x47DE70u, true)]
    [InlineData(0x47DE70u, false)]
    [InlineData(0x47E120u, true)]
    public void Actual_descriptor_execution_requests_one_ordered_acknowledgement(uint callback, bool reset)
    {
        var (logical, child, process) = Fixture();
        BattleSession.PreparedHostedDispatch staleAction = child.PrepareHostedAction(Header, [Contact]);
        NativeBattleActionPacketsState packets = PopulatedPackets(child.NativeActionPackets);
        Field(child, "nativeActionPackets").SetValue(child, packets);
        NativeBattleGraphicsState before = child.NativeGraphics;
        BattleSession.PreparedHostedDispatch prepared = process.PrepareCompletion(callback, Owner, ActorId);
        Assert.Same(before, child.NativeGraphics);
        Assert.Same(packets, child.NativeActionPackets);

        long token = process.PublishCompletion(prepared);
        Assert.Null(process.PendingCompletion);
        Assert.False(process.IsCompletionAcknowledged(token));
        Assert.Throws<InvalidOperationException>(() => child.PublishHostedAction(staleAction));
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletion(prepared));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletionAck(token, true));
        NativeBattleDescriptorPacket queued = child.NativeGraphics!.Descriptors.Packets[Assert.Single(child.NativeGraphics.Descriptors.Queue)];
        Assert.Equal((ushort)10, queued.Command);
        Assert.Equal((byte)128, queued.PriorityState);
        Assert.Equal(callback, queued.Argument);

        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(before.ActorModels.Roster);
        services.Emit(new NativeBattleModelParticipantReleased(Owner, ActorId));
        RunDescriptors(child, services);
        services.Emit(new NativeBattleModelParticipantSuspended(Owner, ActorId, 3));
        NativeBattlePresentationCompletionRequested requested = Assert.IsType<NativeBattlePresentationCompletionRequested>(services.ReadOrderedReceipts()[1]);
        Assert.Collection(services.ReadOrderedReceipts(), item => Assert.IsType<NativeBattleModelParticipantReleased>(item), item => Assert.Same(requested, item), item => Assert.IsType<NativeBattleModelParticipantSuspended>(item));
        Assert.Equal(new NativeBattlePresentationCompletionRequested(token, callback, Owner, ActorId), requested);
        Assert.Same(requested, process.PendingCompletion);
        Assert.Empty(child.NativeGraphics!.Descriptors.Queue);
        Assert.Null(child.HostedActiveActionToken);
        Assert.Same(packets, child.NativeActionPackets);
        Assert.False(process.IsCompletionAcknowledged(token));

        NativeBattleGraphicsState retired = child.NativeGraphics;
        long epoch = (long)Field(child, "hostedPreparationEpoch").GetValue(child)!;
        NativeGlobalRandomChange? random = child.NativeGlobalRandomChange;
        NativeGlobalRandomChange? mainRandom = logical.NativeGlobalRandomChange;
        NativeExamTimerState? mainTimer = logical.NativeExamTimer;
        long? mainCallbacks = logical.NativeHostCallbacks;
        Field(logical, "expandedPresentationProcess").SetValue(logical, process);
        Assert.Throws<InvalidOperationException>(() => logical.Tick());
        Assert.Throws<InvalidOperationException>(() => process.Advance(logical));
        Assert.Throws<InvalidOperationException>(() => child.AdvanceHostedBattlePresentationFrame(logical, null));
        Assert.Throws<InvalidOperationException>(() => process.AccumulateElapsed(TimeSpan.FromSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletion(callback, Owner, ActorId));
        Assert.Throws<InvalidOperationException>(() => child.PrepareHostedAction(Header, [Contact]));
        Assert.Same(retired, child.NativeGraphics);
        Assert.Equal(epoch, Field(child, "hostedPreparationEpoch").GetValue(child));
        Assert.Equal(random, child.NativeGlobalRandomChange);
        Assert.Equal(mainRandom, logical.NativeGlobalRandomChange);
        Assert.Same(mainTimer, logical.NativeExamTimer);
        Assert.Equal(mainCallbacks, logical.NativeHostCallbacks);
        if (callback != 0x47DE70)
        {
            Assert.Throws<InvalidDataException>(() => process.PrepareCompletionAck(token, false));
        }

        BattleSession.PreparedHostedCompletionAck ack = process.PrepareCompletionAck(token, reset);
        var (_, _, foreign) = Fixture();
        Assert.Throws<InvalidOperationException>(() => foreign.PublishCompletionAck(ack));
        SetGraphics(child, retired with { });
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(ack));
        Assert.Same(packets, child.NativeActionPackets);
        Assert.Same(requested, process.PendingCompletion);

        ack = process.PrepareCompletionAck(token, reset);
        process.ValidatePreparedCompletionAck(ack);
        process.PublishCompletionAck(ack);
        Assert.True(process.IsCompletionAcknowledged(token));
        Assert.Null(process.PendingCompletion);
        Assert.Equal(reset ? 0 : 1, child.NativeActionPackets.ActionCount);
        Assert.Equal(reset ? 0 : 1, child.NativeActionPackets.ContactCount);
        Assert.Same(packets.ReadHeader(First), child.NativeActionPackets.ReadHeader(First));
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(ack));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletionAck(token, true));
        Assert.Equal(1L, child.BeginHostedAction(Header, [Contact]));
    }

    [Fact]
    public void Later_descriptor_failure_preserves_the_retired_completion_and_remaining_queue()
    {
        var (logical, child, process) = Fixture();
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DDA0, Owner, ActorId));
        NativeBattleGraphicsState admitted = child.NativeGraphics!;
        int completionSlot = Assert.Single(admitted.Descriptors.Queue);
        (NativeBattleDescriptorState queued, int unsupportedSlot) = admitted.Descriptors.Enqueue(999, 128, 0);
        SetGraphics(child, admitted with { Descriptors = queued });
        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(admitted.ActorModels.Roster);

        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => RunDescriptors(child, services));

        Assert.Contains("descriptor999", Assert.IsType<NotSupportedException>(failure.InnerException).Message);
        NativeBattlePresentationCompletionRequested receipt = Assert.IsType<NativeBattlePresentationCompletionRequested>(Assert.Single(services.ReadOrderedReceipts()));
        Assert.Same(receipt, process.PendingCompletion);
        Assert.Equal(token, receipt.Token);
        NativeBattleGraphicsState retired = child.NativeGraphics!;
        Assert.Equal(unsupportedSlot, Assert.Single(retired.Descriptors.Queue));
        Assert.Equal((byte)0, retired.Descriptors.Packets[completionSlot].PriorityState);
        Assert.Same(queued.Packets[unsupportedSlot], retired.Descriptors.Packets[unsupportedSlot]);
        NativeBattleActionPacketsState packets = child.NativeActionPackets;
        object? epoch = Field(child, "hostedPreparationEpoch").GetValue(child);
        NativeGlobalRandomChange? childRandom = child.NativeGlobalRandomChange;
        NativeGlobalRandomChange? mainRandom = logical.NativeGlobalRandomChange;
        NativeExamTimerState? childTimer = child.NativeExamTimer;
        NativeExamTimerState? mainTimer = logical.NativeExamTimer;
        long? mainCallbacks = logical.NativeHostCallbacks;
        Field(logical, "expandedPresentationProcess").SetValue(logical, process);
        Assert.Throws<InvalidOperationException>(() => logical.Tick());
        Assert.Throws<InvalidOperationException>(() => process.Advance(logical));
        Assert.Throws<InvalidOperationException>(() => process.AccumulateElapsed(TimeSpan.FromSeconds(1)));
        Assert.Same(retired, child.NativeGraphics);
        Assert.Equal(epoch, Field(child, "hostedPreparationEpoch").GetValue(child));
        Assert.Equal(childRandom, child.NativeGlobalRandomChange);
        Assert.Equal(mainRandom, logical.NativeGlobalRandomChange);
        Assert.Same(childTimer, child.NativeExamTimer);
        Assert.Same(mainTimer, logical.NativeExamTimer);
        Assert.Equal(mainCallbacks, logical.NativeHostCallbacks);

        process.PublishCompletionAck(process.PrepareCompletionAck(token, true));
        Assert.True(process.IsCompletionAcknowledged(token));
        Assert.Null(process.PendingCompletion);
        Assert.Same(retired, child.NativeGraphics);
        Assert.Equal(unsupportedSlot, Assert.Single(child.NativeGraphics!.Descriptors.Queue));
        Assert.Same(packets.FindStoredContact(new BattleContactOccurrence(0)), child.NativeActionPackets.FindStoredContact(new BattleContactOccurrence(0)));
        Assert.Same(receipt, Assert.Single(services.ReadOrderedReceipts()));
    }

    [Fact]
    public void Retired_completion_cannot_be_acknowledged_after_its_actor_binding_changes()
    {
        var (logical, child, process) = Fixture();
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DDA0, Owner, ActorId));
        RunDescriptors(child, logical.CaptureModelLifecycleServices(child.NativeGraphics!.ActorModels.Roster));
        NativeBattlePresentationCompletionRequested request = Assert.IsType<NativeBattlePresentationCompletionRequested>(process.PendingCompletion);
        NativeBattleGraphicsState retired = child.NativeGraphics!;
        NativeBattleActionPacketsState packets = child.NativeActionPackets;
        BattleSession.PreparedHostedCompletionAck prepared = process.PrepareCompletionAck(token, true);

        BattlePartyActorModel replacement = new(new("replacement"));
        NativeBattleGraphicsState rebound = retired with { ActorModels = retired.ActorModels.ReplaceOwner(Owner, replacement, retired.ActorModels.Get(Owner)) };
        SetGraphics(child, rebound);
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(prepared));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletionAck(token, true));
        Assert.Same(rebound, child.NativeGraphics);
        Assert.Same(request, process.PendingCompletion);
        Assert.Same(packets, child.NativeActionPackets);
        Assert.False(process.IsCompletionAcknowledged(token));
    }

    [Fact]
    public void Unowned_combat_callback_and_invalid_admission_leave_state_unchanged()
    {
        var (logical, child, process) = Fixture();
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        BattleSession.PreparedHostedDispatch prepared = process.PrepareCompletion(0x47DDA0, Owner, ActorId);
        Assert.Throws<ArgumentOutOfRangeException>(() => process.PrepareCompletion(0x47DD70, Owner, ActorId));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletion(0x47DDA0, Missing, ActorId));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletion(0x47DDA0, Owner, "foreign"));
        Assert.Same(graphics, child.NativeGraphics);
        var (_, _, foreign) = Fixture();
        Assert.Throws<InvalidOperationException>(() => foreign.PublishCompletion(prepared));
        var (queue, _) = graphics.Descriptors.Enqueue(10, 128, 0x47DDA0);
        NativeBattleGraphicsState unowned = graphics with { Descriptors = queue };
        SetGraphics(child, unowned);
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletion(prepared));

        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => RunDescriptors(child, services));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(unowned, child.NativeGraphics);
        Assert.Empty(services.ReadOrderedReceipts());
        Assert.Null(process.PendingCompletion);
    }

    [Fact]
    public void Retired_completion_and_appended_dispatch_share_one_atomic_packet_transition()
    {
        var (logical, child, process) = Fixture();
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        BattleModelRoster roster = graphics.ActorModels.Roster;
        int boundary = NativeBattleRetainedActionPackets.ContactCapacity;
        NativeBattleActionPacketsState packets = child.NativeActionPackets.Prepare(Header).State;
        packets = packets.AppendContacts(Enumerable.Repeat(Contact, boundary)).Publish(boundary).State;
        packets = packets.Prepare(Header with { Command = 0, Sequence = 10, Action = 9 }).State.Publish(0).State;
        NativeBattleActionHeaderSlot retainedEmpty = packets.ReadHeader(new BattleActionHeaderReference(1));
        Field(child, "nativeActionPackets").SetValue(child, packets);
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DE70, Owner, ActorId));
        RunDescriptors(child, logical.CaptureModelLifecycleServices(roster));
        NativeBattlePresentationCompletionRequested pending = Assert.IsType<NativeBattlePresentationCompletionRequested>(process.PendingCompletion);
        NativeBattleGraphicsState retired = child.NativeGraphics!;
        BattleSession.PreparedHostedCompletionAck ack = process.PrepareCompletionAck(token, false);
        BattleSession.HostedDispatchCompletion completion = new(0x47E120, Owner, ActorId);
        BattleSession.HostedDispatchHeader addition = new(Header, [Contact]);
        NativeBattleContactPacket unbound = Contact with { Primary = Contact.Primary with { Recipient = Missing } };

        Assert.Throws<InvalidDataException>(() => process.PrepareDispatch([addition, new(Header, [unbound])], dispatchAppended: true, completion: completion, acknowledgment: ack));
        Assert.Throws<InvalidDataException>(() => child.PrepareHostedDispatch([addition], dispatchAppended: true, completion: completion, acknowledgment: ack, freshActionPackets: true));
        BattleSession.PreparedHostedDispatch grown = process.PrepareDispatch([.. Enumerable.Repeat(addition, NativeBattleRetainedActionPackets.HeaderCapacity)], dispatchAppended: true, completion: completion, acknowledgment: ack);
        Assert.Equal(NativeBattleRetainedActionPackets.HeaderCapacity + 2, grown.Packets.ActionCount);
        Assert.Equal(NativeBattleRetainedActionPackets.HeaderCapacity, grown.Packets.ExportRetained().Headers.Length);
        Assert.Equal(Contact, Assert.Single(grown.Packets.ReadHeaderContacts(grown.AppendedHeaders[^1])));
        Assert.Same(packets, child.NativeActionPackets);
        Assert.Same(retired, child.NativeGraphics);
        Assert.Same(pending, process.PendingCompletion);

        BattleSession.PreparedHostedDispatch prepared = process.PrepareDispatch([addition], dispatchAppended: true, completion: completion, acknowledgment: ack);
        Assert.Same(packets, child.NativeActionPackets);
        Assert.Same(pending, process.PendingCompletion);
        Assert.Equal(new BattleActionHeaderReference(2), Assert.Single(prepared.AppendedHeaders));
        Assert.Equal(boundary + 1, prepared.Packets.ContactCount);
        Assert.Same(packets.ReadHeader(First), prepared.Packets.ReadHeader(First));
        NativeBattleActionHeaderSlot movedEmpty = prepared.Packets.ReadHeader(new BattleActionHeaderReference(1));
        Assert.Equal(new BattleContactCursor(boundary), movedEmpty.FirstContact);
        Assert.Same(retainedEmpty, movedEmpty);
        Assert.Equal(boundary, prepared.Packets.ExportRetained().Headers[1].FirstContactIndex);
        Assert.Equal(packets.ExportRetained().Headers[1], prepared.Packets.ExportRetained().Headers[1]);
        Assert.Equal((byte)0, prepared.Packets.ExportRetained().Headers[2].ReadOriginalByte(17));
        prepared.Packets.Validate();

        NativeBattleEffectContactBridge<string> bridge = new([("enemy", Contact)], roster, new BattleContactCursor(boundary));
        NativeBattleContactPacket appended = Assert.Single(prepared.Packets.ReadHeaderContacts(new BattleActionHeaderReference(2)));
        Assert.Equal(new BattleContactOccurrence(boundary), appended.Occurrence);
        Assert.Equal((0, "enemy"), bridge.ResolveOccurrence(appended));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(prepared.Packets.ReadContact(new BattleContactOccurrence(boundary - 1))));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(appended with { Occurrence = new BattleContactOccurrence(boundary + 1) }));
        NativeBattleEffectContactBridge<string> following = new([("enemy", Contact), ("enemy", Contact)], roster, new BattleContactCursor(boundary + 1));
        Assert.Equal(2, following.Count);

        process.ValidatePreparedDispatch(prepared);
        process.CommitDispatch(prepared);
        Assert.True(process.IsCompletionAcknowledged(token));
        Assert.Null(process.PendingCompletion);
        Assert.Same(prepared.Packets, child.NativeActionPackets);
        Assert.Collection(child.NativeGraphics!.Descriptors.Queue,
            slot => Assert.Equal((ushort)104, child.NativeGraphics.Descriptors.Packets[slot].Command),
            slot => Assert.Equal((ushort)10, child.NativeGraphics.Descriptors.Packets[slot].Command));
        Assert.Throws<InvalidOperationException>(() => process.ValidatePreparedDispatch(prepared));
        Assert.Equal(new BattleActionHeaderReference(2), prepared.Action!.Invocation.Header);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0x40, false)]
    [InlineData(0x100, false)]
    public void Native_self_contact_controls_wait_for_the_bound_extended_target(ushort resourceFlags, bool busy)
    {
        var (_, child, _) = Fixture();
        NativeBattleGraphicsState graphics = child.NativeGraphics!;
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(Owner) with { Flags0 = 0, Flags8 = 0, Kind5 = 0, ResourceFlags44 = resourceFlags };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(Owner, actor) };
        NativeBattleActionHeaderPreparation self = Header with { Command = 0, Sequence = 0, Action = 2 };
        NativeBattleContactPacket contact = Contact with { Primary = Contact.Primary with { Recipient = Owner } };
        NativeBattleActionPacketsState packets = child.NativeActionPackets.Prepare(self).State;
        packets = packets.AppendContacts([contact]).Publish(1).State.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, new BattleActionHeaderReference(0));
        Assert.True(context.TargetActors.IsSingle(Owner));
        Assert.Equal(busy, typeof(BattleSession).GetMethod("NativeHeaderTargetsBusy", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [graphics, context]));
    }

    private static NativeBattleActionPacketsState PopulatedPackets(NativeBattleActionPacketsState packets)
    {
        (packets, _) = packets.Prepare(Header);
        packets = packets.AppendContacts([Contact]);
        return packets.Publish(1).State;
    }

    private static (BattleSession Logical, BattleSession Child, ExpandedBattlePresentationProcess Process) Fixture()
    {
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [.. Enumerable.Range(0, 3).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true }), new() { Id = ActorId, Name = "Extended", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeGlobalRandom = new(123)
        };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeExamTimer))!.SetValue(logical, NativeExamTimerState.FromColdProcess().SetRemaining(100));
        BattleSession child = new(definition, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => new(0) { ModelId4 = owner == Owner ? (byte)1 : (byte)0 });
        SetGraphics(child, graphics with { EntryReady = true, EntryLifecycle = 4 });
        Field(child, "hostedBattlePresentation").SetValue(child, true);
        FieldInfo entry = Field(child, "hostedEntryDescriptors");
        entry.SetValue(child, Activator.CreateInstance(entry.FieldType));
        Field(child, "presentationNativeTargets").SetValue(child, new Dictionary<BattleModelOwner, NativeBattleEffectModelBinding>
        {
            [Owner] = new(Owner, graphics.ActorModels.Get(Owner), new([0, 0]))
        });
        var process = (ExpandedBattlePresentationProcess)RuntimeHelpers.GetUninitializedObject(typeof(ExpandedBattlePresentationProcess));
        Field(process, "logicalOwner").SetValue(process, logical);
        Field(process, "presentation").SetValue(process, child);
        return (logical, child, process);
    }

    private static void RunDescriptors(BattleSession child, NativeBattleModelLifecycleServices services)
    {
        FieldInfo active = Field(child, "hostedFrameServices");
        active.SetValue(child, services);
        try
        {
            var graphics = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("AdvanceNativeDescriptors", PrivateInstance)!.Invoke(child, [child.NativeGraphics])!;
            SetGraphics(child, graphics);
        }
        finally
        {
            active.SetValue(child, null);
        }
    }

    private static void SetGraphics(BattleSession child, NativeBattleGraphicsState graphics)
    {
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(child, graphics);
    }

    private static FieldInfo Field(object owner, string name)
    {
        return owner.GetType().GetField(name, PrivateInstance)!;
    }
}
