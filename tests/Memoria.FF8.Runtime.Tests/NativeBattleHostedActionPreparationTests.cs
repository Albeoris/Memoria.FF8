using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionPreparationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleActionHeaderPreparation Header = new(OriginalOwner(0), 1, 5, -1, 0, 0, null);
    private static readonly NativeBattleContactPacket Contact = new(new(OriginalOwner(3), 0, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));

    [Fact]
    public void PreparationLeavesLiveOwnersUntouchedAndPublishesTheSameAdmissionAsBegin()
    {
        BattleSession session = Fixture();
        BattleSession direct = Fixture();
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        NativeBattleModelCpuResources cpu = session.NativeModelCpuResources!;
        NativeBattleModelTextureCache cache = session.NativeModelTextureCache!;
        NativeGlobalRandomChange? random = session.NativeGlobalRandomChange;

        BattleSession.PreparedHostedDispatch prepared = session.PrepareHostedAction(Header, [Contact, Contact]);
        session.ValidatePreparedHostedAction(prepared);

        Assert.Same(graphics, session.NativeGraphics);
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(memory, session.NativeEffectMemory);
        Assert.Same(cpu, session.NativeModelCpuResources);
        Assert.Same(cache, session.NativeModelTextureCache);
        Assert.Equal(random, session.NativeGlobalRandomChange);
        Assert.Null(session.HostedActiveActionToken);
        Assert.Null(Field("presentationSourceResource").GetValue(session));

        long token = session.PublishHostedAction(prepared);
        Assert.Equal(direct.BeginHostedAction(Header, [Contact, Contact]), token);
        Assert.Equal(NativeBattleTestSnapshots.Json(direct.NativeActionPackets), NativeBattleTestSnapshots.Json(session.NativeActionPackets));
        Assert.Equal(NativeBattleTestSnapshots.Json(direct.NativeGraphics!.Descriptors), NativeBattleTestSnapshots.Json(session.NativeGraphics!.Descriptors));
        Assert.Equal(graphics.Models, session.NativeGraphics.Models);
        Assert.Same(memory, session.NativeEffectMemory);
        Assert.Same(cpu, session.NativeModelCpuResources);
        Assert.Same(cache, session.NativeModelTextureCache);
        Assert.Equal(random, session.NativeGlobalRandomChange);
    }

    [Fact]
    public void CompetingPreparationsAreSingleUseAndOwnerBound()
    {
        BattleSession session = Fixture();
        BattleSession other = Fixture();
        BattleSession.PreparedHostedDispatch first = session.PrepareHostedAction(Header, [Contact]);
        BattleSession.PreparedHostedDispatch second = session.PrepareHostedAction(Header, [Contact, Contact]);
        NativeBattleGraphicsState otherGraphics = other.NativeGraphics!;
        NativeBattleActionPacketsState otherPackets = other.NativeActionPackets;

        Assert.Throws<InvalidOperationException>(() => other.PublishHostedAction(first));
        Assert.Same(otherGraphics, other.NativeGraphics);
        Assert.Same(otherPackets, other.NativeActionPackets);
        Assert.Null(other.HostedActiveActionToken);

        Assert.Equal(1L, session.PublishHostedAction(second));
        NativeBattleGraphicsState admitted = session.NativeGraphics!;
        NativeBattleActionPacketsState admittedPackets = session.NativeActionPackets;
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(first));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(second));
        Assert.Same(admitted, session.NativeGraphics);
        Assert.Same(admittedPackets, session.NativeActionPackets);
        Assert.Equal(2, admittedPackets.ContactCount);
        Assert.Equal(1L, session.HostedActiveActionToken);
    }

    [Fact]
    public void FailedFrameAfterItsFirstWriteInvalidatesPreparationEvenWithSameGraphicsReference()
    {
        BattleSession session = Fixture();
        BattleSession logical = new(Definition(), BattleAtbMode.TurnBased);
        BattleSession.PreparedHostedDispatch prepared = session.PrepareHostedAction(Header, [Contact]);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        // Missing I/O fails after valid canonical facts and the start of frame mutation, before graphics changes.
        Assert.Null(session.NativeFileIo);
        NotSupportedException failure = Assert.Throws<NotSupportedException>(() => session.AdvanceHostedBattlePresentationFrame(logical, null));
        Assert.Contains("482590", failure.Message);
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Throws<InvalidOperationException>(() => session.ValidatePreparedHostedAction(prepared));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(prepared));
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Null(session.HostedActiveActionToken);

        Assert.Equal(1L, session.BeginHostedAction(Header, [Contact]));
    }

    [Fact]
    public void FrameValidationRejectionAndLateInvalidPreparationKeepTheEarlierPreparationValid()
    {
        BattleSession session = Fixture();
        BattleSession conflicting = new(Definition("other"), BattleAtbMode.TurnBased);
        BattleSession.PreparedHostedDispatch first = session.PrepareHostedAction(Header, [Contact]);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        NativeGlobalRandomChange? random = session.NativeGlobalRandomChange;

        Assert.Throws<InvalidDataException>(() => session.AdvanceHostedBattlePresentationFrame(conflicting, null));
        NativeBattleContactPacket late = Contact with { Primary = Contact.Primary with { ActorIndex = 6 } };
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedAction(Header, [Contact, late]));
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Equal(random, session.NativeGlobalRandomChange);
        Assert.Equal(1L, session.PublishHostedAction(first));
    }

    [Fact]
    public void Before_core_can_replace_stale_admission_but_graphics_services_block_admission()
    {
        BattleSession session = Fixture();
        BattleSession logical = new(Definition(), BattleAtbMode.TurnBased);
        BattleSession.PreparedHostedDispatch stale = session.PrepareHostedAction(Header, [Contact]);
        bool ran = false;

        NotSupportedException missingIo = Assert.Throws<NotSupportedException>(() => session.AdvanceHostedBattlePresentationFrame(logical, null, () =>
        {
            ran = true;
            Assert.Throws<InvalidOperationException>(() => session.ValidatePreparedHostedAction(stale));
            BattleSession.PreparedHostedDispatch prepared = session.PrepareHostedAction(Header, [Contact]);
            BattleModelRoster roster = session.NativeGraphics!.PresentationRoster!;
            Field("hostedFrameServices").SetValue(session, logical.CaptureModelLifecycleServices(roster));
            try
            {
                Assert.Throws<InvalidOperationException>(() => session.PrepareHostedAction(Header, [Contact]));
                Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(prepared));
            }
            finally
            {
                Field("hostedFrameServices").SetValue(session, null);
            }
            Assert.Equal(1L, session.PublishHostedAction(prepared));
        }));

        Assert.True(ran);
        Assert.Contains("482590", missingIo.Message);
        Assert.Equal(1L, session.HostedActiveActionToken);
        Assert.Null(Field("hostedFrameServices").GetValue(session));
    }

    private static BattleDefinition Definition(string allyId = "ally") => new()
    {
        UsesExpandedRoster = true,
        Combatants = [new() { Id = allyId, Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
        NativeGlobalRandom = new(123)
    };

    private static BattleSession Fixture()
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        BattleModelRoster roster = session.ReadBattlePresentationRoster();
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        models[0] = models[0] with { ModelId4 = 1 };
        models[3] = models[3] with { ModelId4 = 16 };
        Set(session, nameof(BattleSession.NativeGraphics), new NativeBattleGraphicsState(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = roster,
            EntryReady = true
        });
        Field("hostedBattlePresentation").SetValue(session, true);
        Field("presentationNativeTargets").SetValue(session, new Dictionary<int, NativeBattleEffectModelBinding>
        {
            [0] = new("ally", models[0], new([0, 0])) { IsParty = true }
        });
        Set(session, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { Textures = NativeBattleEffectTextureState.FromColdProcess() });
        Set(session, nameof(BattleSession.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster));
        Set(session, nameof(BattleSession.NativeModelTextureCache), NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster));
        return session;
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
}
