using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSlotsTargetTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Immune_enemy_already_carrying_status_still_has_lowest_priority(bool cards, int repetitions)
    {
        BattleSession battle = Create(Spell(2, 0x50) with { Statuses0 = 8, StatusAttack = 250 }, cards,
            Ally("selphie", 500), Enemy("first") with { NativeStatus0 = 8 }, Enemy("second") with { NativeStatus0 = 8 });
        Queue(ref battle, cards, repetitions);
        BattleTargetRequest request = Assert.Single(battle.QueuedActions).TargetRequest;
        ushort originalMask = NativeBattleTargetRequestAdapter.ProjectOriginalWord(request, target => battle.Combatants.Single(actor => actor.Id == target.ActorId).Slot) ?? throw new InvalidDataException("The original Slots fixture requires a target WORD.");
        BattleCombatant original = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && (originalMask & (1 << actor.EncodeNativeSlot())) != 0);
        original.MutableNativeStatusResistances[3] = 200;
        Assert.DoesNotContain(new BattleEnemyActorModel(new(original.Id)), Resolve(battle, repetitions));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Healing_reconsiders_current_hp_after_each_cast_and_ignores_knocked_out_allies(bool cards, int repetitions)
    {
        BattleSession battle = Create(Spell(4, 0x10) with { Power = 2 }, cards,
            Ally("selphie", 100), Ally("second", 150), Ally("ko", 0), Enemy("enemy"));
        Queue(ref battle, cards, repetitions);
        Assert.Equal(new BattleModelOwner[] { new BattlePartyActorModel(new("selphie")), new BattlePartyActorModel(new("second")), new BattlePartyActorModel(new("selphie")) }.Take(repetitions), Resolve(battle, repetitions));
        Assert.Equal(0, battle.Combatants[2].CurrentHp);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Buffs_spread_to_living_allies_missing_the_status(bool cards, int repetitions)
    {
        BattleSession battle = Create(Spell(2, 0x10) with { Statuses1 = 0x20, StatusAttack = 255 }, cards,
            Ally("selphie", 500), Ally("second", 500), Ally("third", 500), Enemy("enemy"));
        Queue(ref battle, cards, repetitions);
        BattleModelOwner[] targets = Resolve(battle, repetitions);
        Assert.Equal(repetitions, targets.Distinct().Count());
        Assert.All(targets, target => Assert.NotEqual(0u, battle.Combatants.Single(actor => actor.Id == Assert.IsType<BattlePartyActorModel>(target).Actor.ActorId).NativeStatus1 & 0x20));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Debuffs_prefer_missing_status_and_put_immune_enemies_last(bool cards, int repetitions)
    {
        byte[] immune = new byte[40];
        immune[3] = 200;
        BattleSession battle = Create(Spell(2, 0x50) with { Statuses0 = 8, StatusAttack = 250 }, cards,
            Ally("selphie", 500), Enemy("immune") with { NativeStatusResistances = immune }, Enemy("affected") with { NativeStatus0 = 8 }, Enemy("fresh"));
        Queue(ref battle, cards, repetitions);
        BattleModelOwner[] targets = Resolve(battle, repetitions);
        Assert.Equal(new BattleEnemyActorModel(new("fresh")), targets[0]);
        Assert.DoesNotContain(new BattleEnemyActorModel(new("immune")), targets);
        Assert.NotEqual(0, battle.Combatants.Single(actor => actor.Id == "fresh").NativeStatus0 & 8);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Damaging_magic_puts_element_absorbers_last(bool cards, int repetitions)
    {
        BattleSession battle = Create(Spell(2, 0x50) with { Power = 1, Element = 1 }, cards,
            Ally("selphie", 500), Enemy("absorber") with { ElementDefenses = [1000, 800, 800, 800, 800, 800, 800, 800] }, Enemy("vulnerable"));
        Queue(ref battle, cards, repetitions);
        Assert.All(Resolve(battle, repetitions), target => Assert.Equal(new BattleEnemyActorModel(new("vulnerable")), target));
    }

    private static BattleModelOwner[] Resolve(BattleSession battle, int repetitions)
    {
        for (int frame = 0; frame < 300 && battle.NativeActionPackets.ActionCount < repetitions; frame++)
        {
            battle.Tick();
        }
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        Assert.Equal(repetitions, packets.ActionCount);
        return Enumerable.Range(0, repetitions).Select(index => packets.ReadContact(packets.ReadHeader(new BattleActionHeaderReference(index)).FirstContact).Primary.Recipient).ToArray();
    }

    private static void Queue(ref BattleSession battle, bool cards, int repetitions)
    {
        BattleNativeSlotsRoll roll = new(1, (byte)repetitions);
        if (cards)
        {
            BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
            battle = BattleSession.Restore(battle.Capture() with { PendingSlotsDeal = deal with { Candidates = deal.Candidates.SetItem(0, roll) } });
            Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
            Assert.True(battle.SubmitNativeSlotsCard(deal.Id));
        }
        else
        {
            battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4) { Slots = roll }] });
            battle.QueueNativePlayerCommand("selphie", "selphie", 16, 1);
        }
        battle = BattleSession.Restore(battle.Capture());
    }

    private static BattleSession Create(BattleNativeAction spell, bool cards, params BattleCombatantDefinition[] actors)
    {
        BattleNativeLimitDefinition rules = new(5, 16, 0, 0, new byte[32].ToImmutableArray())
        {
            SlotsSelectionBytes = new byte[80].ToImmutableArray(),
            SlotsSpellBytes = Enumerable.Repeat((byte)1, 16).ToImmutableArray()
        };
        actors[0] = actors[0] with { ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = rules };
        BattleSession battle = new(new BattleDefinition
        {
            SelphieCardSlots = cards, NativeGlobalRandom = new(12345), NativeLimitsDisabled = false,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [spell], NativeStatusRules = new(0, Enumerable.Repeat((byte)30, 14).ToImmutableArray()), Combatants = actors
        }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4)] });
    }

    private static BattleCombatantDefinition Ally(string id, int hp) => new() { Id = id, Name = id, IsPlayer = true, MaximumHp = 1000, CurrentHp = hp, NativeStatus0 = (ushort)(hp == 0 ? 1 : 0), NativeStatusResistances = new byte[40], Speed = 1 };

    private static BattleCombatantDefinition Enemy(string id) => new() { Id = id, Name = id, MaximumHp = 60000, CurrentHp = 60000, NativeStatusResistances = new byte[40], Speed = 1 };

    private static BattleNativeAction Spell(byte attackType, byte targetFlags) => new(16, 1, 0, 0)
    {
        Name = "Spell", MagicId = 1, AttackType = attackType, Power = 0, HitCount = 1, TargetFlags = targetFlags, AttackFlags = 0, DamagePresentation = 0,
        Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [], SupplementalRecord = []
    };
}

