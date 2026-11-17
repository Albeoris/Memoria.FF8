using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionPreparationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattlePartyModelOwner Source = new BattlePartyActorModel(new("ally"));
    private static readonly BattleEnemyModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly NativeBattleActionHeaderPreparation Header = new(Source, 1, 5, -1, 0, 0, null);
    private static readonly NativeBattleContactPacket Contact = new(new(Enemy, 0, 1, 0, 0, 0, 0), null);

    [Fact]
    public void PreparationLeavesLiveOwnersUntouchedAndPublishesTheSameAdmissionAsBegin()
    {
        BattlePresentationProcess session = Fixture();
        BattlePresentationProcess direct = Fixture();
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        NativeBattleEffectMemory memory = session.Memory;
        NativeBattleModelCpuResources cpu = session.ModelCpuResources;
        NativeBattleModelTextureCache cache = session.ModelTextureCache;
        NativeGlobalRandomChange? random = session.NativeGlobalRandomChange;

        BattlePresentationProcess.PreparedHostedDispatch prepared = session.PrepareHostedAction(Header, [Contact, Contact]);
        session.ValidatePreparedHostedAction(prepared);

        Assert.Same(graphics, session.Graphics);
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(memory, session.Memory);
        Assert.Same(cpu, session.ModelCpuResources);
        Assert.Same(cache, session.ModelTextureCache);
        Assert.Equal(random, session.NativeGlobalRandomChange);
        Assert.Null(session.HostedActiveActionToken);
        Assert.Null(Field("presentationSourceResource").GetValue(session));

        long token = session.PublishHostedAction(prepared);
        Assert.Equal(direct.BeginHostedAction(Header, [Contact, Contact]), token);
        Assert.Equal(NativeBattleTestSnapshots.Json(direct.NativeActionPackets.ExportRetained()), NativeBattleTestSnapshots.Json(session.NativeActionPackets.ExportRetained()));
        Assert.Equal(NativeBattleTestSnapshots.Json(direct.Graphics.Descriptors), NativeBattleTestSnapshots.Json(session.Graphics.Descriptors));
        Assert.Same(graphics.ActorModels, session.Graphics.ActorModels);
        Assert.Same(memory, session.Memory);
        Assert.Same(cpu, session.ModelCpuResources);
        Assert.Same(cache, session.ModelTextureCache);
        Assert.Equal(random, session.NativeGlobalRandomChange);
    }

    [Fact]
    public void CompetingPreparationsAreSingleUseAndOwnerBound()
    {
        BattlePresentationProcess session = Fixture();
        BattlePresentationProcess other = Fixture();
        BattlePresentationProcess.PreparedHostedDispatch first = session.PrepareHostedAction(Header, [Contact]);
        BattlePresentationProcess.PreparedHostedDispatch second = session.PrepareHostedAction(Header, [Contact, Contact]);
        NativeBattleGraphicsState otherGraphics = other.Graphics;
        NativeBattleActionPacketsState otherPackets = other.NativeActionPackets;

        Assert.Throws<InvalidOperationException>(() => other.PublishHostedAction(first));
        Assert.Same(otherGraphics, other.Graphics);
        Assert.Same(otherPackets, other.NativeActionPackets);
        Assert.Null(other.HostedActiveActionToken);

        Assert.Equal(1L, session.PublishHostedAction(second));
        NativeBattleGraphicsState admitted = session.Graphics;
        NativeBattleActionPacketsState admittedPackets = session.NativeActionPackets;
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(first));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(second));
        Assert.Same(admitted, session.Graphics);
        Assert.Same(admittedPackets, session.NativeActionPackets);
        Assert.Equal(2, admittedPackets.ContactCount);
        Assert.Equal(1L, session.HostedActiveActionToken);
    }

    [Fact]
    public void FailedFrameAfterItsFirstWriteInvalidatesPreparationEvenWithSameGraphicsReference()
    {
        BattlePresentationProcess session = Fixture();
        BattlePresentationProcess.PreparedHostedDispatch prepared = session.PrepareHostedAction(Header, [Contact]);
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        // Missing I/O fails after valid canonical facts and the start of frame mutation, before graphics changes.
        Assert.Null(session.NativeFileIo);
        NotSupportedException failure = Assert.Throws<NotSupportedException>(() => session.AdvanceHostedBattlePresentationFrame(null));
        Assert.Contains("482590", failure.Message);
        Assert.Same(graphics, session.Graphics);
        Assert.Throws<InvalidOperationException>(() => session.ValidatePreparedHostedAction(prepared));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedAction(prepared));
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Null(session.HostedActiveActionToken);

        Assert.Equal(1L, session.BeginHostedAction(Header, [Contact]));
    }

    [Fact]
    public void LateInvalidPreparationKeepsTheEarlierPreparationValid()
    {
        BattlePresentationProcess session = Fixture();
        BattlePresentationProcess.PreparedHostedDispatch first = session.PrepareHostedAction(Header, [Contact]);
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        NativeGlobalRandomChange? random = session.NativeGlobalRandomChange;

        NativeBattleContactPacket late = Contact with { Primary = Contact.Primary with { Recipient = new BattleEnemyActorModel(new("unbound")) } };
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedAction(Header, [Contact, late]));
        Assert.Same(graphics, session.Graphics);
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Equal(random, session.NativeGlobalRandomChange);
        Assert.Equal(1L, session.PublishHostedAction(first));
    }

    [Fact]
    public void Before_core_can_replace_stale_admission_but_graphics_services_block_admission()
    {
        BattlePresentationProcess session = Fixture();
        BattlePresentationProcess.PreparedHostedDispatch stale = session.PrepareHostedAction(Header, [Contact]);
        bool ran = false;

        NotSupportedException missingIo = Assert.Throws<NotSupportedException>(() => session.AdvanceHostedBattlePresentationFrame(null, () =>
        {
            ran = true;
            Assert.Throws<InvalidOperationException>(() => session.ValidatePreparedHostedAction(stale));
            BattlePresentationProcess.PreparedHostedDispatch prepared = session.PrepareHostedAction(Header, [Contact]);
            BattleModelRoster roster = session.Graphics.ActorModels.Roster;
            Field("hostedFrameServices").SetValue(session, NativeBattleHostedTestFixture.ReadModelServices(session, roster));
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
        Combatants = [new() { Id = allyId, Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
        NativeGlobalRandom = new(123)
    };

    private static BattlePresentationProcess Fixture()
    {
        BattleSession logical = new(Definition(), BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner == Source ? (byte)1 : (byte)16 });
        NativeBattleHostedTestFixture.InstallModelBindings(logical, new NativeBattleGraphicsBinding("ally", 1, new([0, 0])), new NativeBattleGraphicsBinding("enemy", 16, new([0, 0])));
        BattlePresentationProcess session = NativeBattleHostedTestFixture.SeedProcess(logical, graphics with { EntryReady = true });
        Set(session, nameof(BattlePresentationProcess.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { Textures = NativeBattleEffectTextureState.FromColdProcess() });
        Set(session, nameof(BattlePresentationProcess.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster, aliases));
        Set(session, nameof(BattlePresentationProcess.NativeModelTextureCache), NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster));
        return session;
    }

    private static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, PrivateInstance)!;

    private static void Set(BattlePresentationProcess session, string property, object value) => typeof(BattlePresentationProcess).GetProperty(property)!.SetValue(session, value);
}
