using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MagicBattleReservationTests
{
    [Fact]
    public void Removing_all_reservation_links_from_an_unperformed_spell_rejects_checkpoint()
    {
        BattleSession battle = Create();
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        BattleSnapshot saved = battle.Capture();
        Assert.Single(saved.MagicVitals!.MagicSystem.Reservations);
        NativeBattleCommandBanks banks = saved.NativeCommandBanks! with
        {
            Banks = saved.NativeCommandBanks!.Banks.Select(bank => bank with { LogicalCommands = bank.LogicalCommands.Select(command => command is null ? null : command with { MagicReservationId = null }).ToImmutableArray() }).ToImmutableArray()
        };
        NativeCharacterVitals owner = saved.MagicVitals!.DeepCopy();
        owner = owner with { MagicSystem = owner.MagicSystem with { Reservations = [] } };
        BattleSnapshot changed = saved with { NativeCommandBanks = banks, QueuedActions = saved.QueuedActions.Select(command => command with { MagicReservationId = null }).ToArray(), MagicVitals = owner };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(changed));
    }

    [Fact]
    public void Ordinary_selected_magic_cannot_be_reclassified_as_a_free_special_source_in_checkpoint()
    {
        BattleSession battle = Create();
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        BattleSnapshot saved = battle.Capture();
        NativeBattleCommandBanks banks = saved.NativeCommandBanks! with
        {
            Banks = saved.NativeCommandBanks!.Banks.Select(bank => bank with { LogicalCommands = bank.LogicalCommands.Select(command => command is null ? null : command with { FreeNativeMagic = true, MagicReservationId = null }).ToImmutableArray() }).ToImmutableArray()
        };
        NativeCharacterVitals owner = saved.MagicVitals!.DeepCopy();
        owner = owner with { MagicSystem = owner.MagicSystem with { Reservations = [] } };
        BattleSnapshot changed = saved with { NativeCommandBanks = banks, QueuedActions = saved.QueuedActions.Select(command => command with { FreeNativeMagic = true, MagicReservationId = null }).ToArray(), MagicVitals = owner };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(changed));
    }

    [Fact]
    public void Changing_saved_cast_rules_without_changing_encounter_rules_rejects_checkpoint()
    {
        BattleSnapshot saved = Create().Capture();
        NativeCharacterVitals owner = saved.MagicVitals!.DeepCopy();
        owner = owner with { MagicSystem = owner.MagicSystem with { InfiniteMagic = false } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { MagicVitals = owner }));
    }

    [Fact]
    public void Saved_bank_record_cannot_change_the_acting_profile_behind_the_battle_definition()
    {
        BattleSnapshot saved = Create().Capture();
        NativeCharacterVitals owner = saved.MagicVitals!.DeepCopy();
        owner.Records[4] = owner.Records[4] with { CharacterId = 0 };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { MagicVitals = owner }));
    }

    [Fact]
    public void Reservation_for_another_spell_rejects_checkpoint()
    {
        BattleSession battle = Create();
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        BattleSnapshot saved = battle.Capture();
        NativeCharacterVitals owner = saved.MagicVitals!.DeepCopy();
        owner = owner with { MagicSystem = owner.MagicSystem with { Reservations = owner.MagicSystem.Reservations.Select(reservation => reservation with { SpellId = 2 }).ToArray() } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { MagicVitals = owner }));
    }

    [Fact]
    public void Triple_command_cannot_restore_with_only_one_reserved_ticket()
    {
        BattleSession battle = Create(transient: 0x40000);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        BattleSnapshot saved = battle.Capture();
        NativeCharacterVitals owner = saved.MagicVitals!.DeepCopy();
        owner = owner with { MagicSystem = owner.MagicSystem with { Reservations = owner.MagicSystem.Reservations.Select(reservation => reservation with { Count = 1, TicketMask = 1 }).ToArray() } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { MagicVitals = owner }));
    }

    [Fact]
    public void Combatant_inventory_view_cannot_overwrite_the_saved_authoritative_shared_bank()
    {
        BattleSnapshot saved = Create().Capture();
        BattleCombatantSnapshot[] actors = saved.Combatants.Select(actor => actor.Id == "caster" ? actor with { NativeMagicInventory = actor.NativeMagicInventory!.Select(stock => stock.SpellId == 1 ? stock with { Count = 99 } : stock).ToArray(), MagicStocks = [new BattleStock(1, 99)] } : actor).ToArray();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { Combatants = actors }));
        Assert.Equal(100, saved.MagicVitals!.GetMagicCount(4, 1));
    }

    [Fact]
    public void Silence_after_manual_selection_cancels_without_spending_stock_or_tickets()
    {
        BattleSession battle = Create();
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        BattleSnapshot saved = battle.Capture();
        BattleCombatantSnapshot[] actors = saved.Combatants.Select(actor => actor.Id == "caster" ? actor with { NativeStatus = actor.NativeStatus! with { Saved = (ushort)(actor.NativeStatus!.Saved | 0x10) } } : actor).ToArray();
        BattleSession restored = BattleSession.Restore(saved with { Combatants = actors });
        Advance(restored);
        Assert.Equal(100, restored.MagicVitals!.GetMagicCount(4, 1));
        Assert.Equal(0, restored.MagicVitals.MagicSystem.SharedBattleMasks[1]);
        Assert.Empty(restored.MagicVitals.MagicSystem.Reservations);
    }

    [Fact]
    public void Triple_interrupted_after_first_enemy_dies_spends_one_and_releases_two()
    {
        BattleSession battle = Create(transient: 0x40000, enemyHp: 1);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        Assert.Equal(3, Assert.Single(battle.MagicVitals!.MagicSystem.Reservations).Count);
        Advance(battle);
        Assert.Equal(1, battle.MagicVitals!.MagicSystem.SharedBattleMasks[1]);
        Assert.Equal(100, battle.MagicVitals.GetMagicCount(4, 1));
        Assert.Empty(battle.MagicVitals.MagicSystem.Reservations);
        Assert.Equal(3, battle.GetAvailableMagicCasts("caster", 1));
    }

    [Fact]
    public void Shared_bound_owner_can_spend_two_finite_stock_units_as_one_double_group()
    {
        BattleSession battle = Create(infinite: false, stock: 2, transient: 0x20000, enemyHp: 60000);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy"]);
        Advance(battle);
        Assert.Equal(0, battle.MagicVitals!.GetMagicCount(4, 1));
        Assert.Empty(battle.MagicVitals.MagicSystem.Reservations);
        Assert.Equal(0, battle.MagicVitals.MagicSystem.SharedBattleMasks[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unlimited_magic_triple_with_one_stock_keeps_junction_and_round_trips(bool limitedMode)
    {
        BattleSession battle = Create(infinite: limitedMode, stock: 1, transient: 0x40000, unlimited: true);
        Assert.Contains(3, battle.GetNativeMagicCastCounts("caster", 1));
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        BattleSnapshot saved = battle.Capture();
        Assert.Equal(3, Assert.Single(saved.MagicVitals!.MagicSystem.Reservations).Count);
        BattleSession restored = BattleSession.Restore(saved);
        Advance(restored);
        Assert.Equal(1, restored.MagicVitals!.GetMagicCount(4, 1));
        Assert.Equal(1, restored.MagicVitals.Records[4].Junction.MagicJunctions[0]);
        Assert.Equal(0, restored.MagicVitals.MagicSystem.SharedBattleMasks[1]);
        Assert.Empty(restored.MagicVitals.MagicSystem.Reservations);
        NativeCharacterVitals changed = saved.MagicVitals.DeepCopy();
        changed = changed with { MagicSystem = changed.MagicSystem with { UnlimitedMagic = false } };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { MagicVitals = changed }));
    }

    private static void Advance(BattleSession battle)
    {
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames + 2; frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(bool infinite = true, int stock = 100, uint transient = 0, int enemyHp = 60000, bool unlimited = false)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, stock), shared: true, infinite: infinite);
        vitals = vitals with { MagicSystem = vitals.MagicSystem with { UnlimitedMagic = unlimited } };
        vitals = MagicJunctionPolicy.Assign(vitals, 4, 0, 1);
        BattleNativeAction spell = new(2, 1, 1, 1) { Name = "Spell", MagicId = 1, AttackType = 2, Power = 255, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = [], SupplementalRecord = [] };
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        BattleNativePlayerStatRules rules = new(4, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), vitals.Records[4].Junction.MagicJunctions.ToImmutableArray(), 0, vitals.Definitions) { NativeStatusPassiveFlags = 0 };
        BattleNativeCharacterGrowth growth = new(0, new byte[6].ToImmutableArray());
        int maximumHp = rules.Project(vitals.GetMagicInventory(4), growth).MaximumHp;
        return new(new BattleDefinition
        {
            MagicVitals = vitals,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [spell],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Spell", BattleActionEffect.Native, 0, false) { NativeAction = spell }],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 1, MagicRecordId = 4, MaximumHp = maximumHp, CurrentHp = maximumHp, InitialAtb = BattleSession.AtbMaximum, Magic = 100, Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativePlayerStatRules = rules, NativeStatusResistances = new byte[40], NativeGrowth = growth, NativeCommandSlots = [1, 2, 0, 0], NativeStatus1 = transient },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, MaximumHp = enemyHp, CurrentHp = enemyHp, ElementDefenses = [800, 800, 800, 800, 800, 800, 800, 800] }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
