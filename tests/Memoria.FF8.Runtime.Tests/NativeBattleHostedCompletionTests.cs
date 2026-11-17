using System.Collections.Immutable;
using System.Reflection;

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
        var (logical, process) = Fixture();
        BattlePresentationProcess.PreparedHostedDispatch staleAction = process.PrepareHostedAction(Header, [Contact]);
        NativeBattleActionPacketsState packets = PopulatedPackets(process.NativeActionPackets);
        Field(process, "nativeActionPackets").SetValue(process, packets);
        NativeBattleGraphicsState before = Assert.IsType<NativeBattleGraphicsState>(process.Graphics);
        BattlePresentationProcess.PreparedHostedDispatch prepared = process.PrepareCompletion(callback, Owner, ActorId);
        Assert.Same(before, process.Graphics);
        Assert.Same(packets, process.NativeActionPackets);

        long token = process.PublishCompletion(prepared);
        Assert.Null(process.PendingCompletion);
        Assert.False(process.IsCompletionAcknowledged(token));
        Assert.Throws<InvalidOperationException>(() => process.PublishHostedAction(staleAction));
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletion(prepared));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletionAck(token, true));
        NativeBattleDescriptorPacket queued = process.Graphics.Descriptors.Packets[Assert.Single(process.Graphics.Descriptors.Queue)];
        Assert.Equal((ushort)10, queued.Command);
        Assert.Equal((byte)128, queued.PriorityState);
        Assert.Equal(callback, queued.Argument);

        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(before.ActorModels.Roster);
        services.Emit(new NativeBattleModelParticipantReleased(Owner, ActorId));
        RunDescriptors(process, services);
        services.Emit(new NativeBattleModelParticipantSuspended(Owner, ActorId, 3));
        NativeBattlePresentationCompletionRequested requested = Assert.IsType<NativeBattlePresentationCompletionRequested>(services.ReadOrderedReceipts()[1]);
        Assert.Collection(services.ReadOrderedReceipts(), item => Assert.IsType<NativeBattleModelParticipantReleased>(item), item => Assert.Same(requested, item), item => Assert.IsType<NativeBattleModelParticipantSuspended>(item));
        Assert.Equal(new NativeBattlePresentationCompletionRequested(token, callback, Owner, ActorId), requested);
        Assert.Same(requested, process.PendingCompletion);
        Assert.Empty(process.Graphics.Descriptors.Queue);
        Assert.Null(process.HostedActiveActionToken);
        Assert.Same(packets, process.NativeActionPackets);
        Assert.False(process.IsCompletionAcknowledged(token));

        NativeBattleGraphicsState retired = process.Graphics;
        long epoch = (long)Field(process, "hostedPreparationEpoch").GetValue(process)!;
        NativeGlobalRandomChange? random = process.NativeGlobalRandomChange;
        NativeGlobalRandomChange? mainRandom = logical.NativeGlobalRandomChange;
        NativeExamTimerState? mainTimer = logical.NativeExamTimer;
        long? mainCallbacks = logical.NativeHostCallbacks;
        Field(logical, "expandedPresentationProcess").SetValue(logical, process);
        Assert.Throws<InvalidOperationException>(() => logical.Tick());
        Assert.Throws<InvalidOperationException>(() => process.Advance(static () => { }, static (_, _) => { }));
        Assert.Throws<InvalidOperationException>(() => process.AdvanceHostedBattlePresentationFrame(null));
        Assert.Throws<InvalidOperationException>(() => process.AccumulateElapsed(TimeSpan.FromSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletion(callback, Owner, ActorId));
        Assert.Throws<InvalidOperationException>(() => process.PrepareHostedAction(Header, [Contact]));
        Assert.Same(retired, process.Graphics);
        Assert.Equal(epoch, Field(process, "hostedPreparationEpoch").GetValue(process));
        Assert.Equal(random, process.NativeGlobalRandomChange);
        Assert.Equal(mainRandom, logical.NativeGlobalRandomChange);
        Assert.Same(mainTimer, logical.NativeExamTimer);
        Assert.Equal(mainCallbacks, logical.NativeHostCallbacks);
        if (callback != 0x47DE70)
        {
            Assert.Throws<InvalidDataException>(() => process.PrepareCompletionAck(token, false));
        }

        BattlePresentationProcess.PreparedHostedCompletionAck ack = process.PrepareCompletionAck(token, reset);
        var (_, foreign) = Fixture();
        Assert.Throws<InvalidOperationException>(() => foreign.PublishCompletionAck(ack));
        SetGraphics(process, retired with { });
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(ack));
        Assert.Same(packets, process.NativeActionPackets);
        Assert.Same(requested, process.PendingCompletion);

        ack = process.PrepareCompletionAck(token, reset);
        process.ValidatePreparedCompletionAck(ack);
        process.PublishCompletionAck(ack);
        Assert.True(process.IsCompletionAcknowledged(token));
        Assert.Null(process.PendingCompletion);
        Assert.Equal(reset ? 0 : 1, process.NativeActionPackets.ActionCount);
        Assert.Equal(reset ? 0 : 1, process.NativeActionPackets.ContactCount);
        Assert.Same(packets.ReadHeader(First), process.NativeActionPackets.ReadHeader(First));
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(ack));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletionAck(token, true));
        Assert.Equal(1L, process.BeginHostedAction(Header, [Contact]));
    }

    [Fact]
    public void Later_descriptor_failure_preserves_the_retired_completion_and_remaining_queue()
    {
        var (logical, process) = Fixture();
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DDA0, Owner, ActorId));
        NativeBattleGraphicsState admitted = process.Graphics;
        int completionSlot = Assert.Single(admitted.Descriptors.Queue);
        (NativeBattleDescriptorState queued, int unsupportedSlot) = admitted.Descriptors.Enqueue(999, 128, 0);
        SetGraphics(process, admitted with { Descriptors = queued });
        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(admitted.ActorModels.Roster);

        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => RunDescriptors(process, services));

        Assert.Contains("descriptor999", Assert.IsType<NotSupportedException>(failure.InnerException).Message);
        NativeBattlePresentationCompletionRequested receipt = Assert.IsType<NativeBattlePresentationCompletionRequested>(Assert.Single(services.ReadOrderedReceipts()));
        Assert.Same(receipt, process.PendingCompletion);
        Assert.Equal(token, receipt.Token);
        NativeBattleGraphicsState retired = process.Graphics;
        Assert.Equal(unsupportedSlot, Assert.Single(retired.Descriptors.Queue));
        Assert.Equal((byte)0, retired.Descriptors.Packets[completionSlot].PriorityState);
        Assert.Same(queued.Packets[unsupportedSlot], retired.Descriptors.Packets[unsupportedSlot]);
        NativeBattleActionPacketsState packets = process.NativeActionPackets;
        object? epoch = Field(process, "hostedPreparationEpoch").GetValue(process);
        NativeGlobalRandomChange? childRandom = process.NativeGlobalRandomChange;
        NativeGlobalRandomChange? mainRandom = logical.NativeGlobalRandomChange;
        NativeExamTimerState? childTimer = process.NativeExamTimer;
        NativeExamTimerState? mainTimer = logical.NativeExamTimer;
        long? mainCallbacks = logical.NativeHostCallbacks;
        Field(logical, "expandedPresentationProcess").SetValue(logical, process);
        Assert.Throws<InvalidOperationException>(() => logical.Tick());
        Assert.Throws<InvalidOperationException>(() => process.Advance(static () => { }, static (_, _) => { }));
        Assert.Throws<InvalidOperationException>(() => process.AccumulateElapsed(TimeSpan.FromSeconds(1)));
        Assert.Same(retired, process.Graphics);
        Assert.Equal(epoch, Field(process, "hostedPreparationEpoch").GetValue(process));
        Assert.Equal(childRandom, process.NativeGlobalRandomChange);
        Assert.Equal(mainRandom, logical.NativeGlobalRandomChange);
        Assert.Same(childTimer, process.NativeExamTimer);
        Assert.Same(mainTimer, logical.NativeExamTimer);
        Assert.Equal(mainCallbacks, logical.NativeHostCallbacks);

        process.PublishCompletionAck(process.PrepareCompletionAck(token, true));
        Assert.True(process.IsCompletionAcknowledged(token));
        Assert.Null(process.PendingCompletion);
        Assert.Same(retired, process.Graphics);
        Assert.Equal(unsupportedSlot, Assert.Single(process.Graphics.Descriptors.Queue));
        Assert.Same(packets.FindStoredContact(new BattleContactOccurrence(0)), process.NativeActionPackets.FindStoredContact(new BattleContactOccurrence(0)));
        Assert.Same(receipt, Assert.Single(services.ReadOrderedReceipts()));
    }

    [Fact]
    public void Retired_completion_cannot_be_acknowledged_after_its_actor_binding_changes()
    {
        var (logical, process) = Fixture();
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DDA0, Owner, ActorId));
        RunDescriptors(process, logical.CaptureModelLifecycleServices(process.Graphics.ActorModels.Roster));
        NativeBattlePresentationCompletionRequested request = Assert.IsType<NativeBattlePresentationCompletionRequested>(process.PendingCompletion);
        NativeBattleGraphicsState retired = process.Graphics;
        NativeBattleActionPacketsState packets = process.NativeActionPackets;
        BattlePresentationProcess.PreparedHostedCompletionAck prepared = process.PrepareCompletionAck(token, true);

        BattlePartyActorModel replacement = new(new("replacement"));
        NativeBattleGraphicsState rebound = retired with { ActorModels = retired.ActorModels.ReplaceOwner(Owner, replacement, retired.ActorModels.Get(Owner)) };
        SetGraphics(process, rebound);
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(prepared));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletionAck(token, true));
        Assert.Same(rebound, process.Graphics);
        Assert.Same(request, process.PendingCompletion);
        Assert.Same(packets, process.NativeActionPackets);
        Assert.False(process.IsCompletionAcknowledged(token));
    }

    [Fact]
    public void Unowned_combat_callback_and_invalid_admission_leave_state_unchanged()
    {
        var (logical, process) = Fixture();
        NativeBattleGraphicsState graphics = process.Graphics;
        BattlePresentationProcess.PreparedHostedDispatch prepared = process.PrepareCompletion(0x47DDA0, Owner, ActorId);
        Assert.Throws<ArgumentOutOfRangeException>(() => process.PrepareCompletion(0x47DD70, Owner, ActorId));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletion(0x47DDA0, Missing, ActorId));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletion(0x47DDA0, Owner, "foreign"));
        Assert.Same(graphics, process.Graphics);
        var (_, foreign) = Fixture();
        Assert.Throws<InvalidOperationException>(() => foreign.PublishCompletion(prepared));
        var (queue, _) = graphics.Descriptors.Enqueue(10, 128, 0x47DDA0);
        NativeBattleGraphicsState unowned = graphics with { Descriptors = queue };
        SetGraphics(process, unowned);
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletion(prepared));

        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => RunDescriptors(process, services));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(unowned, process.Graphics);
        Assert.Empty(services.ReadOrderedReceipts());
        Assert.Null(process.PendingCompletion);
    }

    [Fact]
    public void Retired_completion_and_appended_dispatch_share_one_atomic_packet_transition()
    {
        var (logical, process) = Fixture();
        NativeBattleGraphicsState graphics = process.Graphics;
        BattleModelRoster roster = graphics.ActorModels.Roster;
        int boundary = NativeBattleRetainedActionPackets.ContactCapacity;
        NativeBattleActionPacketsState packets = process.NativeActionPackets.Prepare(Header).State;
        packets = packets.AppendContacts(Enumerable.Repeat(Contact, boundary)).Publish(boundary).State;
        packets = packets.Prepare(Header with { Command = 0, Sequence = 10, Action = 9 }).State.Publish(0).State;
        NativeBattleActionHeaderSlot retainedEmpty = packets.ReadHeader(new BattleActionHeaderReference(1));
        Field(process, "nativeActionPackets").SetValue(process, packets);
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DE70, Owner, ActorId));
        RunDescriptors(process, logical.CaptureModelLifecycleServices(roster));
        NativeBattlePresentationCompletionRequested pending = Assert.IsType<NativeBattlePresentationCompletionRequested>(process.PendingCompletion);
        NativeBattleGraphicsState retired = process.Graphics;
        BattlePresentationProcess.PreparedHostedCompletionAck ack = process.PrepareCompletionAck(token, false);
        BattlePresentationProcess.HostedDispatchCompletion completion = new(0x47E120, Owner, ActorId);
        BattlePresentationProcess.HostedDispatchHeader addition = new(Header, [Contact]);
        NativeBattleContactPacket unbound = Contact with { Primary = Contact.Primary with { Recipient = Missing } };

        Assert.Throws<InvalidDataException>(() => process.PrepareDispatch([addition, new(Header, [unbound])], dispatchAppended: true, completion: completion, acknowledgment: ack));
        Assert.Throws<InvalidDataException>(() => process.PrepareHostedDispatch([addition], dispatchAppended: true, completion: completion, acknowledgment: ack, freshActionPackets: true));
        BattlePresentationProcess.PreparedHostedDispatch grown = process.PrepareDispatch([.. Enumerable.Repeat(addition, NativeBattleRetainedActionPackets.HeaderCapacity)], dispatchAppended: true, completion: completion, acknowledgment: ack);
        Assert.Equal(NativeBattleRetainedActionPackets.HeaderCapacity + 2, grown.Packets.ActionCount);
        Assert.Equal(NativeBattleRetainedActionPackets.HeaderCapacity, grown.Packets.ExportRetained().Headers.Length);
        Assert.Equal(Contact, Assert.Single(grown.Packets.ReadHeaderContacts(grown.AppendedHeaders[^1])));
        Assert.Same(packets, process.NativeActionPackets);
        Assert.Same(retired, process.Graphics);
        Assert.Same(pending, process.PendingCompletion);

        BattlePresentationProcess.PreparedHostedDispatch prepared = process.PrepareDispatch([addition], dispatchAppended: true, completion: completion, acknowledgment: ack);
        Assert.Same(packets, process.NativeActionPackets);
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
        Assert.Same(prepared.Packets, process.NativeActionPackets);
        Assert.Collection(process.Graphics.Descriptors.Queue,
            slot => Assert.Equal((ushort)104, process.Graphics.Descriptors.Packets[slot].Command),
            slot => Assert.Equal((ushort)10, process.Graphics.Descriptors.Packets[slot].Command));
        Assert.Throws<InvalidOperationException>(() => process.ValidatePreparedDispatch(prepared));
        Assert.Equal(new BattleActionHeaderReference(2), prepared.Action!.Invocation.Header);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0x40, false)]
    [InlineData(0x100, false)]
    public void Native_self_contact_controls_wait_for_the_bound_extended_target(ushort resourceFlags, bool busy)
    {
        var (_, process) = Fixture();
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(Owner) with { Flags0 = 0, Flags8 = 0, Kind5 = 0, ResourceFlags44 = resourceFlags };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(Owner, actor) };
        NativeBattleActionHeaderPreparation self = Header with { Command = 0, Sequence = 0, Action = 2 };
        NativeBattleContactPacket contact = Contact with { Primary = Contact.Primary with { Recipient = Owner } };
        NativeBattleActionPacketsState packets = process.NativeActionPackets.Prepare(self).State;
        packets = packets.AppendContacts([contact]).Publish(1).State.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, new BattleActionHeaderReference(0));
        Assert.True(context.TargetActors.Resolve(packets.ModelAliases).IsSingle(Owner));
        Assert.Equal(busy, typeof(BattleSession).GetMethod("NativeHeaderTargetsBusy", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [graphics, context]));
    }

    private static NativeBattleActionPacketsState PopulatedPackets(NativeBattleActionPacketsState packets)
    {
        (packets, _) = packets.Prepare(Header);
        packets = packets.AppendContacts([Contact]);
        return packets.Publish(1).State;
    }

    private static (BattleSession Logical, BattlePresentationProcess Process) Fixture()
    {
        BattleDefinition definition = new()
        {
            Combatants = [.. Enumerable.Range(0, 3).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true }), new() { Id = ActorId, Name = "Extended", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeGlobalRandom = new(123)
        };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeExamTimer))!.SetValue(logical, NativeExamTimerState.FromColdProcess().SetRemaining(100));
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => new(0) { ModelId4 = owner == Owner ? (byte)1 : (byte)0 });
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics with { EntryReady = true, EntryLifecycle = 4 }, definition with
        {
            NativeGraphics = new(rules, [new(ActorId, 1, new([0, 0]))], [])
        });
        return (logical, process);
    }

    private static void RunDescriptors(BattlePresentationProcess process, NativeBattleModelLifecycleServices services)
    {
        FieldInfo active = Field(process, "hostedFrameServices");
        active.SetValue(process, services);
        try
        {
            var graphics = (NativeBattleGraphicsState)typeof(BattlePresentationProcess).GetMethod("AdvanceNativeDescriptors", PrivateInstance)!.Invoke(process, [process.Graphics])!;
            SetGraphics(process, graphics);
        }
        finally
        {
            active.SetValue(process, null);
        }
    }

    private static void SetGraphics(BattlePresentationProcess process, NativeBattleGraphicsState graphics)
    {
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(process, graphics);
    }

    private static FieldInfo Field(object owner, string name)
    {
        return owner.GetType().GetField(name, PrivateInstance)!;
    }
}
