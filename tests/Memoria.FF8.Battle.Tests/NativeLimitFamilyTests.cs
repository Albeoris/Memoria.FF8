using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeLimitFamilyTests
{
    [Fact]
    public void Invalid_duplicate_or_wrong_source_family_definitions_fail_before_battle_state_exists()
    {
        BattleNativeLimitFamilyDefinition ice = new(BattleLimitFamily.IceStrike, Limit(7, 22));

        Assert.Throws<InvalidDataException>(() => CreateBattle([ice, ice]));
        Assert.Throws<InvalidDataException>(() => CreateBattle([ice with { Definition = Limit(6, 22) }]));
        Assert.Throws<InvalidDataException>(() => CreateBattle([ice with { Family = (BattleLimitFamily)999 }]));
    }

    [Fact]
    public void Selecting_three_families_preserves_recipient_crisis_and_random_state()
    {
        BattleSession battle = CreateBattle();
        battle.SelectPlayerCommandActor("recipient");
        byte crisis = battle.GetNativeLimitLevel("recipient");
        string random = JsonSerializer.Serialize(battle.Capture().Random);

        foreach (BattleLimitFamily family in new[] { BattleLimitFamily.IceStrike, BattleLimitFamily.Duel, BattleLimitFamily.Slot })
        {
            battle.SelectNativeLimitFamily("recipient", family);
            Assert.Equal(crisis, battle.GetNativeLimitLevel("recipient"));
            Assert.Equal(random, JsonSerializer.Serialize(battle.Capture().Random));
        }

        Assert.Equal(3, battle.GetNativeLimitFamilies("recipient").Count);
        BattleCombatant actor = battle.Combatants.Single(actor => actor.Id == "recipient");
        Assert.Equal(9, actor.ModelId);
        Assert.Equal(9, actor.NativeLimitReadiness!.CharacterId);
        Assert.Equal(5, actor.NativeLimit!.CharacterId);
    }

    [Fact]
    public void Guest_action_row_comes_from_family_source_and_selection_survives_snapshot()
    {
        BattleSession battle = CreateBattle();
        battle.SelectPlayerCommandActor("recipient");
        battle.SelectNativeLimitFamily("recipient", BattleLimitFamily.IceStrike);

        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("recipient", true));
        Assert.Equal((ushort)1, choice.NativeAction!.ActionId);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(BattleLimitFamily.IceStrike, restored.Combatants.Single(actor => actor.Id == "recipient").SelectedNativeLimitFamily);
        Assert.Equal((ushort)1, Assert.Single(restored.GetNativePlayerActionChoices("recipient", true)).NativeAction!.ActionId);
    }

    [Fact]
    public void Explicit_empty_does_not_restore_native_family_or_support_ownership()
    {
        BattleSession battle = CreateBattle([]);

        Assert.Empty(battle.GetNativeLimitFamilies("recipient"));
        Assert.False(battle.SupportsPlayerLimitSelection("recipient"));
        Assert.False(battle.HasNativeLimitFamily("recipient", BattleLimitFamily.Combine));
    }

    [Fact]
    public void Weapon_requirement_rejects_selection_without_changing_crisis_or_rng()
    {
        BattleSession battle = CreateBattle([new(BattleLimitFamily.Renzokuken, Limit(0, 5))]);
        battle.SelectPlayerCommandActor("recipient");
        string before = JsonSerializer.Serialize(battle.Capture());

        Assert.False(Assert.Single(battle.GetNativeLimitFamilies("recipient")).Available);
        Assert.Throws<InvalidOperationException>(() => battle.SelectNativeLimitFamily("recipient", BattleLimitFamily.Renzokuken));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static BattleNativeLimitDefinition Limit(int source, byte command)
    {
        return new(source, command, 0, source == 9 ? (byte)250 : (byte)1, Enumerable.Repeat((byte)0, 32).ToImmutableArray());
    }

    private static BattleSession CreateBattle(BattleNativeLimitFamilyDefinition[]? families = null)
    {
        return new(new BattleDefinition
        {
            NativeLimitsDisabled = false,
            NativeActions = [GuestAction(1), GuestAction(3)],
            Combatants =
            [
                new()
                {
                    Id = "recipient",
                    Name = "Recipient",
                    IsPlayer = true,
                    ModelId = 9,
                    MaximumHp = 100,
                    CurrentHp = 1,
                    InitialAtb = BattleSession.AtbMaximum,
                    NativeLimit = Limit(9, 18),
                    NativeLimitFamilies = families ?? [new(BattleLimitFamily.IceStrike, Limit(7, 22)), new(BattleLimitFamily.Duel, Limit(1, 11)), new(BattleLimitFamily.Slot, Limit(5, 16))],
                    WeaponProfile = new(0, 0, 0, BattleWeaponCapabilities.None, 0)
                },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction GuestAction(ushort row)
    {
        return new(22, row, 18, row)
        {
            CharacterId = 7,
            MagicId = 1,
            AttackType = 1,
            Power = 10,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = 0,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = 100,
            Critical = 0,
            PowerModifier = 0,
            LevelModifier = 0,
            RawRecord = [],
            SupplementalRecord = []
        };
    }
}
