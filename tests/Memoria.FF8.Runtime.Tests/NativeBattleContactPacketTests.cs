using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactPacketTests
{
    [Theory]
    [InlineData("0305020434127856EFCDAB800116AC80EFBEFFFF76543210")]
    [InlineData("0305020434127856EFCDAB80FF16AC80EFBEFFFF76543210")]
    [InlineData("000000000000000000000000000000000000000000000000")]
    public void OriginalRawLayoutRoundTripsEveryByte(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        NativeBattleRawContactPacket raw = NativeBattleContactPacketCodec.Decode(bytes);

        Assert.Equal(bytes[12], raw.Secondary.ActorIndex);
        Assert.Equal(bytes[15], raw.Secondary.HitFlags3);
        Assert.Equal(bytes, NativeBattleContactPacketCodec.Encode(raw));
    }

    [Fact]
    public void RawJsonReadsOriginalPropertyNamesAndExtendedDamage()
    {
        const string json = """
            {"Primary":{"ActorIndex":3,"Sequence":5,"Flags2":2,"HitFlags3":4,"SavedStatuses4":4660,"Amount6":60000,"BattleStatuses8":2166574575,"ExtendedDamageAmount":60000,"PresentedAmount":60000,"ContactFlags":1026},"Secondary":{"ActorIndex":255,"Sequence":22,"Flags2":172,"HitFlags3":128,"SavedStatuses4":48879,"Amount6":65535,"BattleStatuses8":271733878,"ExtendedDamageAmount":null}}
            """;
        NativeBattleRawContactPacket raw = JsonSerializer.Deserialize<NativeBattleRawContactPacket>(json)!;

        Assert.Equal((byte)3, raw.Primary.ActorIndex);
        Assert.Equal(60000, raw.Primary.ExtendedDamageAmount);
        Assert.Equal((byte)0x80, raw.Secondary.HitFlags3);
        Assert.Equal(raw, JsonSerializer.Deserialize<NativeBattleRawContactPacket>(JsonSerializer.Serialize(raw)));
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacketCodec.Decode(new byte[23]));
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacketCodec.Encode(raw with { Primary = raw.Primary with { Amount6 = 3 } }));
    }

    [Fact]
    public void OriginalBindingDecodesCoverAndReencodesExactSelectedOwners()
    {
        var (bindings, models, recipient, cover) = OriginalOwners();
        var raw = NativeBattleContactPacketCodec.Decode(Convert.FromHexString("0316028834127856EFCDAB800216AC01EFBEFFFF76543210"));

        NativeBattleContactPacket live = NativeBattleContactPacketCodec.DecodeOriginal(raw, bindings, models);

        Assert.Equal(recipient, live.Primary.Recipient);
        Assert.Equal(cover, live.Primary.CoverSource);
        Assert.Equal((byte)8, live.Primary.HitFlags);
        Assert.Equal((ushort)0x0802, live.Primary.ContactFlags);
        Assert.NotNull(live.Secondary);
        Assert.Equal(cover, live.Secondary.Recipient);
        Assert.True(NativeBattleContactPacketCodec.TryEncodeOriginal(live, bindings, models, out NativeBattleRawContactPacket? encoded));
        Assert.Equal(raw, encoded);
    }

    [Fact]
    public void AbsentSecondaryAndUnflaggedCoverBitsAreOpaqueInRetainedBytes()
    {
        var (bindings, models, _, _) = OriginalOwners();
        byte[] bytes = Convert.FromHexString("0305028434127856EFCDAB80FF16AC80EFBEFFFF76543210");
        NativeBattleRawContactPacket raw = NativeBattleContactPacketCodec.Decode(bytes);

        NativeBattleContactPacket live = NativeBattleContactPacketCodec.DecodeOriginal(raw, bindings, models);

        Assert.Null(live.Secondary);
        Assert.Null(live.Primary.CoverSource);
        Assert.Equal((byte)4, live.Primary.HitFlags);
        Assert.True(NativeBattleContactPacketCodec.TryEncodeOriginal(live, bindings, models, out NativeBattleRawContactPacket? encoded));
        Assert.NotNull(encoded);
        Assert.Equal(raw.Primary with { HitFlags3 = 4 }, encoded.Primary);
        Assert.Equal(new NativeBattleRawContactHalf(255, 0, 0, 0, 0, 0, 0), encoded.Secondary);
        Assert.Equal(bytes, NativeBattleContactPacketCodec.Encode(raw));
    }

    [Fact]
    public void MissingOriginalAliasesAndOutOfRangeCoverCannotPublishRawOutput()
    {
        var (bindings, models, recipient, _) = OriginalOwners();
        var extra = new BattlePartyActorModel(new("extra"));
        var distant = new BattleEnemyActorModel(new("distant"));
        (bindings, models) = bindings.BindOriginalSlot(NativeBattleModelSlot.Decode(4), distant, models);
        models = models.Add(extra, new NativeBattleGraphicsModel(0));
        NativeBattleContactHalf primary = new(recipient, 0, 0, 0, 0, 0, 0);

        AssertUnencodable(new(primary with { Recipient = extra }, null));
        AssertUnencodable(new(primary, primary with { Recipient = extra }));
        AssertUnencodable(new(primary with { HitFlags = 8, CoverSource = extra }, null));
        AssertUnencodable(new(primary with { HitFlags = 8, CoverSource = distant }, null));

        void AssertUnencodable(NativeBattleContactPacket packet)
        {
            Assert.False(NativeBattleContactPacketCodec.TryEncodeOriginal(packet, bindings, models, out NativeBattleRawContactPacket? raw));
            Assert.Null(raw);
        }
    }

    [Fact]
    public void ReboundOriginalAliasCannotEncodeTheRetiredRecipient()
    {
        var (bindings, models, retired, _) = OriginalOwners();
        NativeBattleContactPacket original = new(new(retired, 0, 0, 0, 0, 0, 0), null);
        Assert.True(NativeBattleContactPacketCodec.TryEncodeOriginal(original, bindings, models, out NativeBattleRawContactPacket? bytes));
        var replacement = new BattleEnemyActorModel(new("new allocation"));
        var slot = NativeBattleModelSlot.Decode(3);
        var (vacant, remaining) = bindings.UnbindOriginalSlot(slot, models);

        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacketCodec.DecodeOriginal(bytes!, vacant, remaining));
        (bindings, models) = vacant.BindOriginalSlot(slot, replacement, remaining);
        Assert.False(NativeBattleContactPacketCodec.TryEncodeOriginal(original, bindings, models, out NativeBattleRawContactPacket? output));
        Assert.Null(output);
        Assert.Equal(replacement, NativeBattleContactPacketCodec.DecodeOriginal(bytes!, bindings, models).Primary.Recipient);
    }

    [Fact]
    public void LiveEqualityIgnoresOccurrenceAndValidationChecksTypedCover()
    {
        var (_, _, recipient, cover) = OriginalOwners();
        NativeBattleContactHalf primary = new(recipient, 5, 2, 8, 0, 60000, 0) { CoverSource = cover, ExtendedDamageAmount = 60000 };
        NativeBattleContactPacket packet = new(primary, null) { Occurrence = new BattleContactOccurrence(3) };
        NativeBattleContactPacket repeated = packet with { Occurrence = new BattleContactOccurrence(300) };

        packet.Validate();
        Assert.Equal(60000, packet.Primary.PresentedAmount);
        Assert.Equal(packet, repeated);
        Assert.Equal(packet.GetHashCode(), repeated.GetHashCode());
        Assert.Throws<InvalidDataException>(() => (packet with { Primary = primary with { HitFlags = 0x88 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (packet with { Primary = primary with { CoverSource = null } }).Validate());
        Assert.Throws<InvalidDataException>(() => (packet with { Primary = primary with { HitFlags = 0 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (packet with { Primary = primary with { HitFlags = 9 } }).Validate());
    }

    [Theory]
    [InlineData(-1, 65535)]
    [InlineData(65536, 0)]
    [InlineData(60000, 60000)]
    public void ScratchDamagePublishesOnlyItsLowWord(int dword, int expected)
    {
        var recipient = new BattleEnemyActorModel(new("target"));
        NativeBattleContactHalf half = NativeBattleContactHalf.FromScratch(recipient, 5, 0, 0, 0, dword, 0);

        Assert.Equal((ushort)expected, half.Amount6);
    }

    [Theory]
    [InlineData(131072, "131072")]
    [InlineData(int.MaxValue, "2147483647")]
    public void WidePositiveDamageRetainsItsFullAmountForPresentation(int damage, string text)
    {
        var (bindings, models, recipient, _) = OriginalOwners();
        NativeBattleContactHalf half = NativeBattleContactHalf.FromScratch(recipient, 5, 0, 0, 0, damage, 0) with { ExtendedDamageAmount = damage };
        NativeBattleContactPacket packet = new(half, null);

        Assert.True(NativeBattleContactPacketCodec.TryEncodeOriginal(packet, bindings, models, out NativeBattleRawContactPacket? raw));
        Assert.NotNull(raw);
        Assert.Equal(unchecked((ushort)damage), NativeBattleContactPacketCodec.Decode(NativeBattleContactPacketCodec.Encode(raw)).Primary.Amount6);
        NativeBattleContactHalf restored = NativeBattleContactPacketCodec.DecodeOriginal(raw, bindings, models).Primary;
        Assert.Equal(damage, restored.PresentedAmount);
        NativeBattleEffectsState effects = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).PrependDamageText(restored.Recipient, restored.ContactFlags, restored.PresentedAmount, models.Roster);
        NativeBattleEffectsStep step = effects.Advance(models, _ => new(160, 100));
        Assert.Equal(text.Select(digit => (byte)(56 + digit - '0')).Append((byte)0), Assert.Single(step.State.TextDraws).Glyphs);
    }

    private static (NativeBattleModelBoundary Bindings, BattleActorModels Models, BattleModelOwner Recipient, BattleModelOwner Cover) OriginalOwners()
    {
        var bindings = new NativeBattleModelBoundary(Enumerable.Range(0, 7).Select(_ => new NativeBattleGraphicsModel(0)));
        BattleActorModels models = new([]);
        var recipient = new BattleEnemyActorModel(new("recipient"));
        var cover = new BattlePartyActorModel(new("cover"));
        (bindings, models) = bindings.BindOriginalSlot(NativeBattleModelSlot.Decode(3), recipient, models);
        (bindings, models) = bindings.BindOriginalSlot(NativeBattleModelSlot.Decode(2), cover, models);
        return (bindings, models, recipient, cover);
    }
}
