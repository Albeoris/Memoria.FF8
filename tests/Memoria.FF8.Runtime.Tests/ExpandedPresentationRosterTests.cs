using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedPresentationRosterTests
{
    private static readonly MethodInfo AdvanceAction = typeof(BattleSession).GetMethod("AdvanceExpandedAction", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(3, 4, false)]
    [InlineData(4, 5, true)]
    [InlineData(11, 14, true)]
    public void Presentation_owners_preserve_original_slot_order_then_extended_side_order(int allies, int enemies, bool expanded)
    {
        BattleSession battle = Create(allies, enemies, expanded: expanded);
        BattleSnapshot captured = battle.Capture();
        string before = JsonSerializer.Serialize(captured);
        BattleModelRoster roster = battle.ReadBattlePresentationRoster();
        BattlePartyActorModel[] party = Enumerable.Range(0, allies).Select(position => new BattlePartyActorModel(new($"ally-{position}"))).ToArray();
        BattleEnemyActorModel[] opponents = Enumerable.Range(0, enemies).Select(position => new BattleEnemyActorModel(new($"enemy-{position}"))).ToArray();
        BattleModelOwner[] expected = [.. party.Take(3), .. opponents.Take(4), .. party.Skip(3), .. opponents.Skip(4)];
        Assert.Equal(expected, roster.Owners);
        Assert.Equal(expected, battle.NativeActionPackets.Roster.Owners);
        Assert.Equal(allies + enemies, roster.Count);
        Assert.Equal(party.Cast<BattlePartyModelOwner>(), roster.Party);
        Assert.Equal(opponents.Cast<BattleEnemyModelOwner>(), roster.Enemies);
        foreach (BattleCombatant actor in battle.Combatants)
        {
            BattleModelOwner actual = battle.ReadPresentationModelOwner(actor.Id);
            if (actor.IsPlayer)
            {
                Assert.Equal(actor.Id, Assert.IsType<BattlePartyActorModel>(actual).Actor.ActorId);
            }
            else
            {
                Assert.Equal(actor.Id, Assert.IsType<BattleEnemyActorModel>(actual).Actor.ActorId);
            }
            if (actor.Slot is { } slot && NativeBattleSlots.TryEncode(slot) is null)
            {
                Assert.Null(actor.NativeActorIndex);
            }
        }
        Assert.Equal(before, Snapshot(battle));
        BattleSession restored = BattleSession.Restore(captured);
        Assert.Equal(expected, restored.ReadBattlePresentationRoster().Owners);
        Assert.Equal(expected, restored.NativeActionPackets.Roster.Owners);
    }

    [Fact]
    public void Constructor_without_retained_memory_starts_with_unknown_rows_and_known_zero_counts()
    {
        BattleSession battle = Create(3, 4, expanded: false);
        NativeBattleRetainedActionPackets raw = battle.NativeActionPackets.ExportRetained();

        Assert.Equal((byte)0, raw.ActionCount);
        Assert.Equal(0, raw.ContactCount);
        Assert.Equal((byte)0, raw.UnknownCountMask);
        Assert.Null(raw.ActiveHeaderIndex);
        Assert.Null(raw.ActiveContactIndex);
        Assert.All(raw.Headers, header => Assert.Equal(NativeBattleRetainedActionHeaderSlot.AllUnknown, header.UnknownByteMask));
        Assert.All(raw.Contacts, Assert.Null);

        BattleModelOwner first = battle.ReadPresentationModelOwner("ally-0");
        NativeBattleActionPacketsState prepared = battle.NativeActionPackets.Prepare(new(first, 1, 7, -1, 0, 0, null)).State;
        Assert.Equal((byte)0, prepared.ExportRetained().Headers[0].ReadKnownByte(0));
    }

    [Fact]
    public void Constructor_keeps_imported_raw_facts_and_only_original_graphics_aliases_encode()
    {
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.FromColdProcess();
        NativeBattleRetainedActionPackets cold = memory.ActionPackets;
        NativeBattleRetainedActionHeaderSlot header = cold.Headers[0] with { Payload = cold.Headers[0].Payload.SetItem(0, 2).SetItem(16, 6).SetItem(19, 0xB4) };
        byte[] contact = [3, 7, 0, 2, 0, 0, 37, 0, 0, 0, 0, 0, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        NativeBattleRetainedActionPackets retained = cold with
        {
            ActionCount = 1,
            ContactCount = 1,
            ActiveHeaderIndex = 0,
            ActiveContactIndex = 0,
            Headers = cold.Headers.SetItem(0, header),
            Contacts = cold.Contacts.SetItem(0, NativeBattleContactPacketCodec.Decode(contact))
        };
        BattleSession battle = Create(4, 5, retained: memory with { ActionPackets = retained });
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        NativeBattleRetainedActionPackets raw = packets.ExportRetained();

        Assert.Null(battle.NativeGraphics);
        Assert.Equal((byte)0, raw.ActionCount);
        Assert.Equal(0, raw.ContactCount);
        Assert.Equal((byte)0, raw.UnknownCountMask);
        Assert.Equal(0, raw.ActiveHeaderIndex);
        Assert.Equal(0, raw.ActiveContactIndex);
        Assert.Null(packets.ActiveHeader);
        Assert.Null(packets.ActiveContact);
        Assert.Equal(header.Payload.ToArray(), raw.Headers[0].Payload.ToArray());
        Assert.Equal(contact, NativeBattleContactPacketCodec.Encode(raw.Contacts[0]!));

        BattleModelOwner first = battle.ReadPresentationModelOwner("ally-0");
        BattleModelOwner fourth = battle.ReadPresentationModelOwner("ally-3");
        NativeBattleActionHeaderPreparation preparation = new(first, 1, 7, -1, 0, 0, null);
        NativeBattleRetainedActionHeaderSlot original = packets.Prepare(preparation).State.ExportRetained().Headers[0];
        NativeBattleRetainedActionHeaderSlot extra = packets.Prepare(preparation with { Source = fourth }).State.ExportRetained().Headers[0];
        Assert.Equal((byte)0, original.ReadKnownByte(0));
        Assert.Equal((byte)2, extra.Payload[0]);
        Assert.Null(extra.ReadKnownByte(0));
        Assert.Equal((byte)6, extra.ReadKnownByte(16));
        Assert.Equal((byte)0xB4, extra.ReadKnownByte(19));
    }

    [Fact]
    public void An_allocated_inactive_hidden_actor_retains_its_owner_until_replacement()
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant retained = battle.Combatants.Single(actor => actor.Id == "enemy-1");
        BattleCombatant replacement = battle.Combatants.Single(actor => actor.Id == "enemy-2");
        retained.IsActive = false;
        retained.Hidden = true;
        replacement.BindRosterSlot(null);
        BattleEnemyActorModel retainedOwner = new(new(retained.Id));
        BattleEnemyActorModel replacementOwner = new(new(replacement.Id));
        BattleModelRoster retainedRoster = battle.ReadBattlePresentationRoster();
        Assert.Equal(retainedOwner, battle.ReadPresentationModelOwner(retained.Id));
        Assert.True(retainedRoster.Contains(retainedOwner));
        Assert.False(retainedRoster.Contains(replacementOwner));
        Assert.Equal(8, retainedRoster.Count);
        Assert.Throws<InvalidDataException>(() => battle.ReadPresentationModelOwner(replacement.Id));

        retained.BindRosterSlot(null);
        replacement.BindRosterSlot(BattleRosterSlot.Enemy(1));
        BattleModelRoster replaced = battle.ReadBattlePresentationRoster();
        Assert.Equal(replacementOwner, battle.ReadPresentationModelOwner(replacement.Id));
        Assert.Equal(retainedRoster.Owners.Select(owner => owner == retainedOwner ? replacementOwner : owner), replaced.Owners);
        Assert.True(retainedRoster.Contains(retainedOwner));
        Assert.False(replaced.Contains(retainedOwner));
        Assert.Throws<InvalidDataException>(() => battle.ReadPresentationModelOwner(retained.Id));
    }

    [Fact]
    public void Original_graphics_alias_does_not_require_or_publish_an_ai_alias()
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant original = battle.Combatants.Single(actor => actor.Id == "enemy-0");
        BattleCombatant extra = battle.Combatants.Single(actor => actor.Id == "enemy-4");
        original.BindRosterSlot(BattleRosterSlot.Enemy(0), bindNative: false);
        var (roster, aliases) = battle.ReadBattlePresentationPacketBindings();
        BattleModelOwner owner = battle.ReadPresentationModelOwner(original.Id);

        Assert.Equal(battle.ReadBattlePresentationRoster().Owners, roster.Owners);
        Assert.True(aliases.TryEncodeExact(owner, out byte originalOperand));
        Assert.Equal((byte)3, originalOperand);
        Assert.Equal(owner, aliases.DecodeLiteral(originalOperand));
        Assert.Null(original.NativeActorIndex);
        Assert.False(aliases.TryEncodeExact(battle.ReadPresentationModelOwner(extra.Id), out _));
    }

    [Fact]
    public void Presentation_rejects_duplicate_slots()
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant last = battle.Combatants.Single(actor => actor.Id == "enemy-4");
        last.BindRosterSlot(BattleRosterSlot.Enemy(3));
        Assert.Throws<InvalidDataException>(() => battle.ReadBattlePresentationRoster());
    }

    [Theory]
    [InlineData("enemy-0", 4)]
    [InlineData("enemy-4", 6)]
    public void Presentation_rejects_mismatched_aliases(string actorId, int alias)
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant actor = battle.Combatants.Single(actor => actor.Id == actorId);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.NativeActorIndex))!.SetValue(actor, (int?)alias);
        Assert.Throws<InvalidDataException>(() => battle.ReadBattlePresentationRoster());
        Assert.Throws<InvalidDataException>(() => battle.ReadPresentationModelOwner(actorId));
    }

    [Fact]
    public void Presentation_rejects_slots_on_the_wrong_side()
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant actor = battle.Combatants.Single(actor => actor.Id == "enemy-4");
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.Slot))!.SetValue(actor, (BattleRosterSlot?)BattleRosterSlot.Ally(4));
        Assert.Throws<InvalidDataException>(() => battle.ReadBattlePresentationRoster());
        Assert.Throws<InvalidDataException>(() => battle.ReadPresentationModelOwner(actor.Id));
    }

    private static BattleSession Create(int allies, int enemies, BattleNativeAction? native = null, bool expanded = true, NativeBattleRetainedMemory? retained = null)
    {
        BattleCombatantDefinition[] party = Enumerable.Range(0, allies).Select(position => new BattleCombatantDefinition
        {
            Id = $"ally-{position}", Name = $"Ally {position}", ModelId = position, IsPlayer = true, CurrentHp = 5000, MaximumHp = 10000, Speed = 1
        }).ToArray();
        if (native is not null)
        {
            party[^1] = party[^1] with { InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], Magic = 100, MagicStocks = [new(1, 2)] };
        }
        BattleCombatantDefinition[] opponents = Enumerable.Range(0, enemies).Select(position => new BattleCombatantDefinition
        {
            Id = $"enemy-{position}", Name = $"Enemy {position}", CurrentHp = 60000, MaximumHp = 60000, Speed = 1, NativeStatusResistances = new byte[40]
        }).ToArray();
        return new(new()
        {
            UsesExpandedRoster = expanded,
            NativeRetainedMemory = retained,
            Combatants = [.. party, .. opponents],
            NativeActions = native is null ? [] : [native],
            ActionDefinitions = native is null ? [] : [new(BattleAbility.Magic, 1, "Synthetic Drain", BattleActionEffect.Native, native.Power, false, true) { NativeAction = native }]
        }, BattleAtbMode.TurnBased);
    }

    private static string Snapshot(BattleSession battle)
    {
        return JsonSerializer.Serialize(battle.Capture());
    }
}
