using System.Text.Json;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverZombieRecoveryTests
{
    [Theory]
    [InlineData(1, 5000u, 0u, false, false)]
    [InlineData(1, 5000u, 0u, true, true)]
    [InlineData(1, 0u, 1u, false, true)]
    [InlineData(4, 5000u, 0u, true, true)]
    [InlineData(4, 0u, 1u, true, false)]
    public void Revival_target_contract_accepts_living_Zombie_and_preserves_enemy_removal_boundary(int slot, uint hp, uint statuses, bool zombie, bool legal)
    {
        Ff7BattleAiActor actor = new(slot, statuses, 8, 0, 0, hp, 9999);
        Assert.Equal(legal, CrossoverGuestHostActions.AbilityTargetIsLegal(0x15, actor, zombie ? Ff7OriginSavedStatusRules.Zombie : (ushort)0));
    }

    [Fact]
    public void Prepared_living_Zombie_revival_keeps_its_offensive_zero_magnitude_contact()
    {
        BattleNativeAction action = Action(5);
        Ff7BattleActionVitals living = new(5000, 9999, 0, 0, 0) { OriginSavedStatuses = Ff7OriginSavedStatusRules.Zombie };
        Ff7BattleActionReceipt receipt = new(living, living with { Hp = 0, Statuses = 1 }, new(false, false, 0), 1, 0);
        Ff7BattleGuestPreparedContact prepared = new(new(1, 300, 0, 0, 1), living, living, receipt);
        Assert.Equal((byte)0, CrossoverGuestHostActions.PreparedContactFlags(action, prepared));
        Assert.Equal((byte)4, CrossoverGuestHostActions.PreparedContactFlags(action, prepared with { TargetBefore = living with { OriginSavedStatuses = 0, Hp = 0, Statuses = 1 } }));
        Assert.Equal((byte)1, CrossoverGuestHostActions.PreparedContactFlags(action, prepared with { Receipt = receipt with { Damage = new(true, false, 0) } }));
    }

    [Fact]
    public void Source_saved_cure_is_described_by_status_mask_and_flags_without_a_host_status_alias()
    {
        BattleNativeAction action = Action(4) with { TargetFlags = 0x14, Statuses0 = Ff7OriginSavedStatusRules.Zombie, StatusAttack = 200 };
        Assert.Null(CrossoverGuestHostActions.OriginItemUnsupportedReason(action));
        Ff7BattleGuestItemEffect effect = CrossoverGuestHostActions.DescribeOriginItem(action, 0);
        Assert.Equal(Ff7OriginSavedStatusRules.Zombie, effect.RemoveOriginSavedStatuses);
        Assert.Equal(0u, effect.RemoveStatuses);
        Assert.Equal(effect, CrossoverGuestHostActions.DescribeOriginItem(action with { ActionId = 197, MagicId = 1234, Name = "Other identity" }, 0));
        Assert.Equal(effect, JsonSerializer.Deserialize<Ff7BattleGuestItemEffect>(JsonSerializer.Serialize(effect)));
    }

    [Theory]
    [InlineData("Life")]
    [InlineData("Full-life")]
    public void Original_revival_selects_living_Zombie_and_publishes_one_offensive_contact(string spell)
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalSpells: [spell]);
        MakeZombie(fixture, 1);
        Ff7BattleSnapshot active = fixture.Host.Capture();
        fixture.RestoreQueuedBoundary(active with
        {
            GuestOriginStatuses = [new(1, Ff7GuestBattleStatuses.OriginMulticast)],
            Actors = active.Actors.Select(actor => actor.Actor.Slot == 1 ? actor with { ParameterStatus = new(Ff7GuestBattleStatuses.OriginDefenseReduction, 32, 100, 100), ParameterStatusInitialized = true } : actor).ToArray()
        });
        ushort ability = fixture.Spell(spell);
        int stock = fixture.Stock(ability);
        string nativeInventory = JsonSerializer.Serialize(fixture.Host.Capture().Inventory);
        fixture.Cast(0, spell, 2);
        Assert.Equal(0u, fixture.Hp(1));
        Assert.NotEqual(0u, fixture.Host.Actors.Single(actor => actor.Actor.Slot == 1).Actor.Statuses & Ff7NativeBattleActionResolver.Death);
        Assert.Equal(stock - 1, fixture.Stock(ability));
        Assert.Equal(0, fixture.Host.LastHit!.Damage.Damage);
        Assert.False(fixture.Host.LastHit.Damage.Missed);
        Assert.False(Ff7OriginSavedStatusRules.IsZombie(fixture.Host.GetOriginSavedStatuses(1)));
        Assert.Null(fixture.Host.Capture().Actors.Single(actor => actor.Actor.Slot == 1).ParameterStatus);
        Assert.Equal(0u, fixture.Host.GetGuestOriginBattleStatuses(1) & (Ff7GuestBattleStatuses.OriginMulticast | Ff7GuestBattleStatuses.OriginDefenseReduction));
        fixture.RestoreQueuedBoundary();
        fixture.Cast(0, spell, 2);
        Assert.True(fixture.Hp(1) > 0);
        Assert.Equal(stock - 2, fixture.Stock(ability));
        Assert.True(fixture.Host.LastHit!.Damage.Damage < 0);
        Assert.False(Ff7OriginSavedStatusRules.IsZombie(fixture.Host.GetOriginSavedStatuses(1)));
        Assert.Null(fixture.Host.Capture().Actors.Single(actor => actor.Actor.Slot == 1).ParameterStatus);
        Assert.Equal(0u, fixture.Host.GetGuestOriginBattleStatuses(1) & (Ff7GuestBattleStatuses.OriginMulticast | Ff7GuestBattleStatuses.OriginDefenseReduction));
        Assert.Equal(nativeInventory, JsonSerializer.Serialize(fixture.Host.Capture().Inventory));
    }

    [Fact]
    public void Original_potion_inverts_once_after_queued_restore_and_Holy_Water_cures_without_replaying_stock()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalItems: ["Potion", "Holy Water"]);
        MakeZombie(fixture, 1);
        ushort potion = fixture.Item("Potion");
        int potionStock = fixture.Providers[0].ItemStock(potion);
        uint hp = fixture.Hp(1);
        ushort damage = fixture.Providers[0].DescribeItem(potion).HitPoints;
        string nativeInventory = JsonSerializer.Serialize(fixture.Host.Capture().Inventory);
        fixture.UseItem(0, "Potion", 1, restoreQueued: true);
        Assert.Equal(hp - damage, fixture.Hp(1));
        Assert.Equal(potionStock - 1, fixture.Providers[0].ItemStock(potion));
        Assert.True(Ff7OriginSavedStatusRules.IsZombie(fixture.Host.GetOriginSavedStatuses(1)));
        ushort holyWater = fixture.Item("Holy Water");
        int waterStock = fixture.Providers[0].ItemStock(holyWater);
        uint beforeCure = fixture.Hp(1);
        fixture.UseItem(0, "Holy Water", 1);
        Assert.Equal(beforeCure, fixture.Hp(1));
        Assert.False(Ff7OriginSavedStatusRules.IsZombie(fixture.Host.GetOriginSavedStatuses(1)));
        Assert.Equal(waterStock - 1, fixture.Providers[0].ItemStock(holyWater));
        fixture.RestoreQueuedBoundary();
        Assert.False(Ff7OriginSavedStatusRules.IsZombie(fixture.Host.GetOriginSavedStatuses(1)));
        Assert.Equal(nativeInventory, JsonSerializer.Serialize(fixture.Host.Capture().Inventory));
    }

    [Fact]
    public void Original_Phoenix_Down_targets_living_Zombie_as_an_offensive_item()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalItems: ["Phoenix Down"]);
        MakeZombie(fixture, 1);
        ushort item = fixture.Item("Phoenix Down");
        int stock = fixture.Providers[0].ItemStock(item);
        fixture.UseItem(0, "Phoenix Down", 1, restoreQueued: true);
        Assert.Equal(0u, fixture.Hp(1));
        Assert.Equal(0, fixture.Host.LastHit!.Damage.Damage);
        Assert.False(fixture.Host.LastHit.Damage.Missed);
        Assert.Equal(stock - 1, fixture.Providers[0].ItemStock(item));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_cached_contact_rejection_does_not_survive_an_original_source_status_cure(bool group)
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new(additionalItems: ["Holy Water"], additionalSpells: group ? ["Full-cure"] : null);
        MakeZombie(fixture, 1);
        fixture.Ready(0);
        ushort cure = fixture.Spell(group ? "Full-cure" : "Cure");
        CrossoverGuestHostActions provider = fixture.Providers[0];
        MethodInfo presentation = typeof(CrossoverGuestHostActions).GetMethod("PresentationUnsupportedReason", BindingFlags.NonPublic | BindingFlags.Instance)!;
        BattleNativeAction original = (BattleNativeAction)typeof(CrossoverGuestHostActions).GetMethod("NativeAbility", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(provider, [cure])!;
        BattleNativeAction action = original;
        int primary = group ? 0 : 1;
        Assert.Null(ReadReason());
        IDictionary cache = (IDictionary)typeof(CrossoverGuestHostActions).GetField("abilityCapabilities", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(provider)!;
        object[] keys = cache.Keys.Cast<object>().Where(key => key is ITuple tuple && (ushort)tuple[0]! == cure && (int)tuple[1]! == primary && (ushort)tuple[2]! == (group ? 3 : 2)).ToArray();
        Assert.NotEmpty(keys);
        const string rejectedContact = "A prepared source contact requires a presentation operation that this host cannot supply.";
        foreach (object key in keys)
        {
            cache[key] = rejectedContact;
        }
        Assert.Equal(rejectedContact, ReadReason());

        fixture.UseItem(0, "Holy Water", 1);

        Assert.Same(provider, fixture.Providers[0]);
        Assert.False(Ff7OriginSavedStatusRules.IsZombie(fixture.Host.GetOriginSavedStatuses(1)));
        fixture.Ready(0);
        Assert.Null(ReadReason());
        Assert.All(keys, key => Assert.Equal(rejectedContact, cache[key]));

        string? ReadReason() => group ? (string?)presentation.Invoke(provider, [action, cure, primary]) : fixture.Host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, cure, primary);
    }

    private static void MakeZombie(CrossoverGuestStatusHostTests.Fixture fixture, int slot)
    {
        Ff7BattleSnapshot snapshot = fixture.Host.Capture();
        fixture.RestoreQueuedBoundary(snapshot with { Actors = snapshot.Actors.Select(actor => actor.Actor.Slot == slot ? actor with { OriginSavedStatuses = Ff7OriginSavedStatusRules.Zombie } : actor).ToArray() });
    }

    private static BattleNativeAction Action(byte type) => new(4, 7, 7, 7)
    {
        MagicId = 58, AttackType = type, Power = 0, HitCount = 1, TargetFlags = 0x15, AttackFlags = 0xE2, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 200, Statuses0 = 1, Statuses1 = 0, Hit = 255, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
    };
}
