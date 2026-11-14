using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedCompletionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const byte Row = 7;
    private const string ActorId = "extended";
    private static readonly NativeBattleActionHeaderPreparation Header = new(Row, 1, 5, -1, 0, 0, null);
    private static readonly NativeBattleContactPacket Contact = new(new(3, 0, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));

    [Theory]
    [InlineData(0x47DDA0u, true)]
    [InlineData(0x47DE70u, true)]
    [InlineData(0x47DE70u, false)]
    [InlineData(0x47E120u, true)]
    public void Actual_descriptor_execution_requests_one_ordered_acknowledgement(uint callback, bool reset)
    {
        var (logical, child, process) = Fixture();
        BattleSession.PreparedHostedAction staleAction = child.PrepareHostedAction(Header, [Contact]);
        NativeBattleActionPacketsState packets = PopulatedPackets(child.NativeGraphics!.PresentationRoster!);
        Field(child, "nativeActionPackets").SetValue(child, packets);
        NativeBattleGraphicsState before = child.NativeGraphics;
        BattleSession.PreparedHostedCompletion prepared = process.PrepareCompletion(callback, Row, ActorId);
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

        NativeBattleModelLifecycleServices services = logical.CaptureExpandedModelLifecycleServices(before.PresentationRoster!);
        services.Emit(new NativeBattleModelParticipantReleased(Row, ActorId));
        RunDescriptors(child, services);
        services.Emit(new NativeBattleModelParticipantSuspended(Row, ActorId, 3));
        NativeBattlePresentationCompletionRequested requested = Assert.IsType<NativeBattlePresentationCompletionRequested>(services.ReadOrderedReceipts()[1]);
        Assert.Collection(services.ReadOrderedReceipts(), item => Assert.IsType<NativeBattleModelParticipantReleased>(item), item => Assert.Same(requested, item), item => Assert.IsType<NativeBattleModelParticipantSuspended>(item));
        Assert.Equal(new NativeBattlePresentationCompletionRequested(token, callback, Row, ActorId), requested);
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
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletion(callback, Row, ActorId));
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
        Assert.Same(packets.Headers[0], child.NativeActionPackets.Headers[0]);
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletionAck(ack));
        Assert.Throws<InvalidOperationException>(() => process.PrepareCompletionAck(token, true));
        Assert.Equal(1L, child.BeginHostedAction(Header, [Contact]));
    }

    [Fact]
    public void Later_descriptor_failure_preserves_the_retired_completion_and_remaining_queue()
    {
        var (logical, child, process) = Fixture();
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DDA0, Row, ActorId));
        NativeBattleGraphicsState admitted = child.NativeGraphics!;
        int completionSlot = Assert.Single(admitted.Descriptors.Queue);
        (NativeBattleDescriptorState queued, int unsupportedSlot) = admitted.Descriptors.Enqueue(999, 128, 0);
        SetGraphics(child, admitted with { Descriptors = queued });
        NativeBattleModelLifecycleServices services = logical.CaptureExpandedModelLifecycleServices(admitted.PresentationRoster!);

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
        Assert.Same(packets.Headers[0], child.NativeActionPackets.Headers[0]);
        Assert.Same(receipt, Assert.Single(services.ReadOrderedReceipts()));
    }

    [Fact]
    public void Retired_completion_cannot_be_acknowledged_after_its_actor_binding_changes()
    {
        var (logical, child, process) = Fixture();
        long token = process.PublishCompletion(process.PrepareCompletion(0x47DDA0, Row, ActorId));
        RunDescriptors(child, logical.CaptureExpandedModelLifecycleServices(child.NativeGraphics!.PresentationRoster!));
        NativeBattlePresentationCompletionRequested request = Assert.IsType<NativeBattlePresentationCompletionRequested>(process.PendingCompletion);
        NativeBattleGraphicsState retired = child.NativeGraphics!;
        NativeBattleActionPacketsState packets = child.NativeActionPackets;
        BattleSession.PreparedHostedCompletionAck prepared = process.PrepareCompletionAck(token, true);

        NativeBattleGraphicsRoster roster = retired.PresentationRoster!;
        NativeBattleGraphicsState rebound = retired with
        {
            PresentationRoster = new(roster.Rows.SetItem(Row, new("replacement", true))),
            ActorIds = retired.ActorIds.SetItem(Row, "replacement")
        };
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
        BattleSession.PreparedHostedCompletion prepared = process.PrepareCompletion(0x47DDA0, Row, ActorId);
        Assert.Throws<ArgumentOutOfRangeException>(() => process.PrepareCompletion(0x47DD70, Row, ActorId));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletion(0x47DDA0, 6, ActorId));
        Assert.Throws<InvalidDataException>(() => process.PrepareCompletion(0x47DDA0, Row, "foreign"));
        Assert.Same(graphics, child.NativeGraphics);
        var (_, _, foreign) = Fixture();
        Assert.Throws<InvalidOperationException>(() => foreign.PublishCompletion(prepared));
        var (queue, _) = graphics.Descriptors.Enqueue(10, 128, 0x47DDA0);
        NativeBattleGraphicsState unowned = graphics with { Descriptors = queue };
        SetGraphics(child, unowned);
        Assert.Throws<InvalidOperationException>(() => process.PublishCompletion(prepared));

        NativeBattleModelLifecycleServices services = logical.CaptureExpandedModelLifecycleServices(graphics.PresentationRoster!);
        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => RunDescriptors(child, services));
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(unowned, child.NativeGraphics);
        Assert.Empty(services.ReadOrderedReceipts());
        Assert.Null(process.PendingCompletion);
    }

    private static NativeBattleActionPacketsState PopulatedPackets(NativeBattleGraphicsRoster roster)
    {
        var (packets, _) = NativeBattleActionPacketsState.FromPresentationStorage(roster).Prepare(Header);
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
        NativeBattleGraphicsRoster roster = logical.ReadExpandedPresentationRoster();
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        models[Row] = models[Row] with { ModelId4 = 1 };
        SetGraphics(child, new(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster, EntryReady = true, EntryLifecycle = 4 });
        Field(child, "hostedBattlePresentation").SetValue(child, true);
        FieldInfo entry = Field(child, "hostedEntryDescriptors");
        entry.SetValue(child, Activator.CreateInstance(entry.FieldType));
        Field(child, "presentationNativeTargets").SetValue(child, new Dictionary<int, NativeBattleEffectModelBinding>
        {
            [Row] = new(ActorId, models[Row], new([0, 0])) { IsParty = true }
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
