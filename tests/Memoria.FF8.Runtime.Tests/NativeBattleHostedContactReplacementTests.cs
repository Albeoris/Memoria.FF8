using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedContactReplacementTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattlePartyModelOwner Ally = new BattlePartyActorModel(new("ally"));
    private static readonly BattleEnemyModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly BattleEnemyModelOwner Missing = new BattleEnemyActorModel(new("missing"));
    private static readonly NativeBattleActionHeaderPreparation Header = new(Ally, 1, 5, -1, 0, 0, null);

    [Fact]
    public void ReplacesExtendedOccurrencePayloadsWithoutChangingTheHeaderCursorOrInvocation()
    {
        BattleSession session = Fixture();
        NativeBattleContactPacket[] original = Enumerable.Range(0, 260).Select(index => Contact(index % 2 == 0 ? Enemy : Ally)).ToArray();
        long token = session.BeginHostedAction(Header, original.ToImmutableArray());
        NativeBattleActionPacketsState packets = session.NativeActionPackets.ActivateHeader(new BattleActionHeaderReference(0)).AdvanceContact().ResetCounts();
        Field("nativeActionPackets").SetValue(session, packets);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleContactPacket[] replacements = original.Select((packet, index) => Contact(packet.Primary.Recipient, (ushort)(index + 1), index == 259 ? Ally : null)).ToArray();

        BattleSession.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, replacements.ToImmutableArray());
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(graphics, session.NativeGraphics);
        session.ValidatePreparedHostedContactReplacement(prepared);
        session.PublishHostedContactReplacement(prepared);

        NativeBattleActionPacketsState published = session.NativeActionPackets;
        Assert.Same(packets.Roster, published.Roster);
        Assert.Equal(packets.ActionCount, published.ActionCount);
        Assert.Equal(packets.ContactCount, published.ContactCount);
        Assert.Equal(packets.ActiveHeader, published.ActiveHeader);
        Assert.Same(packets.ReadHeader(new BattleActionHeaderReference(0)), published.ReadHeader(new BattleActionHeaderReference(0)));
        Assert.Equal(260, published.ReadHeader(new BattleActionHeaderReference(0)).ReadContactCount());
        Assert.Equal(new BattleContactCursor(1), published.ActiveContact);
        Assert.Equal(replacements, published.ReadHeaderContacts(new BattleActionHeaderReference(0)));
        Assert.Equal(new BattleContactOccurrence(259), published.ReadContact(new BattleContactOccurrence(259)).Occurrence);
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.False(session.IsHostedActionComplete(token));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(published, session.NativeActionPackets);
    }

    [Fact]
    public void LateInvalidReplacementKeepsAllExistingPacketsAndDescriptorOwnership()
    {
        BattleSession session = Fixture();
        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(1, [Contact(Enemy)]));
        long token = session.BeginHostedAction(Header, [Contact(Enemy), Contact(Ally), Contact(Enemy)]);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        NativeBattleContactPacket malformed = Contact(Enemy, 20) with { Primary = Contact(Enemy, 20).Primary with { ExtendedDamageAmount = 40_000 } };

        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(token + 1, [Contact(Enemy), Contact(Ally), Contact(Enemy)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, default));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy), Contact(Ally)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy), Contact(Enemy), Contact(Ally)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy), Contact(Ally), Contact(Missing)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy), Contact(Ally), Contact(Enemy, 1, Enemy)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy), Contact(Ally), malformed]));
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Equal(token, session.HostedActiveActionToken);
    }

    [Fact]
    public void PreparedReplacementRejectsForeignProcessesAndStaleOwnerFacts()
    {
        BattleSession session = Fixture();
        BattleSession other = Fixture();
        long token = session.BeginHostedAction(Header, [Contact(Enemy)]);
        Assert.Equal(token, other.BeginHostedAction(Header, [Contact(Enemy)]));
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        NativeBattleActionPacketsState foreign = other.NativeActionPackets;
        BattleSession.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)]);

        Assert.Throws<InvalidOperationException>(() => other.PublishHostedContactReplacement(prepared));
        Assert.Same(foreign, other.NativeActionPackets);
        Field("hostedFrameServices").SetValue(session, new NativeBattleModelLifecycleServices([]));
        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)]));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Field("hostedFrameServices").SetValue(session, null);
        Assert.Same(original, session.NativeActionPackets);

        Field("nativeActionPackets").SetValue(session, original.ActivateHeader(new BattleActionHeaderReference(0)));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        NativeBattleActionPacketsState activated = session.NativeActionPackets;
        prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)]);
        SetGraphics(session, graphics with { });
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(activated, session.NativeActionPackets);

        prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)]);
        FieldInfo epoch = Field("hostedPreparationEpoch");
        epoch.SetValue(session, (long)epoch.GetValue(session)! + 1);
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(activated, session.NativeActionPackets);
        Assert.Equal(graphics.Descriptors, session.NativeGraphics!.Descriptors);
        Assert.Equal(token, session.HostedActiveActionToken);
    }

    private static BattleSession Fixture()
    {
        BattleSession session = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        BattleModelRoster roster = new([Ally, Enemy]);
        NativeBattleModelAliases aliases = new(roster, []);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner == Ally ? (byte)1 : (byte)16 });
        SetGraphics(session, graphics with { EntryReady = true });
        Field("nativeActionPackets").SetValue(session, NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases));
        Field("hostedBattlePresentation").SetValue(session, true);
        NativeBattleHostedTestFixture.InstallModelBindings(session, new NativeBattleGraphicsBinding("ally", 1, new([0, 0])), new NativeBattleGraphicsBinding("enemy", 16, new([0, 0])));
        return session;
    }

    [Fact]
    public void Ordinary_trigger_replacement_acknowledges_only_the_exact_pending_receipt()
    {
        BattleSession session = Fixture();
        long token = session.BeginHostedAction(Header, [Contact(Enemy)]);
        NativeBattleTriggerReceipt receipt = new(Ally, 1, 1, 25, 32);
        NativeBattleGraphicsState graphics = session.NativeGraphics! with { PendingTrigger = receipt };
        SetGraphics(session, graphics);
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)], receipt with { }));
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Same(original, session.NativeActionPackets);

        BattleSession.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)], receipt);
        SetGraphics(session, graphics with { });
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(receipt, session.NativeGraphics!.PendingTrigger);
        Assert.Same(original, session.NativeActionPackets);

        prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)], receipt);
        session.PublishHostedContactReplacement(prepared);
        Assert.Null(session.NativeGraphics!.PendingTrigger);
        Assert.Same(original.ReadHeader(new BattleActionHeaderReference(0)), session.NativeActionPackets.ReadHeader(new BattleActionHeaderReference(0)));
        Assert.Equal((ushort)2, session.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary.Amount6);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner owner, ushort amount = 0, BattleModelOwner? secondary = null)
    {
        return new(new(owner, 0, 1, 0, 0, amount, 0), secondary is null ? null : new(secondary, 0, 0, 0, 0, 0, 0));
    }

    private static void SetGraphics(BattleSession session, NativeBattleGraphicsState graphics) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
}
