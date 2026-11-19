using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeLimitFamilyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unlocked_Angel_Wing_commits_with_native_grouped_or_explicit_family_when_Combine_is_unavailable(bool explicitFamily)
    {
        BattleSession battle = RinoaBattle(explicitFamily ? [BattleLimitFamily.AngelWing] : null);
        Assert.True(battle.SelectDiagnosticLimit("rinoa", new(1, 4, 256)).Accepted);
        Assert.False(battle.GetNativeLimitFamilies("rinoa").SingleOrDefault(choice => choice.Family == BattleLimitFamily.Combine)?.Available ?? false);
        Assert.Equal((ushort)4, Assert.Single(battle.GetNativePlayerActionChoices("rinoa", true)).NativeAction!.ActionId);
        string random = JsonSerializer.Serialize(battle.Capture().Random);

        battle.QueueNativePlayerCommand("rinoa", "rinoa", 19, 4);

        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((ushort)4, queued.NativeAction!.ActionId);
        Assert.Equal("rinoa", queued.TargetId);
        Assert.Equal(random, JsonSerializer.Serialize(battle.Capture().Random));
    }

    [Fact]
    public void Angel_Wing_action_cannot_bypass_an_explicit_Combine_family()
    {
        BattleSession battle = RinoaBattle([BattleLimitFamily.Combine, BattleLimitFamily.AngelWing]);
        Assert.True(battle.SelectDiagnosticLimit("rinoa", new(1, 4, 256)).Accepted);
        string before = JsonSerializer.Serialize(battle.Capture());

        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("rinoa", "rinoa", 19, 4));

        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Empty(battle.QueuedActions);
    }

    [Fact]
    public void Single_Angel_Wing_assignment_uses_its_own_invocation_name_instead_of_shared_command_name()
    {
        BattleNativeLimitDefinition rinoa = Limit(4, 19) with { InvocationChoices = [new(19, "Combine", 0, 0), new(19, "Angel Wing", 0, 1)] };
        BattleSession battle = CreateBattle([new(BattleLimitFamily.AngelWing, rinoa)]);

        Assert.Equal("Angel Wing", battle.GetNativeLimitCommandName("recipient"));
    }

    [Fact]
    public void Assignment_arrays_are_detached_from_callers_and_definition_inspection()
    {
        BattleNativeLimitFamilyDefinition[] families = [new(BattleLimitFamily.IceStrike, Limit(7, 22))];
        BattleSession battle = CreateBattle(families);
        families[0] = new(BattleLimitFamily.Duel, Limit(1, 11));
        BattleDefinition inspected = battle.ReadDefinition();
        inspected.Combatants[0].NativeLimitFamilies![0] = families[0];

        Assert.True(battle.HasNativeLimitFamily("recipient", BattleLimitFamily.IceStrike));
        Assert.False(battle.HasNativeLimitFamily("recipient", BattleLimitFamily.Duel));
    }

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
        Assert.True(battle.SelectDiagnosticLimit("recipient", new(1, 4, 256)).Accepted);
        byte crisis = battle.GetNativeLimitLevel("recipient");
        Assert.InRange(crisis, (byte)1, (byte)4);
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
        Assert.True(battle.SelectDiagnosticLimit("recipient", new(1, 4, 256)).Accepted);
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

    private static BattleSession RinoaBattle(BattleLimitFamily[]? families)
    {
        BattleNativeLimitDefinition rinoa = Limit(4, 19) with { HitPointCoefficient = 250, InvocationChoices = [new(19, "Combine", 0x50, 0), new(19, "Angel Wing", 0x18, 1)] };
        BattleNativeAction angelWing = GuestAction(4) with { Command = 19, CharacterId = 4, KernelSection = 25, KernelIndex = 4, AttackType = 2, Power = 0, TargetFlags = 0x18, StatusAttack = 255, Statuses1 = 0x2000000, CrisisLevel = null };
        return new(new BattleDefinition
        {
            NativeLimitsDisabled = false,
            NativeSavedBattleFlagsKnownMask = 0x30,
            NativeSavedBattleFlags = 0x30,
            LimitProgress = new(new string('0', 64), 0, 0, 0, 0, new byte[8].ToImmutableArray()),
            NativeActions = [angelWing],
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants =
            [
                new() { Id = "rinoa", Name = "Rinoa", IsPlayer = true, ModelId = 4, MaximumHp = 100, CurrentHp = 1, InitialAtb = BattleSession.AtbMaximum, NativeLimit = rinoa, NativeLimitFamilies = families?.Select(family => new BattleNativeLimitFamilyDefinition(family, rinoa)).ToArray() },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
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
