using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionAdmissionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo ObserveRemoval = typeof(BattleSession).GetMethod("ObserveHostedActionPacketRemoved", PrivateInstance)!;
    private static readonly BattlePartyModelOwner Ally = new BattlePartyActorModel(new("ally"));
    private static readonly BattleEnemyModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly BattleEnemyModelOwner Missing = new BattleEnemyActorModel(new("missing"));
    private static readonly NativeBattleActionHeaderPreparation Header = new(Ally, 1, 5, -1, 0, 0, null);

    [Fact]
    public void AdmissionQueuesOneHeaderAndRebindsItsSourceWithoutResettingProcessOwners()
    {
        BattleSession session = Fixture();
        NativeBattleGraphicsState before = session.NativeGraphics!;
        NativeBattleEffectMemory? memory = session.NativeEffectMemory;
        NativeGlobalRandomChange? rng = session.NativeGlobalRandomChange;
        NativeBattleGraphicsResource resource = Bindings(session)[Ally].Resource;
        NativeBattleContactPacket contact = Contact(Enemy);

        long token = session.BeginHostedAction(Header, [contact, contact]);

        Assert.Equal(1L, token);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.False(session.IsHostedActionComplete(token));
        NativeBattleDescriptorState queue = session.NativeGraphics!.Descriptors;
        NativeBattleDescriptorPacket descriptor = queue.Packets[Assert.Single(queue.Queue)];
        Assert.Equal(104, descriptor.Command);
        Assert.Equal(128, descriptor.PriorityState);
        Assert.Equal(new BattleActionHeaderReference(0), descriptor.Header);
        Assert.Equal(0x1D280C4u, descriptor.Argument);
        Assert.Equal(1, session.NativeActionPackets.ActionCount);
        Assert.Equal(0, session.NativeActionPackets.ExportRetained().Headers[0].ReadOriginalByte(17));
        Assert.Equal(2, session.NativeActionPackets.ReadHeader(new BattleActionHeaderReference(0)).ReadContactCount());
        Assert.Equal(contact, session.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)));
        Assert.Equal(contact, session.NativeActionPackets.ReadContact(new BattleContactOccurrence(1)));
        Assert.Equal(Ally, Field("presentationSourceOwner").GetValue(session));
        Assert.Same(resource, Field("presentationSourceResource").GetValue(session));
        Assert.Same(before.ActorModels, session.NativeGraphics.ActorModels);
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
        long previous = session.BeginHostedAction(Header, [Contact(Enemy)]);
        CompleteThroughQueue(session);
        NativeBattleActionHeaderPreparation header = Header with { Source = Enemy };
        ImmutableArray<NativeBattleContactPacket> contacts = [Contact(Ally)];
        switch (failure)
        {
            case "late contact":
                contacts = contacts.Add(Contact(Missing));
                break;
            case "source hole":
                header = header with { Source = Missing };
                break;
            case "stale identity":
                NativeBattleGraphicsState graphics = session.NativeGraphics!;
                SetGraphics(session, graphics with { ActorModels = graphics.ActorModels.ReplaceOwner(Enemy, new BattleEnemyActorModel(new("replaced")), graphics.ActorModels.Get(Enemy)) });
                break;
            case "missing source resource":
                Bindings(session).Remove(Enemy);
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
        Assert.Equal(Ally, Field("presentationSourceOwner").GetValue(session));
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
        SetGraphics(session, graphics with { ActorModels = graphics.ActorModels.Set(Ally, graphics.ActorModels.Get(Ally) with { ModelId4 = null }) });

        Assert.Equal(1L, session.BeginHostedAction(Header, [Contact(Enemy)]));

        Assert.Null(Field("presentationSourceModelId").GetValue(session));
        Assert.Same(Bindings(session)[Ally].Resource, Field("presentationSourceResource").GetValue(session));
    }

    [Fact]
    public void StageVisibilityUsesTheDurableHostedStageWithoutALegacyArena()
    {
        BattleSession session = Fixture();
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeStageVisibilityOwner", PrivateInstance)!;
        PropertyInfo stageProperty = typeof(BattleSession).GetProperty(nameof(BattleSession.NativeStage))!;
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        Field("presentationForeignActors").SetValue(session, new Dictionary<BattleModelOwner, NativeBattleForeignActor>());
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
        Assert.Throws<InvalidOperationException>(() => session.BeginHostedAction(Header, [Contact(Enemy)]));

        SetGraphics(session, session.NativeGraphics! with { EntryReady = true });
        Field("hostedFrameServices").SetValue(session, new NativeBattleModelLifecycleServices([]));
        Assert.Throws<InvalidOperationException>(() => session.BeginHostedAction(Header, [Contact(Enemy)]));
        Field("hostedFrameServices").SetValue(session, null);

        long active = session.BeginHostedAction(Header, [Contact(Enemy)]);
        NativeBattleGraphicsState admitted = session.NativeGraphics!;
        Assert.Equal(1L, active);
        Assert.Throws<InvalidOperationException>(() => session.BeginHostedAction(Header with { Source = Enemy }, [Contact(Ally)]));
        Assert.Equal(active, session.HostedActiveActionToken);
        Assert.Same(admitted, session.NativeGraphics);
    }

    [Fact]
    public void OnlyTheExactCompletedDescriptorRemovalCompletesTheCurrentToken()
    {
        BattleSession session = Fixture();
        long old = session.BeginHostedAction(Header, [Contact(Enemy)]);
        NativeBattleDescriptorPacket stale = CompleteThroughQueue(session);
        long active = session.BeginHostedAction(Header with { Source = Enemy }, [Contact(Ally)]);
        NativeBattleDescriptorState queue = session.NativeGraphics!.Descriptors;
        int slot = Assert.Single(queue.Queue);
        NativeBattleDescriptorPacket packet = queue.Packets[slot];
        NativeBattleDescriptorPacket ready = queue.SetPacketState(slot, 255).Packets[slot];

        Observe(session, slot, stale);
        Observe(session, (slot + 1) % queue.Packets.Length, ready);
        Observe(session, slot, ready with { Bytes = ready.Bytes.SetItem(2, 10) });
        Observe(session, slot, ready with { Header = new BattleActionHeaderReference(1) });
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
            long token = session.BeginHostedAction(Header, [Contact(Enemy)]);
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
        BattleModelRoster roster = new([Ally, Enemy]);
        NativeBattleModelAliases aliases = new(roster, []);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner == Ally ? (byte)1 : (byte)16 });
        SetGraphics(session, graphics with { EntryReady = ready });
        Field("nativeActionPackets").SetValue(session, NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases));
        Field("hostedBattlePresentation").SetValue(session, true);
        NativeBattleGraphicsResource allyResource = new([0, 0]);
        NativeBattleGraphicsResource enemyResource = new([0, 0]);
        Field("presentationNativeTargets").SetValue(session, new Dictionary<BattleModelOwner, NativeBattleEffectModelBinding>
        {
            [Ally] = new(Ally, graphics.ActorModels.Get(Ally), allyResource),
            [Enemy] = new(Enemy, graphics.ActorModels.Get(Enemy), enemyResource)
        });
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        Field("definition").SetValue(session, session.ReadDefinition() with
        {
            NativeGraphics = new(rules, [new("ally", 1, allyResource), new("enemy", 16, enemyResource)], [])
        });
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess());
        return session;
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner owner) => new(new(owner, 0, 1, 0, 0, 0, 0), null);

    private static void Observe(BattleSession session, int slot, NativeBattleDescriptorPacket packet) => ObserveRemoval.Invoke(session, [slot, packet]);

    private static void SetGraphics(BattleSession session, NativeBattleGraphicsState graphics) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;

    private static Dictionary<BattleModelOwner, NativeBattleEffectModelBinding> Bindings(BattleSession session) => (Dictionary<BattleModelOwner, NativeBattleEffectModelBinding>)Field("presentationNativeTargets").GetValue(session)!;
}
