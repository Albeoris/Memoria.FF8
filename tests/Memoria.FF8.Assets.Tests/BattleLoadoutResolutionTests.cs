using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleLoadoutResolutionTests
{
    [Fact]
    public void Installed_weapons_preserve_recipient_stat_inputs_and_use_the_selected_weapon_record()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        string original = JsonSerializer.Serialize(vitals);
        BattleNativePlayerStatRules baseline = assets.ReadPlayerNativeStatRules(vitals, 0);
        foreach (BattleWeaponDefinition weapon in assets.WeaponDefinitions)
        {
            BattleNativePlayerStatRules effective = assets.ReadPlayerNativeStatRules(vitals, 0, weapon.Id);
            Assert.Equal(baseline.CharacterId, effective.CharacterId);
            Assert.Equal(baseline.CharacterRecord, effective.CharacterRecord);
            Assert.Equal(baseline.Level, effective.Level);
            Assert.Equal(baseline.MagicJunctions, effective.MagicJunctions);
            Assert.Equal(baseline.Abilities, effective.Abilities);
            Assert.Equal(assets.NativeKernelRecords.Single(record => record.Section == 4 && record.Index == weapon.Id).Bytes, effective.WeaponRecord);
            BattleNativeAction attack = assets.ReadPlayerNativeAttackData(vitals, 0, weapon.Id);
            Assert.Equal(weapon.Id, attack.KernelIndex);
            Assert.Equal(weapon.AttackPower, attack.Power);
            Assert.Equal((byte?)weapon.Critical, attack.Critical);
            Assert.Equal(0, attack.CharacterId);
            Assert.Equal((byte?)assets.ReadPlayerStats(vitals, 0, weapon.Id).Hit, attack.Hit);
        }
        Assert.Equal(original, JsonSerializer.Serialize(vitals));
    }

    [Fact]
    public void Resolution_keeps_explicit_empty_families_and_does_not_couple_weapons_to_limits()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int native = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 0).Id;
        int borrowed = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 8).Id;
        GameState original = new();
        BattleResolvedLoadout defaults = assets.ResolveBattleLoadout(original, 0, native);
        Assert.Equal(native, defaults.Weapon.Id);
        Assert.Equal(BattleLimitFamily.Renzokuken, Assert.Single(defaults.Families).Family);
        GameState changed = original with { BattleLoadouts = new() { Characters = ImmutableSortedDictionary<int, BattleCharacterLoadoutOverride>.Empty.Add(0, new() { WeaponId = borrowed, LimitFamilies = [] }) } };
        BattleResolvedLoadout empty = assets.ResolveBattleLoadout(changed, 0, native);
        Assert.Equal(borrowed, empty.Weapon.Id);
        Assert.Empty(empty.Families);
        GameState weaponOnly = changed with { BattleLoadouts = new() { Characters = changed.BattleLoadouts!.Characters.SetItem(0, new() { WeaponId = borrowed }) } };
        Assert.Equal(BattleLimitFamily.Renzokuken, Assert.Single(assets.ResolveBattleLoadout(weaponOnly, 0, native).Families).Family);
        Assert.Null(original.BattleLoadouts);
    }

    [Fact]
    public void Installed_profiles_do_not_grant_unrelated_firearm_capabilities_or_foreign_finisher_bits()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        foreach (BattleWeaponDefinition weapon in assets.WeaponDefinitions)
        {
            BattleWeaponProfile profile = assets.ReadWeaponProfile(weapon.Id);
            Assert.Equal(weapon.CharacterId is 0 or 6, profile.Supports(BattleWeaponCapabilities.Gunblade));
            Assert.Equal(weapon.CharacterId == 2, profile.Supports(BattleWeaponCapabilities.ShotFirearm));
            Assert.Equal(weapon.CharacterId == 8, profile.Supports(BattleWeaponCapabilities.MachineGun));
            if (weapon.CharacterId != 0)
            {
                Assert.Equal((byte)0, profile.RenzokukenFinisherMask);
            }
        }
        Assert.Throws<InvalidDataException>(() => assets.ReadWeaponProfile(assets.WeaponDefinitions.Count));
    }
}
