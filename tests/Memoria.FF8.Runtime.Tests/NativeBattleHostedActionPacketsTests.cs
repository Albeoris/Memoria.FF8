using System.Text.Json;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionPacketsTests
{
    private static readonly BattlePartyActorModel Source = new(new("source"));
    private static readonly BattleEnemyActorModel Target = new(new("target"));
    private static readonly BattleEnemyActorModel Unbound = new(new("unbound"));
    private static readonly BattleModelRoster Roster = new([Source, Target]);
    private static readonly BattleActionHeaderReference First = new(0);

    [Fact]
    public void Native_bridge_preserves_packet_bytes_and_retained_json()
    {
        NativeBattleContactPacket contact = Contact(OriginalOwner(3));
        NativeBattleEffectContactBridge<string> bridge = new([("target", contact)], OriginalRoster);
        NativeBattleActionPacketsState empty = NativeBattleActionPacketsState.Create(OriginalRoster, ColdStorage(), OriginalAliases);
        (NativeBattleActionPacketsState packets, BattleModelTargets targets) = bridge.Prepare(empty, 2, 5, 6, 7, OriginalOwner(0));
        Assert.True(targets.IsSingle(OriginalOwner(3)));
        Assert.Equal((ushort)8, OriginalAliases.EncodeTargetMask(targets));
        NativeBattleRetainedActionPackets retained = packets.ExportRetained();
        Assert.Equal(new byte[] { 0, 2, 5, 255, 6, 0, 7, 0, 0x44, 0x83, 0xD2, 1, 0, 0, 0, 0, 1, 0, 0, 0 }, retained.Headers[0].Payload.ToArray());
        byte[] physical = NativeBattleContactPacketCodec.Encode(retained.Contacts[0]!);
        Assert.Equal((byte)3, physical[0]);
        Assert.Equal((byte)255, physical[12]);
        Assert.Equal(new byte[24], NativeBattleContactPacketCodec.Encode(retained.Contacts[48]!));
        Assert.Equal(contact, packets.ReadContact(new BattleContactOccurrence(0)));

        string json = JsonSerializer.Serialize(retained);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("PresentationRoster", out _));
        Assert.False(document.RootElement.TryGetProperty("StorageCapacity", out _));
        Assert.False(document.RootElement.GetProperty("Headers")[0].TryGetProperty("Source", out _));
        NativeBattleRetainedActionPackets restored = JsonSerializer.Deserialize<NativeBattleRetainedActionPackets>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidOperationException>(() => bridge.Prepare(packets, 2, 5, 6, 7, OriginalOwner(0)));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(300)]
    public void Hosted_bridge_preserves_identical_occurrences_and_full_header_counts(int count)
    {
        NativeBattleContactPacket contact = Contact(Target, Source);
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("target", contact), count), Roster);
        (NativeBattleActionPacketsState packets, BattleModelTargets targets) = bridge.Prepare(HostedPackets(), 2, 5, 6, 7, Source);
        packets.Validate();
        Assert.True(targets.IsSingle(Target));
        Assert.Null(packets.ModelAliases.EncodeTargetMask(targets));
        Assert.Equal(count, packets.ContactCount);
        Assert.Equal(count, packets.ReadHeader(First).ReadContactCount());
        NativeBattleRetainedActionPackets retained = packets.ExportRetained();
        Assert.Equal(32, retained.Headers.Length);
        Assert.Equal(49, retained.Contacts.Length);
        Assert.Equal(255, retained.Contacts[0]!.UnknownBits[0]);
        Assert.Equal(255, retained.Contacts[0]!.UnknownBits[12]);
        if (count > byte.MaxValue)
        {
            Assert.Throws<NotSupportedException>(() => retained.Headers[0].ReadOriginalByte(16));
            Assert.Equal(255, retained.ContactCount);
            Assert.Equal(2, retained.UnknownCountMask & 2);
        }
        else
        {
            Assert.Equal((byte)count, retained.Headers[0].ReadOriginalByte(16));
            Assert.Equal(count, retained.ContactCount);
        }
        Assert.Equal((48, "target"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(48))));
        Assert.Equal((49, "target"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(49))));
        Assert.Equal((count - 1, "target"), bridge.ResolveOccurrence(packets.ReadHeaderContacts(First)[^1]));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(contact));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(contact with { Occurrence = new BattleContactOccurrence(count) }));
    }

    [Fact]
    public void Hosted_cursor_and_following_header_continue_through_private_copy_and_dynamic_growth()
    {
        NativeBattleActionPacketsState packets = HostedPackets();
        Assert.Throws<NotSupportedException>(() => packets.ReadContact(new BattleContactOccurrence(0)));
        (packets, _) = packets.Prepare(Header());
        packets = packets.AppendContacts(Enumerable.Repeat(Contact(Target), 49));
        (packets, _) = packets.Publish(49);
        (packets, BattleActionHeaderReference second) = packets.Prepare(Header());
        Assert.Equal(new BattleContactCursor(49), packets.ReadHeader(second).FirstContact);
        Assert.Equal(49, packets.ExportRetained().Headers[1].FirstContactIndex);
        (packets, _) = packets.AppendContact(Contact(Target));
        (packets, _) = packets.Publish(1);
        packets = packets.ActivateHeader(second);

        NativeBattleActionPacketsState restored = NativeBattleTestSnapshots.Independent(packets);
        restored.Validate();
        Assert.Equal(new BattleContactOccurrence(49), restored.ReadActiveContact().Occurrence);
        restored = restored.AdvanceContact();
        (restored, BattleContactOccurrence added) = restored.AppendContact(Contact(Target));
        Assert.Equal(new BattleContactOccurrence(50), added);
        Assert.Equal(added, restored.ReadActiveContact().Occurrence);
        restored = restored.AdvanceContact();
        (restored, BattleActionHeaderReference third) = restored.Prepare(Header());
        Assert.Equal(new BattleContactCursor(51), restored.ReadHeader(third).FirstContact);
        Assert.Null(restored.ExportRetained().Headers[2].FirstContactIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.ReadActiveContact());
        (restored, BattleContactOccurrence following) = restored.AppendContact(Contact(Target));
        Assert.Equal(new BattleContactOccurrence(51), following);
        Assert.Equal(new BattleContactOccurrence(49), packets.ReadActiveContact().Occurrence);
        Assert.Equal(50, packets.ContactCount);
    }

    [Fact]
    public void Reset_and_reprepare_retain_count_and_repeat_until_the_next_publish()
    {
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("target", Contact(Target)), 300), Roster);
        (NativeBattleActionPacketsState packets, _) = bridge.Prepare(HostedPackets(), 2, 5, 6, 7, Source);
        packets = packets.WritePlayerRepeatMode(First, 3).ActivateHeader(First).ResetCounts();
        packets.Validate();
        Assert.Equal(0, packets.ContactCount);
        Assert.Equal(300, packets.ReadHeader(First).ReadContactCount());
        Assert.Equal(300, packets.ReadHeaderContacts(First).Length);
        (packets, _) = packets.Prepare(Header());
        Assert.Equal(300, packets.ReadHeader(First).ReadContactCount());
        Assert.Equal((byte)2, packets.ExportRetained().Headers[0].ReadOriginalByte(17));
        (packets, _) = packets.AppendContact(Contact(Target));
        (packets, _) = packets.Publish(1);
        packets.Validate();
        Assert.Equal(1, packets.ReadHeader(First).ReadContactCount());
        Assert.Equal((byte)2, packets.ExportRetained().Headers[0].ReadOriginalByte(17));
    }

    [Fact]
    public void Hosted_packet_writers_require_current_owners_and_compatible_secondary_source()
    {
        NativeBattleActionPacketsState packets = HostedPackets();
        Assert.Throws<InvalidDataException>(() => packets.Prepare(Header() with { Source = Unbound }));
        Assert.Throws<InvalidDataException>(() => packets.AppendContact(Contact(Unbound)));
        Assert.Throws<InvalidDataException>(() => packets.AppendContacts([Contact(Target, Unbound)]));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("target", Contact(Unbound))], Roster));
        NativeBattleEffectContactBridge<string> wrongSecondary = new([("target", Contact(Target, Target))], Roster);
        Assert.Throws<InvalidDataException>(() => wrongSecondary.Prepare(packets, 2, 5, 6, 7, Source));
        Assert.Equal(0, packets.ActionCount);
        Assert.Equal(0, packets.ContactCount);
    }

    [Fact]
    public void Malformed_raw_pointer_and_unwritten_live_extent_fail_at_their_respective_boundaries()
    {
        NativeBattleActionPacketsState packets = HostedPackets();
        (packets, _) = packets.Prepare(Header());
        (packets, _) = packets.Publish(1);
        Assert.Throws<NotSupportedException>(() => packets.ReadHeaderContacts(First));
        NativeBattleRetainedActionPackets retained = packets.ExportRetained();
        NativeBattleRetainedActionHeaderSlot header = retained.Headers[0];
        Assert.Throws<InvalidDataException>(() => (retained with { Headers = retained.Headers.SetItem(0, header with { FirstContactIndex = 1 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (retained with { Headers = retained.Headers.SetItem(0, header with { UnknownByteMask = header.UnknownByteMask | (1u << 8) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (retained with { ContactCount = -1 }).Validate());
    }

    [Fact]
    public void Batch_append_grows_beyond_original_storage_and_leaves_prior_packets_intact_on_failure()
    {
        NativeBattleActionPacketsState packets = HostedPackets().AppendContacts(Enumerable.Repeat(Contact(Target), 47));
        NativeBattleActionPacketsState grown = packets.AppendContacts(Enumerable.Repeat(Contact(Target), 4));
        Assert.Equal(51, grown.ContactCount);
        Assert.Equal(Contact(Target), grown.ReadContact(new BattleContactOccurrence(50)));
        Assert.Throws<InvalidDataException>(() => grown.AppendContacts([Contact(Target), Contact(Unbound), Contact(Target)]));
        Assert.Equal(51, grown.ContactCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => grown.ReadContact(new BattleContactOccurrence(51)));
        Assert.Equal(47, packets.ContactCount);
        Assert.Equal(Contact(Target), packets.ReadContact(new BattleContactOccurrence(46)));
    }

    private static NativeBattleActionPacketsState HostedPackets() => NativeBattleActionPacketsState.Create(Roster, ColdStorage(), new NativeBattleModelAliases(Roster, []));

    private static NativeBattleRetainedActionPackets ColdStorage() => NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]);

    private static NativeBattleActionHeaderPreparation Header() => new(Source, 2, 5, -1, 6, 7, null);

    private static NativeBattleContactPacket Contact(BattleModelOwner primary, BattleModelOwner? secondary = null)
    {
        NativeBattleContactHalf? reflection = secondary is null ? null : new(secondary, 0, 0, 0, 0, 0, 0);
        return new(new(primary, 5, 2, 0, 4, 6, 8), reflection);
    }
}
