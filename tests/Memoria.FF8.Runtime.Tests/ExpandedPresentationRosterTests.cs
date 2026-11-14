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
        string before = Snapshot(battle);
        BattleModelRoster roster = battle.ReadBattlePresentationRoster();
        BattlePartyActorModel[] party = Enumerable.Range(0, allies).Select(position => new BattlePartyActorModel(new($"ally-{position}"))).ToArray();
        BattleEnemyActorModel[] opponents = Enumerable.Range(0, enemies).Select(position => new BattleEnemyActorModel(new($"enemy-{position}"))).ToArray();
        BattleModelOwner[] expected = [.. party.Take(3), .. opponents.Take(4), .. party.Skip(3), .. opponents.Skip(4)];
        Assert.Equal(expected, roster.Owners);
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
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!);
        Assert.Equal(expected, restored.ReadBattlePresentationRoster().Owners);
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

    [Fact]
    public void Synthetic_drain_effect_one_passes_all_repeated_hits_and_extra_caster_to_host_before_commit()
    {
        BattleNativeAction native = new(2, 1, 1, 1)
        {
            AttackType = 2, Power = 15, Hit = 255, HitCount = 3, TargetFlags = 0x48, AttackFlags = 3,
            MagicId = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 100, Statuses0 = 0, Statuses1 = 0x8000,
            Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleSession live = Create(11, 14, native);
        InvalidOperationException failure = new("Reject the prepared presentation at the host boundary.");
        string? before = null;
        int calls = 0;
        string[] recipients = Enumerable.Range(0, 14).Select(position => $"enemy-{position}").ToArray();
        BattleCombatant liveCaster = live.Combatants.Single(actor => actor.Id == "ally-10");
        live.AttachExpandedEffectFactory((candidate, caster, action, _, contacts, _) =>
        {
            calls++;
            Assert.NotSame(live, candidate);
            Assert.Equal(liveCaster.Id, caster.Id);
            Assert.Equal(new BattlePartyActorModel(new(caster.Id)), candidate.ReadPresentationModelOwner(caster.Id));
            Assert.Equal(1, action.MagicId);
            Assert.Equal(42, contacts.Length);
            Assert.Equal(Enumerable.Range(0, 3).SelectMany(_ => recipients), contacts.Select(contact => contact.RecipientId));
            for (int hit = 0; hit < 3; hit++)
            {
                for (int target = 0; target < 14; target++)
                {
                    NativeBattleContactPacket packet = contacts[hit * 14 + target].Packet;
                    Assert.Equal(new BattleEnemyActorModel(new(recipients[target])), packet.Primary.Recipient);
                    Assert.Equal(new BattlePartyActorModel(new(caster.Id)), Assert.IsType<NativeBattleContactHalf>(packet.Secondary).Recipient);
                }
            }
            Assert.Equal(1, Assert.Single(caster.MagicStocks).Count);
            Assert.Equal(before, Snapshot(live));
            throw failure;
        });
        live.QueueAction(liveCaster.Id, recipients[0], BattleAbility.Magic, 1);
        live.Tick();
        for (int frame = 0; frame < 120 && live.ActiveAction is { Phase: BattleActionPhase.Windup, RemainingFrames: > 1 }; frame++)
        {
            live.Tick();
        }
        Assert.Equal(BattleActionPhase.Windup, live.ActiveAction?.Phase);
        Assert.Equal(1, live.ActiveAction?.RemainingFrames);
        before = Snapshot(live);
        Assert.Same(failure, Assert.Throws<TargetInvocationException>(() => AdvanceAction.Invoke(live, [live.ActiveAction!])).InnerException);
        Assert.Equal(1, calls);
        Assert.Equal(before, Snapshot(live));
        Assert.Equal(2, Assert.Single(liveCaster.MagicStocks).Count);
        Assert.Null(live.ExpandedEffect);
    }

    private static BattleSession Create(int allies, int enemies, BattleNativeAction? native = null, bool expanded = true)
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
