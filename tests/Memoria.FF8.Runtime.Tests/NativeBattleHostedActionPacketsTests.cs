using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionPacketsTests
{
    [Fact]
    public void Native_bridge_preserves_packet_bytes_and_legacy_json()
    {
        NativeBattleContactPacket contact = Contact(3);
        NativeBattleEffectContactBridge<string> bridge = new([("target", contact)]);
        (NativeBattleActionPacketsState packets, ushort targetMask) = bridge.Prepare(2, 5, 6, 7);
        Assert.Equal((ushort)8, targetMask);
        Assert.Equal(new byte[] { 0, 2, 5, 255, 6, 0, 7, 0, 0x44, 0x83, 0xD2, 1, 0, 0, 0, 0, 1, 0, 0, 0 }, packets.Headers[0].Payload.ToArray());
        Assert.Equal(contact.Encode(), packets.ReadContactBytes(0));
        Assert.Equal(new byte[24], packets.ReadContactBytes(48));
        string json = JsonSerializer.Serialize(packets);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattleActionPacketsState.PresentationRoster), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattleActionPacketsState.StorageCapacity), out _));
        Assert.False(document.RootElement.GetProperty("Headers")[0].TryGetProperty(nameof(NativeBattleActionHeaderSlot.PresentationContactCount), out _));
        NativeBattleActionPacketsState restored = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidOperationException>(() => bridge.PrepareHosted(2, 5, 6, 7));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(300)]
    public void Hosted_bridge_preserves_identical_occurrences_and_full_header_counts(int count)
    {
        NativeBattleGraphicsRoster roster = Roster();
        NativeBattleContactPacket contact = Contact(24, 7);
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("target", contact), count), roster);
        (NativeBattleActionPacketsState packets, NativeBattleGraphicsActorSet targets) = bridge.PrepareHosted(2, 5, 6, 7, source: 7);
        packets.Validate();
        Assert.True(targets.IsSingle(24));
        Assert.Equal((ushort)0, targets.NativePhysicalBits);
        Assert.Equal(count, packets.StorageCapacity);
        Assert.Equal(count, packets.ContactCount);
        Assert.Equal(count, packets.Headers[0].ReadContactCount());
        Assert.Equal(unchecked((byte)count), packets.Headers[0].Payload[16]);
        if (count > byte.MaxValue)
        {
            Assert.Throws<NotSupportedException>(() => packets.Headers[0].ReadByte(16));
        }
        else
        {
            Assert.Equal((byte)count, packets.Headers[0].ReadByte(16));
        }
        Assert.Equal((48, "target"), bridge.ResolveOccurrence(packets.ReadContact(48)));
        Assert.Equal((49, "target"), bridge.ResolveOccurrence(packets.ReadContact(49)));
        Assert.Equal((count - 1, "target"), bridge.ResolveOccurrence(packets.ReadHeaderContacts506BA0(0)[^1]));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(contact));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(contact with { PhysicalContactIndex = count }));
        Assert.Throws<InvalidOperationException>(() => bridge.Prepare(2, 5, 6, 7, source: 7));
    }

    [Fact]
    public void Hosted_cursor_and_following_header_survive_json_and_continue_at_extension_addresses()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(Roster(), 51);
        Assert.Throws<NotSupportedException>(() => packets.ReadContact(0));
        (packets, _) = packets.Prepare(Header());
        packets = packets.AppendContacts(Enumerable.Repeat(Contact(24), 49));
        (packets, _) = packets.Publish(49);
        (packets, _) = packets.Prepare(Header());
        Assert.Equal(NativeBattleContactAddress.Encode(49), Pointer(packets.Headers[1]));
        Assert.Null(packets.Headers[1].PresentationContactCount);
        (packets, _) = packets.AppendContact(Contact(24));
        (packets, _) = packets.Publish(1);
        packets = packets.ActivateHeader(1);

        string json = JsonSerializer.Serialize(packets);
        NativeBattleActionPacketsState restored = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(49, restored.ReadActiveContact().PhysicalContactIndex);
        restored = restored.AdvanceContact50A690();
        (restored, int added) = restored.AppendContact(Contact(24));
        Assert.Equal(50, added);
        Assert.Equal(50, restored.ReadActiveContact().PhysicalContactIndex);
        restored = restored.AdvanceContact50A690();
        (restored, _) = restored.Prepare(Header());
        Assert.Equal(NativeBattleContactAddress.EncodeCursor(51, 51), Pointer(restored.Headers[2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.ReadActiveContact());
        Assert.Throws<NotSupportedException>(() => restored.AdvanceContact50A690());
        Assert.Throws<InvalidOperationException>(() => restored.AppendContact(Contact(24)));
    }

    [Fact]
    public void Reset_retains_hosted_header_counts_and_reprepare_clears_only_typed_count()
    {
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("target", Contact(24)), 300), Roster());
        (NativeBattleActionPacketsState packets, _) = bridge.PrepareHosted(2, 5, 6, 7, source: 7);
        packets = packets.WritePlayerRepeatMode(0, 3).ActivateHeader(0).ResetCounts();
        packets.Validate();
        Assert.Equal(0, packets.ContactCount);
        Assert.Equal(300, packets.Headers[0].ReadContactCount());
        Assert.Equal(300, packets.ReadHeaderContacts506BA0(0).Length);
        (packets, _) = packets.Prepare(Header());
        Assert.Null(packets.Headers[0].PresentationContactCount);
        Assert.Equal((byte)2, packets.Headers[0].ReadByte(17));
        (packets, _) = packets.AppendContact(Contact(24));
        (packets, _) = packets.Publish(1);
        packets.Validate();
        Assert.Equal(1, packets.Headers[0].ReadContactCount());
        Assert.Equal((byte)2, packets.Headers[0].ReadByte(17));
    }

    [Fact]
    public void Hosted_packet_and_bank_reject_unbound_or_out_of_range_actor_bytes()
    {
        NativeBattleContactPacket contact = Contact(24, 7);
        Assert.Throws<InvalidDataException>(() => contact.Encode());
        byte[] bytes = contact.Encode(actorCount: 25);
        Assert.Equal((byte)24, bytes[0]);
        Assert.Equal((byte)7, bytes[12]);
        Assert.Equal(contact, NativeBattleContactPacket.Decode(bytes, actorCount: 25));
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacket.Decode(bytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => contact.Validate(actorCount: 26));

        NativeBattleGraphicsRoster roster = Roster();
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(roster);
        Assert.Throws<InvalidDataException>(() => packets.Prepare(Header() with { Actor = 6 }));
        Assert.Throws<InvalidDataException>(() => packets.AppendContact(Contact(6)));
        Assert.Throws<InvalidDataException>(() => packets.AppendContacts([Contact(24, 6)]));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("target", Contact(6))], roster));
        NativeBattleEffectContactBridge<string> wrongSecondary = new([("target", Contact(24, 24))], roster);
        Assert.Throws<InvalidDataException>(() => wrongSecondary.PrepareHosted(2, 5, 6, 7, source: 7));
    }

    [Fact]
    public void Malformed_hosted_counts_roster_and_raw_pointer_are_rejected()
    {
        NativeBattleEffectContactBridge<string> bridge = new([("target", Contact(24))], Roster());
        (NativeBattleActionPacketsState packets, _) = bridge.PrepareHosted(2, 5, 6, 7, source: 7);
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        AssertInvalidHeader(header with { PresentationContactCount = null });
        AssertInvalidHeader(header with { PresentationContactCount = -1 });
        AssertInvalidHeader(header with { PresentationContactCount = 2 });
        AssertInvalidHeader(header with { PresentationContactCount = 2, Payload = header.Payload.SetItem(16, 2) });
        AssertInvalidHeader(header with { FirstContactIndex = null });
        AssertInvalidHeader(header with { UnknownByteMask = 1u << 16 });
        AssertInvalidHeader(header with { Payload = header.Payload.SetItem(8, 0) });
        AssertInvalidHeader(header with { Payload = header.Payload.SetItem(0, 6) });
        Assert.Throws<InvalidDataException>((packets with { PresentationRoster = null }).Validate);
        Assert.Throws<InvalidDataException>((packets with { ContactCount = -1 }).Validate);
        Assert.Throws<InvalidDataException>((packets with { Contacts = packets.Contacts.SetItem(5, Contact(6)) }).Validate);

        NativeBattleActionPacketsState empty = NativeBattleActionPacketsState.FromPresentationStorage(Roster());
        (empty, _) = empty.Prepare(Header());
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.Publish(1));

        void AssertInvalidHeader(NativeBattleActionHeaderSlot invalid)
        {
            Assert.Throws<InvalidDataException>((packets with { Headers = packets.Headers.SetItem(0, invalid) }).Validate);
        }
    }

    [Fact]
    public void Batch_append_stops_at_one_occurrence_beyond_remaining_capacity()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(Roster());
        packets = packets.AppendContacts(Enumerable.Repeat(Contact(24), 47));
        int yielded = 0;
        Assert.Throws<InvalidOperationException>(() => packets.AppendContacts(OverCapacity()));
        Assert.Equal(3, yielded);
        Assert.Equal(47, packets.ContactCount);
        Assert.Null(packets.Contacts[47]);

        IEnumerable<NativeBattleContactPacket> OverCapacity()
        {
            while (yielded < 3)
            {
                yielded++;
                yield return Contact(24);
            }
            throw new InvalidOperationException("Enumerator advanced past the overflow occurrence.");
        }
    }

    private static NativeBattleGraphicsRoster Roster()
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[25];
        rows[7] = new("source", true);
        rows[24] = new("target", false);
        return new(rows.ToImmutableArray());
    }

    private static uint Pointer(NativeBattleActionHeaderSlot header) => BinaryPrimitives.ReadUInt32LittleEndian(header.Payload.AsSpan()[8..]);

    private static NativeBattleActionHeaderPreparation Header() => new(7, 2, 5, -1, 6, 7, null);

    private static NativeBattleContactPacket Contact(byte primary, byte secondary = 255) => new(new(primary, 5, 2, 0, 4, 6, 8), new(secondary, 0, 0, 0, 0, 0, 0));
}
