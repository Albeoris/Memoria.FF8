using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedContactReplacementTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleActionHeaderPreparation Header = new(7, 1, 5, -1, 0, 0, null);

    [Fact]
    public void ReplacesExtendedOccurrencePayloadsWithoutChangingTheHeaderCursorOrInvocation()
    {
        BattleSession session = Fixture();
        NativeBattleContactPacket[] original = Enumerable.Range(0, 260).Select(index => Contact(index % 2 == 0 ? (byte)24 : (byte)7)).ToArray();
        long token = session.BeginHostedAction(Header, original.ToImmutableArray());
        NativeBattleActionPacketsState packets = session.NativeActionPackets.ActivateHeader(0).AdvanceContact50A690().ResetCounts();
        Field("nativeActionPackets").SetValue(session, packets);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleContactPacket[] replacements = original.Select((packet, index) => Contact(packet.Primary.ActorIndex, (ushort)(index + 1), index == 259 ? (byte)7 : byte.MaxValue)).ToArray();

        BattleSession.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, replacements.ToImmutableArray());
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(graphics, session.NativeGraphics);
        session.ValidatePreparedHostedContactReplacement(prepared);
        session.PublishHostedContactReplacement(prepared);

        NativeBattleActionPacketsState published = session.NativeActionPackets;
        Assert.Equal(packets with { Contacts = published.Contacts }, published);
        Assert.Same(packets.Headers[0], published.Headers[0]);
        Assert.Equal(260, published.Headers[0].ReadContactCount());
        Assert.Equal(1, published.ActiveContactIndex);
        Assert.Equal(replacements, published.ReadHeaderContacts506BA0(0));
        Assert.Equal(259, published.ReadContact(259).PhysicalContactIndex);
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
        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(1, [Contact(24)]));
        long token = session.BeginHostedAction(Header, [Contact(24), Contact(7), Contact(24)]);
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        NativeBattleContactPacket malformed = Contact(24, 20) with { Primary = Contact(24, 20).Primary with { ExtendedDamageAmount = 40_000 } };

        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(token + 1, [Contact(24), Contact(7), Contact(24)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, default));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(24), Contact(7)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(24), Contact(24), Contact(7)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(24), Contact(7), Contact(23)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(24), Contact(7), Contact(24, 1, 24)]));
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(24), Contact(7), malformed]));
        Assert.Same(packets, session.NativeActionPackets);
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Equal(token, session.HostedActiveActionToken);
    }

    [Fact]
    public void PreparedReplacementRejectsForeignProcessesAndStaleOwnerFacts()
    {
        BattleSession session = Fixture();
        BattleSession other = Fixture();
        long token = session.BeginHostedAction(Header, [Contact(24)]);
        Assert.Equal(token, other.BeginHostedAction(Header, [Contact(24)]));
        NativeBattleGraphicsState graphics = session.NativeGraphics!;
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        NativeBattleActionPacketsState foreign = other.NativeActionPackets;
        BattleSession.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(24, 2)]);

        Assert.Throws<InvalidOperationException>(() => other.PublishHostedContactReplacement(prepared));
        Assert.Same(foreign, other.NativeActionPackets);
        Field("hostedFrameServices").SetValue(session, new NativeBattleModelLifecycleServices([]));
        Assert.Throws<InvalidOperationException>(() => session.PrepareHostedContactReplacement(token, [Contact(24, 2)]));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Field("hostedFrameServices").SetValue(session, null);
        Assert.Same(original, session.NativeActionPackets);

        Field("nativeActionPackets").SetValue(session, original.ActivateHeader(0));
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        NativeBattleActionPacketsState activated = session.NativeActionPackets;
        prepared = session.PrepareHostedContactReplacement(token, [Contact(24, 2)]);
        SetGraphics(session, graphics with { });
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(activated, session.NativeActionPackets);

        prepared = session.PrepareHostedContactReplacement(token, [Contact(24, 2)]);
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
        NativeBattleGraphicsActor?[] actors = new NativeBattleGraphicsActor?[25];
        actors[7] = new("ally", true);
        actors[24] = new("enemy", false);
        NativeBattleGraphicsRoster roster = new(actors.ToImmutableArray());
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        models[7] = models[7] with { ModelId4 = 1 };
        SetGraphics(session, new(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster, EntryReady = true });
        Field("hostedBattlePresentation").SetValue(session, true);
        Field("presentationNativeTargets").SetValue(session, new Dictionary<int, NativeBattleEffectModelBinding>
        {
            [7] = new("ally", models[7], new([0, 0])) { IsParty = true }
        });
        return session;
    }

    [Fact]
    public void Ordinary_trigger_replacement_acknowledges_only_the_exact_pending_receipt()
    {
        BattleSession session = Fixture();
        long token = session.BeginHostedAction(Header, [Contact(24)]);
        NativeBattleTriggerReceipt receipt = new(7, 1, 1, 25, 32);
        NativeBattleGraphicsState graphics = session.NativeGraphics! with { PendingTrigger = receipt };
        SetGraphics(session, graphics);
        NativeBattleActionPacketsState original = session.NativeActionPackets;
        Assert.Throws<InvalidDataException>(() => session.PrepareHostedContactReplacement(token, [Contact(24, 2)], receipt with { }));
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Same(original, session.NativeActionPackets);

        BattleSession.PreparedHostedContactReplacement prepared = session.PrepareHostedContactReplacement(token, [Contact(24, 2)], receipt);
        SetGraphics(session, graphics with { });
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
        Assert.Same(receipt, session.NativeGraphics!.PendingTrigger);
        Assert.Same(original, session.NativeActionPackets);

        prepared = session.PrepareHostedContactReplacement(token, [Contact(24, 2)], receipt);
        session.PublishHostedContactReplacement(prepared);
        Assert.Null(session.NativeGraphics!.PendingTrigger);
        Assert.Same(original.Headers[0], session.NativeActionPackets.Headers[0]);
        Assert.Equal((ushort)2, session.NativeActionPackets.ReadContact(0).Primary.Amount6);
        Assert.Equal(token, session.HostedActiveActionToken);
        Assert.Throws<InvalidOperationException>(() => session.PublishHostedContactReplacement(prepared));
    }

    private static NativeBattleContactPacket Contact(byte row, ushort amount = 0, byte secondary = byte.MaxValue)
    {
        return new(new(row, 0, 1, 0, 0, amount, 0), new(secondary, 0, 0, 0, 0, 0, 0));
    }

    private static void SetGraphics(BattleSession session, NativeBattleGraphicsState graphics) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
}
