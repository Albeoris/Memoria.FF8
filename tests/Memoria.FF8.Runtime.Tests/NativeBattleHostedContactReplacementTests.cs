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
        BattlePresentationProcess session = Fixture();
        NativeBattleContactPacket[] original = Enumerable.Range(0, 260).Select(index => Contact(index % 2 == 0 ? Enemy : Ally)).ToArray();
        long token = session.BeginHostedAction(Header, original.ToImmutableArray());
        NativeBattleActionPacketsState packets = session.NativeActionPackets.ActivateHeader(new BattleActionHeaderReference(0)).AdvanceContact().ResetCounts();
        Field("nativeActionPackets").SetValue(session, packets);
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleContactPacket[] replacements = original.Select((packet, index) => Contact(packet.Primary.Recipient, (ushort)(index + 1), index == 259 ? Ally : null)).ToArray();

        BattlePresentationProcess.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, replacements.ToImmutableArray());
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(graphics, session.Graphics);
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
        Assert.Same(graphics, session.Graphics);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.False(session.IsHostedActionComplete(token));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(published, session.NativeActionPackets);
    }

    [Fact]
    public void LateInvalidReplacementKeepsAllExistingPacketsAndDescriptorOwnership()
    {
        BattlePresentationProcess session = Fixture();
        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(1, [Contact(Enemy)]));
        long token = session.BeginHostedAction(Header, [Contact(Enemy), Contact(Ally), Contact(Enemy)]);
        NativeBattleGraphicsState graphics = session.Graphics;
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
        Assert.Same(graphics, session.Graphics);
        Assert.Equal(token, session.HostedActiveActionToken);
    }

    [Fact]
    public void PreparedReplacementRejectsForeignProcessesAndStaleOwnerFacts()
    {
        BattlePresentationProcess session = Fixture();
        BattlePresentationProcess other = Fixture();
        long token = session.BeginHostedAction(Header, [Contact(Enemy)]);
        Assert.Equal(token, other.BeginHostedAction(Header, [Contact(Enemy)]));
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        NativeBattleActionPacketsState foreign = other.NativeActionPackets;
        BattlePresentationProcess.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)]);

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
        Assert.Equal(graphics.Descriptors, session.Graphics.Descriptors);
        Assert.Equal(token, session.HostedActiveActionToken);
    }

    [Fact]
    public void AtomicAppendPreservesTheActiveCursorAndExistingCompletionQueue()
    {
        BattlePresentationProcess session = Fixture();
        long token = BeginWithCompletion(session);
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        NativeBattleGraphicsState graphics = session.Graphics;
        BattlePresentationProcess.HostedDispatchHeader nested = new(new(Enemy, 2, 0, -1, 13, 107, null), [Contact(Ally, 7)]);
        BattlePresentationProcess.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2), Contact(Ally, 3)], append: [nested]);
        BattleActionHeaderReference suffix = Assert.Single(prepared.AppendedHeaders);
        Assert.Equal(new BattleActionHeaderReference(1), suffix);
        Assert.Same(original, session.NativeActionPackets);
        Assert.Same(graphics, session.Graphics);

        session.PublishHostedContactReplacement(prepared);

        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        Assert.Equal(2, packets.ActionCount);
        Assert.Equal(3, packets.ContactCount);
        Assert.Equal(original.ActiveHeader, packets.ActiveHeader);
        Assert.Equal(original.ActiveContact, packets.ActiveContact);
        Assert.Same(original.ReadHeader(new BattleActionHeaderReference(0)), packets.ReadHeader(new BattleActionHeaderReference(0)));
        Assert.Equal(new BattleContactCursor(2), packets.ReadHeader(suffix).FirstContact);
        Assert.Equal((byte)0, packets.ReadHeader(suffix).ReadRepeatModeMinusOne());
        Assert.Equal(new ushort[] { 2, 3 }, packets.ReadHeaderContacts(new BattleActionHeaderReference(0)).Select(packet => packet.Primary.Amount6));
        Assert.Equal(Ally, Assert.Single(packets.ReadHeaderContacts(suffix)).Primary.Recipient);
        Assert.Equal(Enemy, packets.ReadHeader(suffix).Source);
        Assert.Same(graphics.Descriptors, session.Graphics.Descriptors);
        Assert.Equal(new ushort[] { 104, 10 }, graphics.Descriptors.Queue.Select(index => graphics.Descriptors.Packets[index].Command));
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
    }

    [Fact]
    public void BadOrStaleAppendCannotPublishResolvedContactsOrOverwriteABoundHeader()
    {
        BattlePresentationProcess session = Fixture();
        long token = BeginWithCompletion(session);
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        NativeBattleGraphicsState graphics = session.Graphics;
        NativeBattleContactPacket[] contacts = [Contact(Enemy, 2), Contact(Ally, 3)];
        BattlePresentationProcess.HostedDispatchHeader valid = new(new(Enemy, 2, 0, -1, 13, 107, null), [Contact(Ally)]);
        BattlePresentationProcess.HostedDispatchHeader invalid = valid with { Contacts = [Contact(Missing)] };
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [.. contacts], append: [valid, invalid]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [.. contacts], append: [valid with { Header = valid.Header with { Source = Missing } }]));
        Assert.Same(original, session.NativeActionPackets);
        Assert.Same(graphics, session.Graphics);

        BattlePresentationProcess.PreparedHostedContactReplacement stale = session.PrepareHostedContactReplacement(token, [.. contacts], append: [valid]);
        NativeBattleActionPacketsState reset = original.ResetCounts();
        Field("nativeActionPackets").SetValue(session, reset);
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(stale));
        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(token, [.. contacts], append: [valid]));
        Assert.Same(reset, session.NativeActionPackets);
        Assert.Same(original.ReadHeader(new BattleActionHeaderReference(0)), reset.ReadHeader(new BattleActionHeaderReference(0)));
        Assert.Equal(original.ReadHeaderContacts(new BattleActionHeaderReference(0)), reset.ReadHeaderContacts(new BattleActionHeaderReference(0)));
        Assert.Same(graphics, session.Graphics);
    }

    private static long BeginWithCompletion(BattlePresentationProcess session)
    {
        BattlePresentationProcess.PreparedHostedDispatch admission = session.PrepareHostedDispatch([new(Header, [Contact(Enemy), Contact(Ally)])], dispatchAppended: true, completion: new(0x47DE70, Ally, "ally"));
        long token = session.PublishHostedAction(admission);
        Field("nativeActionPackets").SetValue(session, session.NativeActionPackets.ActivateHeader(new BattleActionHeaderReference(0)).AdvanceContact());
        return token;
    }

    private static BattlePresentationProcess Fixture()
    {
        BattleSession logical = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        BattleModelRoster roster = new([Ally, Enemy]);
        NativeBattleModelAliases aliases = new(roster, []);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner == Ally ? (byte)1 : (byte)16 });
        NativeBattleHostedTestFixture.InstallModelBindings(logical, new NativeBattleGraphicsBinding("ally", 1, new([0, 0])), new NativeBattleGraphicsBinding("enemy", 16, new([0, 0])));
        return NativeBattleHostedTestFixture.SeedProcess(logical, graphics with { EntryReady = true });
    }

    [Fact]
    public void Ordinary_trigger_replacement_acknowledges_only_the_exact_pending_receipt()
    {
        BattlePresentationProcess session = Fixture();
        long token = session.BeginHostedAction(Header, [Contact(Enemy)]);
        NativeBattleTriggerReceipt receipt = new(Ally, 1, 1, 25, 32);
        NativeBattleGraphicsState graphics = session.Graphics with { PendingTrigger = receipt };
        SetGraphics(session, graphics);
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)], receipt with { }));
        Assert.Same(graphics, session.Graphics);
        Assert.Same(original, session.NativeActionPackets);

        BattlePresentationProcess.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)], receipt);
        SetGraphics(session, graphics with { });
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(receipt, session.Graphics.PendingTrigger);
        Assert.Same(original, session.NativeActionPackets);

        prepared = session.PrepareHostedContactReplacement(token, [Contact(Enemy, 2)], receipt);
        session.PublishHostedContactReplacement(prepared);
        Assert.Null(session.Graphics.PendingTrigger);
        Assert.Same(original.ReadHeader(new BattleActionHeaderReference(0)), session.NativeActionPackets.ReadHeader(new BattleActionHeaderReference(0)));
        Assert.Equal((ushort)2, session.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary.Amount6);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner owner, ushort amount = 0, BattleModelOwner? secondary = null)
    {
        return new(new(owner, 0, 1, 0, 0, amount, 0), secondary is null ? null : new(secondary, 0, 0, 0, 0, 0, 0));
    }

    private static void SetGraphics(BattlePresentationProcess session, NativeBattleGraphicsState graphics) => typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session, graphics);
    private static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, PrivateInstance)!;
}
