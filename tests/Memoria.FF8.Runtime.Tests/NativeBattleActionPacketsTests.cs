using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionPacketsTests
{
    private static readonly BattlePartyActorModel Source = new(new("source"));
    private static readonly BattleEnemyActorModel Target = new(new("target"));
    private static readonly BattleEnemyActorModel Extra = new(new("extra"));
    private static readonly BattlePartyActorModel ExtraCover = new(new("extra-cover"));
    private static readonly BattleActionHeaderReference First = new(0);

    [Fact]
    public void Original_writes_match_native_header_contact_bytes_and_retained_knownness()
    {
        var (bindings, models) = OriginalBindings();
        NativeBattleRetainedActionPackets cold = Cold();
        NativeBattleRetainedActionHeaderSlot raw = cold.Headers[0] with { Payload = cold.Headers[0].Payload.SetItem(16, 7).SetItem(17, 4).SetItem(18, 0xA3).SetItem(19, 0xB4) };
        cold = cold with { Headers = cold.Headers.SetItem(0, raw) };
        NativeBattleActionPacketsState arena = NewArena(cold);
        Assert.Throws<InvalidDataException>(() => arena.ReadHeader(First));
        Assert.Throws<NotSupportedException>(() => arena.ReadContact(new BattleContactOccurrence(0)));

        (arena, var header) = arena.Prepare(Preparation() with { Data = new(0x47E970, 6) });
        Assert.Equal(0, arena.ActionCount);
        Assert.Equal(7, arena.ReadHeader(header).ContactCount);
        Assert.Equal((byte)4, arena.ReadHeader(header).RepeatModeMinusOne);
        NativeBattleContactPacket contact = Contact() with { Primary = Contact().Primary with { HitFlags = 8, CoverSource = Source } };
        (arena, _) = arena.AppendContact(contact);
        (arena, _) = arena.Publish(1);
        arena = arena.WritePlayerRepeatMode(header, 0).ActivateHeader(header);
        NativeBattleRetainedActionPackets retained = arena.ExportRetained();

        Assert.Equal(new byte[] { 0, 1, 7, 0xFF, 0x65, 0, 0, 0, 0x44, 0x83, 0xD2, 0x01, 0, 0, 0, 0, 1, 0xFF, 0xA3, 0xB4 }, retained.Headers[0].Payload);
        Assert.Equal(0xF000u, retained.Headers[0].UnknownByteMask);
        Assert.Equal(new byte[] { 3, 4, 0, 8, 0, 0, 37, 0, 0, 0, 0, 0, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, NativeBattleContactPacketCodec.Encode(retained.Contacts[0]!));
        Assert.Equal(contact, NativeBattleContactPacketCodec.DecodeOriginal(retained.Contacts[0]!, bindings, models));
        Assert.Equal((byte)1, retained.ActionCount);
        Assert.Equal(1, retained.ContactCount);
        Assert.Equal(0, retained.ActiveHeaderIndex);
        Assert.Equal(0, retained.ActiveContactIndex);
        Assert.False(JsonSerializer.SerializeToElement(retained).TryGetProperty("UnknownCountMask", out _));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)3)]
    [InlineData(byte.MaxValue)]
    public void Sequence_cursor_reads_the_retained_actor_byte_without_inventing_another_live_contact(byte actor)
    {
        NativeBattleRetainedActionPackets retained = Cold();
        NativeBattleRawContactPacket second = retained.Contacts[1]!;
        second = second with { Primary = second.Primary with { ActorIndex = actor } };
        NativeBattleActionPacketsState arena = NewArena(retained with { Contacts = retained.Contacts.SetItem(1, second) });
        (arena, BattleActionHeaderReference header) = arena.Prepare(Preparation());
        (arena, _) = arena.AppendContact(Contact());
        (arena, _) = arena.Publish(1);
        arena = arena.ActivateHeader(header).AdvanceContact();

        Assert.Equal(new BattleContactCursor(1), arena.ActiveContact);
        Assert.Throws<NotSupportedException>(() => arena.ReadActiveContact());
        Assert.Null(arena.ReadSequenceContactTarget(BattleContactTaskStorageContract.ExpandableArena));
        BattleRetainedModelReference? physical = arena.ReadSequenceContactTarget(BattleContactTaskStorageContract.OriginalArena);
        if (actor == byte.MaxValue)
        {
            Assert.Null(physical);
        }
        else
        {
            Assert.Equal(new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(actor)), physical);
        }
    }

    [Fact]
    public void Duplicate_occurrences_and_reset_keep_the_selected_header_and_cursor()
    {
        NativeBattleActionPacketsState arena = NewArena();
        NativeBattleContactPacket contact = Contact();
        (arena, var first) = arena.Prepare(Preparation());
        arena = arena.AppendContacts([contact, contact]);
        (arena, _) = arena.Publish(2);
        (arena, var second) = arena.Prepare(Preparation() with { Command = 2 });
        (arena, _) = arena.AppendContact(contact);
        (arena, _) = arena.Publish(1);
        arena = arena.ActivateHeader(second).ResetCounts();
        arena.Validate();

        Assert.Equal(0, arena.ActionCount);
        Assert.Equal(0, arena.ContactCount);
        Assert.Equal(second, arena.ActiveHeader);
        Assert.Equal(new BattleContactCursor(2), arena.ActiveContact);
        Assert.Equal(new BattleContactOccurrence(2), arena.ReadActiveContact().Occurrence);
        NativeBattleContactPacket[] pair = arena.ReadHeaderContacts(first).ToArray();
        Assert.Equal(contact, pair[0]);
        Assert.Equal(pair[0], pair[1]);
        Assert.NotEqual(pair[0].Occurrence, pair[1].Occurrence);
        Assert.Null(contact.Occurrence);
        Assert.Equal(new BattleContactOccurrence(1), arena.ResolveContact(first, 1));
        (NativeBattleActionPacketsState next, _) = arena.Prepare(Preparation() with { Command = 4 });
        Assert.Equal(2, next.ReadHeader(first).ContactCount);
        Assert.Equal((byte)1, arena.ReadHeader(first).Command);
    }

    [Theory]
    [InlineData(33, 50, 0)]
    [InlineData(255, 255, 0)]
    [InlineData(256, 300, 3)]
    public void Raw_count_knownness_depends_on_scalar_width_not_original_window_size(int actionCount, int contactCount, byte unknownMask)
    {
        NativeBattleActionPacketsState arena = NewArena();
        for (int index = 1; index < actionCount; index++)
        {
            (arena, _) = arena.Prepare(Preparation());
            (arena, _) = arena.Publish(0);
        }
        (arena, var header) = arena.Prepare(Preparation() with { Source = Extra });
        arena = arena.AppendContacts(Enumerable.Repeat(Contact(), contactCount));
        (arena, _) = arena.Publish(contactCount);
        arena = arena.ActivateHeader(header);
        arena.Validate();

        Assert.Equal(actionCount, arena.ActionCount);
        Assert.Equal(contactCount, arena.ContactCount);
        Assert.Equal(contactCount, arena.ReadHeader(header).ContactCount);
        Assert.Equal(contactCount, arena.ReadHeaderContacts(header).Length);
        Assert.Equal(new BattleContactOccurrence(contactCount - 1), arena.ResolveContact(header, contactCount - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => arena.ResolveContact(header, contactCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => arena.ReadHeader(new(1024)));
        NativeBattleRetainedActionPackets retained = arena.ExportRetained();
        Assert.Equal(unknownMask, retained.UnknownCountMask);
        Assert.Equal((byte)Math.Min(actionCount, byte.MaxValue), retained.ActionCount);
        Assert.Equal(Math.Min(contactCount, byte.MaxValue), retained.ContactCount);
        Assert.Equal(32, retained.Headers.Length);
        Assert.Equal(49, retained.Contacts.Length);
        Assert.Null(retained.ActiveHeaderIndex);
        Assert.Equal(0, retained.ActiveContactIndex);
        NativeBattleRetainedActionPackets reset = arena.ResetCounts().ExportRetained();
        Assert.Equal(retained.ResetCounts(), reset);
    }

    [Fact]
    public void Unknown_or_unwritten_reached_fields_fail_without_reinterpreting_retained_bytes()
    {
        NativeBattleActionPacketsState arena = NewArena();
        (arena, var header) = arena.Prepare(Preparation());
        Assert.Throws<NotSupportedException>(() => arena.ReadHeaderContacts(header));
        Assert.Throws<NotSupportedException>(() => arena.ActivateHeader(header));
        Assert.Throws<ArgumentOutOfRangeException>(() => arena.Publish(-1));
        (arena, _) = arena.Publish(3);
        Assert.Throws<NotSupportedException>(() => arena.ReadHeaderContacts(header));
        Assert.Throws<NotSupportedException>(() => arena.ReadContact(arena.ResolveContact(header, 1)));
        (NativeBattleActionPacketsState outOfRange, _) = arena.Prepare(Preparation());
        (outOfRange, var unavailable) = outOfRange.Publish(1000);
        Assert.Throws<InvalidDataException>(() => outOfRange.ReadHeaderContacts(unavailable));
        Assert.Throws<InvalidDataException>(() => outOfRange.ResolveContact(unavailable, 0));

        NativeBattleActionPacketsState filled = NewArena();
        filled = filled.AppendContacts(Enumerable.Repeat(Contact(), 49));
        (filled, var empty) = filled.Prepare(Preparation());
        (filled, _) = filled.Publish(0);
        filled = filled.ActivateHeader(empty);
        Assert.Empty(filled.ReadHeaderContacts(empty));
        Assert.Throws<NotSupportedException>(() => filled.AdvanceContact());
        Assert.Throws<ArgumentOutOfRangeException>(() => filled.ReadActiveContact());
    }

    [Fact]
    public void Roster_replacement_preserves_retired_facts_and_rejects_them_only_when_reached()
    {
        NativeBattleActionPacketsState arena = NewArena();
        (arena, var header) = arena.Prepare(Preparation() with { Source = Extra });
        (arena, var occurrence) = arena.AppendContact(Contact());
        (arena, _) = arena.Publish(1);
        arena = arena.ActivateHeader(header);
        BattleEnemyActorModel replacement = new(new("target-allocation-2"));
        BattleModelRoster replacementRoster = new([Source, replacement]);
        NativeBattleModelAliases aliases = new(replacementRoster, [(NativeBattleModelSlot.Decode(0), Source), (NativeBattleModelSlot.Decode(3), replacement)]);
        NativeBattleActionPacketsState rebound = arena.WithRoster(replacementRoster, aliases);
        rebound.Validate();

        Assert.Equal(arena.ActiveHeader, rebound.ActiveHeader);
        Assert.Equal(arena.ActiveContact, rebound.ActiveContact);
        Assert.Throws<InvalidDataException>(() => rebound.ReadHeader(header));
        Assert.Throws<InvalidDataException>(() => rebound.ReadContact(occurrence));
        Assert.Throws<InvalidDataException>(() => rebound.AppendContact(Contact()));
        Assert.Throws<InvalidDataException>(() => rebound.Prepare(Preparation() with { Source = Extra }));
        Assert.Throws<InvalidDataException>(() => rebound.AppendContact(new(new(replacement, 4, 0, 8, 0, 37, 0) { CoverSource = ExtraCover }, null)));
        Assert.Equal(Target, arena.ReadContact(occurrence).Primary.Recipient);

        NativeBattleRetainedActionPackets retained = rebound.ExportRetained();
        Assert.Same(arena.ExportRetained(), retained);
        NativeBattleRawContactPacket raw = retained.Contacts[0]!;
        Assert.Equal((byte)3, raw.Primary.ActorIndex);
        Assert.Equal((byte)0, raw.UnknownBitsAt(0));
        var (bindings, models) = OriginalBindings();
        (bindings, models) = bindings.BindOriginalSlot(NativeBattleModelSlot.Decode(3), replacement, models);
        Assert.Equal(replacement, NativeBattleContactPacketCodec.DecodeOriginal(raw, bindings, models).Primary.Recipient);

        NativeBattleActionPacketsState next = rebound.AppendContact(new(new(replacement, 4, 0, 0, 0, 37, 0), null)).State;
        Assert.Equal((byte)3, next.ExportRetained().Contacts[1]!.Primary.ActorIndex);
        Assert.Equal(replacement, next.ReadContact(new BattleContactOccurrence(1)).Primary.Recipient);
    }

    [Fact]
    public void Unencodable_fields_preserve_previous_bytes_and_mark_only_fields_without_native_encodings()
    {
        var (bindings, models) = OriginalBindings();
        NativeBattleRetainedActionPackets cold = Cold();
        byte[] previousContact = new byte[24];
        previousContact[0] = 5;
        previousContact[3] = 0xC2;
        NativeBattleRetainedActionHeaderSlot previousHeader = cold.Headers[0] with { Payload = cold.Headers[0].Payload.SetItem(0, 6).SetItem(8, 0xF3).SetItem(16, 7) };
        cold = cold with { Headers = cold.Headers.SetItem(0, previousHeader), Contacts = cold.Contacts.SetItem(0, NativeBattleContactPacketCodec.Decode(previousContact)) };
        NativeBattleActionPacketsState arena = NewArena(cold);
        NativeBattleContactPacket contact = new(new(Extra, 4, 0, 8, 0, 37, 0) { CoverSource = ExtraCover }, null);
        (arena, _) = arena.AppendContact(contact);
        arena = arena.AppendContacts(Enumerable.Repeat(Contact(), 49));
        (arena, var header) = arena.Prepare(Preparation() with { Source = Extra, Data = new(0x47E970, 6) });
        (arena, _) = arena.Publish(300);
        arena = arena.ActivateHeader(header);
        NativeBattleRetainedActionPackets retained = arena.ExportRetained();

        Assert.Equal((byte)0, retained.UnknownCountMask);
        Assert.Equal(50, retained.ContactCount);
        Assert.Null(retained.ActiveContactIndex);
        NativeBattleRetainedActionHeaderSlot rawHeader = retained.Headers[0];
        Assert.Null(rawHeader.FirstContactIndex);
        Assert.NotNull(rawHeader.Data);
        Assert.Equal(0x1FF01u, rawHeader.UnknownByteMask);
        Assert.Equal((byte)6, rawHeader.Payload[0]);
        Assert.Equal((byte)0xF3, rawHeader.Payload[8]);
        Assert.Equal((byte)7, rawHeader.Payload[16]);
        NativeBattleRawContactPacket raw = retained.Contacts[0]!;
        Assert.Equal(24, raw.UnknownBits.Length);
        Assert.Equal((byte)0xFF, raw.UnknownBits[0]);
        Assert.Equal((byte)0xC0, raw.UnknownBits[3]);
        Assert.Equal(2, raw.UnknownBits.Count(bits => bits != 0));
        Assert.Equal((byte)5, raw.Primary.ActorIndex);
        Assert.Equal((byte)0xC8, raw.Primary.HitFlags3);
        Assert.Equal((ushort)37, raw.Primary.Amount6);
        Assert.Equal(byte.MaxValue, raw.Secondary.ActorIndex);
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacketCodec.DecodeOriginal(raw, bindings, models));
        string json = JsonSerializer.Serialize(retained);
        Assert.DoesNotContain("extra-cover", json);
        NativeBattleRetainedActionPackets restored = JsonSerializer.Deserialize<NativeBattleRetainedActionPackets>(json)!;
        restored.Validate();
        Assert.Equal(raw.UnknownBits.ToArray(), restored.Contacts[0]!.UnknownBits.ToArray());
        Assert.Equal(NativeBattleContactPacketCodec.Encode(raw), NativeBattleContactPacketCodec.Encode(restored.Contacts[0]!));
    }

    [Fact]
    public void Camera_flag_reads_known_bits_without_an_original_actor_or_cover_binding()
    {
        NativeBattleRetainedActionPackets retained = Cold();
        byte[] unknown = new byte[24];
        unknown[0] = 0xFF;
        unknown[3] = 0xC0;
        NativeBattleRawContactPacket raw = retained.Contacts[0]! with
        {
            Primary = retained.Contacts[0]!.Primary with { ActorIndex = 0xF3, HitFlags3 = 0xCA },
            UnknownBits = ImmutableArray.CreateRange(unknown)
        };
        retained = retained with { Contacts = retained.Contacts.SetItem(0, raw) };
        NativeBattleActionPacketsState arena = NewArena(retained).Prepare(Preparation()).State.Publish(0).State.ActivateHeader(First);

        Assert.Equal((short)2, NativeBattleActionPacketBoundary.ReadActiveCameraFlag50A6E0(arena));
        Assert.Equal((short)0, NativeBattleActionPacketBoundary.ReadActiveCameraFlag50A6E0(arena.AppendContact(Contact()).State));
        NativeBattleContactPacket covered = new(new(Extra, 0, 0, 10, 0, 0, 0) { CoverSource = ExtraCover }, null);
        NativeBattleRetainedActionPackets written = arena.AppendContact(covered).State.ExportRetained();
        Assert.Equal((byte)0xC0, written.Contacts[0]!.UnknownBitsAt(3));
        NativeBattleActionPacketsState imported = NewArena(written).Prepare(Preparation()).State.Publish(0).State.ActivateHeader(First);
        Assert.Equal((short)2, NativeBattleActionPacketBoundary.ReadActiveCameraFlag50A6E0(imported));
        unknown[3] |= 2;
        NativeBattleRawContactPacket unavailable = raw with { UnknownBits = ImmutableArray.CreateRange(unknown) };
        retained = retained with { Contacts = retained.Contacts.SetItem(0, unavailable) };
        NativeBattleActionPacketsState unknownArena = NewArena(retained).Prepare(Preparation()).State.Publish(0).State.ActivateHeader(First);
        Assert.Throws<NotSupportedException>(() => NativeBattleActionPacketBoundary.ReadActiveCameraFlag50A6E0(unknownArena));
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacketCodec.Validate(raw with { UnknownBits = [0] }));
    }

    [Fact]
    public void Headless_packet_writes_use_original_aliases_without_any_loaded_models()
    {
        NativeBattleRetainedActionPackets retained = Cold() with { ActionCount = 3, ContactCount = 5, UnknownCountMask = 3, ActiveHeaderIndex = 1, ActiveContactIndex = 2 };
        NativeBattleActionPacketsState fresh = NewArena(retained);
        NativeBattleRetainedActionPackets exported = fresh.ExportRetained();
        Assert.Equal(retained.ResetCounts(), exported);
        Assert.Equal(retained.Headers, exported.Headers);
        Assert.Equal(retained.Contacts, exported.Contacts);
        Assert.Equal(1, exported.ActiveHeaderIndex);
        Assert.Equal(2, exported.ActiveContactIndex);

        NativeBattleActionPacketsState active = fresh.Prepare(Preparation()).State.AppendContact(Contact()).State.Publish(1).State.ActivateHeader(First);
        NativeBattleRetainedActionPackets written = active.ExportRetained();
        Assert.Equal((byte)0, written.Headers[0].ReadKnownByte(0));
        Assert.Equal((byte)3, written.Contacts[0]!.Primary.ActorIndex);
        Assert.Equal((byte)0, written.Contacts[0]!.UnknownBitsAt(0));
        Assert.Equal(0, written.ActiveHeaderIndex);
        Assert.Equal(0, written.ActiveContactIndex);
        Assert.Equal(1, active.AdvanceContact().ExportRetained().ActiveContactIndex);
        Assert.Equal(written.ResetCounts(), active.ResetCounts().ExportRetained());
    }

    [Fact]
    public void Header_writers_preserve_untouched_bytes_and_original_source_encoding()
    {
        NativeBattleRetainedActionPackets retained = Cold();
        NativeBattleRetainedActionHeaderSlot slot = retained.Headers[0] with
        {
            Payload = retained.Headers[0].Payload.SetItem(16, 0xA6).SetItem(17, 0xA7).SetItem(18, 0xA8).SetItem(19, 0xA9),
            UnknownByteMask = (1u << 16) | (1u << 18)
        };
        retained = retained with { Headers = retained.Headers.SetItem(0, slot) };
        NativeBattleActionPacketsState arena = NewArena(retained).Prepare(Preparation()).State;
        NativeBattleRetainedActionHeaderSlot prepared = arena.ExportRetained().Headers[0];
        Assert.Equal(slot.Payload.Skip(16), prepared.Payload.Skip(16));
        Assert.Equal(slot.UnknownByteMask, prepared.UnknownByteMask);
        Assert.Null(arena.ReadHeader(First).ContactCount);
        Assert.Equal((byte)0xA7, arena.ReadHeader(First).RepeatModeMinusOne);

        NativeBattleModelAliases relocated = new(arena.Roster, [(NativeBattleModelSlot.Decode(1), Source), (NativeBattleModelSlot.Decode(3), Target)]);
        arena = arena.WithRoster(arena.Roster, relocated);
        Assert.Same(prepared, arena.ExportRetained().Headers[0]);
        arena = arena.Publish(2).State;
        NativeBattleRetainedActionHeaderSlot published = arena.ExportRetained().Headers[0];
        Assert.Equal(prepared.Payload.SetItem(16, 2).ToArray(), published.Payload.ToArray());
        Assert.Equal(prepared.UnknownByteMask & ~(1u << 16), published.UnknownByteMask);
        arena = arena.WritePlayerRepeatMode(First, 2);
        NativeBattleRetainedActionHeaderSlot repeated = arena.ExportRetained().Headers[0];
        Assert.Equal(published.Payload.SetItem(17, 1).ToArray(), repeated.Payload.ToArray());
        Assert.Equal(published.UnknownByteMask, repeated.UnknownByteMask);

        NativeBattleRetainedActionHeaderSlot next = arena.ResetCounts().Prepare(Preparation()).State.ExportRetained().Headers[0];
        Assert.Equal((byte)1, next.ReadKnownByte(0));
        Assert.Equal(repeated.Payload.Skip(16), next.Payload.Skip(16));
        Assert.Equal(repeated.UnknownByteMask, next.UnknownByteMask);
    }

    [Fact]
    public void Aliases_require_unique_slots_and_owners_with_consistent_side_and_membership()
    {
        BattleModelRoster roster = new([Source, Target]);
        NativeBattleModelSlot first = NativeBattleModelSlot.Decode(0);
        NativeBattleModelSlot second = NativeBattleModelSlot.Decode(1);
        NativeBattleModelSlot opponent = NativeBattleModelSlot.Decode(3);
        Assert.Throws<ArgumentException>(() => new NativeBattleModelAliases(roster, [(first, Source), (first, Source)]));
        Assert.Throws<ArgumentException>(() => new NativeBattleModelAliases(roster, [(first, Source), (second, Source)]));
        Assert.Throws<ArgumentException>(() => new NativeBattleModelAliases(roster, [(first, Target)]));
        Assert.Throws<ArgumentException>(() => new NativeBattleModelAliases(roster, [(opponent, Extra)]));
        NativeBattleModelAliases aliases = new(roster, [(first, Source)]);
        Assert.Throws<InvalidDataException>(() => aliases.DecodeLiteral(opponent.Offset));
        Assert.Throws<ArgumentException>(() => NativeBattleActionPacketsState.Create(new([Source, Extra]), Cold(), aliases));
        Assert.Throws<ArgumentException>(() => NewArena().WithRoster(new([Source, Extra]), aliases));
    }

    private static NativeBattleActionHeaderPreparation Preparation() => new(Source, 1, 7, -1, 101, 0, null);
    private static NativeBattleContactPacket Contact() => new(new(Target, 4, 0, 0, 0, 37, 0), null);
    private static NativeBattleRetainedActionPackets Cold() => NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]);

    private static NativeBattleActionPacketsState NewArena(NativeBattleRetainedActionPackets? retained = null)
    {
        BattleModelRoster roster = new([Source, Target, Extra, ExtraCover]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Source), (NativeBattleModelSlot.Decode(3), Target)]);
        return NativeBattleActionPacketsState.Create(roster, retained ?? NativeBattleRetainedActionPackets.Fresh(), aliases);
    }

    private static (NativeBattleModelBoundary Bindings, BattleActorModels Models) OriginalBindings()
    {
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(null);
        BattleActorModels models = new([]);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), Source, models);
        return boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), Target, models);
    }
}
