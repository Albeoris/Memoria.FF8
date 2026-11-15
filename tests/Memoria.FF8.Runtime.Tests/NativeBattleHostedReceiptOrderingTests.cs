using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedReceiptOrderingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleContactHalf NoSecondary = new(byte.MaxValue, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Hosted_contact_consumer_interleaves_original_packet_occurrences_with_lifecycle_receipts()
    {
        var (session, graphics, services) = Fixture();
        NativeBattleContactPacket repeated = new(new(15, 5, 0, 0, 0, 123, 0), new(7, 0, 0, 1, 0, 14, 0));
        NativeBattleContactPacket later = new(new(15, 5, 0, 0, 0, 234, 0), NoSecondary);
        NativeBattleModelParticipantSuspended suspended = new(OriginalOwner(3), "original", 9);
        NativeBattleModelParticipantReleased released = new(7, "extra-ally");
        NativeBattleModelTextPublished text = new(15, "extra-enemy", "Original text", 6, BattleTextIdentity.Enemy(61, 0));
        services.Emit(suspended);
        graphics = Consume(session, graphics with { PendingContacts = [repeated, repeated] }, services);
        services.Emit(released);
        ImmutableArray<NativeBattlePresentationReceipt> prefix = services.ReadOrderedReceipts();
        graphics = Consume(session, graphics with { PendingContacts = [later] }, services);
        typeof(BattleSession).GetMethod("PublishHostedModelLifecycleCue", PrivateInstance)!.Invoke(session,
            [graphics, graphics.Models[15], services.Facts[15], new NativeBattleSequenceCue(NativeBattleSequenceCueKind.Text, 0, 6), services]);

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
    public void Invalid_late_secondary_row_leaves_prior_receipts_and_models_intact()
    {
        var (session, graphics, services) = Fixture();
        NativeBattleModelParticipantReleased existing = new(OriginalOwner(3), "original");
        services.Emit(existing);
        NativeBattleContactHalf valid = new(15, 5, 0, 0, 0, 123, 0);
        NativeBattleContactPacket first = new(valid, NoSecondary);
        NativeBattleContactPacket bad = new(valid, valid with { ActorIndex = 14 });
        graphics = graphics with { PendingContacts = [first, bad] };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => Consume(session, graphics, services));

        Assert.IsType<InvalidDataException>(invocation.InnerException);
        Assert.Same(existing, Assert.Single(services.ReadOrderedReceipts()));
        Assert.Empty(LegacyContacts(session));
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Null(graphics.Effects.Head);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_and_foreign_component_consumers_do_not_emit_hosted_receipts(bool foreign)
    {
        var (session, hostedGraphics, services) = Fixture();
        if (foreign)
        {
            Field("presentationForeignActors").SetValue(session, new Dictionary<int, NativeBattleForeignActor>());
        }
        Field("hostedFrameServices").SetValue(session, services);
        NativeBattleContactPacket packet = new(new(OriginalOwner(3), 5, 0, 0, 0, 123, 0), NoSecondary);
        NativeBattleGraphicsState nativeGraphics = hostedGraphics with
        {
            PresentationRoster = null,
            Models = hostedGraphics.Models[..7],
            ActorIds = hostedGraphics.ActorIds[..7],
            PendingContacts = [packet]
        };
        NativeBattleGraphicsState result = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("ConsumeNativeGraphicsContacts", PrivateInstance)!.Invoke(session, [nativeGraphics])!;

        Assert.Empty(result.PendingContacts);
        Assert.Empty(services.ReadOrderedReceipts());
        if (foreign)
        {
            Assert.Same(packet, Assert.Single(LegacyContacts(session)));
            Assert.Null(result.Effects.Head);
        }
        else
        {
            Assert.Empty(LegacyContacts(session));
            Assert.NotNull(result.Effects.Head);
        }
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics, NativeBattleModelLifecycleServices Services) Fixture()
    {
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "original", Name = "Original", ModelId = 61 }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        Field("definition").SetValue(session, definition with { NativeGraphics = new(rules, [], []) });
        BattleModelOwner?[] actors = new BattleModelOwner?[16];
        actors[3] = new BattleEnemyActorModel(new("original"));
        actors[7] = new BattlePartyActorModel(new("extra-ally"));
        actors[15] = new BattleEnemyActorModel(new("extra-enemy"));
        BattleModelRoster roster = new(actors.ToImmutableArray());
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        foreach (int row in new[] { 3, 7, 15 })
        {
            models[row] = models[row] with { ModelId4 = row == 7 ? (byte)1 : (byte)77, Flags0 = 2, Flags8 = 0, ResourceFlags44 = 1, Sequence = 1, SequenceState = new(), HasLinkedModelRing140 = false };
        }
        NativeBattleGraphicsResource resource = new([0, 0]) { TextBank = ["Original text"] };
        Field("presentationNativeTargets").SetValue(session, new Dictionary<int, NativeBattleEffectModelBinding>
        {
            [15] = new("extra-enemy", models[15], resource)
        });
        NativeBattleGraphicsState graphics = new(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        NativeBattleModelLifecycleServices services = new(roster.Rows.Select(actor => actor is null ? null : new NativeBattleModelActorFacts(actor.ActorId, actor.IsParty, 61, 0, 0) { RawBattleStatuses = 0 }).ToImmutableArray());
        return (session, graphics, services);
    }

    private static NativeBattleGraphicsState Consume(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("ConsumeHostedNativeGraphicsContacts", PrivateInstance)!.Invoke(session, [graphics, services])!;
    }

    private static List<NativeBattleContactPacket> LegacyContacts(BattleSession session) => (List<NativeBattleContactPacket>)Field("presentationContacts").GetValue(session)!;

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
}
