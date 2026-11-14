using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionAdmissionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo ObserveRemoval = typeof(BattleSession).GetMethod("ObserveHostedActionPacketRemoved", PrivateInstance)!;
    private static readonly NativeBattleActionHeaderPreparation Header = new(7, 1, 5, -1, 0, 0, null);

    [Fact]
    public void AdmissionQueuesOneHeaderAndRebindsItsSourceWithoutResettingProcessOwners()
    {
        BattleSession session = Fixture();
        NativeBattleGraphicsState before = session.NativeGraphics!;
        NativeBattleEffectMemory? memory = session.NativeEffectMemory;
        NativeGlobalRandomChange? rng = session.NativeGlobalRandomChange;
        NativeBattleGraphicsResource resource = Bindings(session)[7].Resource;
        NativeBattleContactPacket contact = Contact(24);

        long token = session.BeginHostedAction(Header, [contact, contact]);

        Assert.Equal(1L, token);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.False(session.IsHostedActionComplete(token));
        NativeBattleDescriptorState queue = session.NativeGraphics!.Descriptors;
        NativeBattleDescriptorPacket descriptor = queue.Packets[Assert.Single(queue.Queue)];
        Assert.Equal(104, descriptor.Command);
        Assert.Equal(128, descriptor.PriorityState);
        Assert.Equal(0x1D280C4u, descriptor.Argument);
        Assert.Equal(1, session.NativeActionPackets.ActionCount);
        Assert.Equal(0, session.NativeActionPackets.Headers[0].ReadByte(17));
        Assert.Equal(2, session.NativeActionPackets.Headers[0].ReadContactCount());
        Assert.Equal(contact, session.NativeActionPackets.ReadContact(0));
        Assert.Equal(contact, session.NativeActionPackets.ReadContact(1));
        Assert.Equal(7, Field("presentationSourceIndex").GetValue(session));
        Assert.Same(resource, Field("presentationSourceResource").GetValue(session));
        Assert.Equal(before.Models, session.NativeGraphics.Models);
        Assert.Same(memory, session.NativeEffectMemory);
        Assert.Equal(rng, session.NativeGlobalRandomChange);
    }

    [Theory]
    [InlineData("late contact")]
    [InlineData("source hole")]
    [InlineData("stale identity")]
    [InlineData("missing source resource")]
    [InlineData("descriptor capacity")]
    public void InvalidAdmissionLeavesCurrentPacketsDescriptorsSourceAndRandomUntouched(string failure)
    {
        BattleSession session = Fixture();
        long previous = session.BeginHostedAction(Header, [Contact(24)]);
        CompleteThroughQueue(session);
        NativeBattleActionHeaderPreparation header = Header with { Actor = 24 };
        ImmutableArray<NativeBattleContactPacket> contacts = [Contact(7)];
        switch (failure)
        {
            case "late contact":
                contacts = contacts.Add(Contact(23));
                break;
            case "source hole":
                header = header with { Actor = 23 };
                break;
            case "stale identity":
                NativeBattleGraphicsState graphics = session.NativeGraphics!;
                SetGraphics(session, graphics with { ActorIds = graphics.ActorIds.SetItem(24, "retired") });
                break;
            case "missing source resource":
                Bindings(session).Remove(24);
                break;
            case "descriptor capacity":
                NativeBattleGraphicsState busy = session.NativeGraphics!;
                NativeBattleDescriptorState descriptors = busy.Descriptors;
                for (int index = 0; index < descriptors.Nodes.Length - 1; index++)
                {
                    (descriptors, _) = descriptors.Enqueue(105, 128, 0);
                }
                SetGraphics(session, busy with { Descriptors = descriptors });
                break;
        }

        NativeBattleGraphicsState before = session.NativeGraphics!;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        object? source = Field("presentationSourceResource").GetValue(session);
        NativeGlobalRandomChange? rng = session.NativeGlobalRandomChange;
        Exception? error = Record.Exception(() => session.BeginHostedAction(header, contacts));

        if (failure == "missing source resource")
        {
            Assert.IsType<NotSupportedException>(error);
        }
        else if (failure == "descriptor capacity")
        {
            Assert.IsType<InvalidOperationException>(error);
        }
        else
        {
            Assert.IsType<InvalidDataException>(error);
        }
        Assert.Same(before, session.NativeGraphics);
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(source, Field("presentationSourceResource").GetValue(session));
        Assert.Equal(7, Field("presentationSourceIndex").GetValue(session));
        Assert.Equal(rng, session.NativeGlobalRandomChange);
        Assert.Null(session.HostedActiveActionToken);
        Assert.True(session.IsHostedActionComplete(previous));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.IsHostedActionComplete(previous + 1));
    }

    [Fact]
    public void AdmissionKeepsUnknownSourceModelIdWhenItsCurrentResourceIsBound()
    {
        BattleSession session = Fixture();
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        SetGraphics(session, graphics with { Models = graphics.Models.SetItem(7, graphics.Models[7] with { ModelId4 = null }) });

        Assert.Equal(1L, session.BeginHostedAction(Header, [Contact(24)]));

        Assert.Null(Field("presentationSourceModelId").GetValue(session));
        Assert.Same(Bindings(session)[7].Resource, Field("presentationSourceResource").GetValue(session));
    }

    [Fact]
    public void StageVisibilityUsesTheDurableHostedStageWithoutALegacyArena()
    {
        BattleSession session = Fixture();
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeStageVisibilityOwner", PrivateInstance)!;
        PropertyInfo stageProperty = typeof(BattleSession).GetProperty(nameof(BattleSession.NativeStage))!;
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        Field("presentationForeignActors").SetValue(session, new Dictionary<int, NativeBattleForeignActor>());
        stageProperty.SetValue(session, stage);

        Assert.Same(stage, read.Invoke(session, null));
        stageProperty.SetValue(session, null);
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => read.Invoke(session, null)).InnerException);

        stageProperty.SetValue(session, stage);
        Field("hostedBattlePresentation").SetValue(session, false);
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => read.Invoke(session, null)).InnerException);
    }

    [Fact]
    public void AdmissionRejectsPreEntryAnActiveInvocationAndAnInFlightFrame()
    {
        BattleSession session = Fixture(ready: false);
        Assert.Throws<InvalidOperationException>(() => session.BeginHostedAction(Header, [Contact(24)]));

        SetGraphics(session, session.NativeGraphics! with { EntryReady = true });
        Field("hostedFrameServices").SetValue(session, new NativeBattleModelLifecycleServices([]));
        Assert.Throws<InvalidOperationException>(() => session.BeginHostedAction(Header, [Contact(24)]));
        Field("hostedFrameServices").SetValue(session, null);

        long active = session.BeginHostedAction(Header, [Contact(24)]);
        NativeBattleGraphicsState admitted = session.NativeGraphics!;
        Assert.Equal(1L, active);
        Assert.Throws<InvalidOperationException>(() => session.BeginHostedAction(Header with { Actor = 24 }, [Contact(7)]));
        Assert.Equal(active, session.HostedActiveActionToken);
        Assert.Same(admitted, session.NativeGraphics);
    }

    [Fact]
    public void OnlyTheExactCompletedDescriptorRemovalCompletesTheCurrentToken()
    {
        BattleSession session = Fixture();
        long old = session.BeginHostedAction(Header, [Contact(24)]);
        NativeBattleDescriptorPacket stale = CompleteThroughQueue(session);
        long active = session.BeginHostedAction(Header with { Actor = 24 }, [Contact(7)]);
        NativeBattleDescriptorState queue = session.NativeGraphics!.Descriptors;
        int slot = Assert.Single(queue.Queue);
        NativeBattleDescriptorPacket packet = queue.Packets[slot];
        NativeBattleDescriptorPacket ready = queue.SetPacketState(slot, 255).Packets[slot];

        Observe(session, slot, stale);
        Observe(session, (slot + 1) % queue.Packets.Length, ready);
        Observe(session, slot, ready with { Bytes = ready.Bytes.SetItem(2, 10) });
        Observe(session, slot, ready with { Bytes = ready.Bytes.SetItem(4, 0) });
        Observe(session, slot, packet);

        Assert.True(session.IsHostedActionComplete(old));
        Assert.False(session.IsHostedActionComplete(active));
        Assert.Equal(active, session.HostedActiveActionToken);
        CompleteThroughQueue(session);
        Assert.Null(session.HostedActiveActionToken);
        Assert.True(session.IsHostedActionComplete(active));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.IsHostedActionComplete(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.IsHostedActionComplete(active + 1));
    }

    [Fact]
    public void InvocationTokensStayMonotonicAcrossNativeDescriptorSerialWrap()
    {
        BattleSession session = Fixture();
        for (int ordinal = 0; ordinal <= 256; ordinal++)
        {
            long token = session.BeginHostedAction(Header, [Contact(24)]);
            Assert.Equal(ordinal + 1L, token);
            Assert.False(session.IsHostedActionComplete(token));
            if (ordinal > 0)
            {
                Assert.True(session.IsHostedActionComplete(token - 1));
            }
            NativeBattleDescriptorPacket removed = CompleteThroughQueue(session);
            Assert.Equal(unchecked((byte)ordinal), removed.Serial);
            Assert.True(session.IsHostedActionComplete(token));
        }
    }

    private static NativeBattleDescriptorPacket CompleteThroughQueue(BattleSession session)
    {
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleDescriptorState queue = graphics.Descriptors;
        int slot = Assert.Single(queue.Queue);
        // Start at the executor's terminal packet state. The Assets fixture drives the real 104 callback.
        queue = queue.SetPacketState(slot, 255) with { Depth = 1 };
        NativeBattleDescriptorPacket completed = queue.Packets[slot];
        NativeBattleDescriptorState remaining = queue.Advance((_, _) => throw new InvalidOperationException("No new dispatch expected."),
            (_, _) => throw new InvalidOperationException("No asynchronous callback expected."),
            observeRemoval: (_, index, packet) => Observe(session, index, packet));
        Assert.Empty(remaining.Queue);
        SetGraphics(session, graphics with { Descriptors = remaining });
        return completed;
    }

    private static BattleSession Fixture(bool ready = true)
    {
        BattleSession session = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeGlobalRandom = new(123)
        }, BattleAtbMode.TurnBased);
        NativeBattleGraphicsActor?[] actors = new NativeBattleGraphicsActor?[25];
        actors[7] = new("ally", true);
        actors[24] = new("enemy", false);
        NativeBattleGraphicsRoster roster = new(actors.ToImmutableArray());
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        models[7] = models[7] with { ModelId4 = 1 };
        models[24] = models[24] with { ModelId4 = 16 };
        SetGraphics(session, new(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = roster,
            EntryReady = ready
        });
        // Only bind the ready-process admission seam. Entry and callback progression are covered by Assets tests.
        Field("hostedBattlePresentation").SetValue(session, true);
        Field("presentationNativeTargets").SetValue(session, new Dictionary<int, NativeBattleEffectModelBinding>
        {
            [7] = new("ally", models[7], new([0, 0])) { IsParty = true },
            [24] = new("enemy", models[24], new([0, 0])) { IsParty = false }
        });
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess());
        return session;
    }

    private static NativeBattleContactPacket Contact(byte row) => new(new(row, 0, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));

    private static void Observe(BattleSession session, int slot, NativeBattleDescriptorPacket packet) => ObserveRemoval.Invoke(session, [slot, packet]);

    private static void SetGraphics(BattleSession session, NativeBattleGraphicsState graphics) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;

    private static Dictionary<int, NativeBattleEffectModelBinding> Bindings(BattleSession session) => (Dictionary<int, NativeBattleEffectModelBinding>)Field("presentationNativeTargets").GetValue(session)!;
}
