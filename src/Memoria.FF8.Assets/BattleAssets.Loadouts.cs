using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattleResolvedLoadout(int CharacterId, BattleWeaponDefinition Weapon, BattleWeaponProfile WeaponProfile, BattleNativeLimitFamilyDefinition[] Families);

public sealed partial class BattleAssets
{
    public void PreparePlayerLoadout(int costume, BattleResolvedLoadout loadout, bool hasLimitOverride)
    {
        if (!hasLimitOverride && loadout.WeaponProfile.ResourceOwner == loadout.CharacterId)
        {
            return;
        }
        BattleCombatantDefinition actor = new()
        {
            IsPlayer = true,
            ModelId = loadout.CharacterId,
            Costume = costume,
            WeaponId = loadout.Weapon.Id,
            WeaponProfile = loadout.WeaponProfile
        };
        // The normal battle resource readers populate the asset owner's motion caches.
        NativeBattleGraphicsResource standing = ReadNativeGraphicsResource(actor);
        foreach (BattleNativeLimitFamilyDefinition family in loadout.Families)
        {
            _ = ReadNativeLimitPresentationResource(actor, family.Family, standing);
        }
    }

    public BattleCombatantDefinition ApplyBattleWeapon(BattleCombatantDefinition actor, int weaponId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsPlayer || actor.NativePlayerStatRules is not { } originalRules || actor.NativeGrowth is not { } growth || actor.NativeAttack is not { } originalAttack || actor.NativeMagicInventory is not { } inventory)
        {
            throw new NotSupportedException("Weapon exchange requires initialized native player statistics, inventory and attack metadata.");
        }
        BattleWeaponProfile profile = ReadWeaponProfile(weaponId);
        BattleWeaponDefinition weapon = WeaponDefinitions[weaponId];
        System.Collections.Immutable.ImmutableArray<byte> record = NativeKernelRecords.Single(item => item.Section == 4 && item.Index == weaponId).Bytes;
        BattleNativePlayerStatRules rules = originalRules with { WeaponRecord = record };
        BattleNativePlayerStatistics stats = rules.Project(inventory, growth);
        BattleNativeAction attack = originalAttack with { KernelIndex = weaponId, AttackType = record[5], Power = record[6], Hit = checked((byte)stats.Hit), Critical = record[10], RawRecord = record };
        return actor with { WeaponId = weaponId, WeaponName = weapon.Name, WeaponProfile = profile, NativePlayerStatRules = rules, NativeAttack = attack, PhysicalPower = weapon.AttackPower, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = Math.Max(1, stats.Speed), Luck = stats.Luck, Hit = stats.Hit, Evasion = stats.Evasion, ElementAttack = stats.ElementAttack, ElementAttackPercent = stats.ElementAttackPercent, ElementDefenses = stats.ElementDefenses.ToArray() };
    }

    public BattleResolvedLoadout ResolveBattleLoadout(GameState state, int characterId, int nativeWeaponId)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.BattleLoadouts?.Validate();
        BattleCharacterLoadoutOverride? assignment = state.BattleLoadouts?.Get(characterId);
        int weaponId = assignment?.WeaponId ?? nativeWeaponId;
        BattleWeaponProfile profile = ReadWeaponProfile(weaponId);
        BattleLimitFamily[] families = assignment?.LimitFamilies ?? BattleLimitFamilyRules.NativeFamilies(characterId);
        return new(characterId, WeaponDefinitions[weaponId], profile, families.Select(ReadNativeLimitFamily).ToArray());
    }

    public BattleWeaponProfile ReadWeaponProfile(int weaponId)
    {
        if ((uint)weaponId >= WeaponDefinitions.Count)
        {
            throw new InvalidDataException("The assigned battle weapon definition is unavailable.");
        }
        BattleWeaponDefinition weapon = WeaponDefinitions[weaponId];
        BattleWeaponCapabilities capabilities = weapon.CharacterId switch
        {
            0 or 6 => BattleWeaponCapabilities.Gunblade,
            1 => BattleWeaponCapabilities.Unarmed,
            2 => BattleWeaponCapabilities.ShotFirearm,
            8 => BattleWeaponCapabilities.MachineGun,
            9 => BattleWeaponCapabilities.PairedBlades,
            10 => BattleWeaponCapabilities.ThrowingSpear,
            _ => BattleWeaponCapabilities.None
        };
        byte finishers = weapon.CharacterId == 0 ? NativeKernelRecords.Single(record => record.Section == 4 && record.Index == weaponId).Bytes[2] : (byte)0;
        return new(weaponId, weapon.CharacterId, weapon.Tier, capabilities, finishers);
    }

    public BattleNativeLimitFamilyDefinition ReadNativeLimitFamily(BattleLimitFamily family)
    {
        return new(family, ReadNativeLimitDefinition(BattleLimitFamilyRules.SourceCharacter(family)));
    }
}
