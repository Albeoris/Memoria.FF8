using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedActionPacketsTests
{
    [Fact]
    public void Cold_and_fresh_physical_storage_have_distinct_knownness()
    {
        NativeBattleRetainedActionPackets fresh = NativeBattleRetainedActionPackets.Fresh();
        fresh.Validate();
        Assert.Equal(32, fresh.Headers.Length);
        Assert.Equal(49, fresh.Contacts.Length);
        Assert.All(fresh.Headers, header =>
        {
            Assert.Equal(0xFFFFFu, header.UnknownByteMask);
            Assert.Null(header.FirstContactIndex);
            Assert.Null(header.Data);
        });
        Assert.All(fresh.Contacts, Assert.Null);

        NativeBattleRetainedActionPackets cold = NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]);
        cold.Validate();
        Assert.All(cold.Headers, header =>
        {
            Assert.Equal(0u, header.UnknownByteMask);
            Assert.All(header.Payload, value => Assert.Equal((byte)0, value));
        });
        Assert.All(cold.Contacts, packet =>
        {
            Assert.NotNull(packet);
            Assert.Equal((byte)0, packet.Secondary.ActorIndex);
            Assert.Equal(new byte[24], NativeBattleContactPacketCodec.Encode(packet));
        });
        Assert.Throws<InvalidDataException>(() => NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[639], new byte[1176]));
        byte[] nonzero = new byte[1176];
        nonzero[^1] = 1;
        Assert.Throws<InvalidDataException>(() => NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], nonzero));
    }

    [Fact]
    public void Legacy_json_keeps_raw_half_fields_and_ignores_old_hosted_properties()
    {
        NativeBattleRetainedActionPackets fresh = NativeBattleRetainedActionPackets.Fresh();
        byte[] bytes = [0xF1, 3, 7, 0xC8, 8, 9, 10, 11, 12, 13, 14, 15, 0xFF, 21, 22, 0xA7, 24, 25, 26, 27, 28, 29, 30, 31];
        const string legacyPacket = """
            {
              "Primary":{"ActorIndex":241,"Sequence":3,"Flags2":7,"HitFlags3":200,"SavedStatuses4":2312,"Amount6":2826,"BattleStatuses8":252579084},
              "Secondary":{"ActorIndex":255,"Sequence":21,"Flags2":22,"HitFlags3":167,"SavedStatuses4":6424,"Amount6":6938,"BattleStatuses8":522067228}
            }
            """;
        JsonObject json = JsonSerializer.SerializeToNode(fresh)!.AsObject();
        json["Contacts"]![0] = JsonNode.Parse(legacyPacket);
        json["Headers"]![0]!["PresentationContactCount"] = 300;
        json["PresentationRoster"] = JsonNode.Parse("""{"Rows":[null]}""");
        NativeBattleRetainedActionPackets imported = JsonSerializer.Deserialize<NativeBattleRetainedActionPackets>(json.ToJsonString())!;
        imported.Validate();
        Assert.Equal(bytes, NativeBattleContactPacketCodec.Encode(imported.Contacts[0]!));

        JsonObject exported = JsonSerializer.SerializeToNode(imported)!.AsObject();
        Assert.Equal(6, exported.Count);
        Assert.False(exported.ContainsKey("PresentationRoster"));
        Assert.Null(exported["Headers"]![0]!["PresentationContactCount"]);
        Assert.Equal((byte)0xFF, exported["Contacts"]![0]!["Secondary"]!["ActorIndex"]!.GetValue<byte>());
        Assert.Equal(bytes, NativeBattleContactPacketCodec.Encode(JsonSerializer.Deserialize<NativeBattleRetainedActionPackets>(exported.ToJsonString())!.Contacts[0]!));
    }

    [Fact]
    public void Resetting_counts_retains_pointer_payload_knownness_and_active_cursors()
    {
        var data = new NativeBattleActionDataReference(0x47E970, 7, "Retained text");
        NativeBattleRetainedActionHeaderSlot header = PreparedHeader(49, data);
        NativeBattleRetainedActionPackets fresh = NativeBattleRetainedActionPackets.Fresh();
        NativeBattleRetainedActionPackets retained = fresh with
        {
            ActionCount = 1,
            ContactCount = 1,
            UnknownCountMask = 3,
            Headers = fresh.Headers.SetItem(0, header),
            Contacts = fresh.Contacts.SetItem(0, NativeBattleContactPacketCodec.Decode(new byte[24])),
            ActiveHeaderIndex = 0,
            ActiveContactIndex = 49
        };

        retained.Validate();
        NativeBattleRetainedActionPackets reset = retained.ResetCounts();
        reset.Validate();
        Assert.Equal((byte)0, reset.ActionCount);
        Assert.Equal(0, reset.ContactCount);
        Assert.Equal((byte)0, reset.UnknownCountMask);
        Assert.Equal(0, reset.ActiveHeaderIndex);
        Assert.Equal(49, reset.ActiveContactIndex);
        Assert.Equal(retained.Headers, reset.Headers);
        Assert.Equal(retained.Contacts, reset.Contacts);
        Assert.Equal(data, reset.Headers[0].Data);
        Assert.Equal((byte)0xEF, reset.Headers[0].Payload[12]);
    }

    [Fact]
    public void Validation_rejects_invalid_extents_missing_physical_data_and_inconsistent_pointer_knownness()
    {
        NativeBattleRetainedActionPackets fresh = NativeBattleRetainedActionPackets.Fresh();
        NativeBattleRetainedActionHeaderSlot prepared = PreparedHeader(0, null);
        NativeBattleRetainedActionPackets valid = fresh with { Headers = fresh.Headers.SetItem(0, prepared), ActionCount = 1 };
        valid.Validate();
        Assert.Throws<InvalidDataException>(() => (fresh with { Headers = fresh.Headers.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (fresh with { Contacts = fresh.Contacts.Add(null) }).Validate());
        Assert.Throws<InvalidDataException>(() => (fresh with { ActiveContactIndex = 50 }).Validate());
        Assert.Throws<InvalidDataException>(() => (fresh with { ActiveHeaderIndex = 32 }).Validate());
        Assert.Throws<InvalidDataException>(() => (fresh with { ContactCount = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (fresh with { ContactCount = 256, UnknownCountMask = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (fresh with { UnknownCountMask = 4 }).Validate());
        (fresh with { ActionCount = 1, ContactCount = 1, UnknownCountMask = 2 }).Validate();
        Reject(prepared with { Payload = prepared.Payload.RemoveAt(0) });
        Reject(prepared with { FirstContactIndex = 50 });
        Reject(prepared with { UnknownByteMask = 1u << 20 });
        (valid with { Headers = valid.Headers.SetItem(0, prepared with { UnknownByteMask = (1u << 16) | 1u }) }).Validate();
        Reject(prepared with { UnknownByteMask = 1u << 8 });
        Reject(prepared with { Payload = prepared.Payload.SetItem(8, 0) });
        Reject(prepared with { Payload = prepared.Payload.SetItem(12, 1) });
        Reject(prepared with { Data = new(0x47E970, 7) });
        Assert.Throws<InvalidDataException>(() => (valid with { Contacts = valid.Contacts.SetItem(0, new(null!, null!)) }).Validate());

        void Reject(NativeBattleRetainedActionHeaderSlot header)
        {
            Assert.Throws<InvalidDataException>(() => (valid with { Headers = valid.Headers.SetItem(0, header) }).Validate());
        }
    }

    private static NativeBattleRetainedActionHeaderSlot PreparedHeader(int firstIndex, NativeBattleActionDataReference? data)
    {
        byte[] payload = new byte[20];
        payload[0] = 0xF1;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), 0x1D28344u + 24u * (uint)firstIndex);
        if (data is not null)
        {
            payload[12] = 0xEF;
        }
        return new(ImmutableArray.CreateRange(payload), data is null ? 0u : 0xF000u, firstIndex, data);
    }
}
