using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedReceiptOrderingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattleEnemyActorModel Original = new(new("original"));
    private static readonly BattlePartyActorModel AddedAlly = new(new("extra-ally"));
    private static readonly BattleEnemyActorModel AddedEnemy = new(new("extra-enemy"));
    private static readonly BattlePartyActorModel Unbound = new(new("missing"));

    [Fact]
    public void Hosted_contact_consumer_interleaves_original_packet_occurrences_with_lifecycle_receipts()
    {
        var (session, graphics, services) = Fixture();
        NativeBattleContactPacket repeated = new(new(AddedEnemy, 5, 0, 0, 0, 123, 0), new(AddedAlly, 0, 0, 1, 0, 14, 0));
        NativeBattleContactPacket later = new(new(AddedEnemy, 5, 0, 0, 0, 234, 0), null);
        NativeBattleModelParticipantSuspended suspended = new(Original, "original", 9);
        NativeBattleModelParticipantReleased released = new(AddedAlly, "extra-ally");
        NativeBattleModelTextPublished text = new(AddedEnemy, "extra-enemy", "Original text", 6, BattleTextIdentity.Enemy(61, 0));
        services.Emit(suspended);
        graphics = Consume(session, graphics with { PendingContacts = [repeated, repeated] }, services);
        services.Emit(released);
        ImmutableArray<NativeBattlePresentationReceipt> prefix = services.ReadOrderedReceipts();
        graphics = Consume(session, graphics with { PendingContacts = [later] }, services);
        typeof(BattlePresentationProcess).GetMethod("PublishHostedModelLifecycleCue", PrivateInstance)!.Invoke(session.Presentation!,
            [graphics, AddedEnemy, graphics.ActorModels.Get(AddedEnemy), services.Facts[AddedEnemy], new NativeBattleSequenceCue(NativeBattleSequenceCueKind.Text, 0, 6), services]);

        Assert.Collection(services.ReadOrderedReceipts(),
            receipt => Assert.Same(suspended, receipt),
            receipt => Assert.Same(repeated, Assert.IsType<NativeBattlePresentationContactReceived>(receipt).Packet),
            receipt => Assert.Same(repeated, Assert.IsType<NativeBattlePresentationContactReceived>(receipt).Packet),
            receipt => Assert.Same(released, receipt),
            receipt => Assert.Same(later, Assert.IsType<NativeBattlePresentationContactReceived>(receipt).Packet),
            receipt => Assert.Equal(text, Assert.IsType<NativeBattleModelTextPublished>(receipt)));
        Assert.Equal(new NativeBattleModelLifecycleReceipt[] { suspended, released, text }, services.ReadReceipts());
        Assert.Equal(4, prefix.Length);
        Assert.Same(released, prefix[^1]);
        Assert.Collection(LegacyContacts(session), packet => Assert.Same(repeated, packet), packet => Assert.Same(repeated, packet), packet => Assert.Same(later, packet));
        Assert.Equal(5, graphics.Effects.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.Empty(graphics.PendingContacts);
    }

    [Fact]
    public void Invalid_late_secondary_owner_leaves_prior_receipts_and_models_intact()
    {
        var (session, graphics, services) = Fixture();
        NativeBattleModelParticipantReleased existing = new(Original, "original");
        services.Emit(existing);
        NativeBattleContactHalf valid = new(AddedEnemy, 5, 0, 0, 0, 123, 0);
        NativeBattleContactPacket first = new(valid, null);
        NativeBattleContactPacket bad = new(valid, valid with { Recipient = Unbound });
        graphics = graphics with { PendingContacts = [first, bad] };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session.Presentation!, graphics);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => Consume(session, graphics, services));

        Assert.IsType<InvalidDataException>(invocation.InnerException);
        Assert.Same(existing, Assert.Single(services.ReadOrderedReceipts()));
        Assert.Empty(LegacyContacts(session));
        Assert.Same(graphics, session.Presentation?.Graphics);
        Assert.Null(graphics.Effects.Head);
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics, NativeBattleModelLifecycleServices Services) Fixture()
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "original", Name = "Original", ModelId = 61 }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        BattleModelRoster roster = new([Original, AddedAlly, AddedEnemy]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(3), Original)]);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => new(2)
        {
            ModelId4 = owner.Side == BattleSide.Allies ? (byte)1 : (byte)77,
            Flags8 = 0,
            ResourceFlags44 = 1,
            Sequence = 1,
            SequenceState = new(),
            HasLinkedModelRing140 = false
        });
        NativeBattleHostedTestFixture.BindProcess(session, graphics, new(rules, [], []));
        NativeBattleGraphicsResource resource = new([0, 0]) { TextBank = ["Original text"] };
        Field("presentationNativeTargets").SetValue(session.Presentation!, new Dictionary<BattleModelOwner, NativeBattleEffectModelBinding>
        {
            [AddedEnemy] = new(AddedEnemy, graphics.ActorModels.Get(AddedEnemy), resource)
        });
        NativeBattleModelLifecycleServices services = new(ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts>.Empty
            .Add(Original, new("original", false, 61, 0, 0) { RawBattleStatuses = 0 })
            .Add(AddedAlly, new("extra-ally", true, 61, 0, 0) { RawBattleStatuses = 0 })
            .Add(AddedEnemy, new("extra-enemy", false, 61, 0, 0) { RawBattleStatuses = 0 }));
        return (session, graphics, services);
    }

    private static NativeBattleGraphicsState Consume(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services)
    {
        return (NativeBattleGraphicsState)typeof(BattlePresentationProcess).GetMethod("ConsumeHostedNativeGraphicsContacts", PrivateInstance)!.Invoke(session.Presentation!, [graphics, services, NativeBattleContactOrigin.Ordinary])!;
    }

    private static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, PrivateInstance)!;
}
