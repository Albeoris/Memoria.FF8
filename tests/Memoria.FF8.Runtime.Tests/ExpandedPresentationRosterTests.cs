using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedPresentationRosterTests
{
    private static readonly MethodInfo AdvanceAction = typeof(BattleSession).GetMethod("AdvanceExpandedAction", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(3, 4, 7)]
    [InlineData(4, 5, 16)]
    [InlineData(11, 14, 25)]
    public void Presentation_rows_preserve_sides_and_native_positions_across_restore(int allies, int enemies, int extent)
    {
        BattleSession battle = Create(allies, enemies);
        string before = Snapshot(battle);
        BattleModelRoster roster = battle.ReadExpandedPresentationRoster();
        Assert.Equal(extent, roster.ActorCount);
        for (int position = 0; position < allies; position++)
        {
            int row = position < 3 ? position : position + 4;
            AssertBinding(battle, roster, $"ally-{position}", row, true);
        }
        for (int position = 0; position < enemies; position++)
        {
            int row = position < 4 ? position + 3 : position + 11;
            AssertBinding(battle, roster, $"enemy-{position}", row, false);
        }
        Assert.Equal(allies + enemies, roster.Rows.Count(actor => actor is not null));
        Assert.Equal(before, Snapshot(battle));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!);
        Assert.Equal(roster.Rows.ToArray(), restored.ReadExpandedPresentationRoster().Rows.ToArray());
    }

    [Fact]
    public void An_allocated_inactive_hidden_model_retains_its_row_until_replacement()
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant retained = battle.Combatants.Single(actor => actor.Id == "enemy-1");
        BattleCombatant replacement = battle.Combatants.Single(actor => actor.Id == "enemy-2");
        retained.IsActive = false;
        retained.Hidden = true;
        replacement.BindRosterSlot(null);
        BattleModelRoster retainedRoster = battle.ReadExpandedPresentationRoster();
        AssertBinding(battle, retainedRoster, retained.Id, 4, false);
        Assert.Null(retainedRoster.Rows[5]);
        Assert.Throws<InvalidDataException>(() => battle.ReadExpandedPresentationRow(replacement.Id));

        retained.BindRosterSlot(null);
        replacement.BindRosterSlot(BattleRosterSlot.Enemy(1));
        BattleModelRoster replaced = battle.ReadExpandedPresentationRoster();
        AssertBinding(battle, replaced, replacement.Id, 4, false);
        Assert.DoesNotContain(replaced.Rows, actor => actor?.ActorId == retained.Id);
        Assert.Null(replaced.Rows[5]);
    }

    [Fact]
    public void Presentation_rejects_duplicate_and_out_of_range_slot_bindings()
    {
        BattleSession battle = Create(4, 5);
        BattleCombatant last = battle.Combatants.Single(actor => actor.Id == "enemy-4");
        last.BindRosterSlot(BattleRosterSlot.Enemy(3));
        Assert.Throws<InvalidDataException>(() => battle.ReadExpandedPresentationRoster());
        last.BindRosterSlot(BattleRosterSlot.Enemy(14));
        Assert.Throws<InvalidDataException>(() => battle.ReadExpandedPresentationRoster());
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
            Assert.Equal(14, candidate.ReadExpandedPresentationRow(caster.Id));
            Assert.Equal(1, action.MagicId);
            Assert.Equal(42, contacts.Length);
            Assert.Equal(Enumerable.Range(0, 3).SelectMany(_ => recipients), contacts.Select(contact => contact.RecipientId));
            for (int hit = 0; hit < 3; hit++)
            {
                for (int target = 0; target < 14; target++)
                {
                    NativeBattleContactPacket packet = contacts[hit * 14 + target].Packet;
                    Assert.Equal(target < 4 ? target + 3 : target + 11, packet.Primary.ActorIndex);
                    Assert.Equal(14, packet.Secondary.ActorIndex);
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

    private static BattleSession Create(int allies, int enemies, BattleNativeAction? native = null)
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
            UsesExpandedRoster = true,
            Combatants = [.. party, .. opponents],
            NativeActions = native is null ? [] : [native],
            ActionDefinitions = native is null ? [] : [new(BattleAbility.Magic, 1, "Synthetic Drain", BattleActionEffect.Native, native.Power, false, true) { NativeAction = native }]
        }, BattleAtbMode.TurnBased);
    }

    private static void AssertBinding(BattleSession battle, BattleModelRoster roster, string id, int row, bool party)
    {
        Assert.Equal(new BattleModelOwner(id, party), roster.Rows[row]);
        Assert.Equal(row, battle.ReadExpandedPresentationRow(id));
    }

    private static string Snapshot(BattleSession battle)
    {
        return JsonSerializer.Serialize(battle.Capture());
    }
}
